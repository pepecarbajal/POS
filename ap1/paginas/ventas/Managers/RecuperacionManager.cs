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
    /// Manager para gestionar recuperación de ventas/tiempos cancelados
    /// </summary>
    public class RecuperacionManager
    {
        private readonly AppDbContext _context;
        private readonly TiempoService _tiempoService;
        private readonly VentaService _ventaService;
        private readonly PrecioTiempoService _precioTiempoService;

        public RecuperacionManager(
            AppDbContext context,
            TiempoService tiempoService,
            VentaService ventaService,
            PrecioTiempoService precioTiempoService)
        {
            _context = context;
            _tiempoService = tiempoService;
            _ventaService = ventaService;
            _precioTiempoService = precioTiempoService;
        }

        /// <summary>
        /// Mueve una venta pendiente al carrito (recuperación normal - SIN cargo de tarjeta)
        /// </summary>
        public async Task<VentaTiempoRecuperable?> MoverVentaPendienteACarritoAsync(TiempoActivo tiempo)
        {
            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                        .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == tiempo.Id && v.Estado == (int)EstadoVenta.Pendiente);

                if (venta == null)
                {
                    throw new InvalidOperationException("No se encontró la venta pendiente");
                }

                var recuperable = new VentaTiempoRecuperable
                {
                    Id = venta.Id,
                    EsCombo = true,
                    IdNfc = venta.IdNfc ?? "",
                    HoraEntrada = venta.HoraEntrada ?? DateTime.Now,
                    MinutosTiempo = venta.MinutosTiempoCombo ?? 0,
                    Items = new List<ItemCarrito>()
                };

                // Calcular excedente si aplica
                if (venta.HoraEntrada.HasValue)
                {
                    var tiempoTranscurrido = (DateTime.Now - venta.HoraEntrada.Value).TotalMinutes;
                    var minutosIncluidos = venta.MinutosTiempoCombo ?? 0;

                    if (tiempoTranscurrido > minutosIncluidos)
                    {
                        var (excedente, minutosExtra) = await CalcularExcedenteAsync(tiempoTranscurrido, minutosIncluidos);

                        if (excedente > 0)
                        {
                            var itemExcedente = new ItemCarrito
                            {
                                ProductoId = -999,
                                Nombre = $"Excedente de tiempo ({minutosExtra} min extra)",
                                PrecioUnitario = excedente,
                                Cantidad = 1,
                                Total = excedente
                            };

                            recuperable.Items.Add(itemExcedente);
                        }
                    }
                }

                // Agregar detalles de la venta
                foreach (var detalle in venta.DetallesVenta)
                {
                    var itemCarrito = new ItemCarrito
                    {
                        ProductoId = detalle.TipoItem == (int)TipoItemVenta.Producto
                            ? detalle.ProductoId ?? 0
                            : -detalle.ItemReferenciaId ?? 0,
                        Nombre = detalle.NombreParaMostrar,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Cantidad = detalle.Cantidad,
                        Total = detalle.Subtotal
                    };

                    recuperable.Items.Add(itemCarrito);
                }

                // NO SE AGREGA CARGO DE TARJETA EXTRAVIADA EN RECUPERACIÓN NORMAL
                // Solo se agrega cuando se cancela específicamente por tarjeta perdida

                return recuperable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al mover venta al carrito: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Mueve una venta pendiente al carrito CON CARGO de tarjeta extraviada
        /// (Solo se usa cuando explícitamente se cancela por tarjeta perdida)
        /// </summary>
        public async Task<VentaTiempoRecuperable?> MoverVentaPendienteConCargoTarjetaAsync(TiempoActivo tiempo)
        {
            try
            {
                // Primero obtener la venta normal
                var recuperable = await MoverVentaPendienteACarritoAsync(tiempo);

                if (recuperable == null)
                {
                    return null;
                }

                // Agregar cargo de tarjeta perdida
                var itemTarjetaPerdida = new ItemCarrito
                {
                    ProductoId = -998,
                    Nombre = "Tarjeta extraviada/dañada",
                    PrecioUnitario = 50,
                    Cantidad = 1,
                    Total = 50
                };

                recuperable.Items.Add(itemTarjetaPerdida);

                return recuperable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al mover venta con cargo de tarjeta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Mueve un tiempo individual al carrito (cuando se cancela por tarjeta perdida)
        /// </summary>
        public async Task<VentaTiempoRecuperable?> MoverTiempoACarritoAsync(TiempoActivo tiempo)
        {
            try
            {
                var tiempoDb = await _context.Tiempos.FindAsync(tiempo.Id);

                if (tiempoDb == null || tiempoDb.Estado != "Activo")
                {
                    throw new InvalidOperationException("No se encontró el tiempo activo");
                }

                // Finalizar el tiempo
                var tiempoFinalizado = await _tiempoService.RegistrarSalidaAsync(tiempoDb.Id);

                var tiempoTranscurrido = (tiempoFinalizado.HoraSalida!.Value - tiempoFinalizado.HoraEntrada).TotalMinutes;

                var itemCarrito = new ItemCarrito
                {
                    ProductoId = -tiempoFinalizado.Id,
                    Nombre = $"Tiempo - ({Math.Ceiling(tiempoTranscurrido)} min)",
                    PrecioUnitario = tiempoFinalizado.Total,
                    Cantidad = 1,
                    Total = tiempoFinalizado.Total
                };

                var itemTarjetaPerdida = new ItemCarrito
                {
                    ProductoId = -998,
                    Nombre = "Tarjeta extraviada/dañada",
                    PrecioUnitario = 50,
                    Cantidad = 1,
                    Total = 50
                };

                var recuperable = new VentaTiempoRecuperable
                {
                    Id = tiempoFinalizado.Id,
                    EsCombo = false,
                    IdNfc = tiempoFinalizado.IdNfc,
                    HoraEntrada = tiempoFinalizado.HoraEntrada,
                    Items = new List<ItemCarrito> { itemCarrito, itemTarjetaPerdida }
                };

                return recuperable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al mover tiempo al carrito: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Restaura un item recuperable (revertir cancelación)
        /// </summary>
        public async Task<bool> RestaurarItemRecuperableAsync(VentaTiempoRecuperable recuperable)
        {
            try
            {
                if (recuperable.EsCombo)
                {
                    // Es una venta pendiente con combo
                    var venta = await _context.Ventas
                        .Include(v => v.DetallesVenta)
                        .FirstOrDefaultAsync(v => v.Id == recuperable.Id);

                    if (venta != null)
                    {
                        // Eliminar el detalle de tarjeta extraviada si existe
                        var detalleTarjeta = venta.DetallesVenta
                            .FirstOrDefault(d => d.NombreItem == "Tarjeta extraviada/dañada");

                        if (detalleTarjeta != null)
                        {
                            _context.DetallesVenta.Remove(detalleTarjeta);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    // Es un tiempo individual - reactivarlo
                    var tiempo = await _context.Tiempos.FindAsync(recuperable.Id);

                    if (tiempo != null && tiempo.Estado == "Finalizado")
                    {
                        tiempo.Estado = "Activo";
                        tiempo.HoraSalida = null;
                        tiempo.Total = 0;

                        _context.Entry(tiempo).State = EntityState.Modified;
                        await _context.SaveChangesAsync();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al restaurar item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Métodos privados auxiliares

        private async Task<(decimal excedente, int minutosExtra)> CalcularExcedenteAsync(double tiempoTranscurrido, int minutosIncluidos)
        {
            var precios = await _precioTiempoService.GetPreciosTiempoActivosAsync();

            if (precios == null || !precios.Any())
            {
                return (0, 0);
            }

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

            return (excedente, minutosExtra);
        }
    }
}