using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using POS.Services;
using POS.Interfaces;
using POS.Models;
using POS.Data;
using System.Linq;

namespace POS.paginas.categoria
{
    public partial class CategoriasPag : Page
    {
        private readonly ICategoriaService _categoriaService;
        public ObservableCollection<Categoria> Categorias { get; set; }
        public ObservableCollection<Categoria> CategoriasFiltradas { get; set; }
        private Categoria? _categoriaEnEdicion;
        private bool _isSearchPlaceholder = true;

        public CategoriasPag()
        {
            InitializeComponent();

            var context = new AppDbContext();
            _categoriaService = new CategoriaService(context);

            Categorias = new ObservableCollection<Categoria>();
            CategoriasFiltradas = new ObservableCollection<Categoria>();
            CategoriasDataGrid.ItemsSource = CategoriasFiltradas;

            LoadCategoriasAsync();
        }

        private async void LoadCategoriasAsync()
        {
            try
            {
                var categorias = await _categoriaService.GetAllCategoriasAsync();
                Categorias.Clear();
                CategoriasFiltradas.Clear();

                foreach (var categoria in categorias)
                {
                    Categorias.Add(categoria);
                    CategoriasFiltradas.Add(categoria);
                }

                ActualizarContador();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar categorías: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GuardarCategoria_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreCategoriaTextBox.Text))
            {
                MessageBox.Show("Por favor ingrese el nombre de la categoría", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_categoriaEnEdicion != null)
                {
                    // Updating existing category
                    _categoriaEnEdicion.Nombre = NombreCategoriaTextBox.Text;
                    var resultado = await _categoriaService.UpdateCategoriaAsync(_categoriaEnEdicion.Id, _categoriaEnEdicion);

                    if (resultado)
                    {
                        LoadCategoriasAsync();
                        LimpiarFormulario();
                        MessageBox.Show("Categoría actualizada exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No se pudo actualizar la categoría", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Creating new category
                    var nuevaCategoria = new Categoria
                    {
                        Nombre = NombreCategoriaTextBox.Text
                    };

                    var categoriaCreada = await _categoriaService.CreateCategoriaAsync(nuevaCategoria);
                    Categorias.Add(categoriaCreada);
                    CategoriasFiltradas.Add(categoriaCreada);
                    LimpiarFormulario();
                    ActualizarContador();
                    MessageBox.Show("Categoría agregada exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar categoría: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var button = sender as Button;
            var categoria = button?.Tag as Categoria;

            if (categoria == null) return;

            _categoriaEnEdicion = categoria;
            NombreCategoriaTextBox.Text = categoria.Nombre;
            GuardarButton.Content = "Actualizar Categoría";
            CancelarButton.Visibility = Visibility.Visible;
        }

        private void CancelarEdicion_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
        }

        private async void EliminarCategoria_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var categoria = button?.Tag as Categoria;

            if (categoria == null) return;

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar la categoría '{categoria.Nombre}'?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var eliminado = await _categoriaService.DeleteCategoriaAsync(categoria.Id);

                    if (eliminado)
                    {
                        Categorias.Remove(categoria);
                        CategoriasFiltradas.Remove(categoria);
                        ActualizarContador();
                        MessageBox.Show("Categoría eliminada exitosamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No se pudo eliminar la categoría", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar categoría: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
                SearchTextBox.Text = "Buscar categorías...";
                SearchTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6b7280"));
                _isSearchPlaceholder = true;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSearchPlaceholder) return;

            FiltrarCategorias();
        }

        private void FiltrarCategorias()
        {
            var searchText = SearchTextBox.Text?.ToLower() ?? "";

            CategoriasFiltradas.Clear();

            var categoriasFiltradas = Categorias.Where(c =>
                c.Nombre.ToLower().Contains(searchText)
            );

            foreach (var categoria in categoriasFiltradas)
            {
                CategoriasFiltradas.Add(categoria);
            }

            ActualizarContador();
        }

        private void ActualizarContador()
        {
            int total = Categorias.Count;
            int filtradas = CategoriasFiltradas.Count;

            if (filtradas == total)
            {
                CategoriaCountText.Text = $"Mostrando {total} de {total} categorías";
            }
            else
            {
                CategoriaCountText.Text = $"Mostrando {filtradas} de {total} categorías";
            }
        }
    }
}