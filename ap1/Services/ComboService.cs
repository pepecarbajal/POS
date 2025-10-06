using POS.Data;
using POS.Models;
using Microsoft.EntityFrameworkCore;
using POS.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Services
{
    public class ComboService : IComboService
    {
        private readonly AppDbContext _context;

        public ComboService(AppDbContext context)
        {
            _context = context;
        }

        // --- Métodos CRUD para Combo ---

        public async Task<IEnumerable<Combo>> GetAllCombosAsync()
        {
            return await _context.Combos.AsNoTracking().ToListAsync();
        }

        public async Task<Combo> GetComboByIdAsync(int id)
        {
            // Include carga los productos relacionados con este combo
            return await _context.Combos
                                 .Include(c => c.Productos)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Combo> CreateComboAsync(Combo combo)
        {
            await _context.Combos.AddAsync(combo);
            await _context.SaveChangesAsync();
            return combo;
        }

        public async Task<bool> UpdateComboAsync(int id, Combo combo)
        {
            if (id != combo.Id) return false;

            var existingCombo = await _context.Combos.FindAsync(id);
            if (existingCombo == null) return false;

            _context.Entry(existingCombo).CurrentValues.SetValues(combo);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteComboAsync(int id)
        {
            var combo = await _context.Combos.FindAsync(id);
            if (combo == null) return false;

            _context.Combos.Remove(combo);
            await _context.SaveChangesAsync();
            return true;
        }

        // --- Métodos para manejar productos en un combo ---

        public async Task<bool> AddProductoToComboAsync(int comboId, int productoId)
        {
            var combo = await _context.Combos.Include(c => c.Productos)
                                    .FirstOrDefaultAsync(c => c.Id == comboId);
            var producto = await _context.Productos.FindAsync(productoId);

            if (combo == null || producto == null)
            {
                return false; // Combo o producto no existen
            }

            // Evitar duplicados
            if (!combo.Productos.Any(p => p.Id == productoId))
            {
                combo.Productos.Add(producto);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> RemoveProductoFromComboAsync(int comboId, int productoId)
        {
            var combo = await _context.Combos.Include(c => c.Productos)
                                    .FirstOrDefaultAsync(c => c.Id == comboId);

            if (combo == null) return false;

            var producto = combo.Productos.FirstOrDefault(p => p.Id == productoId);
            if (producto == null) return false; // El producto no estaba en el combo

            combo.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Producto>> GetProductosByComboIdAsync(int comboId)
        {
            var combo = await _context.Combos
                                 .Include(c => c.Productos)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(c => c.Id == comboId);

            return combo?.Productos ?? new List<Producto>();
        }
    }
}