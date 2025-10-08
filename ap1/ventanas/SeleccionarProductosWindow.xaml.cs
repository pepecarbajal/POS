using POS.paginas.combos;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.ventanas
{
    public partial class SeleccionarProductosWindow : Window
    {
        public ObservableCollection<ProductoSeleccionable> ProductosSeleccionados { get; private set; }

        public SeleccionarProductosWindow(ObservableCollection<ProductoSeleccionable> productos)
        {
            InitializeComponent();
            ProductosListBox.ItemsSource = productos;
            ProductosSeleccionados = productos;
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

        private void ProductItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Obtener el Grid que disparó el evento
            var grid = sender as Grid;
            if (grid == null) return;

            // Obtener el objeto de datos (el producto)
            var producto = grid.Tag;
            if (producto == null) return;

            // Buscar si el clic fue en un botón o TextBox (para no interferir con ellos)
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource != null)
            {
                // Si el clic fue en un Button o TextBox, no hacer nada
                if (IsChildOf<Button>(originalSource) || IsChildOf<TextBox>(originalSource))
                    return;
            }

            // Alternar la propiedad IsSelected del producto
            var propertyInfo = producto.GetType().GetProperty("IsSelected");
            if (propertyInfo != null)
            {
                bool currentValue = (bool)propertyInfo.GetValue(producto);
                propertyInfo.SetValue(producto, !currentValue);
            }

            e.Handled = true;
        }

        // Método auxiliar para verificar si un elemento es hijo de un tipo específico
        private bool IsChildOf<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = child;
            while (parent != null)
            {
                if (parent is T)
                    return true;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return false;
        }
    }
}
