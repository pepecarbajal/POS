using Microsoft.Win32;
using POS.Helpers;
using POS.Models;
using POS.paginas.ventas;
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

        public VentaTicketWindow(Venta venta, List<ItemCarrito> items, decimal montoRecibido, decimal cambio)
        {
            InitializeComponent();
            _venta = venta;
            _items = items;
            _montoRecibido = montoRecibido;
            _cambio = cambio;
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
                _pdfBytes = TicketPdfGenerator.GenerarTicket(_venta, _items, _montoRecibido, _cambio);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("PDF guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Abrir el PDF
                    Process.Start(new ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Imprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Guardar temporalmente el PDF
                var tempPath = Path.Combine(Path.GetTempPath(), $"Ticket_{_venta.Id}.pdf");
                File.WriteAllBytes(tempPath, _pdfBytes);

                // Abrir con el visor predeterminado para imprimir
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

                MessageBox.Show("Se abrió el PDF. Use la opción de imprimir de su visor de PDF.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir el PDF para imprimir: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
