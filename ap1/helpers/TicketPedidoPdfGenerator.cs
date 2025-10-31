using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using POS.Models;
using POS.paginas.ventas;
using POS.Data;
using Microsoft.EntityFrameworkCore;

namespace POS.Helpers
{
    /// <summary>
    /// Generador de tickets de pedido para cocina/barra
    /// Agrupa los items por categoría para facilitar la preparación
    /// </summary>
    public class TicketPedidoPdfGenerator
    {
        private readonly AppDbContext _context;

        public TicketPedidoPdfGenerator(AppDbContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<byte[]> GenerarTicketPedidoAsync(
            Venta venta,
            List<ItemCarrito> items,
            string nombreMesero,
            string numeroMesa,
            int anchoMm = 80)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var itemsConCategoria = await ObtenerItemsConCategoriaAsync(items);

            // Convertir mm a puntos (1 mm = 2.83465 puntos)
            float anchoPuntos = anchoMm * 2.83465f;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // CORRECCIÓN: ContinuousSize con ancho fijo = orientación vertical
                    // El ancho determina la orientación: ancho pequeño = vertical
                    page.ContinuousSize(anchoPuntos);
                    page.Margin(10);
                    page.DefaultTextStyle(x => x.FontFamily("Courier New").FontSize(9));

                    page.Content()
                        .Column(column =>
                        {
                            // Encabezado principal
                            column.Item().AlignCenter().Text("CAFETERÍA").FontSize(12).Bold();
                            column.Item().AlignCenter().Text("Sistema POS").FontSize(8);

                            column.Item().PaddingVertical(3).AlignCenter()
                                .Text(new string('=', GetLineLength(anchoMm))).FontSize(7);

                            // Información de la comanda
                            column.Item().PaddingTop(3).Row(row =>
                            {
                                row.RelativeItem().AlignRight().Text($"{nombreMesero}").FontSize(8);
                            });

                            column.Item().PaddingTop(2).Text($"Fecha: {venta.Fecha:dd/MM/yyyy}").FontSize(8);
                            column.Item().Text($"Hora: {venta.Fecha:HH:mm:ss}").FontSize(8);

                            column.Item().PaddingVertical(3).AlignCenter()
                                .Text(new string('─', GetLineLength(anchoMm))).FontSize(7);

                            // Items agrupados por categoría
                            var itemsAgrupados = AgruparPorCategoria(itemsConCategoria);

                            foreach (var categoria in itemsAgrupados)
                            {
                                column.Item().PaddingTop(4).AlignCenter()
                                    .Text($"╔═══ {categoria.Key.ToUpper()} ═══╗")
                                    .FontSize(9).Bold();

                                foreach (var item in categoria.Value)
                                {
                                    column.Item().PaddingTop(3).Row(row =>
                                    {
                                        row.ConstantItem(35).AlignCenter()
                                            .Text($"[ {item.Cantidad}x ]").FontSize(9).Bold();

                                        row.RelativeItem()
                                            .Text(TruncateText(item.Nombre.ToUpper(), GetMaxChars(anchoMm) - 5))
                                            .FontSize(9);
                                    });

                                    if (item.EsCombo && item.DetallesCombo != null && item.DetallesCombo.Any())
                                    {
                                        column.Item().PaddingTop(1).PaddingLeft(40)
                                            .Text("Incluye:").FontSize(7).Italic();

                                        foreach (var detalle in item.DetallesCombo)
                                        {
                                            column.Item().PaddingLeft(45)
                                                .Text($"▸ {TruncateText(detalle, GetMaxChars(anchoMm) - 6)}")
                                                .FontSize(7).Italic();
                                        }
                                    }
                                }

                                // Separador entre categorías
                                column.Item().PaddingTop(2).AlignCenter()
                                    .Text(new string('·', GetLineLength(anchoMm) / 2)).FontSize(6);
                            }

                            column.Item().PaddingTop(4).AlignCenter()
                                .Text(new string('═', GetLineLength(anchoMm))).FontSize(7);

                            // Información del cliente
                            if (!string.IsNullOrEmpty(venta.NombreCliente))
                            {
                                column.Item().PaddingTop(3).AlignCenter()
                                    .Text("CLIENTE").FontSize(8).Bold();
                                column.Item().AlignCenter()
                                    .Text(TruncateText(venta.NombreCliente.ToUpper(), GetMaxChars(anchoMm)))
                                    .FontSize(9);
                            }

                            // Resumen
                            var totalItems = items.Sum(i => i.Cantidad);
                            column.Item().PaddingTop(4).AlignCenter()
                                .Text($"═══════════════════").FontSize(8);

                            column.Item().PaddingTop(2).AlignCenter()
                                .Text($"TOTAL DE ITEMS: {totalItems}").FontSize(11).Bold();

                            column.Item().PaddingTop(1).AlignCenter()
                                .Text($"═══════════════════").FontSize(8);

                            // Información adicional del pedido
                            column.Item().PaddingTop(4).AlignCenter()
                                .Text("TICKET DE PEDIDO").FontSize(9).Bold();

                            column.Item().PaddingTop(2).AlignCenter()
                                .Text("Para: Cocina / Barra").FontSize(8);

                            column.Item().PaddingTop(2).AlignCenter()
                                .Text($"Impreso: {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7);



                            // Pie de página
                            column.Item().PaddingTop(5).AlignCenter()
                                .Text("¡Buen servicio!").FontSize(8).Italic();

                            // Espacio final
                            column.Item().PaddingBottom(10);
                        });
                });
            });

            return document.GeneratePdf();
        }

        private async System.Threading.Tasks.Task<List<ItemCarritoConCategoria>> ObtenerItemsConCategoriaAsync(List<ItemCarrito> items)
        {
            var itemsConCategoria = new List<ItemCarritoConCategoria>();

            foreach (var item in items)
            {
                var itemConCat = new ItemCarritoConCategoria
                {
                    Cantidad = item.Cantidad,
                    Nombre = item.Nombre,
                    EsCombo = item.ProductoId < 0 && item.ProductoId != -998 && item.ProductoId != -999,
                    Categoria = "OTROS"
                };

                if (item.ProductoId > 0)
                {
                    var producto = await _context.Productos
                        .Include(p => p.Categoria)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductoId);

                    if (producto?.Categoria != null)
                        itemConCat.Categoria = producto.Categoria.Nombre.ToUpper();
                }
                else if (item.ProductoId < 0 && item.ProductoId != -998 && item.ProductoId != -999)
                {
                    int comboId = Math.Abs(item.ProductoId);
                    var combo = await _context.Combos
                        .Include(c => c.ComboProductos)
                            .ThenInclude(cp => cp.Producto)
                                .ThenInclude(p => p.Categoria)
                        .FirstOrDefaultAsync(c => c.Id == comboId);

                    if (combo != null)
                    {
                        itemConCat.DetallesCombo = combo.ComboProductos
                            .Where(cp => cp.Producto != null)
                            .Select(cp => $"{cp.Producto!.Nombre} x{cp.Cantidad}")
                            .ToList();

                        var primerProducto = combo.ComboProductos
                            .FirstOrDefault(cp => cp.Producto?.Categoria != null);

                        itemConCat.Categoria = primerProducto?.Producto?.Categoria?.Nombre.ToUpper() ?? "COMBOS";
                    }
                }
                else if (item.ProductoId == -998 || item.ProductoId == -999)
                {
                    itemConCat.Categoria = "VARIOS";
                }

                itemsConCategoria.Add(itemConCat);
            }

            return itemsConCategoria;
        }

        private Dictionary<string, List<ItemCarritoConCategoria>> AgruparPorCategoria(List<ItemCarritoConCategoria> items)
        {
            var agrupados = new Dictionary<string, List<ItemCarritoConCategoria>>();
            var ordenCategorias = new List<string>
            {
                "ENTRANTES","ENSALADAS","PRIMEROS","SEGUNDOS","CARNES","PESCADOS",
                "PIZZAS","HAMBURGUESAS","POSTRES","BEBIDAS","BEBIDAS FRÍAS","BEBIDAS CALIENTES","COMBOS","VARIOS"
            };

            foreach (var item in items)
            {
                string cat = item.Categoria ?? "OTROS";
                if (!agrupados.ContainsKey(cat))
                    agrupados[cat] = new List<ItemCarritoConCategoria>();
                agrupados[cat].Add(item);
            }

            var resultado = new Dictionary<string, List<ItemCarritoConCategoria>>();
            foreach (var cat in ordenCategorias)
                if (agrupados.ContainsKey(cat))
                    resultado[cat] = agrupados[cat];

            foreach (var cat in agrupados.Keys.Where(k => !ordenCategorias.Contains(k)).OrderBy(k => k))
                resultado[cat] = agrupados[cat];

            return resultado;
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

        public static void GuardarTicketPedido(byte[] pdfBytes, string nombreArchivo)
        {
            var carpetaTickets = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "POS",
                "tickets_pedidos"
            );

            if (!Directory.Exists(carpetaTickets))
                Directory.CreateDirectory(carpetaTickets);

            File.WriteAllBytes(Path.Combine(carpetaTickets, nombreArchivo), pdfBytes);
        }

        public static string GuardarYAbrirTicketPedido(byte[] pdfBytes, int ventaId, string numeroMesa)
        {
            var nombreArchivo = $"Pedido_Mesa{numeroMesa}_{ventaId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            GuardarTicketPedido(pdfBytes, nombreArchivo);

            var carpetaTickets = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "POS",
                "tickets_pedidos"
            );

            var rutaCompleta = Path.Combine(carpetaTickets, nombreArchivo);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = rutaCompleta,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al abrir el ticket: {ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            return rutaCompleta;
        }
    }

    internal class ItemCarritoConCategoria
    {
        public int Cantidad { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = "OTROS";
        public bool EsCombo { get; set; }
        public List<string>? DetallesCombo { get; set; }
    }
}