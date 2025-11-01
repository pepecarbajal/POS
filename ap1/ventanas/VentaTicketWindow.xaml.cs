using Microsoft.Win32;
using POS.Data;
using POS.Helpers;
using POS.Models;
using POS.paginas.ventas;
using POS.paginas.ventas.Managers;
using POS.Services;
using System;
using POS.ventanas;
using System.Diagnostics;
using System.IO;
using System.Windows;
using POS.ventanas;

namespace POS.ventanas
{
    /// <summary>
    /// Ventana que muestra el ticket de venta y puede imprimir tickets de venta y pedido
    /// </summary>
    public partial class VentaTicketWindow : Window
    {
        private readonly Venta _venta;
        private readonly List<ItemCarrito> _items;
        private readonly decimal _montoRecibido;
        private readonly decimal _cambio;
        private readonly int _anchoTicket;
        private readonly AppDbContext _context;
        private readonly TicketImpresionService _ticketImpresionService;

        public VentaTicketWindow(Venta venta, List<ItemCarrito> items, decimal montoRecibido, decimal cambio)
        {
            InitializeComponent();
            _venta = venta;
            _items = items;
            _montoRecibido = montoRecibido;
            _cambio = cambio;

            // Inicializar contexto y servicio de impresión
            _context = new AppDbContext();
            _ticketImpresionService = new TicketImpresionService(_context);

            // Cargar configuración de ancho de ticket
            var config = ConfiguracionService.CargarConfiguracion();
            _anchoTicket = config.AnchoTicket;

            CargarDatos();
        }

        private void CargarDatos()
        {
            FechaTextBlock.Text = $"Fecha: {_venta.Fecha:dd/MM/yyyy HH:mm}";
            TicketNumeroTextBlock.Text = $"Ticket #: {_venta.Id}";
            ItemsListView.ItemsSource = _items;
            TotalTextBlock.Text = $"${_venta.Total:F2}";
            RecibidoTextBlock.Text = $"${_montoRecibido:F2}";
            CambioTextBlock.Text = $"${_cambio:F2}";
        }

        private void GuardarPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // El sistema ahora usa impresión directa, no genera PDFs para guardar
                var resultado = MessageBox.Show(
                    "El sistema ahora usa impresión directa.\n\n" +
                    "¿Desea imprimir el ticket en lugar de guardarlo?",
                    "Impresión Directa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    // Llamar al método de impresión
                    Imprimir_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Imprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfiguracionService.CargarConfiguracion();

                if (string.IsNullOrEmpty(config.ImpresoraNombre))
                {
                    MessageBox.Show("No hay una impresora configurada.\n\n" +
                        "Por favor, configure la impresora en Administración > Ajustes",
                        "Impresora no configurada",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Abrir ventana de selección de tipo de ticket
                var ventanaImprimir = new VentanaImprimir
                {
                    Owner = this
                };

                if (ventanaImprimir.ShowDialog() != true)
                {
                    return; // Usuario canceló
                }

                // Deshabilitar botón mientras imprime
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Imprimiendo...";
                }

                var tipoSeleccionado = ventanaImprimir.TicketSeleccionado;

                try
                {
                    if (tipoSeleccionado == VentanaImprimir.TipoTicket.Venta)
                    {
                        // Imprimir ticket de venta DOS VECES usando impresión directa
                        _ticketImpresionService.ImprimirTicketVenta(_venta, _items, _montoRecibido, _cambio);
                        await System.Threading.Tasks.Task.Delay(500); // Pequeña pausa entre impresiones
                        _ticketImpresionService.ImprimirTicketVenta(_venta, _items, _montoRecibido, _cambio);

                        MessageBox.Show("Ticket de venta impreso correctamente (2 copias).", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (tipoSeleccionado == VentanaImprimir.TipoTicket.Pedido)
                    {
                        // Imprimir solo ticket de pedido UNA VEZ
                        var itemsParaPedido = _items.Where(i => i.ProductoId != -998 && i.ProductoId != -999).ToList();

                        if (itemsParaPedido.Any())
                        {
                            await _ticketImpresionService.ImprimirTicketPedidoAsync(
                                venta: _venta,
                                items: itemsParaPedido,
                                nombreMesero: "Cajero",
                                numeroMesa: "Venta"
                            );

                            MessageBox.Show("Ticket de pedido impreso correctamente.", "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("No hay items para imprimir en el ticket de pedido.", "Información",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else if (tipoSeleccionado == VentanaImprimir.TipoTicket.Ambos)
                    {
                        // Imprimir ticket de venta DOS VECES
                        _ticketImpresionService.ImprimirTicketVenta(_venta, _items, _montoRecibido, _cambio);
                        await System.Threading.Tasks.Task.Delay(500);
                        _ticketImpresionService.ImprimirTicketVenta(_venta, _items, _montoRecibido, _cambio);

                        // Imprimir ticket de pedido UNA VEZ
                        var itemsParaPedido = _items.Where(i => i.ProductoId != -998 && i.ProductoId != -999).ToList();

                        if (itemsParaPedido.Any())
                        {
                            await System.Threading.Tasks.Task.Delay(500);
                            await _ticketImpresionService.ImprimirTicketPedidoAsync(
                                venta: _venta,
                                items: itemsParaPedido,
                                nombreMesero: "Cajero",
                                numeroMesa: "Venta"
                            );
                        }

                        MessageBox.Show("Tickets impresos correctamente (2 copias de venta + 1 de pedido).", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception printEx)
                {
                    MessageBox.Show($"Error al imprimir: {printEx.Message}\n\n" +
                        "Verifique que:\n" +
                        "• La impresora esté encendida y conectada\n" +
                        "• La impresora esté configurada correctamente en Ajustes",
                        "Error de impresión",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Restaurar botón
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Imprimir";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al imprimir: {ex.Message}\n\n" +
                    "Verifique que:\n" +
                    "• La impresora esté encendida y conectada\n" +
                    "• La impresora esté configurada en Ajustes",
                    "Error de impresión",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Restaurar botón en caso de error
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Imprimir";
                }
            }
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            // Limpiar recursos
            _context?.Dispose();
            this.Close();
        }
    }
}