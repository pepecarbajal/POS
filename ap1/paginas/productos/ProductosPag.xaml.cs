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

namespace POS.paginas.productos
{
    public partial class ProductosPag : Page
    {
        public ObservableCollection<ProductoViewModel> Productos { get; set; }
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

                Productos.Add(new ProductoViewModel
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
                });
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
                        UrlImage = selectedImagePath ?? ""
                    };

                    bool actualizado = await _productoService.UpdateProductoAsync(_productoEditandoId.Value, productoActualizado);

                    if (actualizado)
                    {
                        MessageBox.Show("Producto actualizado exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        UrlImage = selectedImagePath ?? ""
                    };

                    await _productoService.CreateProductoAsync(nuevoProducto);
                    MessageBox.Show("Producto guardado exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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
                            MessageBox.Show("Producto eliminado exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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
