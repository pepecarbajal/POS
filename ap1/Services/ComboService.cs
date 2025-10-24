using POS.Data;
using POS.Interfaces;
using POS.Models;
using Microsoft.EntityFrameworkCore;
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

        
        
        public async Task<IEnumerable<Combo>> GetAllCombosAsync()
        {
            return await _context.Combos.AsNoTracking().ToListAsync();
        }

        public async Task<Combo> GetComboByIdAsync(int id)
        {
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

        
        public async Task<bool> AddProductoToComboAsync(int comboId, int productoId, int cantidad = 1)
        {
            var combo = await _context.Combos.FindAsync(comboId);
            var producto = await _context.Productos.FindAsync(productoId);

            if (combo == null || producto == null)
            {
                return false;
            }

                        var existingComboProducto = await _context.ComboProductos
                .FirstOrDefaultAsync(cp => cp.ComboId == comboId && cp.ProductoId == productoId);

            if (existingComboProducto != null)
            {
                                existingComboProducto.Cantidad = cantidad;
            }
            else
            {
                                var comboProducto = new ComboProducto
                {
                    ComboId = comboId,
                    ProductoId = productoId,
                    Cantidad = cantidad
                };
                await _context.ComboProductos.AddAsync(comboProducto);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveProductoFromComboAsync(int comboId, int productoId)
        {
            var combo = await _context.Combos.Include(c => c.Productos)
                                    .FirstOrDefaultAsync(c => c.Id == comboId);

            if (combo == null) return false;

            var producto = combo.Productos.FirstOrDefault(p => p.Id == productoId);
            if (producto == null) return false; 
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

        public async Task<IEnumerable<ComboProducto>> GetComboProductosAsync(int comboId)
        {
            return await _context.ComboProductos
                .Include(cp => cp.Producto)
                .Where(cp => cp.ComboId == comboId)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
