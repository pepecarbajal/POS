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
    public class PrecioTiempoService : IPrecioTiempoService
    {
        private readonly AppDbContext _context;

        public PrecioTiempoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PrecioTiempo>> GetAllPreciosTiempoAsync()
        {
            return await _context.PreciosTiempo
                .AsNoTracking()
                .OrderBy(p => p.Orden)
                .ToListAsync();
        }

        public async Task<PrecioTiempo> GetPrecioTiempoByIdAsync(int id)
        {
            return await _context.PreciosTiempo
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        /// <summary>
        /// Actualiza únicamente el precio de un PrecioTiempo existente.
        /// No permite modificar Minutos, Orden, Estado u otros campos.
        /// </summary>
        public async Task<bool> UpdatePrecioAsync(int id, decimal nuevoPrecio)
        {
            if (nuevoPrecio <= 0)
            {
                throw new ArgumentException("El precio debe ser mayor a cero.");
            }

            var existingPrecio = await _context.PreciosTiempo.FindAsync(id);
            if (existingPrecio == null)
                return false;

            // Solo actualizar el precio
            existingPrecio.Precio = nuevoPrecio;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PrecioTiempo>> GetPreciosTiempoActivosAsync()
        {
            return await _context.PreciosTiempo
                .AsNoTracking()
                .Where(p => p.Estado == "Activo")
                .OrderBy(p => p.Orden)
                .ToListAsync();
        }
    }
}