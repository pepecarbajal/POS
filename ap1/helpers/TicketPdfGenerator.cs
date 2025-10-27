using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using POS.Models;
using POS.paginas.ventas;

namespace POS.Helpers
{
    public class TicketPdfGenerator
    {
        public static byte[] GenerarTicket(Venta venta, List<ItemCarrito> items, decimal montoRecibido, decimal cambio, int anchoMm = 80)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Convertir mm a puntos (1 mm = 2.83465 puntos)
            float anchoPuntos = anchoMm * 2.83465f;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.ContinuousSize(anchoPuntos);
                    page.Margin(10);
                    page.DefaultTextStyle(x => x.FontFamily("Courier New").FontSize(9));

                    page.Content()
                        .Column(column =>
                        {
                            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo", "icono.png");
                            if (File.Exists(logoPath) && anchoMm >= 58)
                            {
                                column.Item().AlignCenter().Height(40).Image(logoPath);
                                column.Item().PaddingTop(3);
                            }

                            column.Item().AlignCenter().Text("Valala").FontSize(11).Bold();

                            column.Item().PaddingTop(2).AlignCenter().Text($"Fecha: {venta.Fecha:dd/MM/yyyy - HH:mm:ss}").FontSize(8);
                            column.Item().AlignCenter().Text($"Ticket No. {venta.Id:D6}").FontSize(8);

                            column.Item().PaddingVertical(3).AlignCenter().Text(new string('=', GetLineLength(anchoMm))).FontSize(7);

                            // Encabezado de la tabla
                            column.Item().PaddingTop(3).Row(row =>
                            {
                                row.RelativeItem(1).Text("CANT").FontSize(7).Bold();
                                row.RelativeItem(4).Text("DESCRIPCION").FontSize(7).Bold();
                                row.RelativeItem(2).AlignRight().Text("PRECIO").FontSize(7).Bold();
                                row.RelativeItem(2).AlignRight().Text("TOTAL").FontSize(7).Bold();
                            });

                            // Items del ticket
                            foreach (var item in items)
                            {
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem(1).Text(item.Cantidad.ToString()).FontSize(7);
                                    row.RelativeItem(4).Text(TruncateText(item.NombreProducto, GetMaxChars(anchoMm))).FontSize(7);
                                    row.RelativeItem(2).AlignRight().Text($"${item.PrecioUnitario:N2}").FontSize(7);
                                    row.RelativeItem(2).AlignRight().Text($"${item.Subtotal:N2}").FontSize(7);
                                });
                            }

                            column.Item().PaddingTop(3).AlignCenter().Text(new string('=', GetLineLength(anchoMm))).FontSize(7);

                            // Total
                            column.Item().PaddingTop(3).AlignCenter().Text($"Total Neto ${venta.Total:N2}").FontSize(11).Bold();
                            column.Item().PaddingTop(2).AlignCenter().Text(new string('=', GetLineLength(anchoMm))).FontSize(7);

                            // Pago con recibido y cambio
                            column.Item().PaddingTop(3).AlignRight().Text($"EFECTIVO pesos ${montoRecibido:N2}").FontSize(8);
                            column.Item().AlignRight().Text($"Cambio pesos ${cambio:N2}").FontSize(8);

                            column.Item().PaddingTop(5).AlignCenter().Text("PUNTO DE VENTA").FontSize(8).Bold();

                            column.Item().PaddingTop(3).AlignCenter().Text("¡Gracias por su compra!").FontSize(9);

                            if (anchoMm >= 58)
                            {
                                column.Item().AlignCenter().Text("Av principal rio azul SAHR").FontSize(8);
                                column.Item().AlignCenter().Text("39076 Chilpancingo de los Bravo, Gro.").FontSize(8);
                                column.Item().AlignCenter().Text("Tel: 747 138 6126").FontSize(8);
                            }

                            column.Item().PaddingBottom(5);
                        });
                });
            });

            return document.GeneratePdf();
        }

        private static int GetLineLength(int anchoMm)
        {
            return anchoMm switch
            {
                50 => 24,
                58 => 32,
                80 => 48,
                _ => 48
            };
        }

        private static int GetMaxChars(int anchoMm)
        {
            return anchoMm switch
            {
                50 => 15,
                58 => 20,
                80 => 30,
                _ => 30
            };
        }

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }

        public static void GuardarTicket(byte[] pdfBytes, string nombreArchivo)
        {
            var carpetaTickets = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "POS",
                "tickets"
            );

            if (!Directory.Exists(carpetaTickets))
            {
                Directory.CreateDirectory(carpetaTickets);
            }

            var rutaCompleta = Path.Combine(carpetaTickets, nombreArchivo);
            File.WriteAllBytes(rutaCompleta, pdfBytes);
        }
    }
}