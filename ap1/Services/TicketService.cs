using POS.Models;
using POS.Services;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;

namespace POS.Services
{
    public class TicketService
    {
        private PrintDocument printDocument;
        private CorteCaja? _corte;
        private ResumenCorteCaja? _resumen;
        private int lineaActual;
        private const int margenIzquierdo = 10;
        private const int anchoTicket = 280;

        public void ImprimirCorteCaja(CorteCaja corte, ResumenCorteCaja resumen)
        {
            _corte = corte;
            _resumen = resumen;
            lineaActual = 10;

            // Cargar configuración
            var config = ConfigService.CargarConfiguracion();

            printDocument = new PrintDocument();
            printDocument.PrintPage += new PrintPageEventHandler(PrintPage_CorteCaja);

            // Configurar impresora desde config.json
            printDocument.PrinterSettings.PrinterName = config.ImpresoraNombre;

            // Configurar tamaño de papel según configuración (en milímetros)
            // Convertir mm a centésimas de pulgada (1 mm = 3.937 hundredths of inch)
            int anchoPulgadas = (int)(config.AnchoTicket * 3.937);
            PaperSize paperSize = new PaperSize("Ticket", anchoPulgadas, 800);
            printDocument.DefaultPageSettings.PaperSize = paperSize;

            try
            {
                // Verificar que la impresora exista
                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new Exception($"La impresora '{config.ImpresoraNombre}' no está disponible.");
                }

                printDocument.Print();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al imprimir: {ex.Message}");
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

            // Detalle de movimientos si hay
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

            // Si está cerrado, mostrar efectivo final y diferencia
            if (_corte.EstaCerrado)
            {
                ImprimirLineaSeparadora(g, fontNormal);
                ImprimirLinea(g, "CIERRE", fontBold);
                ImprimirLineaConValor(g, "Efectivo contado:", _corte.EfectivoFinal, fontNormal);

                // Diferencia con color
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
            string linea = new string('-', 40);
            ImprimirLineaCentrada(g, linea, font);
        }

        private void ImprimirTextoMultilinea(Graphics g, string texto, Font font)
        {
            // Dividir texto en líneas que quepan en el ancho
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
                    // Imprimir la línea actual y empezar nueva
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

            // Imprimir última línea
            if (lineaActualTexto.Length > 0)
            {
                ImprimirLinea(g, lineaActualTexto.ToString(), font);
            }
        }
    }
}