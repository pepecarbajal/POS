using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using POS.Services;
using POS.Interfaces;
using POS.Models;
using POS.Data;
namespace POS.paginas.categoria
{
    public partial class CategoriasPag : Page
    {
        private readonly ICategoriaService _categoriaService;
        public ObservableCollection<Categoria> Categorias { get; set; }
        private Categoria? _categoriaEnEdicion;

        public CategoriasPag()
        {
            InitializeComponent();

            var context = new AppDbContext();
            _categoriaService = new CategoriaService(context);

            Categorias = new ObservableCollection<Categoria>();
            CategoriasDataGrid.ItemsSource = Categorias;

            LoadCategoriasAsync();
        }

        private async void LoadCategoriasAsync()
        {
            try
            {
                var categorias = await _categoriaService.GetAllCategoriasAsync();
                Categorias.Clear();
                foreach (var categoria in categorias)
                {
                    Categorias.Add(categoria);
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
        }

        private void EditarCategoria_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var categoria = button?.Tag as Categoria;

            if (categoria == null) return;

            _categoriaEnEdicion = categoria;
            NombreCategoriaTextBox.Text = categoria.Nombre;
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

        private void ActualizarContador()
        {
            int total = Categorias.Count;
            CategoriaCountText.Text = $"Mostrando {total} de {total} categorías";
        }
    }
}
