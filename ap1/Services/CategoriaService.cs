using POS.Data;
using POS.Interfaces;
using POS.Models;
using Microsoft.EntityFrameworkCore;

namespace POS.Services
{
    public class CategoriaService : ICategoriaService
    {
        private readonly AppDbContext _context;

        public CategoriaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Categoria>> GetAllCategoriasAsync()
        {
            return await _context.Categorias.AsNoTracking().ToListAsync();
        }

        public async Task<Categoria> GetCategoriaByIdAsync(int id)
        {
            return await _context.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Categoria> CreateCategoriaAsync(Categoria categoria)
        {
            if (categoria == null)
            {
                throw new ArgumentNullException(nameof(categoria));
            }

            await _context.Categorias.AddAsync(categoria);
            await _context.SaveChangesAsync();
            return categoria;
        }

        public async Task<bool> UpdateCategoriaAsync(int id, Categoria categoria)
        {
            if (id != categoria.Id)
            {
                return false; // El ID no coincide
            }

            var existingCategoria = await _context.Categorias.FindAsync(id);
            if (existingCategoria == null)
            {
                return false; // No se encontró la categoría
            }

            // Actualiza las propiedades
            _context.Entry(existingCategoria).CurrentValues.SetValues(categoria);

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Manejar excepciones de concurrencia si es necesario
                return false;
            }
        }

        public async Task<bool> DeleteCategoriaAsync(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
            {
                return false; // No se encontró
            }

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}