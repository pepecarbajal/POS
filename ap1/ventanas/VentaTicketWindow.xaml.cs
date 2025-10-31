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
    /// Ventana que muestra el ticket de venta y genera automáticamente el ticket de pedido
    /// </summary>
    public partial class VentaTicketWindow : Window
    {
        private readonly Venta _venta;
        private readonly List<ItemCarrito> _items;
        private byte[] _pdfVentaBytes = Array.Empty<byte>();
        private byte[] _pdfPedidoBytes = Array.Empty<byte>();
        private readonly decimal _montoRecibido;
        private readonly decimal _cambio;
        private readonly int _anchoTicket;
        private readonly AppDbContext _context;

        public VentaTicketWindow(Venta venta, List<ItemCarrito> items, decimal montoRecibido, decimal cambio)
        {
            InitializeComponent();
            _venta = venta;
            _items = items;
            _montoRecibido = montoRecibido;
            _cambio = cambio;

            // Inicializar contexto para ticket de pedido
            _context = new AppDbContext();

            // Cargar configuración de ancho de ticket
            var config = ConfiguracionService.CargarConfiguracion();
            _anchoTicket = config.AnchoTicket;

            CargarDatos();
            GenerarPdfs();
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

        /// <summary>
        /// Genera ambos PDFs: ticket de venta y ticket de pedido
        /// </summary>
        private async void GenerarPdfs()
        {
            try
            {
                // 1. Generar ticket de VENTA (para el cliente)
                _pdfVentaBytes = TicketPdfGenerator.GenerarTicket(_venta, _items, _montoRecibido, _cambio, _anchoTicket);

                // 2. Generar ticket de PEDIDO (para cocina/barra)
                try
                {
                    var ticketPedidoGenerator = new TicketPedidoPdfGenerator(_context);
                    _pdfPedidoBytes = await ticketPedidoGenerator.GenerarTicketPedidoAsync(
                        venta: _venta,
                        items: _items,
                        nombreMesero: "Cajero",
                        numeroMesa: "Venta", // Puedes personalizarlo
                        anchoMm: _anchoTicket
                    );

                    // El ticket de pedido se genera pero NO se abre automáticamente
                    // El usuario puede abrirlo/imprimirlo desde los botones
                }
                catch (Exception pedidoEx)
                {
                    // Si falla el ticket de pedido, no afecta la venta
                    System.Diagnostics.Debug.WriteLine($"Error generando ticket de pedido: {pedidoEx.Message}");
                    MessageBox.Show(
                        "Se generó el ticket de venta pero hubo un problema con el ticket de pedido.\n\n" +
                        $"Error: {pedidoEx.Message}",
                        "Advertencia",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar los PDFs: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Abre el ticket de pedido automáticamente en el visor PDF predeterminado
        /// </summary>
        private void AbrirTicketPedido()
        {
            try
            {
                if (_pdfPedidoBytes == null || _pdfPedidoBytes.Length == 0)
                    return;

                // Guardar ticket de pedido en carpeta temporal
                var carpetaTemp = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "POS",
                    "tickets_pedidos"
                );

                if (!Directory.Exists(carpetaTemp))
                {
                    Directory.CreateDirectory(carpetaTemp);
                }

                var nombreArchivo = $"Pedido_{_venta.Id:D4}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var rutaCompleta = Path.Combine(carpetaTemp, nombreArchivo);

                File.WriteAllBytes(rutaCompleta, _pdfPedidoBytes);

                // Abrir el PDF
                Process.Start(new ProcessStartInfo(rutaCompleta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error abriendo ticket de pedido: {ex.Message}");
            }
        }

        private void GuardarPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Ticket_Venta_{_venta.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                    DefaultExt = ".pdf"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Guardar ticket de venta
                    File.WriteAllBytes(saveFileDialog.FileName, _pdfVentaBytes);

                    // Preguntar si también quiere guardar el ticket de pedido
                    if (_pdfPedidoBytes != null && _pdfPedidoBytes.Length > 0)
                    {
                        var result = MessageBox.Show(
                            "Ticket de venta guardado.\n\n¿Desea guardar también el ticket de pedido?",
                            "Guardar Ticket de Pedido",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            var savePedidoDialog = new SaveFileDialog
                            {
                                Filter = "PDF files (*.pdf)|*.pdf",
                                FileName = $"Ticket_Pedido_{_venta.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                                DefaultExt = ".pdf"
                            };

                            if (savePedidoDialog.ShowDialog() == true)
                            {
                                File.WriteAllBytes(savePedidoDialog.FileName, _pdfPedidoBytes);
                                MessageBox.Show("Ambos tickets guardados exitosamente.", "Éxito",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Ticket de venta guardado exitosamente.", "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Ticket de venta guardado exitosamente.", "Éxito",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    // Abrir el ticket de venta guardado
                    Process.Start(new ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el PDF: {ex.Message}", "Error",
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

                var rutaSumatra = SumatraPrintService.EncontrarSumatra();
                if (string.IsNullOrEmpty(rutaSumatra))
                {
                    MessageBox.Show("SumatraPDF no está instalado o no se puede encontrar.\n\n" +
                        "Por favor, descargue e instale SumatraPDF desde:\n" +
                        "https://www.sumatrapdfreader.org/download-free-pdf-viewer\n\n" +
                        "O configure la ruta en Administración > Ajustes",
                        "SumatraPDF no encontrado",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Abrir tu ventana personalizada VentanaImprimir
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

                if (tipoSeleccionado == VentanaImprimir.TipoTicket.Venta)
                {
                    // Imprimir ticket de venta DOS VECES
                    await SumatraPrintService.ImprimirPdfAsync(_pdfVentaBytes, config.ImpresoraNombre, _anchoTicket);
                    await System.Threading.Tasks.Task.Delay(1000); // Esperar 1 segundo entre impresiones
                    await SumatraPrintService.ImprimirPdfAsync(_pdfVentaBytes, config.ImpresoraNombre, _anchoTicket);
                }
                else if (tipoSeleccionado == VentanaImprimir.TipoTicket.Pedido)
                {
                    // Imprimir solo ticket de pedido UNA VEZ
                    if (_pdfPedidoBytes != null && _pdfPedidoBytes.Length > 0)
                    {
                        await SumatraPrintService.ImprimirPdfAsync(_pdfPedidoBytes, config.ImpresoraNombre, _anchoTicket);
                    }
                    else
                    {
                        MessageBox.Show("❌ No hay ticket de pedido disponible", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (tipoSeleccionado == VentanaImprimir.TipoTicket.Ambos)
                {
                    // Imprimir ticket de venta DOS VECES
                    await SumatraPrintService.ImprimirPdfAsync(_pdfVentaBytes, config.ImpresoraNombre, _anchoTicket);
                    await System.Threading.Tasks.Task.Delay(1000);
                    await SumatraPrintService.ImprimirPdfAsync(_pdfVentaBytes, config.ImpresoraNombre, _anchoTicket);

                    // Imprimir ticket de pedido UNA VEZ
                    if (_pdfPedidoBytes != null && _pdfPedidoBytes.Length > 0)
                    {
                        await System.Threading.Tasks.Task.Delay(1000);
                        await SumatraPrintService.ImprimirPdfAsync(_pdfPedidoBytes, config.ImpresoraNombre, _anchoTicket);
                    }
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
                    "• SumatraPDF esté correctamente instalado\n" +
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