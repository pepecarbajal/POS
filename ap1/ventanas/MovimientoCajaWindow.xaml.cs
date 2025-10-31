using POS.Models;
using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace POS.ventanas
{
    public partial class MovimientoCajaWindow : Window
    {
        public decimal Monto { get; private set; }
        public string Concepto { get; private set; }
        public string? Observaciones { get; private set; }
        public string? Usuario { get; private set; }

        private readonly TipoMovimiento _tipoMovimiento;

        public MovimientoCajaWindow(TipoMovimiento tipoMovimiento)
        {
            InitializeComponent();
            _tipoMovimiento = tipoMovimiento;

            Title = tipoMovimiento == TipoMovimiento.Deposito ? "Registrar Depósito" : "Registrar Retiro";
            lblTitulo.Text = tipoMovimiento == TipoMovimiento.Deposito ? "Depósito a Caja" : "Retiro de Caja";
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
                txtMonto.Focus();
                txtMonto.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtMonto.Text, out decimal monto) && monto > 0)
            {
                if (string.IsNullOrWhiteSpace(txtConcepto.Text))
                {
                    MessageBox.Show("Debe ingresar un concepto.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Monto = monto;
                Concepto = txtConcepto.Text;
                Observaciones = txtObservaciones.Text;
                Usuario = txtUsuario.Text;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Ingrese un monto válido mayor a cero.",
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