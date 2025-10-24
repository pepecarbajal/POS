using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using POS.Interfaces;
using POS.Models;
using POS.Data;
using POS.Services;
using System.Linq;
using System.Threading.Tasks;

namespace POS.paginas.categoria
{
    public partial class CategoriasPag : Page
    {
        private readonly ICategoriaService _categoriaService;
        private readonly ObservableCollection<Categoria> _categorias;
        private readonly ObservableCollection<Categoria> _categoriasFiltradas;
        private Categoria? _categoriaEnEdicion;
        private bool _isSearchPlaceholder = true;

        public CategoriasPag()
        {
            InitializeComponent();

            var context = new AppDbContext();
            _categoriaService = new CategoriaService(context);

            _categorias = new ObservableCollection<Categoria>();
            _categoriasFiltradas = new ObservableCollection<Categoria>();
            CategoriasDataGrid.ItemsSource = _categoriasFiltradas;

            _ = LoadCategoriasAsync();
        }

        private async Task LoadCategoriasAsync()
        {
            try
            {
                var categorias = await _categoriaService.GetAllCategoriasAsync();
                
                _categorias.Clear();
                _categoriasFiltradas.Clear();

                foreach (var categoria in categorias)
                {
                    _categorias.Add(categoria);
                    _categoriasFiltradas.Add(categoria);
                }

                ActualizarContador();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al cargar categorías: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GuardarCategoria_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreCategoriaTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese el nombre de la categoría", "Campo requerido", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_categoriaEnEdicion != null)
                {
                    _categoriaEnEdicion.Nombre = NombreCategoriaTextBox.Text;
                    var resultado = await _categoriaService.UpdateCategoriaAsync(
                        _categoriaEnEdicion.Id, _categoriaEnEdicion);

                    if (resultado)
                    {
                        await LoadCategoriasAsync();
                        LimpiarFormulario();
                    }
                    else
                    {
                        MessageBox.Show("No se pudo actualizar la categoría", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    var nuevaCategoria = new Categoria { Nombre = NombreCategoriaTextBox.Text };
                    var categoriaCreada = await _categoriaService.CreateCategoriaAsync(nuevaCategoria);
                    
                    _categorias.Add(categoriaCreada);
                    _categoriasFiltradas.Add(categoriaCreada);
                    LimpiarFormulario();
                    ActualizarContador();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al guardar categoría: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimpiarFormulario()
        {
            NombreCategoriaTextBox.Clear();
            _categoriaEnEdicion = null;
            GuardarButton.Content = "Guardar Categoría";
            CancelarButton.Visibility = Visibility.Collapsed;
        }

        private void EditarCategoria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Categoria categoria })
            {
                _categoriaEnEdicion = categoria;
                NombreCategoriaTextBox.Text = categoria.Nombre;
                GuardarButton.Content = "Actualizar Categoría";
                CancelarButton.Visibility = Visibility.Visible;
            }
        }

        private void CancelarEdicion_Click(object sender, RoutedEventArgs e) => LimpiarFormulario();

        private async void EliminarCategoria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: Categoria categoria }) return;

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar la categoría '{categoria.Nombre}'?",
                "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var eliminado = await _categoriaService.DeleteCategoriaAsync(categoria.Id);

                if (eliminado)
                {
                    _categorias.Remove(categoria);
                    _categoriasFiltradas.Remove(categoria);
                    ActualizarContador();
                }
                else
                {
                    MessageBox.Show("No se pudo eliminar la categoría", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al eliminar categoría: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!_isSearchPlaceholder) return;
            
            SearchTextBox.Text = "";
            SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(31, 41, 55));
            _isSearchPlaceholder = false;
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text)) return;
            
            SearchTextBox.Text = "Buscar categorías...";
            SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(107, 114, 128));
            _isSearchPlaceholder = true;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSearchPlaceholder) return;
            FiltrarCategorias();
        }

        private void FiltrarCategorias()
        {
            var searchText = SearchTextBox.Text?.ToLower() ?? "";

            _categoriasFiltradas.Clear();

            var categoriasFiltradas = string.IsNullOrEmpty(searchText)
                ? _categorias
                : _categorias.Where(c => c.Nombre.Contains(searchText, 
                    System.StringComparison.OrdinalIgnoreCase));

            foreach (var categoria in categoriasFiltradas)
            {
                _categoriasFiltradas.Add(categoria);
            }

            ActualizarContador();
        }

        private void ActualizarContador()
        {
            int total = _categorias.Count;
            int filtradas = _categoriasFiltradas.Count;

            CategoriaCountText.Text = filtradas == total
                ? $"Mostrando {total} de {total} categorías"
                : $"Mostrando {filtradas} de {total} categorías";
        }
    }
}