using POS.Data;
using POS.Interfaces;
using POS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Services
{
    public class DevolucionService : IDevolucionService
    {
        private readonly AppDbContext _context;

        public DevolucionService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Verifica si una venta puede ser devuelta completamente
        /// </summary>
        public async Task<bool> PuedeSerDespueltaAsync(int ventaId)
        {
            var venta = await _context.Ventas
                .Include(v => v.DetallesVenta)
                .FirstOrDefaultAsync(v => v.Id == ventaId);

            if (venta == null) return false;

            // No se pueden devolver ventas pendientes
            bool esPendiente = venta.Estado == (int)EstadoVenta.Pendiente;

            return !esPendiente;
        }

        /// <summary>
        /// Obtiene los detalles de venta que pueden ser devueltos (incluye productos, combos y tiempo)
        /// </summary>
        public async Task<List<DetalleVenta>> GetDetallesDevolviblesAsync(int ventaId)
        {
            var venta = await _context.Ventas
                .Include(v => v.DetallesVenta)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.Id == ventaId);

            if (venta == null) return new List<DetalleVenta>();

            var detallesDevolvibles = new List<DetalleVenta>();

            foreach (var detalle in venta.DetallesVenta)
            {
                // Agregar todos los detalles (productos, combos y tiempo)
                detallesDevolvibles.Add(detalle);
            }

            return detallesDevolvibles;
        }

        /// <summary>
        /// Devuelve productos específicos de una venta con cantidades parciales
        /// </summary>
        public async Task<bool> DevolverProductosParcialesAsync(int ventaId, Dictionary<int, int> detallesConCantidades)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                    .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                {
                    throw new InvalidOperationException("Venta no encontrada");
                }

                // Verificar que no sea pendiente
                if (venta.Estado == (int)EstadoVenta.Pendiente)
                {
                    throw new InvalidOperationException("No se pueden devolver ventas pendientes");
                }

                decimal totalDevuelto = 0;
                var detallesParaEliminar = new List<DetalleVenta>();

                // Procesar cada detalle seleccionado
                foreach (var kvp in detallesConCantidades)
                {
                    int detalleId = kvp.Key;
                    int cantidadADevolver = kvp.Value;

                    var detalle = venta.DetallesVenta.FirstOrDefault(d => d.Id == detalleId);
                    if (detalle == null) continue;

                    // Validar que la cantidad a devolver no sea mayor a la disponible
                    if (cantidadADevolver > detalle.Cantidad)
                    {
                        throw new InvalidOperationException($"La cantidad a devolver ({cantidadADevolver}) es mayor a la cantidad disponible ({detalle.Cantidad})");
                    }

                    // Calcular el monto proporcional a devolver
                    decimal precioUnitarioReal = detalle.Subtotal / detalle.Cantidad;
                    decimal montoDevolucion = precioUnitarioReal * cantidadADevolver;

                    if (detalle.TipoItem == (int)TipoItemVenta.Combo && detalle.ItemReferenciaId.HasValue)
                    {
                        // Es un combo - restaurar stock de todos sus productos proporcionalmente
                        var combo = await _context.Combos
                            .Include(c => c.ComboProductos)
                            .ThenInclude(cp => cp.Producto)
                            .FirstOrDefaultAsync(c => c.Id == detalle.ItemReferenciaId.Value);

                        if (combo != null)
                        {
                            foreach (var comboProducto in combo.ComboProductos)
                            {
                                if (comboProducto.Producto != null)
                                {
                                    int cantidadARestaurar = comboProducto.Cantidad * cantidadADevolver;
                                    comboProducto.Producto.Stock += cantidadARestaurar;
                                    _context.Entry(comboProducto.Producto).State = EntityState.Modified;
                                }
                            }
                        }

                        // Actualizar o eliminar el detalle del combo
                        if (cantidadADevolver >= detalle.Cantidad)
                        {
                            // Devolver todos los combos
                            detallesParaEliminar.Add(detalle);
                        }
                        else
                        {
                            // Devolver parcialmente combos
                            detalle.Cantidad -= cantidadADevolver;
                            detalle.Subtotal -= montoDevolucion;
                            _context.Entry(detalle).State = EntityState.Modified;
                        }
                    }
                    else if (detalle.TipoItem == (int)TipoItemVenta.Producto && detalle.ProductoId.HasValue)
                    {
                        // Producto individual - restaurar stock
                        var producto = await _context.Productos.FindAsync(detalle.ProductoId.Value);
                        if (producto != null)
                        {
                            producto.Stock += cantidadADevolver;
                            _context.Entry(producto).State = EntityState.Modified;
                        }

                        // Actualizar o eliminar el detalle del producto
                        if (cantidadADevolver >= detalle.Cantidad)
                        {
                            // Devolver todo el producto
                            detallesParaEliminar.Add(detalle);
                        }
                        else
                        {
                            // Devolver parcialmente
                            detalle.Cantidad -= cantidadADevolver;
                            detalle.Subtotal -= montoDevolucion;
                            _context.Entry(detalle).State = EntityState.Modified;
                        }
                    }
                    else if (detalle.TipoItem == (int)TipoItemVenta.Tiempo)
                    {
                        // Es tiempo - no hay stock que restaurar, solo ajustar cantidades
                        if (cantidadADevolver >= detalle.Cantidad)
                        {
                            // Devolver todo el tiempo
                            detallesParaEliminar.Add(detalle);
                        }
                        else
                        {
                            // Devolver parcialmente tiempo
                            detalle.Cantidad -= cantidadADevolver;
                            detalle.Subtotal -= montoDevolucion;
                            _context.Entry(detalle).State = EntityState.Modified;
                        }
                    }

                    totalDevuelto += montoDevolucion;
                }

                // Eliminar los detalles que se devolvieron completamente
                if (detallesParaEliminar.Any())
                {
                    _context.DetallesVenta.RemoveRange(detallesParaEliminar);
                }

                // Actualizar total de la venta
                venta.Total -= totalDevuelto;
                _context.Entry(venta).State = EntityState.Modified;

                // Verificar si quedan detalles después de la devolución
                var detallesRestantes = venta.DetallesVenta
                    .Where(d => !detallesParaEliminar.Contains(d))
                    .ToList();

                if (!detallesRestantes.Any())
                {
                    // Si no queda nada, eliminar la venta completa
                    _context.DetallesVenta.RemoveRange(detallesRestantes);
                    _context.Ventas.Remove(venta);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Devuelve una venta completa
        /// </summary>
        public async Task<bool> DevolverVentaCompletaAsync(int ventaId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                    .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                {
                    throw new InvalidOperationException("Venta no encontrada");
                }

                // Verificar que puede ser devuelta
                bool puedeDevolver = await PuedeSerDespueltaAsync(ventaId);
                if (!puedeDevolver)
                {
                    throw new InvalidOperationException("Esta venta no puede ser devuelta (está pendiente)");
                }

                // Restaurar stock de todos los productos
                foreach (var detalle in venta.DetallesVenta)
                {
                    if (detalle.TipoItem == (int)TipoItemVenta.Producto && detalle.ProductoId.HasValue)
                    {
                        // Producto individual
                        var producto = await _context.Productos.FindAsync(detalle.ProductoId.Value);
                        if (producto != null)
                        {
                            producto.Stock += detalle.Cantidad;
                            _context.Entry(producto).State = EntityState.Modified;
                        }
                    }
                    else if (detalle.TipoItem == (int)TipoItemVenta.Combo && detalle.ItemReferenciaId.HasValue)
                    {
                        // Combo - restaurar stock de cada producto
                        var combo = await _context.Combos
                            .Include(c => c.ComboProductos)
                            .ThenInclude(cp => cp.Producto)
                            .FirstOrDefaultAsync(c => c.Id == detalle.ItemReferenciaId.Value);

                        if (combo != null)
                        {
                            foreach (var comboProducto in combo.ComboProductos)
                            {
                                if (comboProducto.Producto != null)
                                {
                                    int cantidadARestaurar = comboProducto.Cantidad * detalle.Cantidad;
                                    comboProducto.Producto.Stock += cantidadARestaurar;
                                    _context.Entry(comboProducto.Producto).State = EntityState.Modified;
                                }
                            }
                        }
                    }
                }

                // Eliminar todos los detalles
                _context.DetallesVenta.RemoveRange(venta.DetallesVenta);

                // Eliminar la venta
                _context.Ventas.Remove(venta);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}