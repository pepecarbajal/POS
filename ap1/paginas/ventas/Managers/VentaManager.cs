using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using POS.Data;
using POS.Models;
using POS.Services;
using Microsoft.EntityFrameworkCore;
using POS.paginas.ventas;

namespace POS.paginas.ventas.Managers
{
    /// <summary>
    /// Manager para gestionar operaciones de ventas
    /// </summary>
    public class VentaManager
    {
        private readonly AppDbContext _context;
        private readonly VentaService _ventaService;
        private readonly PrecioTiempoService _precioTiempoService;

        public VentaManager(
            AppDbContext context,
            VentaService ventaService,
            PrecioTiempoService precioTiempoService)
        {
            _context = context;
            _ventaService = ventaService;
            _precioTiempoService = precioTiempoService;
        }

        /// <summary>
        /// Crea una venta pendiente (combo con tiempo) CON NOMBRE DE CLIENTE Y TIPO DE PAGO
        /// </summary>
        public async Task<Venta?> CrearVentaPendienteAsync(string idNfc, string nombreCliente, List<ItemCarrito> items, int minutosComboTiempo, TipoPago tipoPago = TipoPago.Efectivo)
        {
            try
            {
                // Validar que no exista venta pendiente
                var ventaExistente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);
                if (ventaExistente != null)
                {
                    MessageBox.Show($"Ya existe una venta pendiente asociada a la tarjeta {idNfc}. Por favor, finalice esa venta primero.",
                        "Tarjeta en uso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var venta = new Venta
                {
                    Fecha = DateTime.Now,
                    Total = items.Sum(i => i.Total),
                    Estado = (int)EstadoVenta.Pendiente,
                    TipoPago = (int)tipoPago, // NUEVO: Asignar tipo de pago
                    IdNfc = idNfc,
                    HoraEntrada = DateTime.Now,
                    MinutosTiempoCombo = minutosComboTiempo,
                    NombreCliente = nombreCliente,
                    DetallesVenta = new List<DetalleVenta>()
                };

                // Procesar items
                foreach (var item in items)
                {
                    await ProcesarItemParaVenta(venta, item);
                }

                await _ventaService.CreateVentaAsync(venta);
                await _context.SaveChangesAsync();

                return venta;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar venta pendiente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        /// <summary>
        /// Finaliza una venta pendiente (combo con tiempo)
        /// </summary>
        public async Task<Venta?> FinalizarVentaPendienteAsync(string idNfc)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                if (!ventaPendiente.HoraEntrada.HasValue)
                {
                    MessageBox.Show("La venta pendiente no tiene hora de entrada registrada.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var horaSalida = DateTime.Now;
                var tiempoTranscurrido = (horaSalida - ventaPendiente.HoraEntrada.Value).TotalMinutes;
                var minutosIncluidos = ventaPendiente.MinutosTiempoCombo ?? 0;

                decimal excedente = 0;
                int minutosExtra = 0;

                // Calcular excedente si aplica
                if (tiempoTranscurrido > minutosIncluidos)
                {
                    var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

                    if (precios != null && precios.Count > 0)
                    {
                        var ultimoTramo = precios.Last();
                        decimal precioPorMinuto;

                        if (precios.Count > 1)
                        {
                            var penultimoTramo = precios[precios.Count - 2];
                            int minutosExcedente = ultimoTramo.Minutos - penultimoTramo.Minutos;
                            decimal precioExcedente = ultimoTramo.Precio - penultimoTramo.Precio;
                            precioPorMinuto = precioExcedente / minutosExcedente;
                        }
                        else
                        {
                            precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
                        }

                        minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
                        excedente = minutosExtra * precioPorMinuto;
                    }
                }

                // Agregar detalle de excedente si existe
                if (excedente > 0)
                {
                    var detalleExcedente = new DetalleVenta
                    {
                        VentaId = ventaPendiente.Id,
                        TipoItem = (int)TipoItemVenta.Tiempo,
                        ProductoId = null,
                        ItemReferenciaId = null,
                        NombreItem = $"Excedente de tiempo ({minutosExtra} min extra)",
                        Cantidad = 1,
                        PrecioUnitario = excedente,
                        Subtotal = excedente
                    };

                    ventaPendiente.DetallesVenta.Add(detalleExcedente);
                    _context.DetallesVenta.Add(detalleExcedente);
                }

                await _ventaService.FinalizarVentaPendienteAsync(idNfc, excedente);

                var ventaFinalizada = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == ventaPendiente.Id);

                return ventaFinalizada;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Crea una venta finalizada normal con tipo de pago
        /// </summary>
        public async Task<Venta?> CrearVentaFinalizadaAsync(List<ItemCarrito> items, TipoPago tipoPago = TipoPago.Efectivo)
        {
            try
            {
                var venta = new Venta
                {
                    Fecha = DateTime.Now,
                    Total = items.Sum(i => i.Total),
                    Estado = (int)EstadoVenta.Finalizada,
                    TipoPago = (int)tipoPago, // NUEVO: Asignar tipo de pago
                    DetallesVenta = new List<DetalleVenta>()
                };

                foreach (var item in items)
                {
                    await ProcesarItemParaVenta(venta, item);
                }

                await _ventaService.CreateVentaAsync(venta);
                await _context.SaveChangesAsync();

                return venta;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar la venta: {ex.Message}\n\nDetalles: {ex.InnerException?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Recupera una venta pendiente y calcula excedente si aplica
        /// </summary>
        public async Task<(Venta? venta, decimal excedente, int minutosExtra)?> RecuperarVentaPendienteAsync(string idNfc)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Sin venta pendiente", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }

                decimal excedente = 0;
                int minutosExtra = 0;

                // Calcular excedente si hay tiempo incluido
                if (ventaPendiente.HoraEntrada.HasValue && ventaPendiente.MinutosTiempoCombo.HasValue)
                {
                    var tiempoTranscurrido = (DateTime.Now - ventaPendiente.HoraEntrada.Value).TotalMinutes;
                    var minutosIncluidos = ventaPendiente.MinutosTiempoCombo.Value;

                    if (tiempoTranscurrido > minutosIncluidos)
                    {
                        var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

                        if (precios != null && precios.Count > 0)
                        {
                            var ultimoTramo = precios.Last();
                            decimal precioPorMinuto;

                            if (precios.Count > 1)
                            {
                                var penultimoTramo = precios[precios.Count - 2];
                                int minutosExcedente = ultimoTramo.Minutos - penultimoTramo.Minutos;
                                decimal precioExcedente = ultimoTramo.Precio - penultimoTramo.Precio;
                                precioPorMinuto = precioExcedente / minutosExcedente;
                            }
                            else
                            {
                                precioPorMinuto = ultimoTramo.Precio / ultimoTramo.Minutos;
                            }

                            minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
                            excedente = minutosExtra * precioPorMinuto;
                        }
                    }
                }

                return (ventaPendiente, excedente, minutosExtra);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recuperar venta pendiente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Actualiza una venta pendiente recuperada (agrega nuevos items)
        /// </summary>
        public async Task<bool> ActualizarVentaPendienteAsync(Venta ventaPendiente, List<ItemCarrito> itemsActuales)
        {
            try
            {
                var idsDetallesOriginales = ventaPendiente.DetallesVenta
                    .Select(d => d.ProductoId ?? 0)
                    .ToHashSet();

                foreach (var item in itemsActuales)
                {
                    bool esItemNuevo = VerificarSiEsNuevoItem(ventaPendiente, item);

                    if (esItemNuevo)
                    {
                        await AgregarNuevoDetalleAVenta(ventaPendiente, item);
                    }
                    else
                    {
                        ActualizarDetalleExistente(ventaPendiente, item);
                    }
                }

                decimal totalFinal = itemsActuales.Sum(i => i.Total);
                ventaPendiente.Total = totalFinal;
                ventaPendiente.Estado = (int)EstadoVenta.Finalizada;

                _context.Entry(ventaPendiente).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al finalizar venta: {ex.Message}\n\nDetalles: {ex.InnerException?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Elimina una venta pendiente
        /// </summary>
        public async Task<bool> EliminarVentaPendienteAsync(int ventaId)
        {
            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                    .FirstOrDefaultAsync(v => v.Id == ventaId && v.Estado == (int)EstadoVenta.Pendiente);

                if (venta == null)
                {
                    throw new InvalidOperationException("No se encontró la venta pendiente");
                }

                _context.DetallesVenta.RemoveRange(venta.DetallesVenta);
                _context.Ventas.Remove(venta);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Métodos privados auxiliares

        private async Task ProcesarItemParaVenta(Venta venta, ItemCarrito item)
        {
            // Tarjeta extraviada
            if (item.ProductoId == -998)
            {
                venta.DetallesVenta.Add(new DetalleVenta
                {
                    TipoItem = (int)TipoItemVenta.Producto,
                    ProductoId = null,
                    ItemReferenciaId = null,
                    NombreItem = "Tarjeta extraviada/dañada",
                    Cantidad = 1,
                    PrecioUnitario = 50,
                    Subtotal = 50
                });
                return;
            }

            // Excedente de tiempo
            if (item.ProductoId == -999)
            {
                venta.DetallesVenta.Add(new DetalleVenta
                {
                    TipoItem = (int)TipoItemVenta.Tiempo,
                    ProductoId = null,
                    ItemReferenciaId = null,
                    NombreItem = item.Nombre,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.PrecioUnitario,
                    Subtotal = item.Total
                });
                return;
            }

            // Producto individual
            if (item.ProductoId > 0)
            {
                await ProcesarProducto(venta, item);
                return;
            }

            // Combo
            if (item.ProductoId < 0)
            {
                await ProcesarCombo(venta, item);
            }
        }

        private async Task ProcesarProducto(Venta venta, ItemCarrito item)
        {
            var producto = await _context.Productos.FindAsync(item.ProductoId);
            if (producto == null)
            {
                throw new InvalidOperationException($"Producto con ID {item.ProductoId} no encontrado");
            }

            if (producto.Stock < item.Cantidad)
            {
                throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre}");
            }

            venta.DetallesVenta.Add(new DetalleVenta
            {
                TipoItem = (int)TipoItemVenta.Producto,
                ProductoId = producto.Id,
                ItemReferenciaId = null,
                NombreItem = producto.Nombre,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                Subtotal = item.Total
            });

            producto.Stock -= item.Cantidad;
            _context.Entry(producto).State = EntityState.Modified;
        }

        private async Task ProcesarCombo(Venta venta, ItemCarrito item)
        {
            int comboId = -item.ProductoId;
            var combo = await _context.Combos
                .Include(c => c.ComboProductos)
                .ThenInclude(cp => cp.Producto)
                .FirstOrDefaultAsync(c => c.Id == comboId);

            if (combo == null)
            {
                throw new InvalidOperationException($"Combo con ID {comboId} no encontrado");
            }

            // Validar y descontar stock
            foreach (var comboProducto in combo.ComboProductos)
            {
                var producto = comboProducto.Producto;
                if (producto != null)
                {
                    int cantidadTotal = comboProducto.Cantidad * item.Cantidad;

                    if (producto.Stock < cantidadTotal)
                    {
                        throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre} en el combo");
                    }

                    producto.Stock -= cantidadTotal;
                    _context.Entry(producto).State = EntityState.Modified;
                }
            }

            venta.DetallesVenta.Add(new DetalleVenta
            {
                TipoItem = (int)TipoItemVenta.Combo,
                ProductoId = null,
                ItemReferenciaId = combo.Id,
                NombreItem = combo.Nombre,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                Subtotal = item.Total
            });
        }

        private bool VerificarSiEsNuevoItem(Venta venta, ItemCarrito item)
        {
            if (item.ProductoId == -999 || item.ProductoId == -998)
            {
                return true;
            }

            if (item.ProductoId > 0)
            {
                var detalleExistente = venta.DetallesVenta
                    .FirstOrDefault(d => d.ProductoId == item.ProductoId && d.TipoItem == (int)TipoItemVenta.Producto);
                return detalleExistente == null;
            }

            if (item.ProductoId < 0)
            {
                int comboId = -item.ProductoId;
                var detalleExistente = venta.DetallesVenta
                    .FirstOrDefault(d => d.ItemReferenciaId == comboId && d.TipoItem == (int)TipoItemVenta.Combo);
                return detalleExistente == null;
            }

            return false;
        }

        private void ActualizarDetalleExistente(Venta venta, ItemCarrito item)
        {
            DetalleVenta? detalle = null;

            if (item.ProductoId > 0)
            {
                detalle = venta.DetallesVenta
                    .FirstOrDefault(d => d.ProductoId == item.ProductoId && d.TipoItem == (int)TipoItemVenta.Producto);
            }
            else if (item.ProductoId < 0)
            {
                int comboId = -item.ProductoId;
                detalle = venta.DetallesVenta
                    .FirstOrDefault(d => d.ItemReferenciaId == comboId && d.TipoItem == (int)TipoItemVenta.Combo);
            }

            if (detalle != null && detalle.Cantidad != item.Cantidad)
            {
                detalle.Cantidad = item.Cantidad;
                detalle.Subtotal = item.Total;
                _context.Entry(detalle).State = EntityState.Modified;
            }
        }

        private async Task AgregarNuevoDetalleAVenta(Venta venta, ItemCarrito item)
        {
            var detalle = await CrearDetalleDesdeItem(venta.Id, item);
            venta.DetallesVenta.Add(detalle);
            _context.DetallesVenta.Add(detalle);
        }

        private async Task<DetalleVenta> CrearDetalleDesdeItem(int ventaId, ItemCarrito item)
        {
            if (item.ProductoId == -999)
            {
                return new DetalleVenta
                {
                    VentaId = ventaId,
                    TipoItem = (int)TipoItemVenta.Tiempo,
                    ProductoId = null,
                    ItemReferenciaId = null,
                    NombreItem = item.Nombre,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.PrecioUnitario,
                    Subtotal = item.Total
                };
            }

            if (item.ProductoId == -998)
            {
                return new DetalleVenta
                {
                    VentaId = ventaId,
                    TipoItem = (int)TipoItemVenta.Producto,
                    ProductoId = null,
                    ItemReferenciaId = null,
                    NombreItem = "Tarjeta extraviada/dañada",
                    Cantidad = 1,
                    PrecioUnitario = 50,
                    Subtotal = 50
                };
            }

            if (item.ProductoId > 0)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);
                if (producto == null)
                {
                    throw new InvalidOperationException($"Producto con ID {item.ProductoId} no encontrado");
                }

                if (producto.Stock < item.Cantidad)
                {
                    throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre}");
                }

                producto.Stock -= item.Cantidad;
                _context.Entry(producto).State = EntityState.Modified;

                return new DetalleVenta
                {
                    VentaId = ventaId,
                    TipoItem = (int)TipoItemVenta.Producto,
                    ProductoId = producto.Id,
                    ItemReferenciaId = null,
                    NombreItem = producto.Nombre,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.PrecioUnitario,
                    Subtotal = item.Total
                };
            }

            // Es un combo
            int comboId = -item.ProductoId;
            var combo = await _context.Combos
                .Include(c => c.ComboProductos)
                .ThenInclude(cp => cp.Producto)
                .FirstOrDefaultAsync(c => c.Id == comboId);

            if (combo == null)
            {
                throw new InvalidOperationException($"Combo con ID {comboId} no encontrado");
            }

            foreach (var comboProducto in combo.ComboProductos)
            {
                var producto = comboProducto.Producto;
                if (producto != null)
                {
                    int cantidadTotal = comboProducto.Cantidad * item.Cantidad;

                    if (producto.Stock < cantidadTotal)
                    {
                        throw new InvalidOperationException($"Stock insuficiente para {producto.Nombre} en el combo");
                    }

                    producto.Stock -= cantidadTotal;
                    _context.Entry(producto).State = EntityState.Modified;
                }
            }

            return new DetalleVenta
            {
                VentaId = ventaId,
                TipoItem = (int)TipoItemVenta.Combo,
                ProductoId = null,
                ItemReferenciaId = combo.Id,
                NombreItem = combo.Nombre,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                Subtotal = item.Total
            };
        }
    }
}