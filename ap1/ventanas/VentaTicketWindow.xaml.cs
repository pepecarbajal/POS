using Microsoft.Win32;
using POS.Helpers;
using POS.Models;
using POS.paginas.ventas;
using POS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace POS.ventanas
{
    public partial class VentaTicketWindow : Window
    {
        private readonly Venta _venta;
        private readonly List<ItemCarrito> _items;
        private byte[] _pdfBytes = Array.Empty<byte>();
        private readonly decimal _montoRecibido;
        private readonly decimal _cambio;
        private readonly int _anchoTicket;

        public VentaTicketWindow(Venta venta, List<ItemCarrito> items, decimal montoRecibido, decimal cambio)
        {
            InitializeComponent();
            _venta = venta;
            _items = items;
            _montoRecibido = montoRecibido;
            _cambio = cambio;

            // Cargar configuración de ancho de ticket
            var config = ConfiguracionService.CargarConfiguracion();
            _anchoTicket = config.AnchoTicket;

            CargarDatos();
            GenerarPdf();
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

        private void GenerarPdf()
        {
            try
            {
                _pdfBytes = TicketPdfGenerator.GenerarTicket(_venta, _items, _montoRecibido, _cambio, _anchoTicket);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GuardarPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Ticket_{_venta.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                    DefaultExt = ".pdf"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveFileDialog.FileName, _pdfBytes);
                    MessageBox.Show("PDF guardado exitosamente.", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Abrir el PDF
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

                // Deshabilitar botón mientras imprime
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Imprimiendo...";
                }

                // Imprimir directamente sin abrir ventanas
                await SumatraPrintService.ImprimirPdfAsync(_pdfBytes, config.ImpresoraNombre, _anchoTicket);

                // Mostrar confirmación breve
                MessageBox.Show("Ticket enviado a impresión", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);

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
            this.Close();
        }
    }
}