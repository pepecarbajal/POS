using POS.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace POS.Interfaces
{
    public interface IDevolucionService
    {
        /// <summary>
        /// Devuelve productos, combos o tiempo con cantidades específicas
        /// </summary>
        Task<bool> DevolverProductosParcialesAsync(int ventaId, Dictionary<int, int> detallesConCantidades);

        /// <summary>
        /// Devuelve una venta completa
        /// </summary>
        Task<bool> DevolverVentaCompletaAsync(int ventaId);

        /// <summary>
        /// Verifica si una venta puede ser devuelta (solo verifica que no esté pendiente)
        /// </summary>
        Task<bool> PuedeSerDespueltaAsync(int ventaId);

        /// <summary>
        /// Obtiene todos los detalles devolvibles de una venta (productos, combos y tiempo)
        /// </summary>
        Task<List<DetalleVenta>> GetDetallesDevolviblesAsync(int ventaId);
    }
}