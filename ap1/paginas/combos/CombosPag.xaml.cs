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
        private readonly PrecioTiempoService _precioTiempoService;
        private ObservableCollection<ComboViewModel> combos;
        private ObservableCollection<ProductoSeleccionable> productosDisponibles;
        private string imagenSeleccionada = "";
        private int? comboEditandoId = null;

        public CombosPag()
        {
            InitializeComponent();
            var context = new AppDbContext();
            _comboService = new ComboService(context);
            _productoService = new ProductoService(context);
            _precioTiempoService = new PrecioTiempoService(context);
            InicializarDatos();
        }

        private async void InicializarDatos()
        {
            await CargarProductosDisponibles();
            await CargarTiemposDisponibles();
            await CargarCombos();
        }

        private async Task CargarTiemposDisponibles()
        {
            try
            {
                var tiempos = await _precioTiempoService.GetPreciosTiempoActivosAsync();

                TiempoComboBox.Items.Clear();
                TiempoComboBox.Items.Add(new ComboBoxItem { Content = "Sin tiempo", Tag = 0 });

                foreach (var tiempo in tiempos)
                {
                    TiempoComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = $"{tiempo.Nombre} - ${tiempo.Precio:F2}",
                        Tag = tiempo.Id
                    });
                }

                TiempoComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar tiempos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CargarProductosDisponibles()
        {
            var productos = await _productoService.GetAllProductosAsync();
            productosDisponibles = new ObservableCollection<ProductoSeleccionable>(
                productos.Select(p => new ProductoSeleccionable
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    CategoriaId = p.CategoriaId,
                    UrlImage = p.UrlImage,
                    Precio = p.Precio,
                    IsSelected = false,
                    Cantidad = 1
                })
            );
        }

        private async Task CargarCombos()
        {
            var combosDb = await _comboService.GetAllCombosAsync();
            combos = new ObservableCollection<ComboViewModel>();

            foreach (var combo in combosDb)
            {
                var comboConProductos = await _comboService.GetComboByIdAsync(combo.Id);
                var productosNombres = comboConProductos?.Productos?.Select(p => p.Nombre) ?? Enumerable.Empty<string>();

                string tiempoInfo = "Sin tiempo";
                if (combo.PrecioTiempoId.HasValue)
                {
                    var precioTiempo = await _precioTiempoService.GetPrecioTiempoByIdAsync(combo.PrecioTiempoId.Value);
                    if (precioTiempo != null)
                    {
                        tiempoInfo = precioTiempo.Nombre;
                    }
                }

                // ⭐ CAMBIO: Mostrar información apropiada si no hay productos
                string productosTexto;
                int productosCount = productosNombres.Count();

                if (productosCount > 0)
                {
                    productosTexto = string.Join(", ", productosNombres);
                }
                else if (combo.PrecioTiempoId.HasValue)
                {
                    productosTexto = "Solo tiempo";
                }
                else
                {
                    productosTexto = "Sin productos";
                }

                combos.Add(new ComboViewModel
                {
                    Id = combo.Id,
                    Nombre = combo.Nombre,
                    UrlImage = combo.UrlImage,
                    Precio = combo.Precio,
                    Productos = productosTexto,
                    ProductosCount = productosCount,
                    TiempoInfo = tiempoInfo,
                    PrecioTiempoId = combo.PrecioTiempoId,
                    Estado = string.IsNullOrEmpty(combo.Estado) ? "Activo" : combo.Estado
                });
            }

            CombosDataGrid.ItemsSource = combos;
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

            // ⭐ CAMBIO: Permitir guardar sin productos si tiene tiempo incluido
            int? precioTiempoId = null;
            if (TiempoComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                int tiempoId = Convert.ToInt32(selectedItem.Tag);
                if (tiempoId > 0)
                {
                    precioTiempoId = tiempoId;
                }
            }

            // Validación: debe tener nombre, precio y al menos productos O tiempo
            if (string.IsNullOrWhiteSpace(NombreTextBox.Text) ||
                string.IsNullOrWhiteSpace(PrecioTextBox.Text))
            {
                MessageBox.Show("Por favor completa el nombre y precio del combo.", "Campos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ⭐ NUEVA VALIDACIÓN: Debe tener productos O tiempo (al menos uno)
            if (productosSeleccionados.Count == 0 && !precioTiempoId.HasValue)
            {
                MessageBox.Show("El combo debe tener al menos productos o tiempo incluido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(PrecioTextBox.Text, out decimal precio) || precio <= 0)
            {
                MessageBox.Show("Por favor ingresa un precio válido mayor a 0.", "Precio inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string imagePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(imagenSeleccionada))
                {
                    imagePath = ImageHelper.SaveImage(imagenSeleccionada);
                }

                string estado = EstadoCheckBox.IsChecked == true ? "Activo" : "Inactivo";

                if (comboEditandoId.HasValue)
                {
                    var comboExistente = await _comboService.GetComboByIdAsync(comboEditandoId.Value);
                    if (comboExistente != null)
                    {
                        comboExistente.Nombre = NombreTextBox.Text;
                        comboExistente.UrlImage = imagePath;
                        comboExistente.Precio = precio;
                        comboExistente.PrecioTiempoId = precioTiempoId;
                        comboExistente.Estado = estado;

                        await _comboService.UpdateComboAsync(comboEditandoId.Value, comboExistente);

                        // Limpiar productos actuales
                        var productosActuales = await _comboService.GetProductosByComboIdAsync(comboEditandoId.Value);
                        foreach (var prod in productosActuales)
                        {
                            await _comboService.RemoveProductoFromComboAsync(comboEditandoId.Value, prod.Id);
                        }

                        // ⭐ CAMBIO: Solo agregar productos si hay seleccionados
                        if (productosSeleccionados.Count > 0)
                        {
                            foreach (var prod in productosSeleccionados)
                            {
                                await _comboService.AddProductoToComboAsync(comboEditandoId.Value, prod.Id, prod.Cantidad);
                            }
                        }

                        MessageBox.Show("Combo actualizado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    var nuevoCombo = new Combo
                    {
                        Nombre = NombreTextBox.Text,
                        UrlImage = imagePath,
                        Precio = precio,
                        PrecioTiempoId = precioTiempoId,
                        Estado = estado
                    };

                    var comboCreado = await _comboService.CreateComboAsync(nuevoCombo);

                    // ⭐ CAMBIO: Solo agregar productos si hay seleccionados
                    if (productosSeleccionados.Count > 0)
                    {
                        foreach (var prod in productosSeleccionados)
                        {
                            await _comboService.AddProductoToComboAsync(comboCreado.Id, prod.Id, prod.Cantidad);
                        }
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
                        PrecioTextBox.Text = combo.Precio.ToString("F2");
                        imagenSeleccionada = combo.UrlImage ?? "";

                        EstadoCheckBox.IsChecked = combo.Estado != "Inactivo";

                        if (!string.IsNullOrEmpty(imagenSeleccionada) && System.IO.File.Exists(imagenSeleccionada))
                        {
                            ImagenPreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagenSeleccionada));
                            PlaceholderText.Visibility = Visibility.Collapsed;
                        }

                        // Cargar tiempo seleccionado
                        if (combo.PrecioTiempoId.HasValue)
                        {
                            for (int i = 0; i < TiempoComboBox.Items.Count; i++)
                            {
                                if (TiempoComboBox.Items[i] is ComboBoxItem item)
                                {
                                    int tiempoId = Convert.ToInt32(item.Tag);
                                    if (tiempoId == combo.PrecioTiempoId.Value)
                                    {
                                        TiempoComboBox.SelectedIndex = i;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            TiempoComboBox.SelectedIndex = 0;
                        }

                        // ⭐ CAMBIO: Cargar productos solo si existen
                        var comboProductos = await _comboService.GetComboProductosAsync(combo.Id);

                        if (comboProductos != null && comboProductos.Any())
                        {
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
                        }
                        else
                        {
                            // Si no hay productos, limpiar todas las selecciones
                            foreach (var producto in productosDisponibles)
                            {
                                producto.IsSelected = false;
                                producto.Cantidad = 1;
                            }
                        }

                        ActualizarProductosSeleccionados();
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

        private void LimpiarFormulario()
        {
            comboEditandoId = null;
            NombreTextBox.Clear();
            PrecioTextBox.Clear();
            TiempoComboBox.SelectedIndex = 0;
            EstadoCheckBox.IsChecked = true; // NUEVO

            foreach (var producto in productosDisponibles)
            {
                producto.IsSelected = false;
                producto.Cantidad = 1;
            }

            ImagenPreview.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            imagenSeleccionada = "";

            ActualizarProductosSeleccionados();
        }

        private void ActualizarContadores()
        {
            int total = combos.Count;
            int activos = combos.Count(c => c.Estado == "Activo"); // NUEVO
            int inactivos = combos.Count(c => c.Estado == "Inactivo"); // NUEVO

            ContadorTextBlock.Text = $"Mostrando {total} de {total} combos";
            ActiveCountText.Text = $"{activos} Activos"; // NUEVO
            InactiveCountText.Text = $"{inactivos} Inactivo{(inactivos != 1 ? "s" : "")}"; // NUEVO
        }

        private void IncrementarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductoSeleccionable producto)
            {
                producto.Cantidad++;
            }
        }

        private void DecrementarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductoSeleccionable producto)
            {
                if (producto.Cantidad > 1)
                {
                    producto.Cantidad--;
                }
            }
        }

        private void AgregarProductos_Click(object sender, RoutedEventArgs e)
        {
            var context = new AppDbContext();
            var categoriaService = new CategoriaService(context);

            var window = new SeleccionarProductosWindow(productosDisponibles, categoriaService)
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
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBlock = new TextBlock
                {
                    Text = $"{producto.Nombre} (x{producto.Cantidad})",
                    FontSize = 13,
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#374151"),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var removeButton = new Button
                {
                    Content = "✕",
                    Width = 24,
                    Height = 24,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#ef4444"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Tag = producto
                };
                removeButton.Click += RemoveProducto_Click;

                Grid.SetColumn(textBlock, 0);
                Grid.SetColumn(removeButton, 1);

                grid.Children.Add(textBlock);
                grid.Children.Add(removeButton);
                border.Child = grid;

                ProductosSeleccionadosStack.Children.Add(border);
            }

            // ⭐ CAMBIO: Mensaje más descriptivo
            if (seleccionados.Count > 0)
            {
                ProductosCountText.Text = $"{seleccionados.Count} producto(s) seleccionado(s)";
            }
            else
            {
                // Verificar si hay tiempo seleccionado
                bool tieneTiempo = TiempoComboBox.SelectedItem is ComboBoxItem item && Convert.ToInt32(item.Tag) > 0;

                if (tieneTiempo)
                {
                    ProductosCountText.Text = "Este combo solo incluye tiempo";
                    ProductosCountText.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#10b981");
                }
                else
                {
                    ProductosCountText.Text = "No hay productos seleccionados";
                    ProductosCountText.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#6b7280");
                }
            }
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

        private void PrecioTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !decimal.TryParse(fullText, out _);
        }
    }

    public class ProductoSeleccionable : System.ComponentModel.INotifyPropertyChanged
    {
        private bool isSelected;
        private int cantidad = 1;

        public int Id { get; set; }
        public string Nombre { get; set; }
        public int CategoriaId { get; set; }
        public string UrlImage { get; set; }
        public decimal Precio { get; set; }

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
        public decimal Precio { get; set; }
        public string Productos { get; set; }
        public int ProductosCount { get; set; }
        public string TiempoInfo { get; set; } = "Sin tiempo";
        public int? PrecioTiempoId { get; set; }
        public string Estado { get; set; } = "Activo"; // NUEVO
    }
}