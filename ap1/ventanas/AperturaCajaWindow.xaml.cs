using System;
using System.Windows;

namespace POS.ventanas
{
    public partial class AperturaCajaWindow : Window
    {
        public decimal EfectivoInicial { get; private set; }
        public string? Observaciones { get; private set; }

        public AperturaCajaWindow()
        {
            InitializeComponent();
        }

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtEfectivoInicial.Text, out decimal monto) && monto >= 0)
            {
                EfectivoInicial = monto;
                Observaciones = txtObservaciones.Text;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Ingrese un monto válido.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
