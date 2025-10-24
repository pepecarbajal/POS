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

        // NUEVO: Obtener venta pendiente por NFC
        Task<Venta> GetVentaPendienteByIdNfcAsync(string idNfc);

        // NUEVO: Finalizar venta pendiente (calcular excedente de tiempo)
        Task<Venta> FinalizarVentaPendienteAsync(string idNfc, decimal excedente = 0);

        // NUEVO: Obtener todas las ventas pendientes
        Task<IEnumerable<Venta>> GetVentasPendientesAsync();
    }
}