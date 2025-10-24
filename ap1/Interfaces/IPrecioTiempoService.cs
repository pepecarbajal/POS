using POS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace POS.Interfaces
{
    public interface IPrecioTiempoService
    {
        Task<List<PrecioTiempo>> GetAllPreciosTiempoAsync();
        Task<PrecioTiempo> GetPrecioTiempoByIdAsync(int id);
        Task<bool> UpdatePrecioAsync(int id, decimal nuevoPrecio);
        Task<List<PrecioTiempo>> GetPreciosTiempoActivosAsync();
    }
}