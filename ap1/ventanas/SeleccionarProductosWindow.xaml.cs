using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using POS.paginas.combos;

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
    }
}
