using POS.Data;
using POS.Interfaces;
using POS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Services
{
    public class TiempoService : ITiempoService
    {
        private readonly AppDbContext _context;
        private readonly IPrecioTiempoService _precioTiempoService;

        public TiempoService(AppDbContext context, IPrecioTiempoService precioTiempoService)
        {
            _context = context;
            _precioTiempoService = precioTiempoService;
        }

        public async Task<List<Tiempo>> GetAllTiemposAsync()
        {
            return await _context.Tiempos
                .AsNoTracking()
                .OrderByDescending(t => t.Fecha)
                .ThenByDescending(t => t.HoraEntrada)
                .ToListAsync();
        }

        public async Task<Tiempo> GetTiempoByIdAsync(int id)
        {
            return await _context.Tiempos
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Tiempo> GetTiempoActivoByIdNfcAsync(string idNfc)
        {
            return await _context.Tiempos
                .AsNoTracking()
                .Where(t => t.IdNfc == idNfc && t.Estado == "Activo")
                .OrderByDescending(t => t.HoraEntrada)
                .FirstOrDefaultAsync();
        }

        public async Task<Tiempo> RegistrarEntradaAsync(string idNfc)
        {
            // Verificar si ya existe una entrada activa para este IdNfc
            var entradaActiva = await GetTiempoActivoByIdNfcAsync(idNfc);
            if (entradaActiva != null)
            {
                throw new InvalidOperationException($"Ya existe una entrada activa para el IdNfc: {idNfc}");
            }

            var ahora = DateTime.Now;
            var tiempo = new Tiempo
            {
                IdNfc = idNfc,
                Fecha = ahora.Date,
                HoraEntrada = ahora,
                HoraSalida = null,
                Total = 0,
                Estado = "Activo"
            };

            await _context.Tiempos.AddAsync(tiempo);
            await _context.SaveChangesAsync();
            return tiempo;
        }

        public async Task<Tiempo> RegistrarSalidaAsync(int id, decimal porcentajeDescuento = 0)
        {
            var tiempo = await _context.Tiempos.FindAsync(id);
            if (tiempo == null)
            {
                throw new KeyNotFoundException($"No se encontró el registro con Id: {id}");
            }

            if (tiempo.Estado == "Finalizado")
            {
                throw new InvalidOperationException("Este registro ya ha sido finalizado.");
            }

            var horaSalida = DateTime.Now;

            // Validar que haya pasado al menos 1 minuto
            var diferenciaMinutos = (horaSalida - tiempo.HoraEntrada).TotalMinutes;
            // if (diferenciaMinutos < 1){throw new InvalidOperationException("Debe transcurrir al menos 1 minuto desde la hora de entrada.");}

            tiempo.HoraSalida = horaSalida;
            tiempo.Total = await CalcularTotalAsync(tiempo.HoraEntrada, horaSalida, porcentajeDescuento);
            tiempo.Estado = "Finalizado";

            _context.Tiempos.Update(tiempo);
            await _context.SaveChangesAsync();
            return tiempo;
        }

        public async Task<List<Tiempo>> GetTiemposActivosAsync()
        {
            return await _context.Tiempos
                .AsNoTracking()
                .Where(t => t.Estado == "Activo")
                .OrderBy(t => t.HoraEntrada)
                .ToListAsync();
        }

        public async Task<List<Tiempo>> GetTiemposByFechaAsync(DateTime fecha)
        {
            return await _context.Tiempos
                .AsNoTracking()
                .Where(t => t.Fecha.Date == fecha.Date)
                .OrderByDescending(t => t.HoraEntrada)
                .ToListAsync();
        }

        /// <summary>
        /// Calcula el total a cobrar basado en el tiempo transcurrido y el porcentaje de descuento.
        /// El cálculo se hace dividiendo el precio base entre los minutos del tramo,
        /// obteniendo el precio por minuto, y multiplicándolo por los minutos usados.
        /// Para tiempos que exceden un tramo, se calcula el excedente de la misma manera.
        /// Los precios son obtenidos dinámicamente desde la base de datos.
        /// </summary>
        private async Task<decimal> CalcularTotalAsync(DateTime horaEntrada, DateTime horaSalida, decimal porcentajeDescuento)
        {
            var tiempoTranscurrido = horaSalida - horaEntrada;
            var minutosTotales = (int)Math.Ceiling(tiempoTranscurrido.TotalMinutes);

            // Obtener los precios desde la base de datos ordenados por orden
            var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

            if (precios == null || !precios.Any())
            {
                throw new InvalidOperationException("No hay precios de tiempo configurados en el sistema.");
            }

            decimal precioTotal = 0;
            int minutosRestantes = minutosTotales;
            int minutosAcumulados = 0;

            for (int i = 0; i < precios.Count; i++)
            {
                var tramo = precios[i];
                var minutosTramo = tramo.Minutos;
                var precioTramo = tramo.Precio;

                // Si es el primer tramo
                if (i == 0)
                {
                    if (minutosRestantes <= minutosTramo)
                    {
                        // Está dentro del primer tramo
                        decimal precioPorMinuto = precioTramo / minutosTramo;
                        precioTotal = precioPorMinuto * minutosRestantes;
                        break;
                    }
                    else
                    {
                        // Usa todo el primer tramo
                        decimal precioPorMinuto = precioTramo / minutosTramo;
                        precioTotal += precioPorMinuto * minutosTramo;
                        minutosRestantes -= minutosTramo;
                        minutosAcumulados = minutosTramo;
                    }
                }
                else
                {
                    // Tramos siguientes: calcular excedentes
                    var tramoAnterior = precios[i - 1];
                    int minutosExcedenteTramo = minutosTramo - tramoAnterior.Minutos;
                    decimal precioExcedenteTramo = precioTramo - tramoAnterior.Precio;

                    if (minutosRestantes <= minutosExcedenteTramo)
                    {
                        // Los minutos restantes están dentro de este tramo
                        decimal precioPorMinutoExcedente = precioExcedenteTramo / minutosExcedenteTramo;
                        precioTotal += precioPorMinutoExcedente * minutosRestantes;
                        break;
                    }
                    else
                    {
                        // Usa todo el excedente de este tramo
                        decimal precioPorMinutoExcedente = precioExcedenteTramo / minutosExcedenteTramo;
                        precioTotal += precioPorMinutoExcedente * minutosExcedenteTramo;
                        minutosRestantes -= minutosExcedenteTramo;
                        minutosAcumulados += minutosExcedenteTramo;
                    }
                }
            }

            // Si quedan minutos después del último tramo configurado, usar el precio por minuto del último tramo
            if (minutosRestantes > 0 && precios.Count > 0)
            {
                var ultimoTramo = precios[precios.Count - 1];

                if (precios.Count > 1)
                {
                    var penultimoTramo = precios[precios.Count - 2];
                    int minutosExcedenteUltimoTramo = ultimoTramo.Minutos - penultimoTramo.Minutos;
                    decimal precioExcedenteUltimoTramo = ultimoTramo.Precio - penultimoTramo.Precio;
                    decimal precioPorMinutoExcedente = precioExcedenteUltimoTramo / minutosExcedenteUltimoTramo;
                    precioTotal += precioPorMinutoExcedente * minutosRestantes;
                }
                else
                {
                    // Solo hay un tramo configurado
                    decimal precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
                    precioTotal += precioPorMinuto * minutosRestantes;
                }
            }

            // Aplicar descuento
            if (porcentajeDescuento > 0)
            {
                decimal descuento = precioTotal * (porcentajeDescuento / 100m);
                precioTotal -= descuento;
            }

            return Math.Round(precioTotal, 2);
        }
    }
}