using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Data;
using POS.Helpers;
using POS.Interfaces;
using POS.Models;
using POS.paginas.ventas.Managers;
using POS.Services;
using POS.ventanas;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace POS.paginas.ventas
{
    /// <summary>
    /// Página de ventas refactorizada con managers especializados
    /// </summary>
    public partial class VentasPag : Page
    {
        // Contexto y servicios base
        private readonly AppDbContext _context;
        private readonly INFCReaderService _nfcReaderService;

        // Managers especializados
        private readonly CarritoManager _carritoManager;
        private readonly TiempoManager _tiempoManager;
        private readonly VentaManager _ventaManager;
        private readonly NFCManager _nfcManager;
        private readonly ProductoManager _productoManager;
        private readonly RecuperacionManager _recuperacionManager;

        // Colecciones observables para UI
        public ObservableCollection<CategoriaItem> Categorias { get; set; }
        public ObservableCollection<TiempoActivo> TiemposActivos { get; set; }

        // Estado de venta recuperada
        // Estado de venta recuperada
        private Venta? _ventaPendienteRecuperada = null;
        private string? _idNfcVentaRecuperada = null;  // ← AGREGAR ESTA LÍNEA

        // Control de pagos
        private decimal _montoRecibido = 0;
        private decimal _cambio = 0;

        // NUEVA PROPIEDAD: Tipo de pago seleccionado
        private TipoPago _tipoPagoSeleccionado = TipoPago.Efectivo;

        public VentasPag()
        {
            InitializeComponent();

            // Inicializar contexto
            _context = new AppDbContext();
            _context.Database.EnsureCreated();

            // Inicializar servicios
            var ventaService = new VentaService(_context);
            var comboService = new ComboService(_context);
            var precioTiempoService = new PrecioTiempoService(_context);
            var tiempoService = new TiempoService(_context, precioTiempoService);

            _nfcReaderService = App.ServiceProvider.GetService<INFCReaderService>()
                ?? throw new InvalidOperationException("NFCReaderService no está registrado");

            // Inicializar managers
            _carritoManager = new CarritoManager(_context, comboService);
            _tiempoManager = new TiempoManager(_context, ventaService, precioTiempoService);
            _ventaManager = new VentaManager(_context, ventaService, precioTiempoService);
            _nfcManager = new NFCManager(_nfcReaderService);
            _productoManager = new ProductoManager(_context);
            _recuperacionManager = new RecuperacionManager(_context, tiempoService, ventaService, precioTiempoService);

            // Suscribirse a eventos
            _nfcManager.TarjetaEscaneada += OnTarjetaEscaneada;
            _nfcManager.EstadoEsperaCambiado += OnEstadoEsperaCambiado;
            CarritoService.Instance.CarritoActualizado += (s, e) => ActualizarTotales();

            // Inicializar colecciones
            Categorias = new ObservableCollection<CategoriaItem>();
            TiemposActivos = new ObservableCollection<TiempoActivo>();

            // Configurar bindings
            ProductosItemsControl.ItemsSource = _productoManager.ProductosVisibles;
            CarritoItemsControl.ItemsSource = _carritoManager.Items;
            TiemposActivosItemsControl.ItemsSource = TiemposActivos;

            // Timer para reconexión NFC
            ConfigurarTimerReconexionNFC();

            // Cargar datos iniciales
            LoadCategoriasAsync();
            LoadProductosAsync();
            ActualizarTotales();

            // NUEVO: Inicializar estilo de tipo de pago
            ActualizarEstiloTipoPago();
        }

        #region Configuración Inicial

        private void ConfigurarTimerReconexionNFC()
        {
            var nfcCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            nfcCheckTimer.Tick += async (s, e) =>
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
        }

        #endregion

        #region Eventos NFC

        private async void OnTarjetaEscaneada(object? sender, string cardId)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                var accion = _nfcManager.AccionEsperada;

                switch (accion)
                {
                    case "combo_tiempo":
                        await AsignarNFCaComboTiempo(cardId);
                        break;
                    case "finalizar_combo_tiempo":
                        await FinalizarComboConTiempo(cardId);
                        break;
                    case "recuperar_venta":
                        await RecuperarVentaPendiente(cardId);
                        break;
                }
            });
        }

        private void OnEstadoEsperaCambiado(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_nfcManager.EsperandoTarjeta)
                {
                    WaitingText.Text = _nfcManager.ObtenerMensajeEspera(_nfcManager.AccionEsperada);
                    WaitingIndicator.Visibility = Visibility.Visible;
                    PendienteButton.Content = "⏳ Esperando tarjeta...";
                    PendienteButton.IsEnabled = false;
                }
                else
                {
                    WaitingIndicator.Visibility = Visibility.Collapsed;
                    PendienteButton.Content = "Guardar Pendiente";
                    PendienteButton.IsEnabled = true;
                }
            });
        }

        #endregion

        #region Carga de Datos

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
            await _productoManager.CargarProductosAsync(categoriaId);
            ProductosScrollViewer.Visibility = Visibility.Visible;
            TiempoPanel.Visibility = Visibility.Collapsed;
        }

        private async void LoadCombosAsync()
        {
            await _productoManager.CargarCombosAsync();
            ProductosScrollViewer.Visibility = Visibility.Visible;
            TiempoPanel.Visibility = Visibility.Collapsed;
        }

        private async Task LoadTiemposActivosAsync()
        {
            try
            {
                _productoManager.MostrarPanelTiempo();

                TiemposActivos.Clear();
                var tiemposActivos = await _tiempoManager.ObtenerTiemposActivosAsync();

                foreach (var tiempo in tiemposActivos)
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

        #endregion

        #region Operaciones con Combos de Tiempo

        private async Task AsignarNFCaComboTiempo(string idNfc)
        {
            // Abrir diálogo para capturar nombre del cliente
            var dialogoNombre = new NombreClienteDialog();
            bool? resultado = dialogoNombre.ShowDialog();

            // Si el usuario canceló el diálogo, cancelar la operación
            if (resultado != true || string.IsNullOrWhiteSpace(dialogoNombre.NombreCliente))
            {
                MessageBox.Show(
                    "Se canceló el registro de la venta pendiente.",
                    "Operación cancelada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Cancelar la espera de tarjeta NFC
                _nfcManager.CancelarEsperaTarjeta();
                return;
            }

            string nombreCliente = dialogoNombre.NombreCliente;

            var venta = await _ventaManager.CrearVentaPendienteAsync(
                idNfc,
                nombreCliente,
                _carritoManager.Items.ToList(),
                _carritoManager.MinutosComboTiempo,
                _tipoPagoSeleccionado // NUEVO: Pasar tipo de pago
            );

            if (venta != null)
            {
                MessageBox.Show(
                    $"Venta asociada a: {nombreCliente}\n" +
                    $"Tarjeta: {idNfc}\n" +
                    $"Hora de entrada: {venta.HoraEntrada:HH:mm}\n" +
                    $"Tiempo incluido: {_carritoManager.MinutosComboTiempo} minutos\n" +
                    $"Tipo de pago: {venta.TipoPagoTexto}",
                    "Venta Pendiente Registrada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _carritoManager.Limpiar();
                CancelarButton.Visibility = Visibility.Visible;
                PendienteButton.Visibility = Visibility.Collapsed;
                _tipoPagoSeleccionado = TipoPago.Efectivo; // Reset tipo de pago
                ActualizarEstiloTipoPago();
                ActualizarTotales();
                LoadProductosAsync();
                await LoadTiemposActivosAsync();
            }
        }

        private async Task FinalizarComboConTiempo(string idNfc)
        {
            var ventaFinalizada = await _ventaManager.FinalizarVentaPendienteAsync(idNfc);

            if (ventaFinalizada != null)
            {
                var itemsParaTicket = ventaFinalizada.DetallesVenta.Select(detalle => new ItemCarrito
                {
                    ProductoId = detalle.ProductoId ?? 0,
                    Nombre = detalle.NombreParaMostrar,
                    PrecioUnitario = detalle.PrecioUnitario,
                    Cantidad = detalle.Cantidad,
                    Total = detalle.Subtotal
                }).ToList();

                var ticketWindow = new VentaTicketWindow(ventaFinalizada, itemsParaTicket, 0, 0);
                ticketWindow.ShowDialog();

                await LoadTiemposActivosAsync();
            }
        }

        #endregion

        #region Recuperar Venta Pendiente

        private async Task RecuperarVentaPendiente(string idNfc)
        {
            // El nuevo método retorna List<ItemCarrito>? directamente
            var itemsRecuperados = await _ventaManager.RecuperarVentaPendienteAsync(idNfc);

            if (itemsRecuperados == null || !itemsRecuperados.Any())
            {
                return;
            }

            if (_carritoManager.Items.Any())
            {
                var result = MessageBox.Show(
                    "El carrito actual tiene productos. ¿Desea vaciarlo y cargar la venta pendiente?",
                    "Confirmar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No) return;

                _carritoManager.Limpiar();
            }

            // ✅ CORRECCIÓN CRÍTICA: Obtener el objeto Venta completo de la BD
            var ventaPendiente = await _context.Ventas
                .Include(v => v.DetallesVenta)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.IdNfc == idNfc && v.Estado == (int)EstadoVenta.Pendiente);

            if (ventaPendiente == null)
            {
                MessageBox.Show("No se pudo cargar la venta pendiente completa.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ✅ Establecer AMBAS variables necesarias
            _idNfcVentaRecuperada = idNfc;
            _ventaPendienteRecuperada = ventaPendiente;  // ⭐ ESTA ERA LA LÍNEA FALTANTE

            // Agregar todos los items recuperados al carrito
            foreach (var item in itemsRecuperados)
            {
                _carritoManager.Items.Add(item);
            }

            // Verificar si hay items especiales
            var itemExcedente = itemsRecuperados.FirstOrDefault(i => i.ProductoId == -999);
            var itemTarjetaPerdida = itemsRecuperados.FirstOrDefault(i => i.ProductoId == -998);

            string mensajeAdicional = "";

            if (itemExcedente != null)
            {
                mensajeAdicional += $"\n\n⚠ Excedente de tiempo: ${itemExcedente.Total:F2}";
            }

            if (itemTarjetaPerdida != null)
            {
                mensajeAdicional += $"\n⚠ Cargo por tarjeta extraviada: ${itemTarjetaPerdida.Total:F2}";
            }

            MessageBox.Show(
                $"Venta pendiente recuperada correctamente." +
                $"\n\nTotal items: {itemsRecuperados.Count}" +
                $"\nTotal: ${itemsRecuperados.Sum(i => i.Total):F2}" +
                mensajeAdicional,
                "Venta Recuperada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            ActualizarTotales();
        }

        #endregion

        #region Operaciones con Carrito

        private async void ProductCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.DataContext is not ProductoVenta producto) return;

            bool agregado = false;

            // Combo con tiempo - AHORA PERMITE MÚLTIPLES
            if (producto.EsCombo && producto.TieneTiempo)
            {
                agregado = await _carritoManager.AgregarComboAsync(producto.ComboId);

                if (agregado)
                {
                    CancelarButton.Visibility = Visibility.Collapsed;
                    PendienteButton.Visibility = Visibility.Visible;
                }
            }
            // Combo sin tiempo
            else if (producto.EsCombo && !producto.TieneTiempo)
            {
                agregado = await _carritoManager.AgregarComboAsync(producto.ComboId);
            }
            // Producto individual
            else
            {
                agregado = await _carritoManager.AgregarProductoAsync(producto);
            }

            if (agregado)
            {
                ActualizarTotales();
            }
        }

        private async void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not ItemCarrito item) return;

            var result = MessageBox.Show(
                $"¿Desea eliminar '{item.Nombre}' del carrito?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Eliminar del carrito
                await _carritoManager.EliminarItemAsync(item);

                // Actualizar visibilidad de botones
                if (!_carritoManager.TieneComboConTiempo)
                {
                    bool quedanCombos = _carritoManager.Items.Any(i => i.ProductoId < 0 && !i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999);

                    if (!quedanCombos)
                    {
                        CancelarButton.Visibility = Visibility.Visible;
                        PendienteButton.Visibility = Visibility.Collapsed;
                    }
                }

                ActualizarTotales();

                if (_productoManager.MostrandoTiempo)
                {
                    await LoadTiemposActivosAsync();
                }
            }
        }

        private async void CancelarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (!_carritoManager.Items.Any())
            {
                MessageBox.Show("El carrito ya está vacío", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "¿Está seguro que desea vaciar el carrito?\n\nLas ventas pendientes se mantendrán como pendientes.",
                "Confirmar cancelación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Restaurar items recuperables
                    foreach (var recuperable in _carritoManager.ObtenerItemsRecuperables())
                    {
                        await _recuperacionManager.RestaurarItemRecuperableAsync(recuperable);
                    }

                    // Limpiar carrito y estado
                    _carritoManager.Limpiar();
                    RecibidoTextBox.Text = "0";
                    _montoRecibido = 0;
                    _cambio = 0;
                    _ventaPendienteRecuperada = null;
                    _tipoPagoSeleccionado = TipoPago.Efectivo; // NUEVO: Reset tipo de pago
                    ActualizarEstiloTipoPago(); // NUEVO: Actualizar UI

                    CancelarButton.Visibility = Visibility.Visible;
                    PendienteButton.Visibility = Visibility.Collapsed;

                    ActualizarTotales();

                    if (_productoManager.MostrandoTiempo)
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

        #endregion

        #region Finalizar Venta

        private async void FinalizarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (!_carritoManager.Items.Any())
            {
                MessageBox.Show("El carrito está vacío", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Si es venta recuperada
            if (_ventaPendienteRecuperada != null)
            {
                await FinalizarVentaRecuperadaAsync();
                return;
            }

            // Si tiene combo con tiempo
            if (_carritoManager.TieneComboConTiempo)
            {
                CancelarButton.Visibility = Visibility.Collapsed;
                PendienteButton.Visibility = Visibility.Visible;

                MessageBox.Show("Este combo incluye tiempo. Use el botón 'Guardar Pendiente' y escanee la tarjeta NFC del cliente.",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Venta normal
            await FinalizarVentaNormalAsync();
        }

        private async Task FinalizarVentaNormalAsync()
        {
            try
            {
                var venta = await _ventaManager.CrearVentaFinalizadaAsync(
                    _carritoManager.Items.ToList(),
                    _tipoPagoSeleccionado // NUEVO: Pasar tipo de pago
                );

                if (venta != null)
                {
                    var itemsParaTicket = _carritoManager.Items.Select(item => new ItemCarrito
                    {
                        ProductoId = item.ProductoId,
                        Nombre = item.Nombre,
                        PrecioUnitario = item.PrecioUnitario,
                        Cantidad = item.Cantidad,
                        Total = item.Total
                    }).ToList();

                    var ticketWindow = new VentaTicketWindow(venta, itemsParaTicket, _montoRecibido, _cambio);
                    ticketWindow.ShowDialog();

                    _carritoManager.Limpiar();
                    RecibidoTextBox.Text = "0";
                    _montoRecibido = 0;
                    _cambio = 0;
                    _tipoPagoSeleccionado = TipoPago.Efectivo; // NUEVO: Reset tipo de pago
                    ActualizarEstiloTipoPago(); // NUEVO: Actualizar UI
                    ActualizarTotales();
                    LoadProductosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar la venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FinalizarVentaRecuperadaAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_idNfcVentaRecuperada))
                {
                    MessageBox.Show("No hay una venta pendiente recuperada para finalizar.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var exitoso = await _ventaManager.ActualizarVentaPendienteAsync(
                    _idNfcVentaRecuperada!,  // ✅ CORRECCIÓN: Usar IdNfc en vez de objeto Venta
                    _carritoManager.Items.ToList()
                );

                if (exitoso)
                {
                    // Buscar la venta FINALIZADA (ya no está pendiente)
                    var ventaCompleta = await _context.Ventas
                        .Include(v => v.DetallesVenta)
                            .ThenInclude(d => d.Producto)
                        .FirstOrDefaultAsync(v => v.IdNfc == _idNfcVentaRecuperada && v.Estado == (int)EstadoVenta.Finalizada);

                    if (ventaCompleta != null)
                    {
                        // Preparar items para el ticket
                        var itemsParaTicket = ventaCompleta.DetallesVenta.Select(detalle => new ItemCarrito
                        {
                            ProductoId = detalle.ProductoId ?? 0,
                            Nombre = detalle.NombreParaMostrar,
                            PrecioUnitario = detalle.PrecioUnitario,
                            Cantidad = detalle.Cantidad,
                            Total = detalle.Subtotal
                        }).ToList();

                        // Mostrar ticket de venta
                        var ticketWindow = new VentaTicketWindow(ventaCompleta, itemsParaTicket, 0, 0);
                        ticketWindow.ShowDialog();
                    }

                    _carritoManager.Limpiar();
                    _idNfcVentaRecuperada = null;
                    _ventaPendienteRecuperada = null;

                    await LoadTiemposActivosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar venta recuperada: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GuardarVentaPendiente_Click(object sender, RoutedEventArgs e)
        {
            if (!_carritoManager.Items.Any())
            {
                MessageBox.Show("El carrito está vacío", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_carritoManager.TieneComboConTiempo)
            {
                MessageBox.Show("Esta opción solo está disponible para combos con tiempo",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _nfcManager.IniciarEsperaTarjeta("combo_tiempo", "");
        }

        #endregion

        #region Cancelar Tiempo/Venta Activa

        private async void CancelarVentaTiempo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not TiempoActivo tiempo) return;

            var resultExtravio = MessageBox.Show(
                $"¿La tarjeta NFC {tiempo.IdNfc} se ha extraviado?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultExtravio == MessageBoxResult.Yes)
            {
                try
                {
                    VentaTiempoRecuperable? recuperable = await _recuperacionManager.MoverVentaPendienteACarritoAsync(tiempo);

                    if (recuperable != null)
                    {
                        // Agregar items al carrito
                        foreach (var item in recuperable.Items)
                        {
                            _carritoManager.Items.Add(item);
                        }

                        // Guardar para posible restauración
                        _carritoManager.AgregarItemRecuperable(recuperable);

                        CancelarButton.Visibility = Visibility.Visible;
                        PendienteButton.Visibility = Visibility.Collapsed;
                        FinalizarVentaButton.Visibility = Visibility.Visible;

                        ActualizarTotales();
                        ProductosScrollViewer.Visibility = Visibility.Visible;
                        TiempoPanel.Visibility = Visibility.Collapsed;

                        await LoadTiemposActivosAsync();
                        MessageBox.Show("Items movidos al carrito correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                var resultEliminar = MessageBox.Show(
                    $"¿Desea eliminar permanentemente este combo de los registros?\n\n" +
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
                        bool eliminado = await _ventaManager.CancelarVentaPendienteAsync(tiempo.IdNfc);

                        if (eliminado)
                        {
                            await LoadTiemposActivosAsync();
                            MessageBox.Show("Registro eliminado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region Botones de Navegación

        private void ProductosButton_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriasComboBox.Items.Count > 0)
            {
                CategoriasComboBox.SelectedIndex = 0;
            }
            LoadProductosAsync();
        }

        private void CombosButton_Click(object sender, RoutedEventArgs e)
        {
            LoadCombosAsync();
        }

        private void TiempoButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadTiemposActivosAsync();
        }

        private void RecuperarVenta_Click(object sender, RoutedEventArgs e)
        {
            _nfcManager.IniciarEsperaTarjeta("recuperar_venta", "");
        }

        private void FinalizarVentaPendiente_Click(object sender, RoutedEventArgs e)
        {
            _nfcManager.IniciarEsperaTarjeta("finalizar_combo_tiempo", "");
        }

        private void CancelarWaiting_Click(object sender, RoutedEventArgs e)
        {
            _nfcManager.CancelarEsperaTarjeta();
        }

        #endregion

        #region Búsqueda y Filtros

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.Trim();
            var categoriaSeleccionada = CategoriasComboBox.SelectedItem as CategoriaItem;
            int? categoriaId = categoriaSeleccionada?.Id == 0 ? null : categoriaSeleccionada?.Id;

            _productoManager.FiltrarProductos(searchText, categoriaId);
        }

        private void CategoriasComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriasComboBox.SelectedItem is CategoriaItem categoria)
            {
                LoadProductosAsync(categoria.Id == 0 ? null : categoria.Id);
            }
        }

        #endregion

        #region Tipo de Pago

        private void EfectivoCheckBox_Click(object sender, MouseButtonEventArgs e)
        {
            _tipoPagoSeleccionado = TipoPago.Efectivo;
            ActualizarEstiloTipoPago();
        }

        private void TarjetaCheckBox_Click(object sender, MouseButtonEventArgs e)
        {
            _tipoPagoSeleccionado = TipoPago.Tarjeta;
            ActualizarEstiloTipoPago();

            // Si es tarjeta, limpiar calculadora de cambio
            RecibidoTextBox.Text = "";
            _montoRecibido = 0;
            _cambio = 0;
            CambioTextBlock.Text = "$0.00";
        }

        private void ActualizarEstiloTipoPago()
        {
            if (_tipoPagoSeleccionado == TipoPago.Efectivo)
            {
                // Efectivo seleccionado
                EfectivoBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryPurple");
                EfectivoBorder.Background = (System.Windows.Media.Brush)FindResource("BackgroundPurple");
                EfectivoIndicador.Fill = (System.Windows.Media.Brush)FindResource("PrimaryPurple");
                EfectivoIndicador.Stroke = (System.Windows.Media.Brush)FindResource("PrimaryPurple");

                // Tarjeta no seleccionada
                TarjetaBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("LightBorder");
                TarjetaBorder.Background = System.Windows.Media.Brushes.White;
                TarjetaIndicador.Fill = System.Windows.Media.Brushes.Transparent;
                TarjetaIndicador.Stroke = (System.Windows.Media.Brush)FindResource("LightBorder");

                // Habilitar calculadora de cambio
                RecibidoTextBox.IsEnabled = true;
            }
            else
            {
                // Tarjeta seleccionada
                TarjetaBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryPurple");
                TarjetaBorder.Background = (System.Windows.Media.Brush)FindResource("BackgroundPurple");
                TarjetaIndicador.Fill = (System.Windows.Media.Brush)FindResource("PrimaryPurple");
                TarjetaIndicador.Stroke = (System.Windows.Media.Brush)FindResource("PrimaryPurple");

                // Efectivo no seleccionado
                EfectivoBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("LightBorder");
                EfectivoBorder.Background = System.Windows.Media.Brushes.White;
                EfectivoIndicador.Fill = System.Windows.Media.Brushes.Transparent;
                EfectivoIndicador.Stroke = (System.Windows.Media.Brush)FindResource("LightBorder");

                // Deshabilitar calculadora de cambio para tarjeta
                RecibidoTextBox.IsEnabled = false;
            }
        }

        #endregion

        #region Cálculo de Cambio

        private void RecibidoTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(RecibidoTextBox.Text, out decimal recibido))
            {
                _montoRecibido = recibido;
                decimal total = _carritoManager.ObtenerTotal();
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

            if (!System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]$"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." && textBox!.Text.Contains("."))
            {
                e.Handled = true;
            }
        }

        private void ActualizarTotales()
        {
            decimal total = _carritoManager.ObtenerTotal();
            TotalTextBlock.Text = $"${total:N2}";

            if (_montoRecibido > 0)
            {
                _cambio = Math.Max(0, _montoRecibido - total);
                CambioTextBlock.Text = $"${_cambio:N2}";
            }
        }

        #endregion

        #region Imprimir

        private async void ImprimirButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_carritoManager.Items.Any())
            {
                MessageBox.Show("El carrito está vacío. Agregue productos antes de imprimir.",
                    "Carrito Vacío", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Preguntar confirmación antes de imprimir
            var resultado = MessageBox.Show(
                "¿Desea imprimir el ticket para cafetería/cocina?",
                "Confirmar Impresión",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado != MessageBoxResult.Yes)
            {
                return; // Usuario canceló
            }

            try
            {
                var config = ConfiguracionService.CargarConfiguracion();

                // Verificar impresora configurada
                if (string.IsNullOrEmpty(config.ImpresoraNombre))
                {
                    MessageBox.Show("No hay una impresora configurada.\n\n" +
                        "Por favor, configure la impresora en Administración > Ajustes",
                        "Impresora no configurada",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Verificar SumatraPDF
                var rutaSumatra = SumatraPrintService.EncontrarSumatra();
                if (string.IsNullOrEmpty(rutaSumatra))
                {
                    MessageBox.Show("SumatraPDF no está instalado o no se puede encontrar.\n\n" +
                        "Por favor, descargue e instale SumatraPDF desde:\n" +
                        "https://www.sumatrapdfreader.org/download-free-pdf-viewer",
                        "SumatraPDF no encontrado",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Crear una venta temporal para generar el ticket de pedido
                var ventaTemporal = new Venta
                {
                    Id = 0, // ID temporal
                    Fecha = DateTime.Now,
                    Total = _carritoManager.ObtenerTotal(),
                    Estado = (int)EstadoVenta.Finalizada,
                    TipoPago = (int)_tipoPagoSeleccionado,
                    NombreCliente = "Pre-venta"
                };

                // Deshabilitar botón mientras genera e imprime
                ImprimirButton.IsEnabled = false;
                ImprimirButton.Content = "⏳";

                // Generar ticket de pedido
                var ticketPedidoGenerator = new TicketPedidoPdfGenerator(_context);
                var pdfPedidoBytes = await ticketPedidoGenerator.GenerarTicketPedidoAsync(
                    venta: ventaTemporal,
                    items: _carritoManager.Items.ToList(),
                    nombreMesero: "Cajero",
                    numeroMesa: "Venta Directa",
                    anchoMm: config.AnchoTicket
                );

                if (pdfPedidoBytes != null && pdfPedidoBytes.Length > 0)
                {
                    // Imprimir directamente
                    await SumatraPrintService.ImprimirPdfAsync(pdfPedidoBytes, config.ImpresoraNombre, config.AnchoTicket);

                }
                else
                {
                    MessageBox.Show("❌ No se pudo generar el ticket de pedido",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Restaurar botón
                ImprimirButton.IsEnabled = true;
                ImprimirButton.Content = "🖨️";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al imprimir ticket de pedido: {ex.Message}\n\n" +
                    "Verifique que:\n" +
                    "• La impresora esté encendida y conectada\n" +
                    "• SumatraPDF esté correctamente instalado",
                    "Error de impresión",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Restaurar botón en caso de error
                ImprimirButton.IsEnabled = true;
                ImprimirButton.Content = "🖨️";
            }
        }

        #endregion
    }
}