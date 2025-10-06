using POS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace POS.Interfaces
{
    public interface IVentaService
    {
        Task<Venta> GetVentaByIdAsync(int id);
        Task<IEnumerable<Venta>> GetAllVentasAsync();
        Task<Venta> CreateVentaAsync(Venta venta);
        // Podrías agregar métodos para actualizar o cancelar ventas si lo necesitas en el futuro
        // Task<bool> UpdateVentaAsync(int id, Venta venta);
        // Task<bool> CancelVentaAsync(int id);
    }
}