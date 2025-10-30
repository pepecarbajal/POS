using Microsoft.EntityFrameworkCore;
using POS.Data;
using POS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Services
{
    public class CorteCajaService
    {
        private readonly AppDbContext _context;

        public CorteCajaService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Abre un nuevo corte de caja con el efectivo inicial
        /// </summary>
        public async Task<CorteCaja> AbrirCorteCaja(decimal efectivoInicial, string? observaciones = null)
        {
            // Verificar si hay un corte abierto
            var corteAbierto = await ObtenerCorteAbierto();
            if (corteAbierto != null)
            {
                throw new InvalidOperationException("Ya existe un corte de caja abierto. Debe cerrarlo primero.");
            }

            var nuevoCorte = new CorteCaja
            {
                FechaApertura = DateTime.Now,
                EfectivoInicial = efectivoInicial,
                EfectivoFinal = 0,
                TotalVentasEfectivo = 0,
                TotalVentasTarjeta = 0,
                TotalDepositos = 0,
                TotalRetiros = 0,
                Diferencia = 0,
                EfectivoEsperado = efectivoInicial,
                EstaCerrado = false,
                Observaciones = observaciones
            };

            _context.CorteCajas.Add(nuevoCorte);
            await _context.SaveChangesAsync();

            return nuevoCorte;
        }

        /// <summary>
        /// Obtiene el corte de caja actualmente abierto
        /// </summary>
        public async Task<CorteCaja?> ObtenerCorteAbierto()
        {
            return await _context.CorteCajas
                .Where(c => !c.EstaCerrado)
                .OrderByDescending(c => c.FechaApertura)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Registra un depósito en el corte actual
        /// </summary>
        public async Task<MovimientoCaja> RegistrarDeposito(decimal monto, string concepto, string? observaciones = null, string? usuario = null)
        {
            var corteAbierto = await ObtenerCorteAbierto();
            if (corteAbierto == null)
            {
                throw new InvalidOperationException("No hay un corte de caja abierto.");
            }

            var movimiento = new MovimientoCaja
            {
                CorteCajaId = corteAbierto.Id,
                Fecha = DateTime.Now,
                TipoMovimiento = (int)TipoMovimiento.Deposito,
                Monto = monto,
                Concepto = concepto,
                Observaciones = observaciones,
                Usuario = usuario
            };

            _context.MovimientosCaja.Add(movimiento);
            await _context.SaveChangesAsync();

            return movimiento;
        }

        /// <summary>
        /// Registra un retiro en el corte actual
        /// </summary>
        public async Task<MovimientoCaja> RegistrarRetiro(decimal monto, string concepto, string? observaciones = null, string? usuario = null)
        {
            var corteAbierto = await ObtenerCorteAbierto();
            if (corteAbierto == null)
            {
                throw new InvalidOperationException("No hay un corte de caja abierto.");
            }

            var movimiento = new MovimientoCaja
            {
                CorteCajaId = corteAbierto.Id,
                Fecha = DateTime.Now,
                TipoMovimiento = (int)TipoMovimiento.Retiro,
                Monto = monto,
                Concepto = concepto,
                Observaciones = observaciones,
                Usuario = usuario
            };

            _context.MovimientosCaja.Add(movimiento);
            await _context.SaveChangesAsync();

            return movimiento;
        }

        /// <summary>
        /// Obtiene los movimientos del corte actual
        /// </summary>
        public async Task<List<MovimientoCaja>> ObtenerMovimientosCorteActual()
        {
            var corteAbierto = await ObtenerCorteAbierto();
            if (corteAbierto == null) return new List<MovimientoCaja>();

            return await _context.MovimientosCaja
                .Where(m => m.CorteCajaId == corteAbierto.Id)
                .OrderBy(m => m.Fecha)
                .ToListAsync();
        }

        /// <summary>
        /// Calcula los totales del corte actual (sin cerrarlo)
        /// </summary>
        public async Task<ResumenCorteCaja> CalcularResumenCorte()
        {
            var corteAbierto = await ObtenerCorteAbierto();
            if (corteAbierto == null)
            {
                throw new InvalidOperationException("No hay un corte de caja abierto.");
            }

            var fechaInicio = corteAbierto.FechaApertura.Date;
            var fechaFin = DateTime.Now;

            // Obtener ventas finalizadas del día
            var ventas = await _context.Ventas
                .Where(v => v.Estado == (int)EstadoVenta.Finalizada &&
                           v.Fecha >= fechaInicio &&
                           v.Fecha <= fechaFin)
                .ToListAsync();

            var totalVentasEfectivo = ventas
                .Where(v => v.TipoPago == (int)TipoPago.Efectivo)
                .Sum(v => v.Total);

            var totalVentasTarjeta = ventas
                .Where(v => v.TipoPago == (int)TipoPago.Tarjeta)
                .Sum(v => v.Total);

            // Obtener movimientos
            var movimientos = await ObtenerMovimientosCorteActual();

            var totalDepositos = movimientos
                .Where(m => m.TipoMovimiento == (int)TipoMovimiento.Deposito)
                .Sum(m => m.Monto);

            var totalRetiros = movimientos
                .Where(m => m.TipoMovimiento == (int)TipoMovimiento.Retiro)
                .Sum(m => m.Monto);

            // Calcular efectivo esperado
            var efectivoEsperado = corteAbierto.EfectivoInicial + totalVentasEfectivo + totalDepositos - totalRetiros;

            return new ResumenCorteCaja
            {
                CorteCajaId = corteAbierto.Id,
                FechaApertura = corteAbierto.FechaApertura,
                EfectivoInicial = corteAbierto.EfectivoInicial,
                TotalVentasEfectivo = totalVentasEfectivo,
                TotalVentasTarjeta = totalVentasTarjeta,
                TotalDepositos = totalDepositos,
                TotalRetiros = totalRetiros,
                EfectivoEsperado = efectivoEsperado,
                CantidadVentasEfectivo = ventas.Count(v => v.TipoPago == (int)TipoPago.Efectivo),
                CantidadVentasTarjeta = ventas.Count(v => v.TipoPago == (int)TipoPago.Tarjeta),
                Movimientos = movimientos
            };
        }

        /// <summary>
        /// Cierra el corte de caja actual con el efectivo final contado
        /// </summary>
        public async Task<CorteCaja> CerrarCorteCaja(decimal efectivoFinal, string? usuario = null, string? observaciones = null)
        {
            var corteAbierto = await ObtenerCorteAbierto();
            if (corteAbierto == null)
            {
                throw new InvalidOperationException("No hay un corte de caja abierto.");
            }

            var resumen = await CalcularResumenCorte();

            // Actualizar el corte
            corteAbierto.FechaCierre = DateTime.Now;
            corteAbierto.EfectivoFinal = efectivoFinal;
            corteAbierto.TotalVentasEfectivo = resumen.TotalVentasEfectivo;
            corteAbierto.TotalVentasTarjeta = resumen.TotalVentasTarjeta;
            corteAbierto.TotalDepositos = resumen.TotalDepositos;
            corteAbierto.TotalRetiros = resumen.TotalRetiros;
            corteAbierto.EfectivoEsperado = resumen.EfectivoEsperado;
            corteAbierto.Diferencia = efectivoFinal - resumen.EfectivoEsperado;
            corteAbierto.UsuarioCierre = usuario;
            corteAbierto.EstaCerrado = true;

            if (!string.IsNullOrWhiteSpace(observaciones))
            {
                corteAbierto.Observaciones = string.IsNullOrWhiteSpace(corteAbierto.Observaciones)
                    ? observaciones
                    : $"{corteAbierto.Observaciones}\n{observaciones}";
            }

            await _context.SaveChangesAsync();

            return corteAbierto;
        }

        /// <summary>
        /// Obtiene el historial de cortes de caja
        /// </summary>
        public async Task<List<CorteCaja>> ObtenerHistorialCortes(DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            var query = _context.CorteCajas.AsQueryable();

            if (fechaInicio.HasValue)
            {
                query = query.Where(c => c.FechaApertura >= fechaInicio.Value);
            }

            if (fechaFin.HasValue)
            {
                query = query.Where(c => c.FechaApertura <= fechaFin.Value);
            }

            return await query
                .OrderByDescending(c => c.FechaApertura)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene un corte específico con sus movimientos
        /// </summary>
        public async Task<CorteCajaDetallado?> ObtenerCortePorId(int corteCajaId)
        {
            var corte = await _context.CorteCajas
                .FirstOrDefaultAsync(c => c.Id == corteCajaId);

            if (corte == null) return null;

            var movimientos = await _context.MovimientosCaja
                .Where(m => m.CorteCajaId == corteCajaId)
                .OrderBy(m => m.Fecha)
                .ToListAsync();

            var fechaInicio = corte.FechaApertura.Date;
            var fechaFin = corte.FechaCierre ?? DateTime.Now;

            var ventas = await _context.Ventas
                .Where(v => v.Estado == (int)EstadoVenta.Finalizada &&
                           v.Fecha >= fechaInicio &&
                           v.Fecha <= fechaFin)
                .ToListAsync();

            return new CorteCajaDetallado
            {
                Corte = corte,
                Movimientos = movimientos,
                Ventas = ventas,
                CantidadVentasEfectivo = ventas.Count(v => v.TipoPago == (int)TipoPago.Efectivo),
                CantidadVentasTarjeta = ventas.Count(v => v.TipoPago == (int)TipoPago.Tarjeta)
            };
        }
    }

    // Clases auxiliares para los resultados
    public class ResumenCorteCaja
    {
        public int CorteCajaId { get; set; }
        public DateTime FechaApertura { get; set; }
        public decimal EfectivoInicial { get; set; }
        public decimal TotalVentasEfectivo { get; set; }
        public decimal TotalVentasTarjeta { get; set; }
        public decimal TotalDepositos { get; set; }
        public decimal TotalRetiros { get; set; }
        public decimal EfectivoEsperado { get; set; }
        public int CantidadVentasEfectivo { get; set; }
        public int CantidadVentasTarjeta { get; set; }
        public List<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();

        public decimal TotalVentas => TotalVentasEfectivo + TotalVentasTarjeta;
    }

    public class CorteCajaDetallado
    {
        public CorteCaja Corte { get; set; }
        public List<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();
        public List<Venta> Ventas { get; set; } = new List<Venta>();
        public int CantidadVentasEfectivo { get; set; }
        public int CantidadVentasTarjeta { get; set; }
    }
}