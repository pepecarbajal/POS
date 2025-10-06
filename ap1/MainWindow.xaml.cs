using System.Windows;
using System.Windows.Threading;
using System;

namespace POS
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer clockTimer = new DispatcherTimer();
        private bool isSubmenuOpen = false;

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
            ClockTextBlock.Text = DateTime.Now.ToString("HH:mm");
        }

        private void ConfiguracionButton_Click(object sender, RoutedEventArgs e)
        {
            isSubmenuOpen = !isSubmenuOpen;
            SubmenuPanel.Visibility = isSubmenuOpen ? Visibility.Visible : Visibility.Collapsed;
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
    }
}
