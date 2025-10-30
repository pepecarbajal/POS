using System.Windows;

namespace POS.ventanas
{
    public partial class VentanaImprimir : Window
    {
        public enum TipoTicket
        {
            Ninguno,
            Venta,
            Pedido,
            Ambos
        }

        public TipoTicket TicketSeleccionado { get; private set; } = TipoTicket.Ninguno;

        public VentanaImprimir()
        {
            InitializeComponent();
        }

        private void VentaButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Venta;
            this.DialogResult = true;
            this.Close();
        }

        private void PedidoButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Pedido;
            this.DialogResult = true;
            this.Close();
        }

        private void AmbosButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Ambos;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelarButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Ninguno;
            this.DialogResult = false;
            this.Close();
        }
    }
}