using System;
using System.Windows;
using System.Windows.Media.Animation;

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.OpacityProperty, fadeIn);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtEfectivoInicial.Focus();
                txtEfectivoInicial.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
