using POS.Models;
using POS.paginas.combos;
using POS.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.ventanas
{
    public partial class SeleccionarProductosWindow : Window
    {
        public ObservableCollection<ProductoSeleccionable> ProductosSeleccionados { get; private set; }
        private ObservableCollection<ProductoSeleccionable> todosLosProductos;
        private readonly CategoriaService _categoriaService;

        public SeleccionarProductosWindow(ObservableCollection<ProductoSeleccionable> productos, CategoriaService categoriaService)
        {
            InitializeComponent();
            _categoriaService = categoriaService;
            todosLosProductos = productos;
            ProductosSeleccionados = productos;
            CargarCategorias();
        }

        private async void CargarCategorias()
        {
            var categorias = await _categoriaService.GetAllCategoriasAsync();
            var categoriasList = new List<CategoriaItem>
            {
                new CategoriaItem { Id = 0, Nombre = "Todas las categorías" }
            };

            categoriasList.AddRange(categorias.Select(c => new CategoriaItem
            {
                Id = c.Id,
                Nombre = c.Nombre
            }));

            CategoriaComboBox.ItemsSource = categoriasList;
            CategoriaComboBox.SelectedIndex = 0;
        }

        private void ProductCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ProductoSeleccionable producto)
            {
                producto.IsSelected = !producto.IsSelected;
            }
        }

        private void CategoriaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriaComboBox.SelectedItem is CategoriaItem categoriaSeleccionada)
            {
                if (categoriaSeleccionada.Id == 0)
                {
                    // Show all products
                    ProductosListBox.ItemsSource = todosLosProductos;
                }
                else
                {
                    // Filter by selected category
                    var productosFiltrados = todosLosProductos
                        .Where(p => p.CategoriaId == categoriaSeleccionada.Id)
                        .ToList();
                    ProductosListBox.ItemsSource = productosFiltrados;
                }
            }
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

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class CategoriaItem
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
    }
}
