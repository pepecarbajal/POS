using POS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace POS.Interfaces
{
    public interface IComboService
    {
        Task<IEnumerable<Combo>> GetAllCombosAsync();
        Task<Combo> GetComboByIdAsync(int id);
        Task<Combo> CreateComboAsync(Combo combo);
        Task<bool> UpdateComboAsync(int id, Combo combo);
        Task<bool> DeleteComboAsync(int id);

        // Métodos para manejar los productos dentro de un combo
        Task<bool> AddProductoToComboAsync(int comboId, int productoId, int cantidad = 1);
        Task<bool> RemoveProductoFromComboAsync(int comboId, int productoId);
        Task<IEnumerable<Producto>> GetProductosByComboIdAsync(int comboId);
        Task<IEnumerable<ComboProducto>> GetComboProductosAsync(int comboId);
    }
}
