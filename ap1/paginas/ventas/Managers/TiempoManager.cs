using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using POS.Data;
using POS.Models;
using POS.Services;
using Microsoft.EntityFrameworkCore;
using POS.paginas.ventas;

namespace POS.paginas.ventas.Managers
{
    /// <summary>
    /// Manager para gestionar operaciones de tiempo (solo combos con tiempo)
    /// </summary>
    public class TiempoManager
    {
        private readonly AppDbContext _context;
        private readonly VentaService _ventaService;
        private readonly PrecioTiempoService _precioTiempoService;

        public TiempoManager(
            AppDbContext context,
            VentaService ventaService,
            PrecioTiempoService precioTiempoService)
        {
            _context = context;
            _ventaService = ventaService;
            _precioTiempoService = precioTiempoService;
        }

        /// <summary>
        /// Obtiene todos los tiempos activos (solo combos con tiempo)
        /// </summary>
        public async Task<List<TiempoActivo>> ObtenerTiemposActivosAsync()
        {
            var tiemposActivos = new List<TiempoActivo>();

            // Ventas pendientes con combo de tiempo
            var ventasPendientes = await _context.Ventas
                .Include(v => v.DetallesVenta)
                .Where(v => v.Estado == (int)EstadoVenta.Pendiente &&
                           v.HoraEntrada.HasValue &&
                           v.MinutosTiempoCombo.HasValue &&
                           v.MinutosTiempoCombo > 0)
                .AsNoTracking()
                .ToListAsync();

            foreach (var venta in ventasPendientes)
            {
                var detalleCombo = venta.DetallesVenta
                    .FirstOrDefault(d => d.TipoItem == (int)TipoItemVenta.Combo);

                string nombreCombo = detalleCombo?.NombreItem ?? "Combo";
                int minutosIncluidos = venta.MinutosTiempoCombo ?? 0;

                tiemposActivos.Add(new TiempoActivo
                {
                    Id = venta.Id,
                    IdNfc = venta.IdNfc ?? "N/A",
                    NombreCliente = venta.NombreCliente ?? "Cliente",
                    HoraEntrada = venta.HoraEntrada!.Value,
                    Estado = "Activo",
                    EsCombo = true,
                    NombreCombo = nombreCombo,
                    MinutosIncluidos = minutosIncluidos,
                    MontoTotal = venta.Total
                });
            }

            return tiemposActivos.OrderByDescending(t => t.HoraEntrada).ToList();
        }

        /// <summary>
        /// Calcula el excedente de tiempo para un combo
        /// </summary>
        public async Task<(int minutosExtra, decimal excedente)> CalcularExcedenteComboAsync(DateTime horaEntrada, int minutosIncluidos)
        {
            var tiempoTranscurrido = (DateTime.Now - horaEntrada).TotalMinutes;

            if (tiempoTranscurrido <= minutosIncluidos)
            {
                return (0, 0);
            }

            var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

            if (precios == null || !precios.Any())
            {
                return (0, 0);
            }

            var ultimoTramo = precios.Last();
            decimal precioPorMinuto;

            if (precios.Count > 1)
            {
                var penultimoTramo = precios[precios.Count - 2];
                int minutosExcedente = ultimoTramo.Minutos - penultimoTramo.Minutos;
                decimal precioExcedente = ultimoTramo.Precio - penultimoTramo.Precio;
                precioPorMinuto = precioExcedente / minutosExcedente;
            }
            else
            {
                precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
            }

            int minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
            decimal excedente = minutosExtra * precioPorMinuto;

            return (minutosExtra, excedente);
        }
    }
}