using POS.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace POS.Interfaces
{
    public interface ITiempoService
    {
        Task<List<Tiempo>> GetAllTiemposAsync();
        Task<Tiempo> GetTiempoByIdAsync(int id);
        Task<Tiempo> GetTiempoActivoByIdNfcAsync(string idNfc);
        Task<Tiempo> RegistrarEntradaAsync(string idNfc);
        Task<Tiempo> RegistrarSalidaAsync(int id, decimal porcentajeDescuento = 0);
        Task<List<Tiempo>> GetTiemposActivosAsync();
        Task<List<Tiempo>> GetTiemposByFechaAsync(DateTime fecha);
    }
}