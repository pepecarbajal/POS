using System.Windows;
using System.Windows.Threading;
using System;
using POS.Ventanas;

namespace POS
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer clockTimer = new DispatcherTimer();
        private bool isSubmenuOpen = false;
        private bool isAdminAuthenticated = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeClock();
        }

        private void InitializeClock()
        {
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += ClockTimer_Tick;
            clockTimer.Start();

            UpdateClock();
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            ClockTextBlock.Text = DateTime.Now.ToString("hh:mm tt");
        }

        private void ConfiguracionButton_Click(object sender, RoutedEventArgs e)
        {
            // Si el submenú está abierto, solo cerrarlo
            if (isSubmenuOpen)
            {
                isSubmenuOpen = false;
                SubmenuPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Si el submenú está cerrado, pedir contraseña antes de abrirlo
            var passwordDialog = new PasswordDialog
            {
                Owner = this
            };
            passwordDialog.ShowDialog();

            if (!passwordDialog.IsAuthenticated)
            {
                return; // Salir si la autenticación falló
            }

            // Si la autenticación fue exitosa, abrir el submenú
            isSubmenuOpen = true;
            SubmenuPanel.Visibility = Visibility.Visible;
        }

        private void ProductosButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Uri("paginas/productos/ProductosPag.xaml", UriKind.Relative));
        }

        private void CategoriasButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Uri("paginas/categorias/CategoriasPag.xaml", UriKind.Relative));
        }

        private void VentasButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Uri("paginas/ventas/VentasPag.xaml", UriKind.Relative));
        }

        private void CombosButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Uri("paginas/combos/CombosPag.xaml", UriKind.Relative));
        }

        private void DetallesVentasButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Uri("paginas/detalles-ventas/DetallesVentasPag.xaml", UriKind.Relative));
        }

        private void PrecioTiempoButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Uri("paginas/precioTiempo/precioTiempoPag.xaml", UriKind.Relative));
        }
    }
}