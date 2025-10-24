using POS.Models;


namespace POS.Interfaces
{
    public interface ICategoriaService
    {
        Task<IEnumerable<Categoria>> GetAllCategoriasAsync();
        Task<Categoria> GetCategoriaByIdAsync(int id);
        Task<Categoria> CreateCategoriaAsync(Categoria categoria);
        Task<bool> UpdateCategoriaAsync(int id, Categoria categoria);
        Task<bool> DeleteCategoriaAsync(int id);
    }
}
