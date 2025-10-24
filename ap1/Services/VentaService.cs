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
                .ThenInclude(d => d.Producto)
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

                public async Task<Venta> GetVentaPendienteByIdNfcAsync(string idNfc)
        {
            return await _context.Ventas
                .Include(v => v.DetallesVenta)
                .ThenInclude(d => d.Producto)
                .Where(v => v.IdNfc == idNfc && v.Estado == (int)EstadoVenta.Pendiente)
                .OrderByDescending(v => v.Fecha)
                .FirstOrDefaultAsync();
        }

                public async Task<IEnumerable<Venta>> GetVentasPendientesAsync()
        {
            return await _context.Ventas
                .Include(v => v.DetallesVenta)
                .ThenInclude(d => d.Producto)
                .Where(v => v.Estado == (int)EstadoVenta.Pendiente)
                .OrderByDescending(v => v.Fecha)
                .AsNoTracking()
                .ToListAsync();
        }

                public async Task<Venta> FinalizarVentaPendienteAsync(string idNfc, decimal excedente = 0)
        {
            var venta = await _context.Ventas
                .Include(v => v.DetallesVenta)
                .FirstOrDefaultAsync(v => v.IdNfc == idNfc && v.Estado == (int)EstadoVenta.Pendiente);

            if (venta == null)
            {
                throw new InvalidOperationException($"No se encontró una venta pendiente para el NFC: {idNfc}");
            }

                        if (excedente > 0)
            {
                venta.Total += excedente;
            }

                        venta.Estado = (int)EstadoVenta.Finalizada;

            _context.Ventas.Update(venta);
            await _context.SaveChangesAsync();

            return venta;
        }

        public async Task<Venta> CreateVentaAsync(Venta venta)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                ValidarVenta(venta);

                var detallesTemp = venta.DetallesVenta.ToList();
                venta.DetallesVenta.Clear();

                await _context.Ventas.AddAsync(venta);
                await _context.SaveChangesAsync();

                decimal totalCalculado = await ProcesarDetallesVenta(venta.Id, detallesTemp);

                ValidarTotal(venta.Total, totalCalculado);

                await _context.SaveChangesAsync();

                venta.DetallesVenta = detallesTemp;

                await transaction.CommitAsync();

                return venta;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private void ValidarVenta(Venta venta)
        {
            if (venta == null || venta.DetallesVenta == null || !venta.DetallesVenta.Any())
            {
                throw new ArgumentException("La venta o sus detalles no pueden ser nulos o vacíos.");
            }
        }

        private async Task<decimal> ProcesarDetallesVenta(int ventaId, List<DetalleVenta> detalles)
        {
            decimal totalCalculado = 0;

            foreach (var detalle in detalles)
            {
                if (detalle.ProductoId.HasValue)
                {
                    await ValidarProductoExiste(detalle.ProductoId.Value);

                    if (detalle.PrecioUnitario == 0 || detalle.Subtotal == 0)
                    {
                        var producto = await _context.Productos.FindAsync(detalle.ProductoId.Value);
                        if (producto != null)
                        {
                            detalle.PrecioUnitario = producto.Precio;
                            detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;
                        }
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(detalle.NombreItem))
                    {
                        throw new InvalidOperationException("El detalle debe tener un nombre de item.");
                    }

                    if (detalle.PrecioUnitario == 0)
                    {
                        throw new InvalidOperationException($"El item '{detalle.NombreItem}' debe tener un precio unitario.");
                    }

                    if (detalle.Subtotal == 0)
                    {
                        detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;
                    }
                }

                detalle.VentaId = ventaId;
                totalCalculado += detalle.Subtotal;
                await _context.DetallesVenta.AddAsync(detalle);
            }

            return totalCalculado;
        }

        private async Task ValidarProductoExiste(int productoId)
        {
            var producto = await _context.Productos.FindAsync(productoId);
            if (producto == null)
            {
                throw new InvalidOperationException($"El producto con Id {productoId} no existe.");
            }
        }

        private void ValidarTotal(decimal totalVenta, decimal totalCalculado)
        {
            if (Math.Abs(totalVenta - totalCalculado) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"El total de la venta ({totalVenta}) no coincide con la suma de los subtotales ({totalCalculado}).");
            }
        }
    }
}