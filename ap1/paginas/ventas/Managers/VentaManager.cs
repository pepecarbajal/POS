using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using POS.Data;
using POS.Models;
using POS.Services;
using POS.Helpers;
using Microsoft.EntityFrameworkCore;
using POS.paginas.ventas;

namespace POS.paginas.ventas.Managers
{
    /// <summary>
    /// Manager para gestionar operaciones de ventas
    /// Incluye funcionalidad de tickets de pedido agrupados por categoría
    /// </summary>
    public class VentaManager
    {
        private readonly AppDbContext _context;
        private readonly VentaService _ventaService;
        private readonly PrecioTiempoService _precioTiempoService;
        private TicketPedidoPdfGenerator? _ticketPedidoGenerator;

        public VentaManager(
            AppDbContext context,
            VentaService ventaService,
            PrecioTiempoService precioTiempoService)
        {
            _context = context;
            _ventaService = ventaService;
            _precioTiempoService = precioTiempoService;
        }

        #region Ventas Pendientes (Combos con Tiempo)

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
                    TipoPago = (int)tipoPago,
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

        #endregion

        #region Ventas Finalizadas Normales

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
                    TipoPago = (int)tipoPago,
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

        #endregion

        #region Recuperación de Ventas

        /// <summary>
        /// Recupera una venta pendiente y calcula excedente si aplica
        /// </summary>
        public async Task<List<ItemCarrito>?> RecuperarVentaPendienteAsync(string idNfc)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }

                var itemsRecuperados = new List<ItemCarrito>();

                // Calcular excedente si aplica
                if (ventaPendiente.HoraEntrada.HasValue)
                {
                    var tiempoTranscurrido = (DateTime.Now - ventaPendiente.HoraEntrada.Value).TotalMinutes;
                    var minutosIncluidos = ventaPendiente.MinutosTiempoCombo ?? 0;

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

                            int minutosExtra = (int)Math.Ceiling(tiempoTranscurrido - minutosIncluidos);
                            decimal excedente = minutosExtra * precioPorMinuto;

                            if (excedente > 0)
                            {
                                itemsRecuperados.Add(new ItemCarrito
                                {
                                    ProductoId = -999,
                                    Nombre = $"Excedente de tiempo ({minutosExtra} min extra)",
                                    PrecioUnitario = excedente,
                                    Cantidad = 1,
                                    Total = excedente
                                });
                            }
                        }
                    }
                }

                // Cargar detalles de venta
                var detalles = await _context.DetallesVenta
                    .Where(d => d.VentaId == ventaPendiente.Id)
                    .Include(d => d.Producto)
                    .ToListAsync();

                foreach (var detalle in detalles)
                {
                    itemsRecuperados.Add(new ItemCarrito
                    {
                        ProductoId = detalle.TipoItem == (int)TipoItemVenta.Producto
                            ? detalle.ProductoId ?? 0
                            : -detalle.ItemReferenciaId ?? 0,
                        Nombre = detalle.NombreParaMostrar,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Cantidad = detalle.Cantidad,
                        Total = detalle.Subtotal
                    });
                }

                // Agregar cargo por tarjeta perdida
                itemsRecuperados.Add(new ItemCarrito
                {
                    ProductoId = -998,
                    Nombre = "Tarjeta extraviada/dañada",
                    PrecioUnitario = 50,
                    Cantidad = 1,
                    Total = 50
                });

                return itemsRecuperados;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recuperar venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Cancela una venta pendiente sin cobrar
        /// </summary>
        public async Task<bool> CancelarVentaPendienteAsync(string idNfc)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Restaurar stock de productos
                var detalles = await _context.DetallesVenta
                    .Include(d => d.Producto)
                    .Where(d => d.VentaId == ventaPendiente.Id)
                    .ToListAsync();

                foreach (var detalle in detalles)
                {
                    if (detalle.TipoItem == (int)TipoItemVenta.Producto && detalle.Producto != null)
                    {
                        detalle.Producto.Stock += detalle.Cantidad;
                        _context.Entry(detalle.Producto).State = EntityState.Modified;
                    }
                    else if (detalle.TipoItem == (int)TipoItemVenta.Combo && detalle.ItemReferenciaId.HasValue)
                    {
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
                                    int cantidadTotal = comboProducto.Cantidad * detalle.Cantidad;
                                    comboProducto.Producto.Stock += cantidadTotal;
                                    _context.Entry(comboProducto.Producto).State = EntityState.Modified;
                                }
                            }
                        }
                    }
                }

                // Eliminar la venta
                _context.DetallesVenta.RemoveRange(detalles);
                _context.Ventas.Remove(ventaPendiente);

                await _context.SaveChangesAsync();

                MessageBox.Show("Venta pendiente cancelada correctamente. El stock ha sido restaurado.",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cancelar venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region Actualización de Ventas Pendientes

        /// <summary>
        /// Actualiza una venta pendiente con items adicionales
        /// </summary>
        public async Task<bool> ActualizarVentaPendienteAsync(string idNfc, List<ItemCarrito> nuevosItems)
        {
            try
            {
                var ventaPendiente = await _ventaService.GetVentaPendienteByIdNfcAsync(idNfc);

                if (ventaPendiente == null)
                {
                    MessageBox.Show($"No se encontró una venta pendiente para la tarjeta {idNfc}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Cargar detalles actuales
                await _context.Entry(ventaPendiente)
                    .Collection(v => v.DetallesVenta)
                    .LoadAsync();

                // Procesar cada item
                foreach (var item in nuevosItems)
                {
                    if (VerificarSiEsNuevoItem(ventaPendiente, item))
                    {
                        await AgregarNuevoDetalleAVenta(ventaPendiente, item);
                    }
                    else
                    {
                        ActualizarDetalleExistente(ventaPendiente, item);
                    }
                }

                // Recalcular total
                ventaPendiente.Total = ventaPendiente.DetallesVenta.Sum(d => d.Subtotal);

                _context.Entry(ventaPendiente).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                MessageBox.Show("Venta actualizada correctamente",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region Tickets de Pedido (NUEVO)

        /// <summary>
        /// Genera un ticket de pedido para la cocina/barra
        /// Este ticket agrupa los productos por categoría para facilitar la preparación
        /// </summary>
        /// <param name="venta">Venta registrada</param>
        /// <param name="itemsCarrito">Items del carrito</param>
        /// <param name="numeroMesa">Número de mesa o identificador</param>
        /// <param name="nombreMesero">Nombre del mesero o cajero</param>
        /// <param name="generarAutomaticamente">Si es true, genera y abre el ticket automáticamente</param>
        /// <returns>Ruta del archivo generado o null si falló</returns>
        public async Task<string?> GenerarTicketPedidoAsync(
            Venta venta,
            List<ItemCarrito> itemsCarrito,
            string numeroMesa = "01",
            string nombreMesero = "Cajero",
            bool generarAutomaticamente = true)
        {
            try
            {
                // Inicializar el generador si no existe
                if (_ticketPedidoGenerator == null)
                {
                    _ticketPedidoGenerator = new TicketPedidoPdfGenerator(_context);
                }

                // Filtrar items que deben aparecer en el ticket de pedido
                // (Excluir items especiales como tarjeta perdida o excedentes)
                var itemsParaTicket = itemsCarrito
                    .Where(i => i.ProductoId != -998 && i.ProductoId != -999)
                    .ToList();

                if (!itemsParaTicket.Any())
                {
                    // No hay items para mostrar en el ticket de pedido
                    return null;
                }

                // Generar el PDF
                byte[] pdfBytes = await _ticketPedidoGenerator.GenerarTicketPedidoAsync(
                    venta: venta,
                    items: itemsParaTicket,
                    nombreMesero: nombreMesero,
                    numeroMesa: numeroMesa,
                    anchoMm: 80  // Puedes hacer esto configurable
                );

                if (generarAutomaticamente)
                {
                    // Guardar y abrir automáticamente
                    string rutaArchivo = TicketPedidoPdfGenerator.GuardarYAbrirTicketPedido(
                        pdfBytes,
                        venta.Id,
                        numeroMesa
                    );

                    return rutaArchivo;
                }
                else
                {
                    // Solo guardar sin abrir
                    var nombreArchivo = $"Pedido_Mesa{numeroMesa}_{venta.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    TicketPedidoPdfGenerator.GuardarTicketPedido(pdfBytes, nombreArchivo);

                    var carpetaTickets = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "POS",
                        "tickets_pedidos"
                    );

                    return System.IO.Path.Combine(carpetaTickets, nombreArchivo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar ticket de pedido: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Log del error (puedes implementar un logger aquí)
                System.Diagnostics.Debug.WriteLine($"Error generando ticket de pedido: {ex}");

                return null;
            }
        }

        /// <summary>
        /// Regenera un ticket de pedido para una venta existente
        /// Útil si se perdió el ticket original
        /// </summary>
        public async Task<string?> RegenerarTicketPedidoAsync(
            int ventaId,
            string numeroMesa = "01",
            string nombreMesero = "Cajero")
        {
            try
            {
                // Buscar la venta en la BD
                var venta = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                {
                    MessageBox.Show("No se encontró la venta especificada.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // Convertir DetalleVenta a ItemCarrito
                var itemsCarrito = venta.DetallesVenta
                    .Where(d => d.TipoItem != (int)TipoItemVenta.Tiempo) // Excluir tiempos
                    .Select(d => new ItemCarrito
                    {
                        ProductoId = d.ProductoId ?? (d.TipoItem == (int)TipoItemVenta.Combo ? -d.ItemReferenciaId ?? 0 : 0),
                        Nombre = d.NombreParaMostrar,
                        PrecioUnitario = d.PrecioUnitario,
                        Cantidad = d.Cantidad,
                        Total = d.Subtotal
                    })
                    .ToList();

                // Generar el ticket
                return await GenerarTicketPedidoAsync(
                    venta: venta,
                    itemsCarrito: itemsCarrito,
                    numeroMesa: numeroMesa,
                    nombreMesero: nombreMesero,
                    generarAutomaticamente: true
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al regenerar ticket de pedido: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        #endregion

        #region Métodos Privados de Procesamiento

        private async Task ProcesarItemParaVenta(Venta venta, ItemCarrito item)
        {
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
            }
            else if (item.ProductoId == -998)
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
            }
            else if (item.ProductoId > 0)
            {
                await ProcesarProducto(venta, item);
            }
            else
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

            producto.Stock -= item.Cantidad;

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

        #endregion
    }
}