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
    public class VentaService : IVentaService
    {
        private readonly AppDbContext _context;

        public VentaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Venta>> GetAllVentasAsync()
        {
            return await _context.Ventas
                .Include(v => v.DetallesVenta)
                .ThenInclude(d => d.Producto) // Incluye el producto en cada detalle
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Venta> GetVentaByIdAsync(int id)
        {
            return await _context.Ventas
                .Include(v => v.DetallesVenta)
                .ThenInclude(d => d.Producto)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Venta> CreateVentaAsync(Venta venta)
        {
            // Iniciar una transacción para asegurar la integridad de los datos
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Validar que la venta y los detalles no sean nulos
                if (venta == null || venta.DetallesVenta == null || !venta.DetallesVenta.Any())
                {
                    throw new ArgumentException("La venta o sus detalles no pueden ser nulos o vacíos.");
                }

                var detallesTemp = venta.DetallesVenta.ToList();
                venta.DetallesVenta.Clear();

                // 2. Guardar la entidad Venta para obtener su Id
                await _context.Ventas.AddAsync(venta);
                await _context.SaveChangesAsync();

                decimal totalCalculado = 0;

                // 3. Procesar cada detalle de la venta
                foreach (var detalle in detallesTemp)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);

                    // Validar que el producto exista y haya stock suficiente
                    if (producto == null)
                    {
                        throw new InvalidOperationException($"El producto con Id {detalle.ProductoId} no existe.");
                    }
                    if (producto.Stock < detalle.Cantidad)
                    {
                        throw new InvalidOperationException($"Stock insuficiente para el producto '{producto.Nombre}'.");
                    }

                    // Actualizar el stock del producto
                    producto.Stock -= detalle.Cantidad;

                    // Asignar el Id de la venta al detalle y calcular subtotal
                    detalle.VentaId = venta.Id;
                    detalle.PrecioUnitario = producto.Precio;
                    detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;

                    totalCalculado += detalle.Subtotal;

                    // Agregar el detalle al contexto
                    await _context.DetallesVenta.AddAsync(detalle);
                }

                // 4. Validar que el total enviado coincida con el total calculado
                if (Math.Abs(venta.Total - totalCalculado) > 0.01m)
                {
                    throw new InvalidOperationException($"El total de la venta ({venta.Total}) no coincide con la suma de los subtotales ({totalCalculado}).");
                }

                // 5. Guardar todos los cambios (detalles y stock actualizado)
                await _context.SaveChangesAsync();

                venta.DetallesVenta = detallesTemp;

                // Si todo fue exitoso, confirmar la transacción
                await transaction.CommitAsync();

                return venta;
            }
            catch (Exception)
            {
                // Si algo falla, revertir todos los cambios
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
