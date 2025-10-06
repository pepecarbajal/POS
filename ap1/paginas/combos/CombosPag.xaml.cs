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

namespace POS.paginas.combos
{
    public partial class CombosPag : Page
    {
        private readonly ComboService _comboService;
        private readonly ProductoService _productoService;
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
            ProductosListBox.ItemsSource = productosDisponibles;
        }

        private async Task CargarCombos()
        {
            var combosDb = await _comboService.GetAllCombosAsync();
            combos = new ObservableCollection<ComboViewModel>();

            foreach (var combo in combosDb)
            {
                var comboConProductos = await _comboService.GetComboByIdAsync(combo.Id);
                var productosNombres = comboConProductos?.Productos?.Select(p => p.Nombre) ?? Enumerable.Empty<string>();

                combos.Add(new ComboViewModel
                {
                    Id = combo.Id,
                    Nombre = combo.Nombre,
                    UrlImage = combo.UrlImage,
                    Productos = string.Join(", ", productosNombres),
                    ProductosCount = productosNombres.Count()
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

            if (string.IsNullOrWhiteSpace(NombreTextBox.Text) ||
                productosSeleccionados.Count == 0)
            {
                MessageBox.Show("Por favor completa el nombre y selecciona al menos un producto.", "Campos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (comboEditandoId.HasValue)
                {
                    var comboExistente = await _comboService.GetComboByIdAsync(comboEditandoId.Value);
                    if (comboExistente != null)
                    {
                        comboExistente.Nombre = NombreTextBox.Text;
                        comboExistente.UrlImage = imagenSeleccionada;

                        await _comboService.UpdateComboAsync(comboEditandoId.Value, comboExistente);

                        // Remove old products and add new ones
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
                        UrlImage = imagenSeleccionada
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
        }

        private void ActualizarContadores()
        {
            int total = combos.Count;
            ContadorTextBlock.Text = $"Mostrando {total} de {total} combos";
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
