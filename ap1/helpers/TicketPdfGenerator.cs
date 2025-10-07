using POS.Models;
using POS.paginas.ventas;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Helpers
{
    public class TicketPdfGenerator
    {
        public static byte[] GenerarTicket(Venta venta, List<ItemCarrito> items)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Column(column =>
                        {
                            column.Item().AlignCenter().Text("TICKET DE VENTA").FontSize(20).Bold();
                            column.Item().AlignCenter().Text($"Fecha: {venta.Fecha:dd/MM/yyyy HH:mm}").FontSize(10);
                            column.Item().AlignCenter().Text($"Ticket #: {venta.Id}").FontSize(10);
                            column.Item().PaddingVertical(10).LineHorizontal(1);
                        });

                    page.Content()
                        .Column(column =>
                        {
                            // Tabla de productos
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Producto
                                    columns.RelativeColumn(1); // Cantidad
                                    columns.RelativeColumn(1.5f); // Precio Unit.
                                    columns.RelativeColumn(1.5f); // Subtotal
                                });

                                // Encabezado
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Producto").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Cant.").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("P. Unit.").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Subtotal").Bold();
                                });

                                // Filas de productos
                                foreach (var item in items)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.NombreProducto);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(item.Cantidad.ToString());
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"${item.PrecioUnitario:F2}");
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"${item.Subtotal:F2}");
                                }
                            });

                            // Total
                            column.Item().PaddingTop(20).AlignRight().Row(row =>
                            {
                                row.AutoItem().PaddingRight(10).Text("TOTAL:").FontSize(16).Bold();
                                row.AutoItem().Text($"${venta.Total:F2}").FontSize(16).Bold();
                            });

                            // Pie de página
                            column.Item().PaddingTop(30).AlignCenter().Text("¡Gracias por su compra!").FontSize(12).Italic();
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                        });
                });
            });

            return document.GeneratePdf();
        }

        public static void GuardarTicket(byte[] pdfBytes, string nombreArchivo)
        {
            var carpetaTickets = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "POS",
                "tickets"
            );

            if (!System.IO.Directory.Exists(carpetaTickets))
            {
                System.IO.Directory.CreateDirectory(carpetaTickets);
            }

            var rutaCompleta = System.IO.Path.Combine(carpetaTickets, nombreArchivo);
            System.IO.File.WriteAllBytes(rutaCompleta, pdfBytes);
        }
    }
}
