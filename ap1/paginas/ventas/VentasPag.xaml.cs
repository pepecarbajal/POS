using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using POS.Data;
using POS.Services;
using POS.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Windows;
using System;
using System.IO;
using System.Collections.Generic;
using POS.ventanas;
using POS.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
namespace POS.paginas.ventas
{
    public partial class VentasPag : Page
    {
        private readonly AppDbContext _context;
        private readonly VentaService _ventaService;
        private readonly ComboService _comboService;
        private readonly TiempoService _tiempoService;
        private readonly PrecioTiempoService _precioTiempoService;
        private readonly INFCReaderService _nfcReaderService;

        public ObservableCollection<ProductoVenta> Productos { get; set; }
        public ObservableCollection<ItemCarrito> Carrito { get; set; }
        public ObservableCollection<CategoriaItem> Categorias { get; set; }
        public ObservableCollection<TiempoActivo> TiemposActivos { get; set; }

        private ObservableCollection<ProductoVenta> _todosLosProductos = new ObservableCollection<ProductoVenta>();
        private List<VentaTiempoRecuperable> _itemsRecuperables = new List<VentaTiempoRecuperable>();

        private bool _mostrandoCombos = false;
        private bool _mostrandoTiempo = false;
        private bool _esperandoTarjeta = false;
        private string _accionEsperada = "";

        private bool _carritoTieneComboConTiempo = false;
        private int? _comboConTiempoId = null;
        private int _minutosComboTiempo = 0;

        private Venta? _ventaPendienteRecuperada = null;
        private decimal _montoRecibido = 0;
        private decimal _cambio = 0;

        public VentasPag()
        {
            InitializeComponent();

            _context = new AppDbContext();
            _context.Database.EnsureCreated();

            _ventaService = new VentaService(_context);
            _comboService = new ComboService(_context);
            _precioTiempoService = new PrecioTiempoService(_context);
            _tiempoService = new TiempoService(_context, _precioTiempoService);

            _nfcReaderService = App.ServiceProvider.GetService<INFCReaderService>()
                ?? throw new InvalidOperationException("NFCReaderService no está registrado");

            var nfcCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            nfcCheckTimer.Tick += (s, e) =>
            {
                try
                {
                    if (!_nfcReaderService.IsConnected)
                    {
                        _nfcReaderService.Initialize();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NFC] Error al intentar reconectar: {ex.Message}");
                }
            };

            nfcCheckTimer.Start();

            Productos = new ObservableCollection<ProductoVenta>();
            Categorias = new ObservableCollection<CategoriaItem>();
            TiemposActivos = new ObservableCollection<TiempoActivo>();

            Carrito = CarritoService.Instance.Items;
            CarritoService.Instance.CarritoActualizado += (s, e) => ActualizarTotales();

            ProductosItemsControl.ItemsSource = Productos;
            CarritoItemsControl.ItemsSource = Carrito;
            TiemposActivosItemsControl.ItemsSource = TiemposActivos;

            LoadCategoriasAsync();
            LoadProductosAsync();
            ActualizarTotales();
        }

        private async void OnCardScanned(object? sender, string cardId)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (!_esperandoTarjeta) return;

                WaitingIndicator.Visibility = Visibility.Collapsed;
                PendienteButton.Content = "Guardar Pendiente";
                PendienteButton.IsEnabled = true;
                _esperandoTarjeta = false;

                _nfcReaderService.CardScanned -= OnCardScanned;

                if (_accionEsperada == "iniciar")
                {
                    await IniciarTiempoConNFC(cardId);
                }
                else if (_accionEsperada == "finalizar")
                {
                    await FinalizarTiempoConNFC(cardId);
                }
                else if (_accionEsperada == "combo_tiempo")
                {
                    await AsignarNFCaComboTiempo(cardId);
                }
                else if (_accionEsperada == "finalizar_combo_tiempo")
                {
                    await FinalizarComboConTiempo(cardId);
                }
                else if (_accionEsperada == "recuperar_venta")
                {
                    await RecuperarVentaPendiente(cardId);
                }

                _accionEsperada = "";
            });
        }

        private async Task AsignarNFCaComboTiempo(string idNfc)
        {
            try
            {
                var ventaExistente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);
                if (ventaExistente != null)
                {
                    MessageBox.Show($"Ya existe una venta pendiente asociada a la tarjeta {idNfc}. Por favor, finalice esa venta primero.",
                        "Tarjeta en uso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var tiempoActivo = await _tiempoService.GetTiempoActivoByIdNfcAsync(idNfc);
                if (tiempoActivo != null)
                {
                    MessageBox.Show($"La tarjeta {idNfc} ya tiene un tiempo individual activo. No puedes asignarla a un combo con tiempo.",
                        "Tarjeta en uso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var venta = new Venta
                {
                    Fecha = DateTime.Now,
                    Total = Carrito.Sum(i => i.Total),
                    Estado = (int)EstadoVenta.Pendiente,
                    IdNfc = idNfc,
                    HoraEntrada = DateTime.Now,
                    MinutosTiempoCombo = _minutosComboTiempo,
                    DetallesVenta = new List<DetalleVenta>()
                };

                foreach (var item in Carrito)
                {
                    if (item.ProductoId > 0)
                    {
                        var producto = await _context.Productos.FindAsync(item.ProductoId);
                        if (producto == null)
                            throw new InvalidOperationException($"Producto con ID {item.ProductoId} no encontrado");

                        if (producto.Stock < item.Cantidad)
                            throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre}");

                        venta.DetallesVenta.Add(new DetalleVenta
                        {
                            TipoItem = (int)TipoItemVenta.Producto,
                            ProductoId = producto.Id,
                            ItemReferenciaId = null,
                            NombreItem = producto.Nombre,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            Subtotal = item.Total
                        });

                        producto.Stock -= item.Cantidad;
                        _context.Entry(producto).State = EntityState.Modified;
                    }
                    else if (item.ProductoId < 0 && !item.Nombre.StartsWith("Tiempo"))
                    {
                        int comboId = -item.ProductoId;
                        var combo = await _context.Combos
                            .Include(c => c.ComboProductos)
                            .ThenInclude(cp => cp.Producto)
                            .FirstOrDefaultAsync(c => c.Id == comboId);

                        if (combo == null)
                            throw new InvalidOperationException($"Combo con ID {comboId} no encontrado");

                        if (combo.ComboProductos.Any())
                        {
                            foreach (var comboProducto in combo.ComboProductos)
                            {
                                var producto = comboProducto.Producto;
                                if (producto != null)
                                {
                                    int cantidadTotal = comboProducto.Cantidad * item.Cantidad;

                                    if (producto.Stock < cantidadTotal)
                                        throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre} en el combo");

                                    producto.Stock -= cantidadTotal;
                                    _context.Entry(producto).State = EntityState.Modified;
                                }
                            }
                        }

                        venta.DetallesVenta.Add(new DetalleVenta
                        {
                            TipoItem = (int)TipoItemVenta.Combo,
                            ProductoId = null,
                            ItemReferenciaId = combo.Id,
                            NombreItem = combo.Nombre,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            Subtotal = item.Total
                        });
                    }
                }

                await _ventaService.CreateVentaAsync(venta);
                await _context.SaveChangesAsync();

                MessageBox.Show($"Venta asociada a la tarjeta {idNfc}.\nHora de entrada: {venta.HoraEntrada:HH:mm}\nTiempo incluido: {_minutosComboTiempo} minutos",
                    "Venta Pendiente Registrada", MessageBoxButton.OK, MessageBoxImage.Information);

                Carrito.Clear();

                _carritoTieneComboConTiempo = false;
                _comboConTiempoId = null;
                _minutosComboTiempo = 0;

                CancelarButton.Visibility = Visibility.Visible;
                PendienteButton.Visibility = Visibility.Collapsed;

                ActualizarTotales();
                LoadProductosAsync();

                await LoadTiemposActivosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar venta pendiente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task FinalizarComboConTiempo(string idNfc)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ventaPendiente.HoraEntrada.HasValue)
                {
                    MessageBox.Show("La venta pendiente no tiene hora de entrada registrada.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var horaSalida = DateTime.Now;
                var tiempoTranscurrido = (horaSalida - ventaPendiente.HoraEntrada.Value).TotalMinutes;
                var minutosIncluidos = ventaPendiente.MinutosTiempoCombo ?? 0;

                decimal excedente = 0;
                int minutosExtra = 0;

                if (tiempoTranscurrido > minutosIncluidos)
                {
                    var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

                    if (precios != null && precios.Count > 0)
                    {
                        var ultimoTramo = precios.Last();
                        decimal precioPorMinuto;

                        if (precios.Count > 1)
                        {
                            var penultimoTramo = precios[precios.Count - 2];
                            int minutosExcedente = ultimoTramo.Minutos - penultimoTramo.Minutos;
                            decimal precioExcedente = ultimoTramo.Precio - penultimoTramo.Precio;
                            precioPorMinuto = precioExcedente / minutosExcedente;
                        }
                        else
                        {
                            precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
                        }

                        minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
                        excedente = minutosExtra * precioPorMinuto;
                    }
                }

                if (excedente > 0)
                {
                    var detalleExcedente = new DetalleVenta
                    {
                        VentaId = ventaPendiente.Id,
                        TipoItem = (int)TipoItemVenta.Tiempo,
                        ProductoId = null,
                        ItemReferenciaId = null,
                        NombreItem = $"Excedente de tiempo ({minutosExtra} min extra)",
                        Cantidad = 1,
                        PrecioUnitario = excedente,
                        Subtotal = excedente
                    };

                    ventaPendiente.DetallesVenta.Add(detalleExcedente);
                    _context.DetallesVenta.Add(detalleExcedente);
                }

                await _ventaService.FinalizarVentaPendienteAsync(idNfc, excedente);

                var ventaFinalizada = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == ventaPendiente.Id);

                if (ventaFinalizada == null)
                {
                    throw new InvalidOperationException("No se pudo cargar la venta finalizada");
                }

                var itemsParaTicket = ventaFinalizada.DetallesVenta.Select(detalle => new ItemCarrito
                {
                    ProductoId = detalle.ProductoId ?? 0,
                    Nombre = detalle.NombreParaMostrar,
                    PrecioUnitario = detalle.PrecioUnitario,
                    Cantidad = detalle.Cantidad,
                    Total = detalle.Subtotal
                }).ToList();

                MessageBox.Show($"Tiempo transcurrido: {(int)Math.Ceiling(tiempoTranscurrido)} minutos\n" +
                               $"Tiempo incluido: {minutosIncluidos} minutos\n" +
                               (excedente > 0 ? $"Excedente: ${excedente:F2}" : "Sin excedente"),
                    "Venta Finalizada", MessageBoxButton.OK, MessageBoxImage.Information);

                var ticketWindow = new VentaTicketWindow(ventaFinalizada, itemsParaTicket, 0, 0);
                ticketWindow.ShowDialog();

                await LoadTiemposActivosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CancelarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (!Carrito.Any())
            {
                MessageBox.Show("El carrito ya está vacío", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "¿Está seguro que desea vaciar el carrito?\n\nLos tiempos se reactivarán y las ventas pendientes se mantendrán como pendientes.",
                "Confirmar cancelación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var tiemposAReactivar = Carrito
    .Where(i => i.ProductoId < 0 && i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999)
    .ToList();

                    foreach (var itemTiempo in tiemposAReactivar)
                    {
                        int tiempoId = -itemTiempo.ProductoId;
                        await ReactivarTiempo(tiempoId);
                    }

                    await RestaurarItemsRecuperables();

                    if (_ventaPendienteRecuperada != null)
                    {
                        var detalleExcedente = _ventaPendienteRecuperada.DetallesVenta
                            .FirstOrDefault(d => d.NombreItem.Contains("Excedente de tiempo"));

                        if (detalleExcedente != null)
                        {
                            _context.DetallesVenta.Remove(detalleExcedente);
                            await _context.SaveChangesAsync();
                        }
                    }

                    Carrito.Clear();
                    RecibidoTextBox.Text = "0";
                    _montoRecibido = 0;
                    _cambio = 0;
                    _ventaPendienteRecuperada = null;
                    _carritoTieneComboConTiempo = false;
                    _comboConTiempoId = null;
                    _minutosComboTiempo = 0;

                    CancelarButton.Visibility = Visibility.Visible;
                    PendienteButton.Visibility = Visibility.Collapsed;

                    ActualizarTotales();

                    if (_mostrandoTiempo)
                    {
                        await LoadTiemposActivosAsync();
                    }

                    MessageBox.Show("Carrito vaciado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cancelar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelarWaiting_Click(object sender, RoutedEventArgs e)
        {
            WaitingIndicator.Visibility = Visibility.Collapsed;
            PendienteButton.Content = "Guardar Pendiente";
            PendienteButton.IsEnabled = true;
            _esperandoTarjeta = false;
            _accionEsperada = "";
            _nfcReaderService.CardScanned -= OnCardScanned;
        }

        private async Task IniciarTiempoConNFC(string idNfc)
        {
            if (string.IsNullOrWhiteSpace(idNfc))
            {
                MessageBox.Show("El ID de la tarjeta NFC no puede estar vacío", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ventaPendienteCombo = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);
            if (ventaPendienteCombo != null && ventaPendienteCombo.MinutosTiempoCombo.HasValue && ventaPendienteCombo.MinutosTiempoCombo > 0)
            {
                MessageBox.Show($"La tarjeta {idNfc} ya está asociada a un combo con tiempo activo. No puedes iniciar un tiempo individual.",
                    "Tarjeta en uso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var entradaActiva = await _tiempoService.GetTiempoActivoByIdNfcAsync(idNfc);
            if (entradaActiva != null)
            {
                MessageBox.Show($"Ya existe una entrada activa para la tarjeta {idNfc}", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tiempo = await _tiempoService.RegistrarEntradaAsync(idNfc);

            await LoadTiemposActivosAsync();
        }


        private async Task FinalizarTiempoConNFC(string idNfc)
        {
            if (string.IsNullOrWhiteSpace(idNfc))
            {
                MessageBox.Show("El ID de la tarjeta NFC no puede estar vacío", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);
            if (ventaPendiente != null && ventaPendiente.MinutosTiempoCombo.HasValue && ventaPendiente.MinutosTiempoCombo > 0)
            {
                await FinalizarComboConTiempo(idNfc);
                await LoadTiemposActivosAsync(); return;
            }

            if (_carritoTieneComboConTiempo)
            {
                MessageBox.Show("No puedes agregar tiempos individuales mientras tengas un combo con tiempo en el carrito.\n\nFinaliza el combo primero o cancela la venta.",
                    "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var entradaActiva = await _tiempoService.GetTiempoActivoByIdNfcAsync(idNfc);
            if (entradaActiva == null)
            {
                MessageBox.Show($"No se encontró una entrada activa para la tarjeta {idNfc}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tiempoFinalizado = await _tiempoService.RegistrarSalidaAsync(entradaActiva.Id);

            var tiempoTranscurrido = (tiempoFinalizado.HoraSalida!.Value - tiempoFinalizado.HoraEntrada).TotalMinutes;
            var itemCarrito = new ItemCarrito
            {
                ProductoId = -tiempoFinalizado.Id,
                Nombre = $"Tiempo - ({Math.Ceiling(tiempoTranscurrido)} min)",
                PrecioUnitario = tiempoFinalizado.Total,
                Cantidad = 1,
                Total = tiempoFinalizado.Total
            };

            Carrito.Add(itemCarrito);
            ActualizarTotales();

            await LoadTiemposActivosAsync();
        }


        private async void LoadCategoriasAsync()
        {
            try
            {
                var categoriasDb = await _context.Categorias
                    .AsNoTracking()
                    .ToListAsync();

                Categorias.Clear();
                Categorias.Add(new CategoriaItem { Id = 0, Nombre = "Todas las categorías" });

                foreach (var categoria in categoriasDb)
                {
                    Categorias.Add(new CategoriaItem
                    {
                        Id = categoria.Id,
                        Nombre = categoria.Nombre
                    });
                }

                CategoriasComboBox.ItemsSource = Categorias;
                CategoriasComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar categorías: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadProductosAsync(int? categoriaId = null)
        {
            try
            {
                _mostrandoCombos = false;
                _mostrandoTiempo = false;

                var todosQuery = _context.Productos
                    .Where(p => p.Estado == "Activo" && p.Stock > 0);

                var todosProductosDb = await todosQuery.AsNoTracking().ToListAsync();

                _todosLosProductos.Clear();
                foreach (var producto in todosProductosDb)
                {
                    string? imagenUrl = null;
                    if (!string.IsNullOrEmpty(producto.UrlImage))
                    {
                        string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", producto.UrlImage);

                        if (File.Exists(imagePath))
                        {
                            imagenUrl = imagePath;
                        }
                        else if (Uri.TryCreate(producto.UrlImage, UriKind.Absolute, out Uri? uri))
                        {
                            imagenUrl = producto.UrlImage;
                        }
                    }

                    var productoVenta = new ProductoVenta
                    {
                        Id = producto.Id,
                        Nombre = producto.Nombre,
                        Precio = producto.Precio,
                        Stock = producto.Stock,
                        ImagenUrl = imagenUrl
                    };

                    _todosLosProductos.Add(productoVenta);
                }

                Productos.Clear();
                var productosAMostrar = _todosLosProductos.AsEnumerable();

                if (categoriaId.HasValue && categoriaId.Value > 0)
                {
                    var productosEnCategoria = await _context.Productos
                        .Where(p => p.CategoriaId == categoriaId.Value && p.Estado == "Activo" && p.Stock > 0)
                        .Select(p => p.Id)
                        .ToListAsync();

                    productosAMostrar = _todosLosProductos.Where(p => productosEnCategoria.Contains(p.Id));
                }

                foreach (var producto in productosAMostrar)
                {
                    Productos.Add(producto);
                }

                ProductosScrollViewer.Visibility = Visibility.Visible;
                TiempoPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar productos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadTiemposActivosAsync()
        {
            try
            {
                _mostrandoCombos = false;
                _mostrandoTiempo = true;

                TiemposActivos.Clear();

                var tiemposActivos = await _tiempoService.GetTiemposActivosAsync();

                foreach (var tiempo in tiemposActivos)
                {
                    TiemposActivos.Add(new TiempoActivo
                    {
                        Id = tiempo.Id,
                        IdNfc = tiempo.IdNfc,
                        HoraEntrada = tiempo.HoraEntrada,
                        Estado = tiempo.Estado,
                        EsCombo = false,
                        NombreCombo = null,
                        MinutosIncluidos = 0,
                        MontoTotal = 0
                    });
                }

                var ventasPendientes = await _context.Ventas
    .Include(v => v.DetallesVenta)
    .Where(v => v.Estado == (int)EstadoVenta.Pendiente &&
               v.HoraEntrada.HasValue &&
               v.MinutosTiempoCombo.HasValue &&
               v.MinutosTiempoCombo > 0)
    .AsNoTracking()
    .ToListAsync();

                foreach (var venta in ventasPendientes)
                {
                    var detalleCombo = venta.DetallesVenta
    .FirstOrDefault(d => d.TipoItem == (int)TipoItemVenta.Combo);

                    string nombreCombo = detalleCombo?.NombreItem ?? "Combo";

                    TiemposActivos.Add(new TiempoActivo
                    {
                        Id = venta.Id,
                        IdNfc = venta.IdNfc ?? "N/A",
                        HoraEntrada = venta.HoraEntrada!.Value,
                        Estado = "Activo",
                        EsCombo = true,
                        NombreCombo = nombreCombo,
                        MinutosIncluidos = venta.MinutosTiempoCombo ?? 0,
                        MontoTotal = venta.Total
                    });
                }

                var tiemposOrdenados = TiemposActivos.OrderByDescending(t => t.HoraEntrada).ToList();
                TiemposActivos.Clear();
                foreach (var tiempo in tiemposOrdenados)
                {
                    TiemposActivos.Add(tiempo);
                }

                ProductosScrollViewer.Visibility = Visibility.Collapsed;
                TiempoPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar tiempos activos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower().Trim();

            Productos.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                var categoriaSeleccionada = CategoriasComboBox.SelectedItem as CategoriaItem;
                if (categoriaSeleccionada != null && categoriaSeleccionada.Id > 0)
                {
                    LoadProductosAsync(categoriaSeleccionada.Id);
                }
                else
                {
                    foreach (var producto in _todosLosProductos)
                    {
                        Productos.Add(producto);
                    }
                }
            }
            else
            {
                var productosFiltrados = _todosLosProductos
                    .Where(p => p.Nombre.ToLower().Contains(searchText))
                    .ToList();

                foreach (var producto in productosFiltrados)
                {
                    Productos.Add(producto);
                }
            }
        }

        private void CategoriasComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriasComboBox.SelectedItem is CategoriaItem categoria)
            {
                LoadProductosAsync(categoria.Id == 0 ? null : categoria.Id);
            }
        }

        private void ProductosButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (CategoriasComboBox.Items.Count > 0)
            {
                CategoriasComboBox.SelectedIndex = 0;
            }
            LoadProductosAsync();
        }

        private void CombosButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadCombosAsync();
        }

        private void TiempoButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadTiemposActivosAsync();
        }

        private void IniciarTiempo_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (_carritoTieneComboConTiempo)
                {
                    MessageBox.Show("No puedes iniciar tiempos individuales mientras tengas un combo con tiempo en el carrito.\n\nFinaliza el combo primero o cancela la venta.",
                        "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_nfcReaderService.IsConnected)
                {
                    MessageBox.Show("El lector NFC no está conectado. Verifique la conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _esperandoTarjeta = true;
                _accionEsperada = "iniciar";
                WaitingText.Text = "Esperando tarjeta para iniciar tiempo...";
                WaitingIndicator.Visibility = Visibility.Visible;

                _nfcReaderService.CardScanned += OnCardScanned;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar tiempo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FinalizarTiempo_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (_carritoTieneComboConTiempo)
                {
                    MessageBox.Show("No puedes finalizar tiempos individuales mientras tengas un combo con tiempo en el carrito.\n\nFinaliza el combo primero o cancela la venta.",
                        "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_nfcReaderService.IsConnected)
                {
                    MessageBox.Show("El lector NFC no está conectado. Verifique la conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _esperandoTarjeta = true;
                _accionEsperada = "finalizar";
                WaitingText.Text = "Esperando tarjeta para finalizar tiempo...";
                WaitingIndicator.Visibility = Visibility.Visible;

                _nfcReaderService.CardScanned += OnCardScanned;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar tiempo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecuperarVenta_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_nfcReaderService.IsConnected)
                {
                    MessageBox.Show("El lector NFC no está conectado. Verifique la conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _esperandoTarjeta = true;
                _accionEsperada = "recuperar_venta";
                WaitingText.Text = "Esperando tarjeta NFC para recuperar venta pendiente...";
                WaitingIndicator.Visibility = Visibility.Visible;

                _nfcReaderService.CardScanned += OnCardScanned;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RecuperarVentaPendiente(string idNfc)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Sin venta pendiente", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (Carrito.Any())
                {
                    var result = MessageBox.Show(
                        "El carrito actual tiene productos. ¿Desea vaciarlo y cargar la venta pendiente?",
                        "Confirmar",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }

                    Carrito.Clear();
                }

                _ventaPendienteRecuperada = ventaPendiente;

                foreach (var detalle in ventaPendiente.DetallesVenta)
                {
                    var itemCarrito = new ItemCarrito
                    {
                        ProductoId = detalle.TipoItem == (int)TipoItemVenta.Producto ? detalle.ProductoId ?? 0 : -detalle.ItemReferenciaId ?? 0,
                        Nombre = detalle.NombreParaMostrar,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Cantidad = detalle.Cantidad,
                        Total = detalle.Subtotal
                    };

                    Carrito.Add(itemCarrito);
                }

                var tiempoTranscurrido = (DateTime.Now - ventaPendiente.HoraEntrada!.Value).TotalMinutes;
                var minutosIncluidos = ventaPendiente.MinutosTiempoCombo ?? 0;
                decimal excedente = 0;
                string mensajeExcedente = "";

                if (tiempoTranscurrido > minutosIncluidos)
                {
                    var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

                    if (precios != null && precios.Count > 0)
                    {
                        var ultimoTramo = precios.Last();
                        decimal precioPorMinuto;

                        if (precios.Count > 1)
                        {
                            var penultimoTramo = precios[precios.Count - 2];
                            int minutosExcedente = ultimoTramo.Minutos - penultimoTramo.Minutos;
                            decimal precioExcedente = ultimoTramo.Precio - penultimoTramo.Precio;
                            precioPorMinuto = precioExcedente / minutosExcedente;
                        }
                        else
                        {
                            precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
                        }

                        int minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
                        excedente = minutosExtra * precioPorMinuto;

                        var excedenteExistente = Carrito.FirstOrDefault(i => i.ProductoId == -999);

                        if (excedenteExistente == null)
                        {
                            Carrito.Add(new ItemCarrito
                            {
                                ProductoId = -999,
                                Nombre = $"Excedente de tiempo ({minutosExtra} min extra)",
                                PrecioUnitario = excedente,
                                Cantidad = 1,
                                Total = excedente
                            });

                            mensajeExcedente = $"\n\n⚠ Se agregó excedente de ${excedente:F2} por {minutosExtra} minutos extra";
                        }
                    }
                }

                MessageBox.Show(
                    $"Venta recuperada exitosamente\n\n" +
                    $"Tarjeta: {idNfc}\n" +
                    $"Hora de entrada: {ventaPendiente.HoraEntrada:HH:mm}\n" +
                    $"Tiempo incluido: {minutosIncluidos} minutos\n" +
                    $"Tiempo transcurrido: {(int)Math.Ceiling(tiempoTranscurrido)} minutos\n" +
                    $"Total: ${ventaPendiente.Total:F2}" +
                    mensajeExcedente +
                    "\n\nPuede agregar más productos o finalizar la venta directamente.",
                    "Venta Recuperada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _carritoTieneComboConTiempo = false;
                _minutosComboTiempo = minutosIncluidos;

                ProductosScrollViewer.Visibility = Visibility.Visible;
                TiempoPanel.Visibility = Visibility.Collapsed;

                CancelarButton.Visibility = Visibility.Visible;
                PendienteButton.Visibility = Visibility.Collapsed;

                ActualizarTotales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recuperar venta pendiente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ProductCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ProductoVenta producto)
            {
                if (producto.EsCombo && producto.TieneTiempo)
                {
                    if (_carritoTieneComboConTiempo)
                    {
                        MessageBox.Show("Solo puedes tener un combo con tiempo en el carrito a la vez.",
                            "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var tiemposIndividuales = Carrito.Where(i =>
    i.ProductoId < 0 && i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999
).ToList();

                    if (tiemposIndividuales.Any())
                    {
                        MessageBox.Show("Para agregar un combo con tiempo, el carrito no puede tener tiempos individuales.\n\nPuedes tener productos y otros combos.",
                            "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await AgregarComboAlCarrito(producto.ComboId);

                    _carritoTieneComboConTiempo = true;
                    _comboConTiempoId = producto.ComboId;
                    _minutosComboTiempo = producto.MinutosTiempo;

                    return;
                }

                if (producto.EsCombo && !producto.TieneTiempo)
                {
                    if (_carritoTieneComboConTiempo)
                    {
                        var tiemposIndividuales = Carrito.Where(i =>
                            i.ProductoId < 0 && i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999
                        ).ToList();

                        if (tiemposIndividuales.Any())
                        {
                            MessageBox.Show("No puedes agregar combos mientras haya tiempos individuales en el carrito.",
                                "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    await AgregarComboAlCarrito(producto.ComboId);
                    return;
                }


                var hayTiemposIndividuales = Carrito.Any(i =>
    i.ProductoId < 0 && i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999
);

                if (hayTiemposIndividuales)
                {
                    MessageBox.Show("No puedes agregar productos mientras haya tiempos individuales en el carrito.\n\nPuede finalizar los tiempos primero o eliminarlos del carrito.",
                        "Restricción", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_carritoTieneComboConTiempo)
                {
                }

                var itemExistente = Carrito.FirstOrDefault(i => i.ProductoId == producto.Id);

                if (itemExistente != null)
                {
                    if (itemExistente.Cantidad < producto.Stock)
                    {
                        itemExistente.Cantidad++;
                        itemExistente.Total = itemExistente.Cantidad * itemExistente.PrecioUnitario;
                    }
                    else
                    {
                        MessageBox.Show($"Stock insuficiente para {producto.Nombre}", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Carrito.Add(new ItemCarrito
                    {
                        ProductoId = producto.Id,
                        Nombre = producto.Nombre,
                        PrecioUnitario = producto.Precio,
                        Cantidad = 1,
                        Total = producto.Precio
                    });
                }

                ActualizarTotales();
            }
        }

        private async void FinalizarVenta_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!Carrito.Any())
            {
                MessageBox.Show("El carrito está vacío", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_ventaPendienteRecuperada != null)
            {
                try
                {
                    var idsDetallesOriginales = _ventaPendienteRecuperada.DetallesVenta
    .Select(d => d.ProductoId ?? 0)
    .ToHashSet();

                    foreach (var item in Carrito)
                    {
                        if (item.ProductoId == -998)
                        {
                            var detalleTarjeta = new DetalleVenta
                            {
                                VentaId = _ventaPendienteRecuperada.Id,
                                TipoItem = (int)TipoItemVenta.Producto,
                                ProductoId = null,
                                ItemReferenciaId = null,
                                NombreItem = "Tarjeta extraviada/dañada",
                                Cantidad = 1,
                                PrecioUnitario = 50,
                                Subtotal = 50
                            };

                            _ventaPendienteRecuperada.DetallesVenta.Add(detalleTarjeta);
                            _context.DetallesVenta.Add(detalleTarjeta);
                            continue;
                        }

                        bool esItemNuevo = false;

                        if (item.ProductoId == -999)
                        {
                            esItemNuevo = true;
                        }
                        else if (item.ProductoId > 0)
                        {
                            var detalleExistente = _ventaPendienteRecuperada.DetallesVenta
    .FirstOrDefault(d => d.ProductoId == item.ProductoId && d.TipoItem == (int)TipoItemVenta.Producto);

                            if (detalleExistente != null)
                            {
                                if (detalleExistente.Cantidad != item.Cantidad)
                                {
                                    detalleExistente.Cantidad = item.Cantidad;
                                    detalleExistente.Subtotal = item.Total;
                                    _context.Entry(detalleExistente).State = EntityState.Modified;
                                }
                            }
                            else
                            {
                                esItemNuevo = true;
                            }
                        }
                        else if (item.ProductoId < 0 && !item.Nombre.StartsWith("Tiempo") && item.ProductoId != -999)
                        {
                            int comboId = -item.ProductoId;
                            var detalleExistente = _ventaPendienteRecuperada.DetallesVenta
                                .FirstOrDefault(d => d.ItemReferenciaId == comboId && d.TipoItem == (int)TipoItemVenta.Combo);

                            if (detalleExistente != null)
                            {
                                if (detalleExistente.Cantidad != item.Cantidad)
                                {
                                    detalleExistente.Cantidad = item.Cantidad;
                                    detalleExistente.Subtotal = item.Total;
                                    _context.Entry(detalleExistente).State = EntityState.Modified;
                                }
                            }
                            else
                            {
                                esItemNuevo = true;
                            }
                        }
                        else if (item.ProductoId < 0 && item.Nombre.StartsWith("Tiempo"))
                        {
                            int tiempoId = -item.ProductoId;
                            var detalleExistente = _ventaPendienteRecuperada.DetallesVenta
                                .FirstOrDefault(d => d.ItemReferenciaId == tiempoId && d.TipoItem == (int)TipoItemVenta.Tiempo);

                            if (detalleExistente == null)
                            {
                                esItemNuevo = true;
                            }
                        }

                        if (esItemNuevo)
                        {
                            if (item.ProductoId == -999)
                            {
                                var detalleExcedente = new DetalleVenta
                                {
                                    VentaId = _ventaPendienteRecuperada.Id,
                                    TipoItem = (int)TipoItemVenta.Tiempo,
                                    ProductoId = null,
                                    ItemReferenciaId = null,
                                    NombreItem = item.Nombre,
                                    Cantidad = item.Cantidad,
                                    PrecioUnitario = item.PrecioUnitario,
                                    Subtotal = item.Total
                                };

                                _ventaPendienteRecuperada.DetallesVenta.Add(detalleExcedente);
                                _context.DetallesVenta.Add(detalleExcedente);
                            }
                            else if (item.ProductoId > 0)
                            {
                                var producto = await _context.Productos.FindAsync(item.ProductoId);
                                if (producto == null)
                                {
                                    throw new InvalidOperationException($"Producto con ID {item.ProductoId} no encontrado");
                                }

                                if (producto.Stock < item.Cantidad)
                                {
                                    throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre}");
                                }

                                var detalleProducto = new DetalleVenta
                                {
                                    VentaId = _ventaPendienteRecuperada.Id,
                                    TipoItem = (int)TipoItemVenta.Producto,
                                    ProductoId = producto.Id,
                                    ItemReferenciaId = null,
                                    NombreItem = producto.Nombre,
                                    Cantidad = item.Cantidad,
                                    PrecioUnitario = item.PrecioUnitario,
                                    Subtotal = item.Total
                                };

                                _ventaPendienteRecuperada.DetallesVenta.Add(detalleProducto);
                                _context.DetallesVenta.Add(detalleProducto);

                                producto.Stock -= item.Cantidad;
                                _context.Entry(producto).State = EntityState.Modified;
                            }
                            else if (item.ProductoId < 0 && !item.Nombre.StartsWith("Tiempo"))
                            {
                                int comboId = -item.ProductoId;
                                var combo = await _context.Combos
                                    .Include(c => c.ComboProductos)
                                    .ThenInclude(cp => cp.Producto)
                                    .FirstOrDefaultAsync(c => c.Id == comboId);

                                if (combo == null)
                                {
                                    throw new InvalidOperationException($"Combo con ID {comboId} no encontrado");
                                }

                                foreach (var comboProducto in combo.ComboProductos)
                                {
                                    var producto = comboProducto.Producto;
                                    if (producto != null)
                                    {
                                        int cantidadTotal = comboProducto.Cantidad * item.Cantidad;

                                        if (producto.Stock < cantidadTotal)
                                        {
                                            throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre} en el combo");
                                        }
                                    }
                                }

                                var detalleCombo = new DetalleVenta
                                {
                                    VentaId = _ventaPendienteRecuperada.Id,
                                    TipoItem = (int)TipoItemVenta.Combo,
                                    ProductoId = null,
                                    ItemReferenciaId = combo.Id,
                                    NombreItem = combo.Nombre,
                                    Cantidad = item.Cantidad,
                                    PrecioUnitario = item.PrecioUnitario,
                                    Subtotal = item.Total
                                };

                                _ventaPendienteRecuperada.DetallesVenta.Add(detalleCombo);
                                _context.DetallesVenta.Add(detalleCombo);

                                foreach (var comboProducto in combo.ComboProductos)
                                {
                                    var producto = comboProducto.Producto;
                                    if (producto != null)
                                    {
                                        int cantidadTotal = comboProducto.Cantidad * item.Cantidad;
                                        producto.Stock -= cantidadTotal;
                                        _context.Entry(producto).State = EntityState.Modified;
                                    }
                                }
                            }
                            else if (item.ProductoId < 0 && item.Nombre.StartsWith("Tiempo"))
                            {
                                int tiempoId = -item.ProductoId;

                                var tiempo = await _context.Tiempos.FindAsync(tiempoId);
                                if (tiempo == null)
                                {
                                    throw new InvalidOperationException($"Registro de tiempo con ID {tiempoId} no encontrado");
                                }

                                var detalleTiempo = new DetalleVenta
                                {
                                    VentaId = _ventaPendienteRecuperada.Id,
                                    TipoItem = (int)TipoItemVenta.Tiempo,
                                    ProductoId = null,
                                    ItemReferenciaId = tiempo.Id,
                                    NombreItem = item.Nombre,
                                    Cantidad = item.Cantidad,
                                    PrecioUnitario = item.PrecioUnitario,
                                    Subtotal = item.Total
                                };

                                _ventaPendienteRecuperada.DetallesVenta.Add(detalleTiempo);
                                _context.DetallesVenta.Add(detalleTiempo);
                            }
                        }
                    }

                    decimal totalFinal = Carrito.Sum(i => i.Total);
                    _ventaPendienteRecuperada.Total = totalFinal;
                    _ventaPendienteRecuperada.Estado = (int)EstadoVenta.Finalizada;

                    _context.Entry(_ventaPendienteRecuperada).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    var ventaCompleta = await _context.Ventas
                        .Include(v => v.DetallesVenta)
                            .ThenInclude(d => d.Producto)
                        .FirstOrDefaultAsync(v => v.Id == _ventaPendienteRecuperada.Id);

                    if (ventaCompleta == null)
                    {
                        throw new InvalidOperationException("No se pudo recargar la venta");
                    }

                    var itemsParaTicket = ventaCompleta.DetallesVenta.Select(detalle => new ItemCarrito
                    {
                        ProductoId = detalle.ProductoId ?? 0,
                        Nombre = detalle.NombreParaMostrar,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Cantidad = detalle.Cantidad,
                        Total = detalle.Subtotal
                    }).ToList();

                    var ticketWindow = new VentaTicketWindow(ventaCompleta, itemsParaTicket, _montoRecibido, _cambio);
                    ticketWindow.ShowDialog();

                    Carrito.Clear();
                    RecibidoTextBox.Text = "0";
                    _montoRecibido = 0;
                    _cambio = 0;
                    _ventaPendienteRecuperada = null;
                    _carritoTieneComboConTiempo = false;
                    _comboConTiempoId = null;
                    _minutosComboTiempo = 0;
                    _itemsRecuperables.Clear();
                    FinalizarVentaButton.Visibility = Visibility.Visible;
                    PendienteButton.Visibility = Visibility.Collapsed;

                    ActualizarTotales();
                    LoadProductosAsync();

                    MessageBox.Show("Venta finalizada exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al finalizar venta: {ex.Message}\n\nDetalles: {ex.InnerException?.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (_carritoTieneComboConTiempo)
            {
                CancelarButton.Visibility = Visibility.Collapsed;
                PendienteButton.Visibility = Visibility.Visible;

                MessageBox.Show("Este combo incluye tiempo. Use el botón 'Guardar Pendiente' y escanee la tarjeta NFC del cliente.",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            try
            {
                Console.WriteLine("Iniciando proceso de venta...");

                var venta = new Venta
                {
                    Fecha = DateTime.Now,
                    Total = Carrito.Sum(i => i.Total),
                    Estado = (int)EstadoVenta.Finalizada,
                    DetallesVenta = new List<DetalleVenta>()
                };

                foreach (var item in Carrito)
                {
                    if (item.ProductoId == -998)
                    {
                        venta.DetallesVenta.Add(new DetalleVenta
                        {
                            TipoItem = (int)TipoItemVenta.Producto,
                            ProductoId = null,
                            ItemReferenciaId = null,
                            NombreItem = "Tarjeta extraviada/dañada",
                            Cantidad = 1,
                            PrecioUnitario = 50,
                            Subtotal = 50
                        });
                        continue;
                    }

                    if (item.ProductoId > 0)
                    {
                        var producto = await _context.Productos.FindAsync(item.ProductoId);
                        if (producto == null)
                        {
                            throw new InvalidOperationException($"Producto con ID {item.ProductoId} no encontrado");
                        }

                        if (producto.Stock < item.Cantidad)
                        {
                            throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre}. Disponible: {producto.Stock}, Requerido: {item.Cantidad}");
                        }

                        venta.DetallesVenta.Add(new DetalleVenta
                        {
                            TipoItem = (int)TipoItemVenta.Producto,
                            ProductoId = producto.Id,
                            ItemReferenciaId = null,
                            NombreItem = producto.Nombre,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            Subtotal = item.Total
                        });

                        producto.Stock -= item.Cantidad;
                        _context.Entry(producto).State = EntityState.Modified;

                        Console.WriteLine($"Producto: {producto.Nombre} - Cantidad: {item.Cantidad} - Stock restante: {producto.Stock}");
                    }
                    else if (item.ProductoId < 0 && item.Nombre.StartsWith("Tiempo") && item.ProductoId != -999)
                    {
                        int tiempoId = -item.ProductoId;

                        var tiempo = await _context.Tiempos.FindAsync(tiempoId);
                        if (tiempo == null)
                        {
                            throw new InvalidOperationException($"Registro de tiempo con ID {tiempoId} no encontrado");
                        }

                        venta.DetallesVenta.Add(new DetalleVenta
                        {
                            TipoItem = (int)TipoItemVenta.Tiempo,
                            ProductoId = null,
                            ItemReferenciaId = tiempo.Id,
                            NombreItem = item.Nombre,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            Subtotal = item.Total
                        });

                        Console.WriteLine($"Tiempo registrado: {item.Nombre} - ID: {tiempo.Id} - Total: ${item.Total}");
                    }
                    else if (item.ProductoId < 0 && !item.Nombre.StartsWith("Tiempo"))
                    {
                        int comboId = -item.ProductoId;
                        Console.WriteLine($"Procesando combo ID: {comboId}");

                        var combo = await _context.Combos
                            .Include(c => c.ComboProductos)
                            .ThenInclude(cp => cp.Producto)
                            .FirstOrDefaultAsync(c => c.Id == comboId);

                        if (combo == null || !combo.ComboProductos.Any())
                        {
                            throw new InvalidOperationException($"Combo con ID {comboId} no encontrado o sin productos");
                        }

                        Console.WriteLine($"Combo encontrado: {combo.Nombre} con {combo.ComboProductos.Count} productos");

                        foreach (var comboProducto in combo.ComboProductos)
                        {
                            var producto = comboProducto.Producto;
                            if (producto != null)
                            {
                                int cantidadRequerida = comboProducto.Cantidad * item.Cantidad;

                                if (producto.Stock < cantidadRequerida)
                                {
                                    throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre} en el combo. Disponible: {producto.Stock}, Requerido: {cantidadRequerida}");
                                }
                            }
                        }

                        venta.DetallesVenta.Add(new DetalleVenta
                        {
                            TipoItem = (int)TipoItemVenta.Combo,
                            ProductoId = null,
                            ItemReferenciaId = combo.Id,
                            NombreItem = combo.Nombre,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            Subtotal = item.Total
                        });

                        foreach (var comboProducto in combo.ComboProductos)
                        {
                            var producto = comboProducto.Producto;
                            if (producto != null)
                            {
                                int cantidadTotal = comboProducto.Cantidad * item.Cantidad;
                                Console.WriteLine($"Descontando {cantidadTotal} unidades de {producto.Nombre} (Stock actual: {producto.Stock})");

                                producto.Stock -= cantidadTotal;
                                _context.Entry(producto).State = EntityState.Modified;
                            }
                        }

                        Console.WriteLine($"Combo registrado: {combo.Nombre} - Cantidad: {item.Cantidad}");
                    }
                }

                Console.WriteLine($"Venta creada con {venta.DetallesVenta.Count} items, Total: ${venta.Total}");

                if (venta.DetallesVenta.Any())
                {
                    await _ventaService.CreateVentaAsync(venta);
                    Console.WriteLine($"Venta guardada exitosamente con ID: {venta.Id}");
                }

                await _context.SaveChangesAsync();
                Console.WriteLine("Stock actualizado correctamente");

                var itemsParaTicket = Carrito.Select(item => new ItemCarrito
                {
                    ProductoId = item.ProductoId,
                    Nombre = item.Nombre,
                    PrecioUnitario = item.PrecioUnitario,
                    Cantidad = item.Cantidad,
                    Total = item.Total
                }).ToList();

                var ticketWindow = new VentaTicketWindow(venta, itemsParaTicket, _montoRecibido, _cambio);
                ticketWindow.ShowDialog();

                Carrito.Clear();
                RecibidoTextBox.Text = "0";
                _montoRecibido = 0;
                _cambio = 0;
                _itemsRecuperables.Clear();
                ActualizarTotales();
                LoadProductosAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar venta: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error al procesar la venta: {ex.Message}\n\nDetalles: {ex.InnerException?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GuardarVentaPendiente_Click(object sender, RoutedEventArgs e)
        {
            if (!Carrito.Any())
            {
                MessageBox.Show("El carrito está vacío", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_carritoTieneComboConTiempo)
            {
                MessageBox.Show("Esta opción solo está disponible para combos con tiempo",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tiemposIndividuales = Carrito.Where(i =>
    i.ProductoId < 0 && i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999
).ToList();

            if (tiemposIndividuales.Any())
            {
                MessageBox.Show("No puedes guardar una venta pendiente con tiempos individuales.\n\nPuedes tener combos y productos individuales.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!_nfcReaderService.IsConnected)
                {
                    MessageBox.Show("El lector NFC no está conectado. Verifique la conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PendienteButton.Content = "⏳ Esperando tarjeta...";
                PendienteButton.IsEnabled = false;

                _esperandoTarjeta = true;
                _accionEsperada = "combo_tiempo";
                WaitingText.Text = "Esperando tarjeta NFC para registrar combo con tiempo...";
                WaitingIndicator.Visibility = Visibility.Visible;

                _nfcReaderService.CardScanned += OnCardScanned;
            }
            catch (Exception ex)
            {
                PendienteButton.Content = "Guardar Pendiente";
                PendienteButton.IsEnabled = true;
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void FinalizarVentaPendiente_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_nfcReaderService.IsConnected)
                {
                    MessageBox.Show("El lector NFC no está conectado. Verifique la conexión.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _esperandoTarjeta = true;
                _accionEsperada = "finalizar_combo_tiempo";
                WaitingText.Text = "Esperando tarjeta NFC para finalizar venta...";
                WaitingIndicator.Visibility = Visibility.Visible;

                _nfcReaderService.CardScanned += OnCardScanned;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarTotales()
        {
            decimal total = Carrito.Sum(i => i.Total);
            TotalTextBlock.Text = $"${total:N2}";

            if (_montoRecibido > 0)
            {
                _cambio = Math.Max(0, _montoRecibido - total);
                CambioTextBlock.Text = $"${_cambio:N2}";
            }
        }

        private async void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ItemCarrito item)
            {
                var result = MessageBox.Show(
                    $"¿Desea eliminar '{item.Nombre}' del carrito?",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (item.ProductoId < 0 && item.Nombre.StartsWith("Tiempo") && item.ProductoId != -999)
                    {
                        int tiempoId = -item.ProductoId;
                        await ReactivarTiempo(tiempoId);
                    }

                    var recuperable = _itemsRecuperables.FirstOrDefault(r =>
    r.Items.Any(i => i.ProductoId == item.ProductoId && i.Nombre == item.Nombre));

                    if (recuperable != null)
                    {
                        recuperable.Items.RemoveAll(i => i.ProductoId == item.ProductoId && i.Nombre == item.Nombre);

                        if (!recuperable.Items.Any(i => Carrito.Contains(i)))
                        {
                            await RestaurarItemRecuperable(recuperable);
                            _itemsRecuperables.Remove(recuperable);
                        }
                    }

                    Carrito.Remove(item);

                    if (_carritoTieneComboConTiempo && _comboConTiempoId.HasValue && item.ProductoId == -_comboConTiempoId.Value)
                    {
                        _carritoTieneComboConTiempo = false;
                        _comboConTiempoId = null;
                        _minutosComboTiempo = 0;

                        bool quedanCombos = Carrito.Any(i => i.ProductoId < 0 && !i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999);

                        if (!quedanCombos)
                        {
                            CancelarButton.Visibility = Visibility.Visible;
                            PendienteButton.Visibility = Visibility.Collapsed;
                        }
                    }

                    ActualizarTotales();
                }
            }
        }

        private async Task ReactivarTiempo(int tiempoId)
        {
            try
            {
                var tiempo = await _context.Tiempos.FindAsync(tiempoId);

                if (tiempo != null && tiempo.Estado == "Finalizado")
                {
                    tiempo.Estado = "Activo";
                    tiempo.HoraSalida = null;
                    tiempo.Total = 0;

                    _context.Entry(tiempo).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    if (_mostrandoTiempo)
                    {
                        await LoadTiemposActivosAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reactivar tiempo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private async Task AgregarComboAlCarrito(int comboId)
        {
            try
            {
                var combo = await _context.Combos
                    .Include(c => c.ComboProductos)
                    .ThenInclude(cp => cp.Producto)
                    .Include(c => c.PrecioTiempo)
                    .FirstOrDefaultAsync(c => c.Id == comboId);

                if (combo == null)
                {
                    MessageBox.Show("No se encontró el combo", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var productosSinStock = new System.Text.StringBuilder();
                foreach (var comboProducto in combo.ComboProductos)
                {
                    var producto = comboProducto.Producto;
                    if (producto != null)
                    {
                        int cantidadRequerida = comboProducto.Cantidad;

                        if (producto.Stock < cantidadRequerida)
                        {
                            productosSinStock.AppendLine($"- {producto.Nombre} (Stock: {producto.Stock}, Requerido: {cantidadRequerida})");
                        }
                    }
                }

                if (productosSinStock.Length > 0)
                {
                    MessageBox.Show($"No se puede agregar el combo. Productos sin stock suficiente:\n{productosSinStock}",
                        "Stock Insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var productosDescripcion = string.Join(", ", combo.ComboProductos
                    .Where(cp => cp.Producto != null)
                    .Select(cp => $"{cp.Producto!.Nombre} x{cp.Cantidad}"));

                var nombreCompleto = combo.Nombre;
                if (combo.PrecioTiempo != null)
                {
                    nombreCompleto += $" + {combo.PrecioTiempo.Nombre}";
                }
                nombreCompleto += $" ({productosDescripcion})";

                var itemExistente = Carrito.FirstOrDefault(i => i.ProductoId == -comboId);

                if (itemExistente != null)
                {
                    if (combo.PrecioTiempoId.HasValue)
                    {
                        MessageBox.Show("Solo puedes tener una unidad de un combo con tiempo en el carrito.",
                            "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    bool stockSuficiente = true;
                    foreach (var comboProducto in combo.ComboProductos)
                    {
                        var producto = comboProducto.Producto;
                        if (producto != null)
                        {
                            int cantidadRequerida = comboProducto.Cantidad * (itemExistente.Cantidad + 1);
                            if (producto.Stock < cantidadRequerida)
                            {
                                stockSuficiente = false;
                                break;
                            }
                        }
                    }

                    if (stockSuficiente)
                    {
                        itemExistente.Cantidad++;
                        itemExistente.Total = itemExistente.Cantidad * itemExistente.PrecioUnitario;
                    }
                    else
                    {
                        MessageBox.Show($"Stock insuficiente para agregar más unidades de {combo.Nombre}",
                            "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Carrito.Add(new ItemCarrito
                    {
                        ProductoId = -comboId,
                        Nombre = nombreCompleto,
                        PrecioUnitario = combo.Precio,
                        Cantidad = 1,
                        Total = combo.Precio
                    });

                    if (combo.PrecioTiempoId.HasValue)
                    {
                        _carritoTieneComboConTiempo = true;
                        _comboConTiempoId = comboId;
                        _minutosComboTiempo = combo.PrecioTiempo?.Minutos ?? 0;

                        CancelarButton.Visibility = Visibility.Collapsed;
                        PendienteButton.Visibility = Visibility.Visible;
                    }
                }

                ActualizarTotales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al agregar combo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecibidoTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Carrito == null) return;

            if (decimal.TryParse(RecibidoTextBox.Text, out decimal recibido))
            {
                _montoRecibido = recibido;
                decimal total = Carrito.Sum(i => i.Total);
                _cambio = Math.Max(0, _montoRecibido - total);
                CambioTextBlock.Text = $"${_cambio:N2}";
            }
            else
            {
                _montoRecibido = 0;
                _cambio = 0;
                CambioTextBlock.Text = "$0.00";
            }
        }

        private void RecibidoTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            if (!System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]$"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." && textBox.Text.Contains("."))
            {
                e.Handled = true;
            }
        }


        private async void LoadCombosAsync()
        {
            try
            {
                _mostrandoCombos = true;
                _mostrandoTiempo = false;

                var combosDb = await _context.Combos
    .Include(c => c.ComboProductos)
    .ThenInclude(cp => cp.Producto)
    .Include(c => c.PrecioTiempo)
    .Where(c => c.Estado == "Activo")
    .AsNoTracking()
    .ToListAsync();

                _todosLosProductos.Clear();
                Productos.Clear();

                foreach (var combo in combosDb)
                {
                    string? imagenUrl = null;
                    if (!string.IsNullOrEmpty(combo.UrlImage))
                    {
                        string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", combo.UrlImage);

                        if (File.Exists(imagePath))
                        {
                            imagenUrl = imagePath;
                        }
                        else if (Uri.TryCreate(combo.UrlImage, UriKind.Absolute, out Uri? uri))
                        {
                            imagenUrl = combo.UrlImage;
                        }
                    }

                    int stockDisponible = int.MaxValue;
                    if (combo.ComboProductos.Any())
                    {
                        foreach (var comboProducto in combo.ComboProductos)
                        {
                            if (comboProducto.Producto != null)
                            {
                                int stockPosible = comboProducto.Producto.Stock / comboProducto.Cantidad;
                                stockDisponible = Math.Min(stockDisponible, stockPosible);
                            }
                        }
                    }
                    else
                    {
                        stockDisponible = 0;
                    }

                    var productoVenta = new ProductoVenta
                    {
                        Id = combo.Id,
                        Nombre = combo.Nombre + (combo.PrecioTiempo != null ? $" + {combo.PrecioTiempo.Nombre}" : ""),
                        Precio = combo.Precio,
                        Stock = stockDisponible,
                        ImagenUrl = imagenUrl,
                        EsCombo = true,
                        ComboId = combo.Id,
                        TieneTiempo = combo.PrecioTiempoId.HasValue,
                        PrecioTiempoId = combo.PrecioTiempoId,
                        MinutosTiempo = combo.PrecioTiempo?.Minutos ?? 0
                    };

                    _todosLosProductos.Add(productoVenta);
                    Productos.Add(productoVenta);
                }

                ProductosScrollViewer.Visibility = Visibility.Visible;
                TiempoPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar combos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CancelarVentaTiempo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TiempoActivo tiempo)
            {
                var resultExtravio = MessageBox.Show(
                    $"¿La tarjeta NFC {tiempo.IdNfc} se ha extraviado?",
                    "Confirmar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultExtravio == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (tiempo.EsCombo)
                        {
                            await MoverVentaPendienteACarrito(tiempo);
                        }
                        else
                        {
                            await MoverTiempoACarrito(tiempo);
                        }

                        await LoadTiemposActivosAsync();
                        MessageBox.Show("Items movidos al carrito correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    var resultEliminar = MessageBox.Show(
    $"¿Desea eliminar permanentemente este {(tiempo.EsCombo ? "combo" : "tiempo")} de los registros?\n\n" +
    $"Tarjeta: {tiempo.IdNfc}\n" +
    $"Hora entrada: {tiempo.HoraEntrada:HH:mm}\n\n" +
    "⚠ ADVERTENCIA: Esta acción eliminará el registro de la base de datos y no se podrá recuperar.",
    "Confirmar eliminación",
    MessageBoxButton.YesNo,
    MessageBoxImage.Warning);

                    if (resultEliminar == MessageBoxResult.Yes)
                    {
                        try
                        {
                            if (tiempo.EsCombo)
                            {
                                await EliminarVentaPendiente(tiempo.Id);
                            }
                            else
                            {
                                await EliminarTiempoIndividual(tiempo.Id);
                            }

                            await LoadTiemposActivosAsync();
                            MessageBox.Show("Registro eliminado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private async Task MoverVentaPendienteACarrito(TiempoActivo tiempo)
        {
            var venta = await _context.Ventas
                .Include(v => v.DetallesVenta)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.Id == tiempo.Id && v.Estado == (int)EstadoVenta.Pendiente);

            if (venta == null)
                throw new InvalidOperationException("No se encontró la venta pendiente");

            _ventaPendienteRecuperada = venta;

            var recuperable = new VentaTiempoRecuperable
            {
                Id = venta.Id,
                EsCombo = true,
                IdNfc = venta.IdNfc ?? "",
                HoraEntrada = venta.HoraEntrada ?? DateTime.Now,
                MinutosTiempo = venta.MinutosTiempoCombo ?? 0,
                Items = new List<ItemCarrito>()
            };

            if (venta.HoraEntrada.HasValue)
            {
                var tiempoTranscurrido = (DateTime.Now - venta.HoraEntrada.Value).TotalMinutes;
                var minutosIncluidos = venta.MinutosTiempoCombo ?? 0;

                if (tiempoTranscurrido > minutosIncluidos)
                {
                    var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

                    if (precios != null && precios.Count > 0)
                    {
                        var ultimoTramo = precios.Last();
                        decimal precioPorMinuto;

                        if (precios.Count > 1)
                        {
                            var penultimoTramo = precios[precios.Count - 2];
                            int minutosExcedente = ultimoTramo.Minutos - penultimoTramo.Minutos;
                            decimal precioExcedente = ultimoTramo.Precio - penultimoTramo.Precio;
                            precioPorMinuto = precioExcedente / minutosExcedente;
                        }
                        else
                        {
                            precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
                        }

                        int minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
                        decimal excedente = minutosExtra * precioPorMinuto;

                        if (excedente > 0)
                        {
                            var itemExcedente = new ItemCarrito
                            {
                                ProductoId = -999,
                                Nombre = $"Excedente de tiempo ({minutosExtra} min extra)",
                                PrecioUnitario = excedente,
                                Cantidad = 1,
                                Total = excedente
                            };

                            recuperable.Items.Add(itemExcedente);
                            Carrito.Add(itemExcedente);
                        }
                    }
                }
            }

            foreach (var detalle in venta.DetallesVenta)
            {
                var itemCarrito = new ItemCarrito
                {
                    ProductoId = detalle.TipoItem == (int)TipoItemVenta.Producto ? detalle.ProductoId ?? 0 : -detalle.ItemReferenciaId ?? 0,
                    Nombre = detalle.NombreParaMostrar,
                    PrecioUnitario = detalle.PrecioUnitario,
                    Cantidad = detalle.Cantidad,
                    Total = detalle.Subtotal
                };

                recuperable.Items.Add(itemCarrito);
                Carrito.Add(itemCarrito);
            }

            var itemTarjetaPerdida = new ItemCarrito
            {
                ProductoId = -998,
                Nombre = "Tarjeta extraviada/dañada",
                PrecioUnitario = 50,
                Cantidad = 1,
                Total = 50
            };

            recuperable.Items.Add(itemTarjetaPerdida);
            Carrito.Add(itemTarjetaPerdida);

            _itemsRecuperables.Add(recuperable);

            _carritoTieneComboConTiempo = false;
            _comboConTiempoId = null;
            _minutosComboTiempo = 0;

            CancelarButton.Visibility = Visibility.Visible;
            PendienteButton.Visibility = Visibility.Collapsed;
            FinalizarVentaButton.Visibility = Visibility.Visible;

            ActualizarTotales();

            ProductosScrollViewer.Visibility = Visibility.Visible;
            TiempoPanel.Visibility = Visibility.Collapsed;
        }

        private async Task MoverTiempoACarrito(TiempoActivo tiempo)
        {
            var tiempoDb = await _context.Tiempos.FindAsync(tiempo.Id);

            if (tiempoDb == null || tiempoDb.Estado != "Activo")
                throw new InvalidOperationException("No se encontró el tiempo activo");

            var tiempoFinalizado = await _tiempoService.RegistrarSalidaAsync(tiempoDb.Id);

            var tiempoTranscurrido = (tiempoFinalizado.HoraSalida!.Value - tiempoFinalizado.HoraEntrada).TotalMinutes;

            var itemCarrito = new ItemCarrito
            {
                ProductoId = -tiempoFinalizado.Id,
                Nombre = $"Tiempo - ({Math.Ceiling(tiempoTranscurrido)} min)",
                PrecioUnitario = tiempoFinalizado.Total,
                Cantidad = 1,
                Total = tiempoFinalizado.Total
            };

            var itemTarjetaPerdida = new ItemCarrito
            {
                ProductoId = -998,
                Nombre = "Tarjeta extraviada/dañada",
                PrecioUnitario = 50,
                Cantidad = 1,
                Total = 50
            };

            var recuperable = new VentaTiempoRecuperable
            {
                Id = tiempoFinalizado.Id,
                EsCombo = false,
                IdNfc = tiempoFinalizado.IdNfc,
                HoraEntrada = tiempoFinalizado.HoraEntrada,
                Items = new List<ItemCarrito> { itemCarrito, itemTarjetaPerdida }
            };

            _itemsRecuperables.Add(recuperable);
            Carrito.Add(itemCarrito);
            Carrito.Add(itemTarjetaPerdida);

            ActualizarTotales();

            ProductosScrollViewer.Visibility = Visibility.Visible;
            TiempoPanel.Visibility = Visibility.Collapsed;
        }

        private async Task RestaurarItemsRecuperables()
        {
            foreach (var recuperable in _itemsRecuperables.ToList())
            {
                await RestaurarItemRecuperable(recuperable);
            }
            _itemsRecuperables.Clear();
        }

        private async Task RestaurarItemRecuperable(VentaTiempoRecuperable recuperable)
        {
            if (recuperable.EsCombo)
            {
                var venta = await _context.Ventas
    .Include(v => v.DetallesVenta)
    .FirstOrDefaultAsync(v => v.Id == recuperable.Id);

                if (venta != null)
                {
                    var detalleTarjeta = venta.DetallesVenta
                        .FirstOrDefault(d => d.NombreItem == "Tarjeta extraviada/dañada");

                    if (detalleTarjeta != null)
                    {
                        _context.DetallesVenta.Remove(detalleTarjeta);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                await ReactivarTiempo(recuperable.Id);
            }
        }

        private async Task EliminarVentaPendiente(int ventaId)
        {
            var venta = await _context.Ventas
                .Include(v => v.DetallesVenta)
                .FirstOrDefaultAsync(v => v.Id == ventaId && v.Estado == (int)EstadoVenta.Pendiente);

            if (venta == null)
                throw new InvalidOperationException("No se encontró la venta pendiente");

            _context.DetallesVenta.RemoveRange(venta.DetallesVenta);

            _context.Ventas.Remove(venta);

            await _context.SaveChangesAsync();
        }

        private async Task EliminarTiempoIndividual(int tiempoId)
        {
            var tiempo = await _context.Tiempos.FindAsync(tiempoId);

            if (tiempo == null)
                throw new InvalidOperationException("No se encontró el tiempo");

            var tiempoEnVenta = await _context.DetallesVenta
    .AnyAsync(d => d.TipoItem == (int)TipoItemVenta.Tiempo && d.ItemReferenciaId == tiempoId);

            if (tiempoEnVenta)
            {
                throw new InvalidOperationException("No se puede eliminar este tiempo porque ya está asociado a una venta");
            }

            _context.Tiempos.Remove(tiempo);
            await _context.SaveChangesAsync();
        }
    }

    public class ProductoVenta
    {
        public required int Id { get; set; }
        public required string Nombre { get; set; }
        public required decimal Precio { get; set; }
        public required int Stock { get; set; }
        public string? ImagenUrl { get; set; }
        public bool EsCombo { get; set; } = false;
        public int ComboId { get; set; } = 0;
        public bool TieneTiempo { get; set; } = false; public int? PrecioTiempoId { get; set; }
        public int MinutosTiempo { get; set; } = 0;
    }

    public class ItemCarrito : System.ComponentModel.INotifyPropertyChanged
    {
        private int _cantidad;
        private decimal _total;

        public required int ProductoId { get; set; }
        public required string Nombre { get; set; }
        public required decimal PrecioUnitario { get; set; }

        public int Cantidad
        {
            get => _cantidad;
            set
            {
                _cantidad = value;
                OnPropertyChanged(nameof(Cantidad));
            }
        }

        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged(nameof(Total));
            }
        }

        public string NombreProducto => Nombre;
        public decimal Subtotal => Total;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public class CategoriaItem
    {
        public required int Id { get; set; }
        public required string Nombre { get; set; }
    }

    public class TiempoActivo : System.ComponentModel.INotifyPropertyChanged
    {
        public required int Id { get; set; }
        public required string IdNfc { get; set; }
        public required DateTime HoraEntrada { get; set; }
        public required string Estado { get; set; }
        public bool EsCombo { get; set; } = false; public string? NombreCombo { get; set; }
        public int MinutosIncluidos { get; set; } = 0; public decimal MontoTotal { get; set; } = 0;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public class VentaTiempoRecuperable
    {
        public int Id { get; set; }
        public bool EsCombo { get; set; }
        public string IdNfc { get; set; } = "";
        public List<ItemCarrito> Items { get; set; } = new List<ItemCarrito>();
        public DateTime HoraEntrada { get; set; }
        public int MinutosTiempo { get; set; }
    }


}
