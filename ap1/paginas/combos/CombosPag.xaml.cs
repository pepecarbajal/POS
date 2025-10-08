using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using POS.Services;
using POS.Data;
using POS.Models;
using System.Threading.Tasks;
using POS.Helpers;
using POS.ventanas;

namespace POS.paginas.combos
{
    public partial class CombosPag : Page
    {
        private readonly ComboService _comboService;
        private readonly ProductoService _productoService;
        private ObservableCollection<ComboViewModel> combos;
        private ObservableCollection<ComboViewModel> combosFiltrados;
        private ObservableCollection<ProductoSeleccionable> productosDisponibles;
        private string imagenSeleccionada = "";
        private int? comboEditandoId = null;
        private bool _isSearchPlaceholder = true;

        public CombosPag()
        {
            InitializeComponent();
            var context = new AppDbContext();
            _comboService = new ComboService(context);
            _productoService = new ProductoService(context);

            combos = new ObservableCollection<ComboViewModel>();
            combosFiltrados = new ObservableCollection<ComboViewModel>();

            InicializarDatos();
        }

        private async void InicializarDatos()
        {
            await CargarProductosDisponibles();
            await CargarCombos();
        }

        private async Task CargarProductosDisponibles()
        {
            var productos = await _productoService.GetAllProductosAsync();
            productosDisponibles = new ObservableCollection<ProductoSeleccionable>(
                productos.Select(p => new ProductoSeleccionable
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    IsSelected = false,
                    Cantidad = 1
                })
            );
        }

        private async Task CargarCombos()
        {
            var combosDb = await _comboService.GetAllCombosAsync();
            combos.Clear();
            combosFiltrados.Clear();

            foreach (var combo in combosDb)
            {
                var comboConProductos = await _comboService.GetComboByIdAsync(combo.Id);
                var productosNombres = comboConProductos?.Productos?.Select(p => p.Nombre) ?? Enumerable.Empty<string>();

                var comboViewModel = new ComboViewModel
                {
                    Id = combo.Id,
                    Nombre = combo.Nombre,
                    UrlImage = combo.UrlImage,
                    Productos = string.Join(", ", productosNombres),
                    ProductosCount = productosNombres.Count()
                };

                combos.Add(comboViewModel);
                combosFiltrados.Add(comboViewModel);
            }

            CombosDataGrid.ItemsSource = combosFiltrados;
            ActualizarContadores();
        }

        private void SeleccionarImagen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos de imagen|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Seleccionar imagen del combo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                imagenSeleccionada = openFileDialog.FileName;
                ImagenPreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagenSeleccionada));
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
        }

        private async void GuardarCombo_Click(object sender, RoutedEventArgs e)
        {
            var productosSeleccionados = productosDisponibles.Where(p => p.IsSelected).ToList();

            if (string.IsNullOrWhiteSpace(NombreTextBox.Text) ||
                productosSeleccionados.Count == 0)
            {
                MessageBox.Show("Por favor completa el nombre y selecciona al menos un producto.", "Campos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string imagePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(imagenSeleccionada))
                {
                    imagePath = ImageHelper.SaveImage(imagenSeleccionada);
                }

                if (comboEditandoId.HasValue)
                {
                    var comboExistente = await _comboService.GetComboByIdAsync(comboEditandoId.Value);
                    if (comboExistente != null)
                    {
                        comboExistente.Nombre = NombreTextBox.Text;
                        comboExistente.UrlImage = imagePath;

                        await _comboService.UpdateComboAsync(comboEditandoId.Value, comboExistente);

                        var productosActuales = await _comboService.GetProductosByComboIdAsync(comboEditandoId.Value);
                        foreach (var prod in productosActuales)
                        {
                            await _comboService.RemoveProductoFromComboAsync(comboEditandoId.Value, prod.Id);
                        }

                        foreach (var prod in productosSeleccionados)
                        {
                            await _comboService.AddProductoToComboAsync(comboEditandoId.Value, prod.Id, prod.Cantidad);
                        }

                        MessageBox.Show("Combo actualizado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    var nuevoCombo = new Combo
                    {
                        Nombre = NombreTextBox.Text,
                        UrlImage = imagePath
                    };

                    var comboCreado = await _comboService.CreateComboAsync(nuevoCombo);

                    foreach (var prod in productosSeleccionados)
                    {
                        await _comboService.AddProductoToComboAsync(comboCreado.Id, prod.Id, prod.Cantidad);
                    }

                    MessageBox.Show("Combo guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await CargarCombos();
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el combo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EditarCombo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ComboViewModel comboViewModel)
            {
                try
                {
                    var combo = await _comboService.GetComboByIdAsync(comboViewModel.Id);
                    if (combo != null)
                    {
                        comboEditandoId = combo.Id;
                        NombreTextBox.Text = combo.Nombre;
                        imagenSeleccionada = combo.UrlImage ?? "";

                        if (!string.IsNullOrEmpty(imagenSeleccionada) && System.IO.File.Exists(imagenSeleccionada))
                        {
                            ImagenPreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagenSeleccionada));
                            PlaceholderText.Visibility = Visibility.Collapsed;
                        }

                        var comboProductos = await _comboService.GetComboProductosAsync(combo.Id);
                        var productosConCantidad = comboProductos.ToDictionary(cp => cp.ProductoId, cp => cp.Cantidad);

                        foreach (var producto in productosDisponibles)
                        {
                            if (productosConCantidad.ContainsKey(producto.Id))
                            {
                                producto.IsSelected = true;
                                producto.Cantidad = productosConCantidad[producto.Id];
                            }
                            else
                            {
                                producto.IsSelected = false;
                                producto.Cantidad = 1;
                            }
                        }

                        ActualizarProductosSeleccionados();
                        GuardarButton.Content = "Actualizar Combo";
                        CancelarButton.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar el combo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BorrarCombo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ComboViewModel comboViewModel)
            {
                var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar el combo '{comboViewModel.Nombre}'?",
                    "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _comboService.DeleteComboAsync(comboViewModel.Id);
                        await CargarCombos();
                        MessageBox.Show("Combo eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar el combo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void LimpiarFormulario_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
        }

        private void LimpiarFormulario()
        {
            comboEditandoId = null;
            NombreTextBox.Clear();

            foreach (var producto in productosDisponibles)
            {
                producto.IsSelected = false;
                producto.Cantidad = 1;
            }

            ImagenPreview.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            imagenSeleccionada = "";

            ActualizarProductosSeleccionados();
            GuardarButton.Content = "Guardar Combo";
            CancelarButton.Visibility = Visibility.Collapsed;
        }

        private void ActualizarContadores()
        {
            int total = combos.Count;
            int filtrados = combosFiltrados.Count;

            if (filtrados == total)
            {
                ContadorTextBlock.Text = $"Mostrando {total} de {total} combos";
            }
            else
            {
                ContadorTextBlock.Text = $"Mostrando {filtrados} de {total} combos";
            }
        }

        private void AgregarProductos_Click(object sender, RoutedEventArgs e)
        {
            var window = new SeleccionarProductosWindow(productosDisponibles)
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() == true)
            {
                ActualizarProductosSeleccionados();
            }
        }

        private void ActualizarProductosSeleccionados()
        {
            ProductosSeleccionadosStack.Children.Clear();
            var seleccionados = productosDisponibles.Where(p => p.IsSelected).ToList();

            foreach (var producto in seleccionados)
            {
                var border = new Border
                {
                    Background = System.Windows.Media.Brushes.White,
                    BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#e5e7eb"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8),
                    CornerRadius = new CornerRadius(6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBlock = new TextBlock
                {
                    Text = $"{producto.Nombre} (x{producto.Cantidad})",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#374151"),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var removeButton = new Button
                {
                    Content = "✕",
                    Width = 28,
                    Height = 28,
                    Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#fee2e2"),
                    BorderThickness = new Thickness(0),
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#dc2626"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Tag = producto
                };

                var removeButtonTemplate = new ControlTemplate(typeof(Button));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.TemplateBindingExtension(Button.BackgroundProperty));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                presenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                presenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                borderFactory.AppendChild(presenterFactory);
                removeButtonTemplate.VisualTree = borderFactory;
                removeButton.Template = removeButtonTemplate;

                var removeButtonStyle = new Style(typeof(Button));
                var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                trigger.Setters.Add(new Setter(Button.BackgroundProperty,
                    new System.Windows.Media.BrushConverter().ConvertFrom("#fecaca")));
                removeButtonStyle.Triggers.Add(trigger);
                removeButton.Style = removeButtonStyle;

                removeButton.Click += RemoveProducto_Click;

                Grid.SetColumn(textBlock, 0);
                Grid.SetColumn(removeButton, 1);

                grid.Children.Add(textBlock);
                grid.Children.Add(removeButton);
                border.Child = grid;

                ProductosSeleccionadosStack.Children.Add(border);
            }

            ProductosCountText.Text = seleccionados.Count > 0
                ? $"{seleccionados.Count} producto(s) seleccionado(s)"
                : "No hay productos seleccionados";
        }

        private void RemoveProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductoSeleccionable producto)
            {
                producto.IsSelected = false;
                producto.Cantidad = 1;
                ActualizarProductosSeleccionados();
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isSearchPlaceholder)
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1f2937"));
                _isSearchPlaceholder = false;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Buscar combos...";
                SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6b7280"));
                _isSearchPlaceholder = true;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSearchPlaceholder) return;

            FiltrarCombos();
        }

        private void FiltrarCombos()
        {
            var searchText = SearchTextBox.Text?.ToLower() ?? "";

            combosFiltrados.Clear();

            var combosFiltradosTemp = combos.Where(c =>
                c.Nombre.ToLower().Contains(searchText) ||
                c.Productos.ToLower().Contains(searchText)
            );

            foreach (var combo in combosFiltradosTemp)
            {
                combosFiltrados.Add(combo);
            }

            ActualizarContadores();
        }
    }

    public class ProductoSeleccionable : System.ComponentModel.INotifyPropertyChanged
    {
        private bool isSelected;
        private int cantidad = 1;

        public int Id { get; set; }
        public string Nombre { get; set; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public int Cantidad
        {
            get => cantidad;
            set
            {
                if (cantidad != value && value > 0)
                {
                    cantidad = value;
                    OnPropertyChanged(nameof(Cantidad));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public class ComboViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string UrlImage { get; set; }
        public string Productos { get; set; }
        public int ProductosCount { get; set; }
    }
}