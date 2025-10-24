using POS.Models;

namespace POS.Interfaces
{
    public interface IProductoService
    {
        Task<IEnumerable<Producto>> GetAllProductosAsync();
        Task<Producto> GetProductoByIdAsync(int id);
        Task<Producto> CreateProductoAsync(Producto producto);
        Task<bool> UpdateProductoAsync(int id, Producto producto);
        Task<bool> DeleteProductoAsync(int id);
    }
}