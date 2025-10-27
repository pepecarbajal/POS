using POS.Helpers;
using POS.Models;
using POS.paginas.ventas;
using POS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.paginas.ajustes
{
    public partial class AjustesPag : Page
    {
        private int _anchoSeleccionado = 80;

        public AjustesPag()
        {
            InitializeComponent();
            CargarConfiguracion();
            VerificarSumatra();
        }

        private void CargarConfiguracion()
        {
            try
            {
                // Cargar impresoras disponibles
                var impresoras = ConfiguracionService.ObtenerImpresorasDisponibles();
                ImpresoraComboBox.ItemsSource = impresoras;

                // Cargar configuración guardada
                var config = ConfiguracionService.CargarConfiguracion();

                // Seleccionar impresora guardada
                if (!string.IsNullOrEmpty(config.ImpresoraNombre) && impresoras.Contains(config.ImpresoraNombre))
                {
                    ImpresoraComboBox.SelectedItem = config.ImpresoraNombre;
                }
                else if (impresoras.Any())
                {
                    // Si no hay guardada, seleccionar la predeterminada del sistema
                    var predeterminada = ConfiguracionService.ObtenerImpresoraPredeterminada();
                    if (!string.IsNullOrEmpty(predeterminada) && impresoras.Contains(predeterminada))
                    {
                        ImpresoraComboBox.SelectedItem = predeterminada;
                    }
                    else
                    {
                        ImpresoraComboBox.SelectedIndex = 0;
                    }
                }

                // Seleccionar ancho guardado
                _anchoSeleccionado = config.AnchoTicket;
                ActualizarSeleccionAncho(_anchoSeleccionado);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar configuración: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VerificarSumatra()
        {
            var rutaSumatra = SumatraPrintService.EncontrarSumatra();

            if (!string.IsNullOrEmpty(rutaSumatra))
            {
                SumatraEstadoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1fae5"));
                SumatraIcono.Text = "✓";
                SumatraIcono.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
                SumatraTexto.Text = $"SumatraPDF detectado: {rutaSumatra}";
                SumatraTexto.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            }
            else
            {
                SumatraEstadoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fecaca"));
                SumatraIcono.Text = "⚠";
                SumatraIcono.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626"));
                SumatraTexto.Text = "SumatraPDF no detectado. Por favor, instale SumatraPDF para habilitar la impresión directa.";
                SumatraTexto.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626"));
            }
        }

        private void ActualizarSeleccionAncho(int ancho)
        {
            // Reset todos
            ResetearBordeAncho(Ancho50Border, Check50);
            ResetearBordeAncho(Ancho58Border, Check58);
            ResetearBordeAncho(Ancho80Border, Check80);

            // Seleccionar el activo
            switch (ancho)
            {
                case 50:
                    SeleccionarBordeAncho(Ancho50Border, Check50);
                    break;
                case 58:
                    SeleccionarBordeAncho(Ancho58Border, Check58);
                    break;
                case 80:
                    SeleccionarBordeAncho(Ancho80Border, Check80);
                    break;
            }

            _anchoSeleccionado = ancho;
        }

        private void ResetearBordeAncho(Border border, Border check)
        {
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e5e7eb"));
            border.Background = Brushes.White;
            check.Visibility = Visibility.Collapsed;
        }

        private void SeleccionarBordeAncho(Border border, Border check)
        {
            border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b5cf6"));
            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#faf5ff"));
            check.Visibility = Visibility.Visible;
        }

        private void Ancho50_Click(object sender, MouseButtonEventArgs e)
        {
            ActualizarSeleccionAncho(50);
        }

        private void Ancho58_Click(object sender, MouseButtonEventArgs e)
        {
            ActualizarSeleccionAncho(58);
        }

        private void Ancho80_Click(object sender, MouseButtonEventArgs e)
        {
            ActualizarSeleccionAncho(80);
        }

        private void ImpresoraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Evento para cuando cambie la selección (opcional)
        }

        private void GuardarConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ImpresoraComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Por favor seleccione una impresora", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = new ConfiguracionService.ConfiguracionImpresora
                {
                    ImpresoraNombre = ImpresoraComboBox.SelectedItem.ToString() ?? "",
                    AnchoTicket = _anchoSeleccionado
                };

                ConfiguracionService.GuardarConfiguracion(config);

                MessageBox.Show("Configuración guardada correctamente", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar configuración: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ProbarImpresion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ImpresoraComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Por favor seleccione una impresora", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var rutaSumatra = SumatraPrintService.EncontrarSumatra();
                if (string.IsNullOrEmpty(rutaSumatra))
                {
                    MessageBox.Show("SumatraPDF no está instalado o no se puede encontrar.\n\n" +
                        "Por favor, descargue e instale SumatraPDF desde:\n" +
                        "https://www.sumatrapdfreader.org/download-free-pdf-viewer",
                        "SumatraPDF no encontrado",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Crear un ticket de prueba
                var ventaPrueba = new Venta
                {
                    Id = 0,
                    Fecha = DateTime.Now,
                    Total = 100.00m,
                    Estado = (int)EstadoVenta.Finalizada
                };

                var itemsPrueba = new List<ItemCarrito>
                {
                    new ItemCarrito
                    {
                        ProductoId = 1,
                        Nombre = "Producto de Prueba",
                        PrecioUnitario = 50.00m,
                        Cantidad = 2,
                        Total = 100.00m
                    }
                };

                // Generar PDF con el ancho seleccionado
                var pdfBytes = TicketPdfGenerator.GenerarTicket(ventaPrueba, itemsPrueba, 100.00m, 0m, _anchoSeleccionado);

                // Imprimir
                var nombreImpresora = ImpresoraComboBox.SelectedItem.ToString() ?? "";
                await SumatraPrintService.ImprimirPdfAsync(pdfBytes, nombreImpresora, _anchoSeleccionado);

                MessageBox.Show("Ticket de prueba enviado a impresión", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al probar impresión: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}