using POS.Interfaces;
using POS.Models;
using POS.Data;
using POS.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Win32;
using System.Diagnostics;

namespace POS.paginas.detalles_ventas
{
    public partial class DetallesVentasPag : Page
    {
        private readonly IVentaService _ventaService;
        private List<Venta> _todasLasVentas = new List<Venta>();
        private List<Venta> _ventasFiltradas = new List<Venta>();

        public DetallesVentasPag()
        {
            InitializeComponent();

            // Configurar licencia de QuestPDF (Community/Gratuita)
            QuestPDF.Settings.License = LicenseType.Community;

            var context = new AppDbContext();
            _ventaService = new VentaService(context);

            FechaFiltro.SelectedDate = DateTime.Today;

            LoadVentas();
        }

        public DetallesVentasPag(IVentaService ventaService)
        {
            InitializeComponent();

            // Configurar licencia de QuestPDF (Community/Gratuita)
            QuestPDF.Settings.License = LicenseType.Community;

            _ventaService = ventaService;
            LoadVentas();
        }

        private async void LoadVentas()
        {
            var ventas = await _ventaService.GetAllVentasAsync();
            _todasLasVentas = ventas.OrderByDescending(v => v.Fecha).ToList();
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (FechaFiltro.SelectedDate == null)
            {
                _ventasFiltradas = _todasLasVentas;
                VentasItemsControl.ItemsSource = _ventasFiltradas;
                ActualizarTotal(_ventasFiltradas);
                return;
            }

            var fechaSeleccionada = FechaFiltro.SelectedDate.Value.Date;
            _ventasFiltradas = _todasLasVentas
                .Where(v => v.Fecha.Date == fechaSeleccionada)
                .ToList();

            VentasItemsControl.ItemsSource = _ventasFiltradas;
            ActualizarTotal(_ventasFiltradas);
        }

        private void ActualizarTotal(List<Venta> ventas)
        {
            var total = ventas.Sum(v => v.Total);
            TotalVentasTextBlock.Text = $"${total:N2}";
            NumeroVentasTextBlock.Text = ventas.Count.ToString();

            // Contar cuántos items de cada tipo se vendieron
            int tiemposVendidos = 0;

            foreach (var venta in ventas)
            {
                foreach (var detalle in venta.DetallesVenta)
                {
                    // Solo contar si el TipoItem es 3 (Tiempo)
                    if (detalle.TipoItem == (int)TipoItemVenta.Tiempo)
                    {
                        tiemposVendidos += detalle.Cantidad;
                    }
                }
            }

            // Actualizar el contador de tiempos vendidos
            TiemposVendidosTextBlock.Text = tiemposVendidos.ToString();
        }

        private void FechaFiltro_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void DescargarPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_ventasFiltradas == null || !_ventasFiltradas.Any())
            {
                MessageBox.Show("No hay ventas para generar el reporte.", "Sin datos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Reporte_Ventas_{FechaFiltro.SelectedDate?.ToString("yyyy-MM-dd") ?? "Todas"}.pdf",
                    DefaultExt = "pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    GenerarPdfVentas(saveDialog.FileName);

                    var result = MessageBox.Show("Reporte generado exitosamente. ¿Desea abrir el archivo?",
                        "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerarPdfVentas(string rutaArchivo)
        {
            var fechaReporte = FechaFiltro.SelectedDate?.ToString("dd/MM/yyyy") ?? "Todas las fechas";
            var totalVentas = _ventasFiltradas.Sum(v => v.Total);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Encabezado
                    page.Header()
                        .Height(100)
                        .Background(Colors.Purple.Medium)
                        .Padding(20)
                        .Column(column =>
                        {
                            column.Item().Text("Reporte de Ventas")
                                .FontSize(24)
                                .Bold()
                                .FontColor(Colors.White);

                            column.Item().Text($"Fecha: {fechaReporte}")
                                .FontSize(14)
                                .FontColor(Colors.White);

                            column.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(10)
                                .FontColor(Colors.White);
                        });

                    // Contenido
                    page.Content()
                        .PaddingVertical(20)
                        .Column(column =>
                        {
                            // Resumen
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(15).Column(col =>
                                {
                                    col.Item().Text("Total de Ventas").Bold().FontSize(12);
                                    col.Item().Text($"${totalVentas:N2}").FontSize(20).Bold().FontColor(Colors.Purple.Medium);
                                });

                                row.Spacing(10);

                                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(15).Column(col =>
                                {
                                    col.Item().Text("Número de Ventas").Bold().FontSize(12);
                                    col.Item().Text(_ventasFiltradas.Count.ToString()).FontSize(20).Bold().FontColor(Colors.Purple.Medium);
                                });
                            });

                            column.Item().PaddingVertical(20);

                            // Tabla de ventas
                            column.Item().Text("Detalle de Ventas").FontSize(16).Bold();

                            column.Item().PaddingVertical(10);

                            foreach (var venta in _ventasFiltradas)
                            {
                                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(ventaCol =>
                                {
                                    // Header de la venta
                                    ventaCol.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text($"Venta #{venta.Id}").Bold().FontSize(13);
                                        row.RelativeItem().Text($"{venta.Fecha:dd/MM/yyyy HH:mm}").FontSize(11);
                                        row.RelativeItem().AlignRight().Text($"${venta.Total:N2}").Bold().FontSize(13).FontColor(Colors.Purple.Medium);
                                    });

                                    ventaCol.Item().PaddingVertical(5);

                                    // Tabla de productos
                                    ventaCol.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(3);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1);
                                        });

                                        // Header
                                        table.Header(header =>
                                        {
                                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Producto").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Cant.").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Precio").Bold();
                                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Subtotal").Bold();
                                        });

                                        // Productos/Items
                                        foreach (var detalle in venta.DetallesVenta)
                                        {
                                            string nombreItem = detalle.Producto?.Nombre ?? detalle.NombreItem ?? "Item desconocido";

                                            table.Cell().Padding(5).Text(nombreItem);
                                            table.Cell().Padding(5).Text(detalle.Cantidad.ToString());
                                            table.Cell().Padding(5).Text($"${detalle.PrecioUnitario:N2}");
                                            table.Cell().Padding(5).Text($"${detalle.Subtotal:N2}");
                                        }
                                    });
                                });

                                column.Item().PaddingVertical(5);
                            }
                        });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                            x.Span(" de ");
                            x.TotalPages();
                        });
                });
            })
            .GeneratePdf(rutaArchivo);
        }

        private void ToggleDetails_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var border = FindParent<Border>(button);
            if (border == null) return;

            var detailsPanel = FindChild<Border>(border, "DetailsPanel");
            if (detailsPanel == null) return;

            var buttonText = FindChild<TextBlock>(button, "ExpandButtonText");

            if (detailsPanel.Visibility == Visibility.Collapsed)
            {
                detailsPanel.Visibility = Visibility.Visible;
                if (buttonText != null) buttonText.Text = "Ocultar Detalles";
            }
            else
            {
                detailsPanel.Visibility = Visibility.Collapsed;
                if (buttonText != null) buttonText.Text = "Ver Detalles";
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            T? parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        private T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            T? foundChild = null;

            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                T? childType = child as T;
                if (childType == null)
                {
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }
    }
}