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
    /// Manager para gestionar operaciones de tiempo (sesiones con NFC)
    /// </summary>
    public class TiempoManager
    {
        private readonly AppDbContext _context;
        private readonly TiempoService _tiempoService;
        private readonly VentaService _ventaService;
        private readonly PrecioTiempoService _precioTiempoService;

        public TiempoManager(
            AppDbContext context,
            TiempoService tiempoService,
            VentaService ventaService,
            PrecioTiempoService precioTiempoService)
        {
            _context = context;
            _tiempoService = tiempoService;
            _ventaService = ventaService;
            _precioTiempoService = precioTiempoService;
        }

        /// <summary>
        /// Inicia un tiempo individual con NFC
        /// </summary>
        public async Task<Tiempo?> IniciarTiempoAsync(string idNfc)
        {
            if (string.IsNullOrWhiteSpace(idNfc))
            {
                MessageBox.Show("El ID de la tarjeta NFC no puede estar vacío", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Validar que no haya combo pendiente con esta tarjeta
            var ventaPendienteCombo = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);
            if (ventaPendienteCombo != null && ventaPendienteCombo.MinutosTiempoCombo.HasValue && ventaPendienteCombo.MinutosTiempoCombo > 0)
            {
                MessageBox.Show($"La tarjeta {idNfc} ya está asociada a un combo con tiempo activo. No puedes iniciar un tiempo individual.",
                    "Tarjeta en uso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Validar que no haya entrada activa
            var entradaActiva = await _tiempoService.GetTiempoActivoByIdNfcAsync(idNfc);
            if (entradaActiva != null)
            {
                MessageBox.Show($"Ya existe una entrada activa para la tarjeta {idNfc}", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var tiempo = await _tiempoService.RegistrarEntradaAsync(idNfc);
            return tiempo;
        }

        /// <summary>
        /// Finaliza un tiempo individual
        /// </summary>
        public async Task<(Tiempo? tiempo, string nombreDisplay)?> FinalizarTiempoAsync(string idNfc)
        {
            if (string.IsNullOrWhiteSpace(idNfc))
            {
                MessageBox.Show("El ID de la tarjeta NFC no puede estar vacío", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Verificar si es un combo con tiempo
            var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);
            if (ventaPendiente != null && ventaPendiente.MinutosTiempoCombo.HasValue && ventaPendiente.MinutosTiempoCombo > 0)
            {
                MessageBox.Show("Esta tarjeta tiene un combo con tiempo. Use la opción de finalizar combo.",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            // Buscar tiempo activo
            var entradaActiva = await _tiempoService.GetTiempoActivoByIdNfcAsync(idNfc);
            if (entradaActiva == null)
            {
                MessageBox.Show($"No se encontró una entrada activa para la tarjeta {idNfc}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Finalizar tiempo
            var tiempoFinalizado = await _tiempoService.RegistrarSalidaAsync(entradaActiva.Id);

            var tiempoTranscurrido = (tiempoFinalizado.HoraSalida!.Value - tiempoFinalizado.HoraEntrada).TotalMinutes;
            var nombreDisplay = $"Tiempo - ({Math.Ceiling(tiempoTranscurrido)} min)";

            return (tiempoFinalizado, nombreDisplay);
        }

        /// <summary>
        /// Reactiva un tiempo (lo pone en estado Activo nuevamente)
        /// </summary>
        public async Task<bool> ReactivarTiempoAsync(int tiempoId)
        {
            try
            {
                var tiempo = await _context.Tiempos.FindAsync(tiempoId);

                if (tiempo != null && tiempo.Estado == "Finalizado")
                {
                    tiempo.Estado = "Activo";
                    tiempo.HoraSalida = null;
                    tiempo.Total = 0;

                    _context.Entry(tiempo).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reactivar tiempo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Elimina un tiempo de la base de datos
        /// </summary>
        public async Task<bool> EliminarTiempoAsync(int tiempoId)
        {
            try
            {
                var tiempo = await _context.Tiempos.FindAsync(tiempoId);

                if (tiempo == null)
                {
                    throw new InvalidOperationException("No se encontró el tiempo");
                }

                // Verificar que no esté en una venta
                var tiempoEnVenta = await _context.DetallesVenta
                    .AnyAsync(d => d.TipoItem == (int)TipoItemVenta.Tiempo && d.ItemReferenciaId == tiempoId);

                if (tiempoEnVenta)
                {
                    throw new InvalidOperationException("No se puede eliminar este tiempo porque ya está asociado a una venta");
                }

                _context.Tiempos.Remove(tiempo);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar tiempo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Obtiene todos los tiempos activos (tanto individuales como combos)
        /// </summary>
        public async Task<List<TiempoActivo>> ObtenerTiemposActivosAsync()
        {
            var tiemposActivos = new List<TiempoActivo>();

            // Tiempos individuales
            var tiempos = await _tiempoService.GetTiemposActivosAsync();
            foreach (var tiempo in tiempos)
            {
                tiemposActivos.Add(new TiempoActivo
                {
                    Id = tiempo.Id,
                    IdNfc = tiempo.IdNfc,
                    HoraEntrada = tiempo.HoraEntrada,
                    Estado = tiempo.Estado,
                    EsCombo = false,
                    NombreCombo = null,
                    MinutosIncluidos = 0,
                    MontoTotal = 0
                });
            }

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

                tiemposActivos.Add(new TiempoActivo
                {
                    Id = venta.Id,
                    IdNfc = venta.IdNfc ?? "N/A",
                    HoraEntrada = venta.HoraEntrada!.Value,
                    Estado = "Activo",
                    EsCombo = true,
                    NombreCombo = nombreCombo,
                    MinutosIncluidos = venta.MinutosTiempoCombo ?? 0,
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