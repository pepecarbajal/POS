using POS.Models;
using POS.Services;
using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace POS.ventanas
{
    public partial class CierreCajaWindow : Window
    {
        public decimal EfectivoFinal { get; private set; }
        public string? Usuario { get; private set; }
        public string? Observaciones { get; private set; }
        private readonly decimal _efectivoEsperado;
        private readonly CorteCaja _corteCaja;
        private readonly ResumenCorteCaja _resumenCorteCaja;

        // Constructor actualizado para recibir el corte y resumen
        public CierreCajaWindow(decimal efectivoEsperado, CorteCaja corteCaja, ResumenCorteCaja resumenCorteCaja)
        {
            InitializeComponent();
            _efectivoEsperado = efectivoEsperado;
            _corteCaja = corteCaja;
            _resumenCorteCaja = resumenCorteCaja;

            // Asegurarse de que los controles estén inicializados
            Loaded += CierreCajaWindow_Loaded;
        }

        private void CierreCajaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Establecer el efectivo esperado después de que la ventana esté completamente cargada
            if (txtEfectivoEsperado != null)
            {
                txtEfectivoEsperado.Text = _efectivoEsperado.ToString("N2");
            }
            // Calcular diferencia inicial (será 0 - esperado = negativo)
            CalcularDiferencia();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Animación de entrada
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.OpacityProperty, fadeIn);

            // Establecer el efectivo esperado
            if (txtEfectivoEsperado != null)
            {
                txtEfectivoEsperado.Text = _efectivoEsperado.ToString("N2");
            }

            CalcularDiferencia();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtEfectivoFinal.Focus();
                txtEfectivoFinal.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void TxtEfectivoFinal_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CalcularDiferencia();
        }

        private void CalcularDiferencia()
        {
            // Verificar que los controles no sean null
            if (txtEfectivoFinal == null || txtDiferencia == null)
                return;

            if (decimal.TryParse(txtEfectivoFinal.Text, out decimal monto))
            {
                var diferencia = monto - _efectivoEsperado;
                txtDiferencia.Text = diferencia.ToString("N2");

                if (Math.Abs(diferencia) < 0.01m)
                {
                    txtDiferencia.Foreground = System.Windows.Media.Brushes.Green;
                }
                else if (diferencia > 0)
                {
                    txtDiferencia.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    txtDiferencia.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            else
            {
                // Si el texto no es un número válido, mostrar 0 - esperado
                var diferencia = 0 - _efectivoEsperado;
                txtDiferencia.Text = diferencia.ToString("N2");
                txtDiferencia.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtEfectivoFinal.Text, out decimal monto) && monto >= 0)
            {
                EfectivoFinal = monto;
                Usuario = txtUsuario.Text;
                Observaciones = txtObservaciones.Text;

                // Actualizar el objeto CorteCaja con los datos del cierre
                _corteCaja.EfectivoFinal = monto;
                _corteCaja.UsuarioCierre = txtUsuario.Text;
                _corteCaja.Observaciones = txtObservaciones.Text;
                _corteCaja.FechaCierre = DateTime.Now;
                _corteCaja.EstaCerrado = true;

                // Calcular la diferencia
                _corteCaja.Diferencia = monto - _efectivoEsperado;

                // Intentar imprimir el ticket automáticamente
                try
                {
                    var ticketService = new TicketImpresionService();
                    ticketService.ImprimirCorteCaja(_corteCaja, _resumenCorteCaja);

                    MessageBox.Show("Cierre de caja completado.\nTicket enviado a impresión.",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    // Si hay error al imprimir, mostrar mensaje pero no cancelar el cierre
                    MessageBox.Show($"El cierre se realizó correctamente, pero hubo un error al imprimir:\n{ex.Message}",
                        "Advertencia",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Ingrese un monto válido mayor o igual a cero.",
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