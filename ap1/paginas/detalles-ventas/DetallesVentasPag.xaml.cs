using Microsoft.Win32;
using POS.Data;
using POS.Interfaces;
using POS.Models;
using POS.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

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

            QuestPDF.Settings.License = LicenseType.Community;

            var context = new AppDbContext();
            _ventaService = new VentaService(context);

            FechaFiltro.SelectedDate = DateTime.Today;

            LoadVentas();
        }

        public DetallesVentasPag(IVentaService ventaService)
        {
            InitializeComponent();

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

            int tiemposVendidos = 0;

            foreach (var venta in ventas)
            {
                foreach (var detalle in venta.DetallesVenta)
                {
                    if (detalle.TipoItem == (int)TipoItemVenta.Tiempo)
                    {
                        tiemposVendidos += detalle.Cantidad;
                    }
                }
            }

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

            int tiemposVendidos = 0;
            foreach (var venta in _ventasFiltradas)
            {
                foreach (var detalle in venta.DetallesVenta)
                {
                    if (detalle.TipoItem == (int)TipoItemVenta.Tiempo)
                    {
                        tiemposVendidos += detalle.Cantidad;
                    }
                }
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(Colors.Black));

                    page.Header()
    .Column(column =>
    {
        column.Item()
            .Row(row =>
            {
                row.ConstantItem(45)
    .Column(logoCol =>
    {
                        try
                        {
                            logoCol.Item()
                                .Width(40)
                                .Height(40)
                                .Image("logo/icono.png");
                        }
                        catch
                        {
                            logoCol.Item()
                                .Width(40)
                                .Height(40)
                                .Border(1)
                                .BorderColor(Colors.Grey.Darken2);
                        }
                    });

                row.RelativeItem()
                    .PaddingLeft(10)
                    .AlignMiddle()
                    .Column(col =>
                    {
                        col.Item().Text("REPORTE DE VENTAS")
                            .FontSize(14)
                            .Bold();

                        col.Item().Row(infoRow =>
                        {
                            infoRow.AutoItem().Text($"Fecha: {fechaReporte}")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken2);

                            infoRow.AutoItem().PaddingLeft(15).Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken2);
                        });
                    });
            });

        column.Item()
    .PaddingTop(8)
    .BorderBottom(1.5f)
    .BorderColor(Colors.Black);
    });

                    page.Content()
    .PaddingTop(12)
    .Column(column =>
    {
        column.Item()
    .Table(table =>
    {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Cell()
    .Border(1)
    .BorderColor(Colors.Black)
    .Background(Colors.Grey.Lighten1)
    .Padding(6)
    .AlignCenter()
    .Text("TOTAL DE VENTAS")
    .Bold()
    .FontSize(8);

                table.Cell()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Background(Colors.Grey.Lighten1)
                    .Padding(6)
                    .AlignCenter()
                    .Text("CANTIDAD DE VENTAS")
                    .Bold()
                    .FontSize(8);

                table.Cell()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Background(Colors.Grey.Lighten1)
                    .Padding(6)
                    .AlignCenter()
                    .Text("TIEMPOS VENDIDOS")
                    .Bold()
                    .FontSize(8);

                table.Cell()
    .Border(1)
    .BorderColor(Colors.Black)
    .Padding(6)
    .AlignCenter()
    .Text($"${totalVentas:N2}")
    .FontSize(11)
    .Bold();

                table.Cell()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(6)
                    .AlignCenter()
                    .Text(_ventasFiltradas.Count.ToString())
                    .FontSize(11)
                    .Bold();

                table.Cell()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(6)
                    .AlignCenter()
                    .Text(tiemposVendidos.ToString())
                    .FontSize(11)
                    .Bold();
            });

        column.Item().PaddingVertical(10);

        column.Item()
    .Background(Colors.Grey.Lighten1)
    .Border(1)
    .BorderColor(Colors.Black)
    .Padding(5)
    .Text("DETALLE DE TRANSACCIONES")
    .FontSize(9)
    .Bold();

        column.Item().PaddingTop(8);

        foreach (var venta in _ventasFiltradas)
        {
            column.Item()
                .Border(1)
                .BorderColor(Colors.Black)
                .Column(ventaCol =>
                {
                    ventaCol.Item()
    .Background(Colors.Grey.Lighten2)
    .Padding(5)
    .Row(row =>
    {
                            row.AutoItem()
                                .Text($"Venta #{venta.Id}")
                                .FontSize(9)
                                .Bold();

                            row.AutoItem()
                                .PaddingLeft(20)
                                .Text($"{venta.Fecha:dd/MM/yyyy HH:mm}")
                                .FontSize(8);

                            row.RelativeItem()
                                .AlignRight()
                                .Text($"Total: ${venta.Total:N2}")
                                .FontSize(9)
                                .Bold();
                        });

                    ventaCol.Item()
    .Table(table =>
    {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(5);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.8f);
                                columns.RelativeColumn(1.8f);
                            });

                            table.Header(header =>
{
                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(4)
                                    .Text("Producto")
                                    .Bold()
                                    .FontSize(8);

                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(4)
                                    .AlignCenter()
                                    .Text("Cant.")
                                    .Bold()
                                    .FontSize(8);

                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(4)
                                    .AlignRight()
                                    .Text("P. Unit.")
                                    .Bold()
                                    .FontSize(8);

                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Darken1)
                                    .Padding(4)
                                    .AlignRight()
                                    .Text("Subtotal")
                                    .Bold()
                                    .FontSize(8);
                            });

                            int rowIndex = 0;
                            foreach (var detalle in venta.DetallesVenta)
                            {
                                string nombreItem = detalle.Producto?.Nombre ?? detalle.NombreItem ?? "Item desconocido";
                                var bgColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                table.Cell()
                                    .Background(bgColor)
                                    .Padding(4)
                                    .Text(nombreItem)
                                    .FontSize(8);

                                table.Cell()
                                    .Background(bgColor)
                                    .Padding(4)
                                    .AlignCenter()
                                    .Text(detalle.Cantidad.ToString())
                                    .FontSize(8);

                                table.Cell()
                                    .Background(bgColor)
                                    .Padding(4)
                                    .AlignRight()
                                    .Text($"${detalle.PrecioUnitario:N2}")
                                    .FontSize(8);

                                table.Cell()
                                    .Background(bgColor)
                                    .Padding(4)
                                    .AlignRight()
                                    .Text($"${detalle.Subtotal:N2}")
                                    .FontSize(8)
                                    .Bold();

                                rowIndex++;
                            }
                        });
                });

            column.Item().PaddingVertical(4);
        }
    });

                    page.Footer()
    .Column(column =>
    {
        column.Item()
            .BorderTop(1)
            .BorderColor(Colors.Grey.Darken1)
            .PaddingTop(5)
            .Row(row =>
            {
                row.RelativeItem()
                    .AlignLeft()
                    .Text("Sistema POS")
                    .FontSize(7)
                    .FontColor(Colors.Grey.Darken2);

                row.RelativeItem()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });

                row.RelativeItem()
                    .AlignRight()
                    .Text($"{DateTime.Now:dd/MM/yyyy}")
                    .FontSize(7)
                    .FontColor(Colors.Grey.Darken2);
            });
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