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
        public int CopiasVenta { get; private set; } = 0;
        public int CopiasPedido { get; private set; } = 0;

        public VentanaImprimir()
        {
            InitializeComponent();
        }

        private void VentaButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Venta;
            CopiasVenta = 2; // Dos copias del ticket de venta
            CopiasPedido = 0;
            this.DialogResult = true;
            this.Close();
        }

        private void PedidoButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Pedido;
            CopiasVenta = 0;
            CopiasPedido = 1; // Una copia del ticket de pedido
            this.DialogResult = true;
            this.Close();
        }

        private void AmbosButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Ambos;
            CopiasVenta = 2; // Dos copias del ticket de venta
            CopiasPedido = 1; // Una copia del ticket de pedido
            this.DialogResult = true;
            this.Close();
        }

        private void CancelarButton_Click(object sender, RoutedEventArgs e)
        {
            TicketSeleccionado = TipoTicket.Ninguno;
            CopiasVenta = 0;
            CopiasPedido = 0;
            this.DialogResult = false;
            this.Close();
        }
    }
}