using System.Windows;
using System.Windows.Input;

namespace POS.ventanas
{
    public partial class NombreClienteDialog : Window
    {
        public string NombreCliente { get; private set; } = string.Empty;

        public NombreClienteDialog()
        {
            InitializeComponent();
            NombreTextBox.Focus();
        }

        private void AceptarButton_Click(object sender, RoutedEventArgs e)
        {
            string nombre = NombreTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show(
                    "Por favor, ingrese un nombre válido.",
                    "Campo requerido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                NombreTextBox.Focus();
                return;
            }

            NombreCliente = nombre;
            DialogResult = true;
            Close();
        }

        private void CancelarButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NombreTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AceptarButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelarButton_Click(sender, e);
            }
        }
    }
}