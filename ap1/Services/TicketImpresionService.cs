using POS.Models;
using POS.Services;
using POS.paginas.ventas;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using POS.Data;

namespace POS.Services
{
    /// <summary>
    /// Servicio unificado para impresión directa de tickets (Venta, Pedido y Corte de Caja)
    /// Usa GDI+ para impresión directa sin necesidad de SumatraPDF
    /// </summary>
    public class TicketImpresionService
    {
        private PrintDocument printDocument;
        private int lineaActual;
        private const int margenIzquierdo = 10;
        private int anchoTicket = 280; // Ancho en píxeles

        // Variables para Ticket de Venta
        private Venta? _venta;
        private List<ItemCarrito>? _itemsVenta;
        private decimal _montoRecibido;
        private decimal _cambio;

        // Variables para Ticket de Pedido
        private string? _nombreMesero;
        private string? _numeroMesa;
        private Dictionary<string, List<ItemCarritoConCategoria>>? _itemsAgrupados;

        // Variables para Corte de Caja
        private CorteCaja? _corte;
        private ResumenCorteCaja? _resumen;

        private readonly AppDbContext? _context;

        public TicketImpresionService()
        {
        }

        public TicketImpresionService(AppDbContext context)
        {
            _context = context;
        }

        #region TICKET DE VENTA

        /// <summary>
        /// Imprime ticket de venta directamente a la impresora
        /// </summary>
        public void ImprimirTicketVenta(Venta venta, List<ItemCarrito> items, decimal montoRecibido, decimal cambio)
        {
            _venta = venta;
            _itemsVenta = items;
            _montoRecibido = montoRecibido;
            _cambio = cambio;
            lineaActual = 10;

            // Cargar configuración
            var config = ConfiguracionService.CargarConfiguracion();
            ConfigurarAnchoTicket(config.AnchoTicket);

            printDocument = new PrintDocument();
            printDocument.PrintPage += new PrintPageEventHandler(PrintPage_TicketVenta);

            // Configurar impresora
            printDocument.PrinterSettings.PrinterName = config.ImpresoraNombre;

            // Configurar tamaño de papel - papel continuo
            // 1 pulgada = 25.4 mm, entonces: pulgadas = mm / 25.4
            // PaperSize usa centésimas de pulgada (1/100 inch)
            double anchoPulgadas = config.AnchoTicket / 25.4;
            int anchoEnCentesimasPulgada = (int)(anchoPulgadas * 100);
            // Para papel continuo térmico, usar alto grande que se ajuste automáticamente
            PaperSize paperSize = new PaperSize("Ticket", anchoEnCentesimasPulgada, 3000);
            printDocument.DefaultPageSettings.PaperSize = paperSize;

            try
            {
                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new Exception($"La impresora '{config.ImpresoraNombre}' no está disponible.");
                }

                printDocument.Print();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al imprimir ticket de venta: {ex.Message}");
            }
        }

        private void PrintPage_TicketVenta(object sender, PrintPageEventArgs e)
        {
            if (_venta == null || _itemsVenta == null) return;

            Graphics g = e.Graphics;
            Font fontTitulo = new Font("Arial", 11, FontStyle.Bold);
            Font fontNormal = new Font("Arial", 9);
            Font fontBold = new Font("Arial", 9, FontStyle.Bold);
            Font fontSmall = new Font("Arial", 8);
            Font fontTableHeader = new Font("Arial", 7, FontStyle.Bold);
            Font fontTableData = new Font("Arial", 7);

            // Logo (si existe y el ancho es >= 58mm)
            if (anchoTicket >= 220)
            {
                try
                {
                    string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo", "icono.png");
                    if (System.IO.File.Exists(logoPath))
                    {
                        Image logo = Image.FromFile(logoPath);
                        int logoWidth = 40;
                        int logoHeight = 40;
                        int logoX = (anchoTicket - logoWidth) / 2;
                        g.DrawImage(logo, logoX, lineaActual, logoWidth, logoHeight);
                        lineaActual += logoHeight + 3;
                        logo.Dispose();
                    }
                }
                catch { /* Si falla el logo, continuar sin él */ }
            }

            // Encabezado - Nombre del negocio
            ImprimirLineaCentrada(g, "Valala", fontTitulo);
            lineaActual += 2;

            // Fecha y número de ticket
            ImprimirLineaCentrada(g, $"Fecha: {_venta.Fecha:dd/MM/yyyy - HH:mm:ss}", fontSmall);
            ImprimirLineaCentrada(g, $"Ticket No. {_venta.Id:D6}", fontSmall);

            lineaActual += 3;

            // Separador
            string separadorIgual = new string('=', GetLineLength());
            ImprimirLineaCentrada(g, separadorIgual, fontTableHeader);

            // Encabezado de tabla
            lineaActual += 3;
            ImprimirLineaTabla(g, "CANT", "DESCRIPCION", "PRECIO", "TOTAL", fontTableHeader);

            // Items
            foreach (var item in _itemsVenta)
            {
                string nombre = TruncateText(item.Nombre, GetMaxChars());
                ImprimirLineaTabla(g,
                    item.Cantidad.ToString(),
                    nombre,
                    $"${item.PrecioUnitario:N2}",
                    $"${item.Subtotal:N2}",
                    fontTableData);
            }

            lineaActual += 3;
            ImprimirLineaCentrada(g, separadorIgual, fontTableHeader);

            // Total
            lineaActual += 3;
            ImprimirLineaCentrada(g, $"Total Neto ${_venta.Total:N2}", fontTitulo);
            lineaActual += 2;
            ImprimirLineaCentrada(g, separadorIgual, fontTableHeader);

            // Pago con recibido y cambio - alineado a la derecha
            lineaActual += 3;
            ImprimirLineaAlineadaDerecha(g, $"EFECTIVO pesos ${_montoRecibido:N2}", fontSmall);
            ImprimirLineaAlineadaDerecha(g, $"Cambio pesos ${_cambio:N2}", fontSmall);

            lineaActual += 5;
            ImprimirLineaCentrada(g, "PUNTO DE VENTA", fontSmall);

            // Pie de página
            lineaActual += 3;
            ImprimirLineaCentrada(g, "¡Gracias por su compra!", fontNormal);

            // Información adicional (solo si es >= 58mm)
            if (anchoTicket >= 220)
            {
                ImprimirLineaCentrada(g, "Av principal rio azul SAHR", fontSmall);
                ImprimirLineaCentrada(g, "39076 Chilpancingo de los Bravo, Gro.", fontSmall);
                ImprimirLineaCentrada(g, "Tel: 747 138 6126", fontSmall);
            }

            lineaActual += 5;

            // Cleanup
            fontTitulo.Dispose();
            fontNormal.Dispose();
            fontBold.Dispose();
            fontSmall.Dispose();
            fontTableHeader.Dispose();
            fontTableData.Dispose();
        }

        #endregion

        #region TICKET DE PEDIDO

        /// <summary>
        /// Imprime ticket de pedido para cocina/barra directamente a la impresora
        /// </summary>
        public async System.Threading.Tasks.Task ImprimirTicketPedidoAsync(
            Venta venta,
            List<ItemCarrito> items,
            string nombreMesero,
            string numeroMesa)
        {
            if (_context == null)
                throw new InvalidOperationException("Se requiere AppDbContext para generar ticket de pedido.");

            _venta = venta;
            _itemsVenta = items;
            _nombreMesero = nombreMesero;
            _numeroMesa = numeroMesa;
            lineaActual = 10;

            // Obtener items con categoría
            var itemsConCategoria = await ObtenerItemsConCategoriaAsync(items);
            _itemsAgrupados = AgruparPorCategoria(itemsConCategoria);

            // Cargar configuración
            var config = ConfigService.CargarConfiguracion();
            ConfigurarAnchoTicket(config.AnchoTicket);

            printDocument = new PrintDocument();
            printDocument.PrintPage += new PrintPageEventHandler(PrintPage_TicketPedido);

            // Configurar impresora
            printDocument.PrinterSettings.PrinterName = config.ImpresoraNombre;

            // Configurar tamaño de papel - papel continuo
            // 1 pulgada = 25.4 mm, entonces: pulgadas = mm / 25.4
            // PaperSize usa centésimas de pulgada (1/100 inch)
            double anchoPulgadas = config.AnchoTicket / 25.4;
            int anchoEnCentesimasPulgada = (int)(anchoPulgadas * 100);
            // Para papel continuo térmico, usar alto grande que se ajuste automáticamente
            PaperSize paperSize = new PaperSize("Ticket", anchoEnCentesimasPulgada, 3000);
            printDocument.DefaultPageSettings.PaperSize = paperSize;

            try
            {
                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new Exception($"La impresora '{config.ImpresoraNombre}' no está disponible.");
                }

                printDocument.Print();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al imprimir ticket de pedido: {ex.Message}");
            }
        }

        private void PrintPage_TicketPedido(object sender, PrintPageEventArgs e)
        {
            if (_venta == null || _itemsAgrupados == null) return;

            Graphics g = e.Graphics;
            Font fontTitulo = new Font("Arial", 12, FontStyle.Bold);
            Font fontTituloGrande = new Font("Arial", 11, FontStyle.Bold);
            Font fontNormal = new Font("Arial", 9);
            Font fontBold = new Font("Arial", 9, FontStyle.Bold);
            Font fontSmall = new Font("Arial", 8);
            Font fontTiny = new Font("Arial", 7, FontStyle.Italic);
            Font fontCategoria = new Font("Arial", 9, FontStyle.Bold);
            Font fontSeparador = new Font("Arial", 6);

            // Encabezado principal
            ImprimirLineaCentrada(g, "CAFETERÍA", fontTitulo);
            ImprimirLineaCentrada(g, "Sistema POS", fontSmall);

            lineaActual += 3;
            string separadorIgual = new string('=', GetLineLength());
            ImprimirLineaCentrada(g, separadorIgual, fontTiny);

            // Información de la comanda - Mesero alineado a la derecha
            lineaActual += 3;
            if (!string.IsNullOrEmpty(_nombreMesero))
            {
                ImprimirLineaAlineadaDerecha(g, _nombreMesero, fontSmall);
            }

            // Fecha y hora
            lineaActual += 2;
            ImprimirLinea(g, $"Fecha: {_venta.Fecha:dd/MM/yyyy}", fontSmall);
            ImprimirLinea(g, $"Hora: {_venta.Fecha:HH:mm:ss}", fontSmall);

            lineaActual += 3;
            string separadorGuion = new string('─', GetLineLength());
            ImprimirLineaCentrada(g, separadorGuion, fontTiny);

            // Items agrupados por categoría
            foreach (var categoria in _itemsAgrupados)
            {
                lineaActual += 4;
                ImprimirLineaCentrada(g, $"╔═══ {categoria.Key.ToUpper()} ═══╗", fontCategoria);

                foreach (var item in categoria.Value)
                {
                    lineaActual += 3;
                    // Cantidad y nombre
                    string cantidad = $"[ {item.Cantidad}x ]";
                    string nombre = TruncateText(item.Nombre.ToUpper(), GetMaxChars() - 5);

                    ImprimirLineaProductoPedidoEstilo2(g, cantidad, nombre, fontNormal);

                    // Si es combo, mostrar detalles
                    if (item.EsCombo && item.DetallesCombo != null && item.DetallesCombo.Any())
                    {
                        lineaActual += 1;
                        ImprimirLineaConMargen(g, "Incluye:", 40, fontTiny);
                        foreach (var detalle in item.DetallesCombo)
                        {
                            string detalleText = $"▸ {TruncateText(detalle, GetMaxChars() - 6)}";
                            ImprimirLineaConMargen(g, detalleText, 45, fontTiny);
                        }
                    }
                }

                // Separador entre categorías
                lineaActual += 2;
                ImprimirLineaCentrada(g, new string('·', GetLineLength() / 2), fontSeparador);
            }

            lineaActual += 4;
            ImprimirLineaCentrada(g, new string('═', GetLineLength()), fontTiny);

            // Información del cliente
            if (!string.IsNullOrEmpty(_venta.NombreCliente))
            {
                lineaActual += 3;
                ImprimirLineaCentrada(g, "CLIENTE", fontSmall);
                ImprimirLineaCentrada(g, TruncateText(_venta.NombreCliente.ToUpper(), GetMaxChars()), fontNormal);
            }

            // Resumen
            var totalItems = _itemsVenta?.Sum(i => i.Cantidad) ?? 0;
            lineaActual += 4;
            ImprimirLineaCentrada(g, "═══════════════════", fontSmall);

            lineaActual += 2;
            ImprimirLineaCentrada(g, $"TOTAL DE ITEMS: {totalItems}", fontTituloGrande);

            lineaActual += 1;
            ImprimirLineaCentrada(g, "═══════════════════", fontSmall);

            // Información adicional del pedido
            lineaActual += 4;
            ImprimirLineaCentrada(g, "TICKET DE PEDIDO", fontBold);

            lineaActual += 2;
            ImprimirLineaCentrada(g, "Para: Cocina / Barra", fontSmall);

            lineaActual += 2;
            ImprimirLineaCentrada(g, $"Impreso: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", fontTiny);

            // Pie de página
            lineaActual += 5;
            ImprimirLineaCentrada(g, "¡Buen servicio!", fontSmall);

            // Espacio final
            lineaActual += 10;

            // Cleanup
            fontTitulo.Dispose();
            fontTituloGrande.Dispose();
            fontNormal.Dispose();
            fontBold.Dispose();
            fontSmall.Dispose();
            fontTiny.Dispose();
            fontCategoria.Dispose();
            fontSeparador.Dispose();
        }

        #endregion

        #region CORTE DE CAJA (Ya existente)

        /// <summary>
        /// Imprime ticket de corte de caja directamente a la impresora
        /// </summary>
        public void ImprimirCorteCaja(CorteCaja corte, ResumenCorteCaja resumen)
        {
            _corte = corte;
            _resumen = resumen;
            lineaActual = 10;

            // Cargar configuración
            var config = ConfigService.CargarConfiguracion();
            ConfigurarAnchoTicket(config.AnchoTicket);

            printDocument = new PrintDocument();
            printDocument.PrintPage += new PrintPageEventHandler(PrintPage_CorteCaja);

            // Configurar impresora
            printDocument.PrinterSettings.PrinterName = config.ImpresoraNombre;

            // Configurar tamaño de papel - papel continuo
            // 1 pulgada = 25.4 mm, entonces: pulgadas = mm / 25.4
            // PaperSize usa centésimas de pulgada (1/100 inch)
            double anchoPulgadas = config.AnchoTicket / 25.4;
            int anchoEnCentesimasPulgada = (int)(anchoPulgadas * 100);
            // Para papel continuo térmico, usar alto grande que se ajuste automáticamente
            PaperSize paperSize = new PaperSize("Ticket", anchoEnCentesimasPulgada, 3000);
            printDocument.DefaultPageSettings.PaperSize = paperSize;

            try
            {
                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new Exception($"La impresora '{config.ImpresoraNombre}' no está disponible.");
                }

                printDocument.Print();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al imprimir corte de caja: {ex.Message}");
            }
        }

        private void PrintPage_CorteCaja(object sender, PrintPageEventArgs e)
        {
            if (_corte == null || _resumen == null) return;

            Graphics g = e.Graphics;
            Font fontTitulo = new Font("Arial", 12, FontStyle.Bold);
            Font fontNormal = new Font("Arial", 9);
            Font fontBold = new Font("Arial", 9, FontStyle.Bold);
            Font fontSmall = new Font("Arial", 8);

            // Encabezado
            ImprimirLineaCentrada(g, "================================", fontNormal);
            ImprimirLineaCentrada(g, "CORTE DE CAJA", fontTitulo);
            ImprimirLineaCentrada(g, "================================", fontNormal);
            lineaActual += 5;

            // Información del corte
            ImprimirLinea(g, $"Fecha Apertura: {_corte.FechaApertura:dd/MM/yyyy HH:mm}", fontSmall);
            if (_corte.EstaCerrado && _corte.FechaCierre.HasValue)
            {
                ImprimirLinea(g, $"Fecha Cierre:   {_corte.FechaCierre.Value:dd/MM/yyyy HH:mm}", fontSmall);
            }
            if (!string.IsNullOrWhiteSpace(_corte.UsuarioCierre))
            {
                ImprimirLinea(g, $"Usuario: {_corte.UsuarioCierre}", fontSmall);
            }
            lineaActual += 5;

            ImprimirLineaSeparadora(g, fontNormal);

            // Efectivo Inicial
            ImprimirLinea(g, "EFECTIVO INICIAL", fontBold);
            ImprimirLineaConValor(g, "Monto inicial:", _corte.EfectivoInicial, fontNormal);
            lineaActual += 5;

            ImprimirLineaSeparadora(g, fontNormal);

            // Ventas
            ImprimirLinea(g, "VENTAS DEL DIA", fontBold);
            ImprimirLineaConValor(g, $"Efectivo ({_resumen.CantidadVentasEfectivo}):", _resumen.TotalVentasEfectivo, fontNormal);
            ImprimirLineaConValor(g, $"Tarjeta ({_resumen.CantidadVentasTarjeta}):", _resumen.TotalVentasTarjeta, fontNormal);
            ImprimirLineaSeparadora(g, fontSmall);
            ImprimirLineaConValor(g, "TOTAL VENTAS:", _resumen.TotalVentas, fontBold);
            lineaActual += 5;

            ImprimirLineaSeparadora(g, fontNormal);

            // Movimientos
            ImprimirLinea(g, "MOVIMIENTOS", fontBold);
            ImprimirLineaConValor(g, "Depositos (+):", _resumen.TotalDepositos, fontNormal);
            ImprimirLineaConValor(g, "Retiros (-):", _resumen.TotalRetiros, fontNormal);
            lineaActual += 5;

            // Detalle de movimientos
            if (_resumen.Movimientos.Any())
            {
                ImprimirLinea(g, "Detalle de movimientos:", fontSmall);
                foreach (var mov in _resumen.Movimientos)
                {
                    string tipo = mov.TipoMovimiento == (int)TipoMovimiento.Deposito ? "+" : "-";
                    ImprimirLinea(g, $"{tipo} {mov.Concepto}", fontSmall);
                    ImprimirLineaConValor(g, $"  {mov.Fecha:HH:mm}", mov.Monto, fontSmall);
                }
                lineaActual += 5;
            }

            ImprimirLineaSeparadora(g, fontNormal);

            // Efectivo Esperado
            ImprimirLineaConValor(g, "EFECTIVO ESPERADO:", _resumen.EfectivoEsperado, fontBold);
            lineaActual += 5;

            // Si está cerrado
            if (_corte.EstaCerrado)
            {
                ImprimirLineaSeparadora(g, fontNormal);
                ImprimirLinea(g, "CIERRE", fontBold);
                ImprimirLineaConValor(g, "Efectivo contado:", _corte.EfectivoFinal, fontNormal);

                string textoDiferencia = "Diferencia:";
                if (Math.Abs(_corte.Diferencia) < 0.01m)
                {
                    textoDiferencia = "Sin diferencia";
                }
                else if (_corte.Diferencia > 0)
                {
                    textoDiferencia = "Sobrante:";
                }
                else
                {
                    textoDiferencia = "Faltante:";
                }

                ImprimirLineaConValor(g, textoDiferencia, Math.Abs(_corte.Diferencia), fontBold);
            }

            // Observaciones
            if (!string.IsNullOrWhiteSpace(_corte.Observaciones))
            {
                lineaActual += 10;
                ImprimirLineaSeparadora(g, fontNormal);
                ImprimirLinea(g, "OBSERVACIONES:", fontBold);
                ImprimirTextoMultilinea(g, _corte.Observaciones, fontSmall);
            }

            // Pie de página
            lineaActual += 10;
            ImprimirLineaCentrada(g, "================================", fontNormal);
            ImprimirLineaCentrada(g, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), fontSmall);
            ImprimirLineaCentrada(g, "Sistema POS", fontSmall);

            // Cleanup
            fontTitulo.Dispose();
            fontNormal.Dispose();
            fontBold.Dispose();
            fontSmall.Dispose();
        }

        #endregion

        #region MÉTODOS AUXILIARES

        private void ConfigurarAnchoTicket(int anchoMm)
        {
            // Convertir mm a píxeles aproximados (asumiendo 96 DPI)
            anchoTicket = anchoMm switch
            {
                50 => 190,
                58 => 220,
                80 => 300,
                _ => 300
            };
        }

        private int GetLineLength()
        {
            return anchoTicket switch
            {
                190 => 24,  // 50mm
                220 => 32,  // 58mm
                300 => 48,  // 80mm
                _ => 48
            };
        }

        private int GetMaxChars()
        {
            return anchoTicket switch
            {
                190 => 15,  // 50mm
                220 => 20,  // 58mm
                300 => 30,  // 80mm
                _ => 30
            };
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }

        private void ImprimirLinea(Graphics g, string texto, Font font)
        {
            g.DrawString(texto, font, Brushes.Black, margenIzquierdo, lineaActual);
            lineaActual += (int)g.MeasureString(texto, font).Height + 2;
        }

        private void ImprimirLineaCentrada(Graphics g, string texto, Font font)
        {
            SizeF size = g.MeasureString(texto, font);
            float x = (anchoTicket - size.Width) / 2;
            g.DrawString(texto, font, Brushes.Black, x, lineaActual);
            lineaActual += (int)size.Height + 2;
        }

        private void ImprimirLineaAlineadaDerecha(Graphics g, string texto, Font font)
        {
            SizeF size = g.MeasureString(texto, font);
            float x = anchoTicket - size.Width - margenIzquierdo;
            g.DrawString(texto, font, Brushes.Black, x, lineaActual);
            lineaActual += (int)size.Height + 2;
        }

        private void ImprimirLineaConValor(Graphics g, string texto, decimal valor, Font font)
        {
            string valorFormateado = valor.ToString("C2");
            SizeF sizeValor = g.MeasureString(valorFormateado, font);

            g.DrawString(texto, font, Brushes.Black, margenIzquierdo, lineaActual);
            g.DrawString(valorFormateado, font, Brushes.Black,
                anchoTicket - sizeValor.Width - margenIzquierdo, lineaActual);

            lineaActual += (int)g.MeasureString(texto, font).Height + 2;
        }

        private void ImprimirLineaSeparadora(Graphics g, Font font)
        {
            string linea = new string('-', GetLineLength());
            ImprimirLineaCentrada(g, linea, font);
        }

        private void ImprimirLineaTabla(Graphics g, string cant, string desc, string precio, string total, Font font)
        {
            int col1 = margenIzquierdo;
            int col2 = margenIzquierdo + 30;
            int col3 = anchoTicket - 120;
            int col4 = anchoTicket - 60;

            g.DrawString(cant, font, Brushes.Black, col1, lineaActual);
            g.DrawString(desc, font, Brushes.Black, col2, lineaActual);

            SizeF sizePrecio = g.MeasureString(precio, font);
            g.DrawString(precio, font, Brushes.Black, col3 - sizePrecio.Width, lineaActual);

            SizeF sizeTotal = g.MeasureString(total, font);
            g.DrawString(total, font, Brushes.Black, col4 - sizeTotal.Width + 50, lineaActual);

            lineaActual += (int)g.MeasureString(cant, font).Height + 2;
        }

        private void ImprimirLineaProductoPedido(Graphics g, string cantidad, string nombre, Font font)
        {
            // Cantidad centrada en área de 40px
            SizeF sizeCant = g.MeasureString(cantidad, font);
            float cantX = margenIzquierdo + (40 - sizeCant.Width) / 2;
            g.DrawString(cantidad, font, Brushes.Black, cantX, lineaActual);

            // Nombre del producto
            g.DrawString(nombre, font, Brushes.Black, margenIzquierdo + 45, lineaActual);

            lineaActual += (int)g.MeasureString(cantidad, font).Height + 3;
        }

        private void ImprimirLineaProductoPedidoEstilo2(Graphics g, string cantidad, string nombre, Font font)
        {
            // Cantidad centrada en área de 35px
            SizeF sizeCant = g.MeasureString(cantidad, font);
            float cantX = margenIzquierdo + (35 - sizeCant.Width) / 2;
            g.DrawString(cantidad, font, Brushes.Black, cantX, lineaActual);

            // Nombre del producto
            g.DrawString(nombre, font, Brushes.Black, margenIzquierdo + 35, lineaActual);

            lineaActual += (int)g.MeasureString(cantidad, font).Height + 2;
        }

        private void ImprimirLineaConMargen(Graphics g, string texto, int margen, Font font)
        {
            g.DrawString(texto, font, Brushes.Black, margen, lineaActual);
            lineaActual += (int)g.MeasureString(texto, font).Height + 1;
        }

        private void ImprimirTextoMultilinea(Graphics g, string texto, Font font)
        {
            string[] palabras = texto.Split(' ');
            StringBuilder lineaActualTexto = new StringBuilder();

            foreach (string palabra in palabras)
            {
                string pruebaLinea = lineaActualTexto.Length == 0
                    ? palabra
                    : lineaActualTexto + " " + palabra;

                SizeF size = g.MeasureString(pruebaLinea, font);

                if (size.Width > anchoTicket - (margenIzquierdo * 2))
                {
                    if (lineaActualTexto.Length > 0)
                    {
                        ImprimirLinea(g, lineaActualTexto.ToString(), font);
                        lineaActualTexto.Clear();
                    }
                    lineaActualTexto.Append(palabra);
                }
                else
                {
                    if (lineaActualTexto.Length > 0)
                        lineaActualTexto.Append(" ");
                    lineaActualTexto.Append(palabra);
                }
            }

            if (lineaActualTexto.Length > 0)
            {
                ImprimirLinea(g, lineaActualTexto.ToString(), font);
            }
        }

        // Métodos para ticket de pedido
        private async System.Threading.Tasks.Task<List<ItemCarritoConCategoria>> ObtenerItemsConCategoriaAsync(List<ItemCarrito> items)
        {
            if (_context == null)
                return new List<ItemCarritoConCategoria>();

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

        #endregion

        #region CLASE AUXILIAR

        private class ItemCarritoConCategoria
        {
            public int Cantidad { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string Categoria { get; set; } = "OTROS";
            public bool EsCombo { get; set; }
            public List<string>? DetallesCombo { get; set; }
        }

        #endregion
    }
}
        