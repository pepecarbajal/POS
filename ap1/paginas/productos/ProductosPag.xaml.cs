using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using POS.Data;
using POS.Services;
using POS.Models;
using POS.Interfaces;
using POS.Helpers; // Added ImageHelper namespace

namespace POS.paginas.productos
{
    public partial class ProductosPag : Page
    {
        public ObservableCollection<ProductoViewModel> Productos { get; set; }
        private ObservableCollection<ProductoViewModel> _todosLosProductos;
        private string? selectedImagePath;
        private readonly AppDbContext _context;
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private int? _productoEditandoId = null;

        public ProductosPag()
        {
            InitializeComponent();

            _context = new AppDbContext();
            _productoService = new ProductoService(_context);
            _categoriaService = new CategoriaService(_context);

            Productos = new ObservableCollection<ProductoViewModel>();
            _todosLosProductos = new ObservableCollection<ProductoViewModel>();

            ProductsDataGrid.ItemsSource = Productos;

            CargarDatos();
        }

        private async void CargarDatos()
        {
            try
            {
                await CargarCategorias();
                await CargarProductos();
                ActualizarContadores();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task CargarCategorias()
        {
            var categorias = await _categoriaService.GetAllCategoriasAsync();
            CategoriaComboBox.Items.Clear();
            CategoriaFilterComboBox.Items.Clear();

            CategoriaFilterComboBox.Items.Add(new ComboBoxItem { Content = "Todas las categorías" });

            foreach (var categoria in categorias)
            {
                CategoriaComboBox.Items.Add(new ComboBoxItem { Content = categoria.Nombre, Tag = categoria.Id });
                CategoriaFilterComboBox.Items.Add(new ComboBoxItem { Content = categoria.Nombre, Tag = categoria.Id });
            }

            if (CategoriaFilterComboBox.Items.Count > 0)
                CategoriaFilterComboBox.SelectedIndex = 0;
        }

        private async System.Threading.Tasks.Task CargarProductos()
        {
            var productos = await _productoService.GetAllProductosAsync();
            var categorias = await _categoriaService.GetAllCategoriasAsync();

            Productos.Clear();
            _todosLosProductos.Clear();

            foreach (var producto in productos)
            {
                var categoria = categorias.FirstOrDefault(c => c.Id == producto.CategoriaId);

                string estado;
                if (producto.Stock == 0)
                    estado = "Inactivo";
                else if (producto.Stock < 5)
                    estado = "Stock Bajo";
                else
                    estado = producto.Estado;

                var productoViewModel = new ProductoViewModel
                {
                    Id = producto.Id,
                    Nombre = producto.Nombre,
                    Categoria = categoria?.Nombre ?? "Sin categoría",
                    CategoriaId = producto.CategoriaId,
                    Precio = $"${producto.Precio:F2}",
                    PrecioDecimal = producto.Precio,
                    Stock = producto.Stock,
                    Estado = estado,
                    ImagePath = producto.UrlImage
                };

                Productos.Add(productoViewModel);
                _todosLosProductos.Add(productoViewModel);
            }
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos de imagen|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Seleccionar imagen del producto"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath = openFileDialog.FileName;

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(selectedImagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ProductImagePreview.Source = bitmap;
                ImagePlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private async void GuardarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese el nombre del producto", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CategoriaComboBox.SelectedItem == null)
            {
                MessageBox.Show("Por favor seleccione una categoría", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PrecioTextBox.Text) || !decimal.TryParse(PrecioTextBox.Text, out decimal precio))
            {
                MessageBox.Show("Por favor ingrese un precio válido", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(StockTextBox.Text) || !int.TryParse(StockTextBox.Text, out int stock))
            {
                MessageBox.Show("Por favor ingrese un stock válido", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var categoriaItem = (ComboBoxItem)CategoriaComboBox.SelectedItem;
                int categoriaId = (int)categoriaItem.Tag;

                string estado = EstadoCheckBox.IsChecked == true ? "Activo" : "Inactivo";

                string imagePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(selectedImagePath))
                {
                    imagePath = ImageHelper.SaveImage(selectedImagePath);
                }

                if (_productoEditandoId.HasValue)
                {
                    // Actualizar producto existente
                    var productoActualizado = new Producto
                    {
                        Id = _productoEditandoId.Value,
                        Nombre = NombreTextBox.Text,
                        CategoriaId = categoriaId,
                        Precio = precio,
                        Stock = stock,
                        Estado = estado,
                        UrlImage = imagePath // Use new image path
                    };

                    bool actualizado = await _productoService.UpdateProductoAsync(_productoEditandoId.Value, productoActualizado);

                    if (actualizado)
                    {
                        _productoEditandoId = null;
                    }
                    else
                    {
                        MessageBox.Show("Error al actualizar el producto", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Crear nuevo producto
                    var nuevoProducto = new Producto
                    {
                        Nombre = NombreTextBox.Text,
                        CategoriaId = categoriaId,
                        Precio = precio,
                        Stock = stock,
                        Estado = estado,
                        UrlImage = imagePath // Use new image path
                    };

                    await _productoService.CreateProductoAsync(nuevoProducto);
                }

                await CargarProductos();
                ActualizarContadores();
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el producto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimpiarFormulario()
        {
            NombreTextBox.Clear();
            CategoriaComboBox.SelectedIndex = -1;
            PrecioTextBox.Clear();
            StockTextBox.Clear();
            EstadoCheckBox.IsChecked = true;
            ProductImagePreview.Source = null;
            ImagePlaceholder.Visibility = Visibility.Visible;
            selectedImagePath = null;
            _productoEditandoId = null;
        }

        private void ActualizarContadores()
        {
            int total = Productos.Count;
            int activos = 0;
            int inactivos = 0;

            foreach (var producto in Productos)
            {
                if (producto.Estado == "Activo" || producto.Estado == "Stock Bajo")
                    activos++;
                else
                    inactivos++;
            }

            ProductCountText.Text = $"Mostrando {total} de {total} productos";
            ActiveCountText.Text = $"{activos} Activos";
            InactiveCountText.Text = $"{inactivos} Inactivo{(inactivos != 1 ? "s" : "")}";
        }

        private void EditarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductoViewModel producto)
            {
                _productoEditandoId = producto.Id;

                NombreTextBox.Text = producto.Nombre;
                PrecioTextBox.Text = producto.PrecioDecimal.ToString("F2");
                StockTextBox.Text = producto.Stock.ToString();

                foreach (ComboBoxItem item in CategoriaComboBox.Items)
                {
                    if ((int)item.Tag == producto.CategoriaId)
                    {
                        CategoriaComboBox.SelectedItem = item;
                        break;
                    }
                }

                EstadoCheckBox.IsChecked = producto.Estado != "Inactivo";

                if (!string.IsNullOrEmpty(producto.ImagePath))
                {
                    try
                    {
                        selectedImagePath = producto.ImagePath;
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(selectedImagePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProductImagePreview.Source = bitmap;
                        ImagePlaceholder.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }
            }
        }

        private async void BorrarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductoViewModel producto)
            {
                var result = MessageBox.Show($"¿Está seguro que desea eliminar el producto '{producto.Nombre}'?",
                                            "Confirmar eliminación",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool eliminado = await _productoService.DeleteProductoAsync(producto.Id);

                        if (eliminado)
                        {
                            await CargarProductos();
                            ActualizarContadores();
                        }
                        else
                        {
                            MessageBox.Show("Error al eliminar el producto", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar el producto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox.Text == "Buscar productos...")
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Buscar productos...";
                SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void CategoriaFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void EstadoFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            if (_todosLosProductos == null || _todosLosProductos.Count == 0)
                return;

            var productosFiltrados = _todosLosProductos.AsEnumerable();

            string searchText = SearchTextBox.Text;
            bool hayBusqueda = !string.IsNullOrWhiteSpace(searchText) && searchText != "Buscar productos...";

            if (hayBusqueda)
            {
                // Only apply search filter, ignore category and status filters
                productosFiltrados = productosFiltrados.Where(p =>
                    p.Nombre.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Categoria.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // No search active, apply category and status filters

                // Apply category filter
                if (CategoriaFilterComboBox.SelectedItem is ComboBoxItem categoriaItem)
                {
                    string categoriaSeleccionada = categoriaItem.Content.ToString() ?? "";
                    if (categoriaSeleccionada != "Todas las categorías")
                    {
                        productosFiltrados = productosFiltrados.Where(p =>
                            p.Categoria.Equals(categoriaSeleccionada, StringComparison.OrdinalIgnoreCase));
                    }
                }

                // Apply status filter
                if (EstadoFilterComboBox.SelectedItem is ComboBoxItem estadoItem)
                {
                    string estadoSeleccionado = estadoItem.Content.ToString() ?? "";
                    if (estadoSeleccionado == "Activos")
                    {
                        productosFiltrados = productosFiltrados.Where(p =>
                            p.Estado == "Activo" || p.Estado == "Stock Bajo");
                    }
                    else if (estadoSeleccionado == "Inactivos")
                    {
                        productosFiltrados = productosFiltrados.Where(p =>
                            p.Estado == "Inactivo");
                    }
                }
            }

            // Update the displayed collection
            Productos.Clear();
            foreach (var producto in productosFiltrados)
            {
                Productos.Add(producto);
            }

            ActualizarContadores();
        }
    }

    public class ProductoViewModel
    {
        public int Id { get; set; }
        public required string Nombre { get; set; }
        public required string Categoria { get; set; }
        public int CategoriaId { get; set; }
        public required string Precio { get; set; }
        public decimal PrecioDecimal { get; set; }
        public int Stock { get; set; }
        public required string Estado { get; set; }
        public string? ImagePath { get; set; }
    }
}
