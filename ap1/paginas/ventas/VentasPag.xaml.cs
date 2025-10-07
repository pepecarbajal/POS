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

namespace POS.paginas.ventas
{
    public partial class VentasPag : Page
    {
        private readonly AppDbContext _context;
        private readonly VentaService _ventaService;
        private readonly ComboService _comboService;

        public ObservableCollection<ProductoVenta> Productos { get; set; }
        public ObservableCollection<ItemCarrito> Carrito { get; set; }
        public ObservableCollection<CategoriaItem> Categorias { get; set; }

        private ObservableCollection<ProductoVenta> _todosLosProductos = new ObservableCollection<ProductoVenta>();
        private bool _mostrandoCombos = false;

        public VentasPag()
        {
            InitializeComponent();

            _context = new AppDbContext();
            _context.Database.EnsureCreated();

            _ventaService = new VentaService(_context);
            _comboService = new ComboService(_context);

            Productos = new ObservableCollection<ProductoVenta>();
            Carrito = new ObservableCollection<ItemCarrito>();
            Categorias = new ObservableCollection<CategoriaItem>();

            ProductosItemsControl.ItemsSource = Productos;
            CarritoItemsControl.ItemsSource = Carrito;

            LoadCategoriasAsync();
            LoadProductosAsync();
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar productos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadCombosAsync()
        {
            try
            {
                _mostrandoCombos = true;

                var combosDb = await _context.Combos
                    .Include(c => c.Productos)
                    .AsNoTracking()
                    .ToListAsync();

                _todosLosProductos.Clear();
                Productos.Clear();
                foreach (var combo in combosDb)
                {
                    var precioTotal = combo.Productos.Sum(p => p.Precio);

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

                    var productoVenta = new ProductoVenta
                    {
                        Id = combo.Id,
                        Nombre = combo.Nombre,
                        Precio = precioTotal,
                        Stock = combo.Productos.Any() ? combo.Productos.Min(p => p.Stock) : 0,
                        ImagenUrl = imagenUrl,
                        EsCombo = true,
                        ComboId = combo.Id
                    };

                    _todosLosProductos.Add(productoVenta);
                    Productos.Add(productoVenta);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar combos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void CombosButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadCombosAsync();
        }

        private async void ProductCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ProductoVenta producto)
            {
                if (producto.EsCombo)
                {
                    await AgregarComboAlCarrito(producto.ComboId);
                    return;
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

            try
            {
                Console.WriteLine("[v0] Iniciando proceso de venta...");

                var venta = new Venta
                {
                    Fecha = DateTime.Now,
                    Total = Carrito.Sum(i => i.Total),
                    DetallesVenta = new List<DetalleVenta>()
                };

                // Add details to the collection without setting VentaId (will be set by service)
                foreach (var item in Carrito)
                {
                    venta.DetallesVenta.Add(new DetalleVenta
                    {
                        ProductoId = item.ProductoId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = item.PrecioUnitario,
                        Subtotal = item.Total
                    });
                }

                Console.WriteLine($"[v0] Venta creada con {venta.DetallesVenta.Count} items, Total: ${venta.Total}");

                await _ventaService.CreateVentaAsync(venta);

                Console.WriteLine($"[v0] Venta guardada exitosamente con ID: {venta.Id}");

                var itemsParaTicket = Carrito.Select(item => new ItemCarrito
                {
                    ProductoId = item.ProductoId,
                    Nombre = item.Nombre,
                    PrecioUnitario = item.PrecioUnitario,
                    Cantidad = item.Cantidad,
                    Total = item.Total
                }).ToList();

                var ticketWindow = new VentaTicketWindow(venta, itemsParaTicket);
                ticketWindow.ShowDialog();

                Carrito.Clear();
                ActualizarTotales();
                LoadProductosAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[v0] Error al procesar venta: {ex.Message}");
                Console.WriteLine($"[v0] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error al procesar la venta: {ex.Message}\n\nDetalles: {ex.InnerException?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarTotales()
        {
            decimal total = Carrito.Sum(i => i.Total);
            TotalTextBlock.Text = $"${total:N2}";
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
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
                    Carrito.Remove(item);
                    ActualizarTotales();
                }
            }
        }

        private void CancelarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (!Carrito.Any())
            {
                MessageBox.Show("El carrito ya está vacío", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "¿Está seguro que desea cancelar la venta y vaciar el carrito?",
                "Confirmar cancelación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Carrito.Clear();
                ActualizarTotales();
                MessageBox.Show("Venta cancelada. El carrito ha sido vaciado.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task AgregarComboAlCarrito(int comboId)
        {
            try
            {
                var comboProductos = await _comboService.GetComboProductosAsync(comboId);

                if (!comboProductos.Any())
                {
                    MessageBox.Show("No se encontró el combo o no tiene productos", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var combo = await _context.Combos.FirstOrDefaultAsync(c => c.Id == comboId);
                var productosAgregados = new System.Text.StringBuilder();
                var productosSinStock = new System.Text.StringBuilder();
                int productosAgregadosCount = 0;

                foreach (var comboProducto in comboProductos)
                {
                    var producto = comboProducto.Producto;
                    var cantidadRequerida = comboProducto.Cantidad;

                    if (producto.Stock < cantidadRequerida)
                    {
                        productosSinStock.AppendLine($"- {producto.Nombre} (stock insuficiente: requiere {cantidadRequerida}, disponible {producto.Stock})");
                        continue;
                    }

                    var itemExistente = Carrito.FirstOrDefault(i => i.ProductoId == producto.Id);

                    if (itemExistente != null)
                    {
                        if (itemExistente.Cantidad + cantidadRequerida <= producto.Stock)
                        {
                            itemExistente.Cantidad += cantidadRequerida;
                            itemExistente.Total = itemExistente.Cantidad * itemExistente.PrecioUnitario;
                            productosAgregados.AppendLine($"- {producto.Nombre} x{cantidadRequerida} (cantidad actualizada)");
                            productosAgregadosCount++;
                        }
                        else
                        {
                            productosSinStock.AppendLine($"- {producto.Nombre} (stock insuficiente)");
                        }
                    }
                    else
                    {
                        Carrito.Add(new ItemCarrito
                        {
                            ProductoId = producto.Id,
                            Nombre = producto.Nombre,
                            PrecioUnitario = producto.Precio,
                            Cantidad = cantidadRequerida,
                            Total = producto.Precio * cantidadRequerida
                        });
                        productosAgregados.AppendLine($"- {producto.Nombre} x{cantidadRequerida}");
                        productosAgregadosCount++;
                    }
                }

                ActualizarTotales();

                var mensaje = $"Combo '{combo?.Nombre}' procesado:\n\n";

                if (productosAgregadosCount > 0)
                {
                    mensaje += $"Productos agregados ({productosAgregadosCount}):\n{productosAgregados}";
                }

                if (productosSinStock.Length > 0)
                {
                    mensaje += $"\nProductos no agregados:\n{productosSinStock}";
                }

                MessageBox.Show(mensaje, "Combo Agregado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al agregar combo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
}
