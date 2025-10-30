using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using POS.Models;

namespace POS.Services.Interfaces
{
    public interface ICorteCajaService
    {
        // Movimientos de caja
        Task<MovimientoCaja> RegistrarEfectivoInicialAsync(decimal monto, string concepto = null, string usuario = null);
        Task<bool> ExisteEfectivoInicialHoyAsync();
        Task<MovimientoCaja> RegistrarDepositoAsync(decimal monto, string concepto, string usuario = null);
        Task<MovimientoCaja> RegistrarRetiroAsync(decimal monto, string concepto, string usuario = null);
        Task<List<MovimientoCaja>> ObtenerMovimientosDiaAsync(DateTime fecha);
        Task<decimal> ObtenerEfectivoInicialDiaAsync(DateTime fecha);

        // Corte de caja
        Task<CorteCaja> RealizarCorteCajaAsync(DateTime fechaInicio, DateTime fechaFin, decimal efectivoContado, string observaciones = null, string usuario = null);
        Task<CorteCaja> ObtenerUltimoCorteAsync();
        Task<List<CorteCaja>> ObtenerCortesAsync(DateTime? desde = null, DateTime? hasta = null);
        Task<CorteCaja> ObtenerCortePorIdAsync(int id);

        // Resumen y reportes
        Task<Dictionary<string, decimal>> ObtenerResumenVentasDiaAsync(DateTime fecha);
        Task<string> GenerarTicketCorteAsync(int corteCajaId);
    }
}