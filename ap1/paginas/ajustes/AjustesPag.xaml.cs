using POS.Helpers;
using POS.Models;
using POS.paginas.ventas;
using POS.Services;
using POS.Data;
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
        private readonly TicketImpresionService _ticketImpresionService;

        public AjustesPag()
        {
            InitializeComponent();

            // Inicializar servicio de impresión
            var context = new AppDbContext();
            _ticketImpresionService = new TicketImpresionService(context);

            CargarConfiguracion();
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

        private void ProbarImpresion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ImpresoraComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Por favor seleccione una impresora", "Validación",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Verificar que la configuración esté guardada
                var config = ConfiguracionService.CargarConfiguracion();
                if (string.IsNullOrEmpty(config.ImpresoraNombre))
                {
                    var resultado = MessageBox.Show(
                        "La configuración no está guardada. ¿Desea guardarla antes de probar?",
                        "Configuración no guardada",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (resultado == MessageBoxResult.Yes)
                    {
                        GuardarConfiguracion_Click(sender, e);
                    }
                    else
                    {
                        return;
                    }
                }

                // Crear un ticket de prueba
                var ventaPrueba = new Venta
                {
                    Id = 999,
                    Fecha = DateTime.Now,
                    Total = 100.00m,
                    Estado = (int)EstadoVenta.Finalizada,
                    TipoPago = (int)TipoPago.Efectivo
                };

                var itemsPrueba = new List<ItemCarrito>
                {
                    new ItemCarrito
                    {
                        ProductoId = 1,
                        Nombre = "Producto de Prueba 1",
                        PrecioUnitario = 30.00m,
                        Cantidad = 2,
                        Total = 60.00m
                    },
                    new ItemCarrito
                    {
                        ProductoId = 2,
                        Nombre = "Producto de Prueba 2",
                        PrecioUnitario = 40.00m,
                        Cantidad = 1,
                        Total = 40.00m
                    }
                };

                // Deshabilitar botón mientras imprime
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "⏳ Imprimiendo...";
                }

                try
                {
                    // Imprimir ticket de venta de prueba usando impresión directa
                    _ticketImpresionService.ImprimirTicketVenta(
                        venta: ventaPrueba,
                        items: itemsPrueba,
                        montoRecibido: 100.00m,
                        cambio: 0m
                    );

                    MessageBox.Show(
                        "✅ Ticket de prueba enviado a impresión correctamente.\n\n" +
                        $"Impresora: {config.ImpresoraNombre}\n" +
                        $"Ancho del ticket: {_anchoSeleccionado}mm\n\n" +
                        "Si no se imprimió, verifique:\n" +
                        "• Que la impresora esté encendida\n" +
                        "• Que tenga papel\n" +
                        "• Que esté conectada correctamente",
                        "Prueba de Impresión",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception impEx)
                {
                    MessageBox.Show(
                        $"❌ Error al imprimir el ticket de prueba:\n\n{impEx.Message}\n\n" +
                        "Verifique que:\n" +
                        "• La impresora esté encendida y conectada\n" +
                        "• La impresora tenga papel\n" +
                        "• El nombre de la impresora sea correcto\n" +
                        "• No haya trabajos de impresión atascados",
                        "Error de Impresión",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                // Restaurar botón
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "🖨️ Probar Impresión";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al probar impresión: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Restaurar botón en caso de error
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "🖨️ Probar Impresión";
                }
            }
        }
    }
}