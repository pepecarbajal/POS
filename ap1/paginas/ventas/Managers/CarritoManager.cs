using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Manager para gestionar todo lo relacionado con el carrito de compras
    /// </summary>
    public class CarritoManager
    {
        private readonly AppDbContext _context;
        private readonly ComboService _comboService;

        // Referencias al carrito compartido
        public ObservableCollection<ItemCarrito> Items => CarritoService.Instance.Items;

        // Control de combos con tiempo
        private bool _carritoTieneComboConTiempo;
        private int? _comboConTiempoId;
        private int _minutosComboTiempo;

        public bool TieneComboConTiempo => _carritoTieneComboConTiempo;
        public int? ComboConTiempoId => _comboConTiempoId;
        public int MinutosComboTiempo => _minutosComboTiempo;

        // Items recuperables (para cancelación)
        private readonly List<VentaTiempoRecuperable> _itemsRecuperables = new List<VentaTiempoRecuperable>();

        public CarritoManager(AppDbContext context, ComboService comboService)
        {
            _context = context;
            _comboService = comboService;
        }

        /// <summary>
        /// Agrega un producto individual al carrito
        /// </summary>
        public async Task<bool> AgregarProductoAsync(ProductoVenta producto)
        {
            // Validar si hay tiempos individuales en el carrito
            if (HayTiemposIndividuales())
            {
                MessageBox.Show("No puedes agregar productos mientras haya tiempos individuales en el carrito.\n\nPuede finalizar los tiempos primero o eliminarlos del carrito.",
                    "Restricción", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var itemExistente = Items.FirstOrDefault(i => i.ProductoId == producto.Id);

            if (itemExistente != null)
            {
                if (itemExistente.Cantidad < producto.Stock)
                {
                    itemExistente.Cantidad++;
                    itemExistente.Total = itemExistente.Cantidad * itemExistente.PrecioUnitario;
                    return true;
                }
                else
                {
                    MessageBox.Show($"Stock insuficiente para {producto.Nombre}", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            Items.Add(new ItemCarrito
            {
                ProductoId = producto.Id,
                Nombre = producto.Nombre,
                PrecioUnitario = producto.Precio,
                Cantidad = 1,
                Total = producto.Precio
            });

            return true;
        }

        /// <summary>
        /// Agrega un combo al carrito
        /// </summary>
        public async Task<bool> AgregarComboAsync(int comboId)
        {
            try
            {
                var combo = await _context.Combos
                    .Include(c => c.ComboProductos)
                    .ThenInclude(cp => cp.Producto)
                    .Include(c => c.PrecioTiempo)
                    .FirstOrDefaultAsync(c => c.Id == comboId);

                if (combo == null)
                {
                    MessageBox.Show("No se encontró el combo", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Validar stock
                var productosSinStock = ValidarStockCombo(combo);
                if (productosSinStock.Any())
                {
                    MessageBox.Show($"No se puede agregar el combo. Productos sin stock suficiente:\n{string.Join("\n", productosSinStock)}",
                        "Stock Insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Si es combo con tiempo
                if (combo.PrecioTiempoId.HasValue)
                {
                    if (_carritoTieneComboConTiempo)
                    {
                        MessageBox.Show("Solo puedes tener un combo con tiempo en el carrito a la vez.",
                            "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    if (HayTiemposIndividuales())
                    {
                        MessageBox.Show("Para agregar un combo con tiempo, el carrito no puede tener tiempos individuales.\n\nPuedes tener productos y otros combos.",
                            "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    _carritoTieneComboConTiempo = true;
                    _comboConTiempoId = comboId;
                    _minutosComboTiempo = combo.PrecioTiempo?.Minutos ?? 0;
                }

                // Construir nombre completo
                var productosDescripcion = string.Join(", ", combo.ComboProductos
                    .Where(cp => cp.Producto != null)
                    .Select(cp => $"{cp.Producto!.Nombre} x{cp.Cantidad}"));

                var nombreCompleto = combo.Nombre;
                if (combo.PrecioTiempo != null)
                {
                    nombreCompleto += $" + {combo.PrecioTiempo.Nombre}";
                }
                nombreCompleto += $" ({productosDescripcion})";

                // Verificar si ya existe en el carrito
                var itemExistente = Items.FirstOrDefault(i => i.ProductoId == -comboId);

                if (itemExistente != null)
                {
                    if (combo.PrecioTiempoId.HasValue)
                    {
                        MessageBox.Show("Solo puedes tener una unidad de un combo con tiempo en el carrito.",
                            "Restricción", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    // Validar stock para cantidad adicional
                    if (ValidarStockParaCantidad(combo, itemExistente.Cantidad + 1))
                    {
                        itemExistente.Cantidad++;
                        itemExistente.Total = itemExistente.Cantidad * itemExistente.PrecioUnitario;
                    }
                    else
                    {
                        MessageBox.Show($"Stock insuficiente para agregar más unidades de {combo.Nombre}",
                            "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
                else
                {
                    Items.Add(new ItemCarrito
                    {
                        ProductoId = -comboId,
                        Nombre = nombreCompleto,
                        PrecioUnitario = combo.Precio,
                        Cantidad = 1,
                        Total = combo.Precio
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al agregar combo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Agrega un tiempo finalizado al carrito
        /// </summary>
        public void AgregarTiempo(int tiempoId, string nombreTiempo, decimal total)
        {
            Items.Add(new ItemCarrito
            {
                ProductoId = -tiempoId,
                Nombre = nombreTiempo,
                PrecioUnitario = total,
                Cantidad = 1,
                Total = total
            });
        }

        /// <summary>
        /// Elimina un item del carrito
        /// </summary>
        public async Task<bool> EliminarItemAsync(ItemCarrito item)
        {
            Items.Remove(item);

            // Si era el combo con tiempo, resetear flags
            if (_carritoTieneComboConTiempo && _comboConTiempoId.HasValue && item.ProductoId == -_comboConTiempoId.Value)
            {
                _carritoTieneComboConTiempo = false;
                _comboConTiempoId = null;
                _minutosComboTiempo = 0;
            }

            return true;
        }

        /// <summary>
        /// Vacía completamente el carrito
        /// </summary>
        public void Limpiar()
        {
            Items.Clear();
            _carritoTieneComboConTiempo = false;
            _comboConTiempoId = null;
            _minutosComboTiempo = 0;
            _itemsRecuperables.Clear();
        }

        /// <summary>
        /// Calcula el total del carrito
        /// </summary>
        public decimal ObtenerTotal()
        {
            return Items.Sum(i => i.Total);
        }

        /// <summary>
        /// Verifica si hay tiempos individuales en el carrito
        /// </summary>
        public bool HayTiemposIndividuales()
        {
            return Items.Any(i => i.ProductoId < 0 && i.Nombre.StartsWith("Tiempo") && i.ProductoId != -999);
        }

        /// <summary>
        /// Agrega item recuperable para posible cancelación
        /// </summary>
        public void AgregarItemRecuperable(VentaTiempoRecuperable recuperable)
        {
            _itemsRecuperables.Add(recuperable);
        }

        /// <summary>
        /// Obtiene un item recuperable por ID
        /// </summary>
        public VentaTiempoRecuperable? ObtenerItemRecuperable(int id)
        {
            return _itemsRecuperables.FirstOrDefault(r => r.Id == id);
        }

        /// <summary>
        /// Elimina un item recuperable
        /// </summary>
        public void EliminarItemRecuperable(VentaTiempoRecuperable recuperable)
        {
            _itemsRecuperables.Remove(recuperable);
        }

        /// <summary>
        /// Obtiene todos los items recuperables
        /// </summary>
        public List<VentaTiempoRecuperable> ObtenerItemsRecuperables()
        {
            return _itemsRecuperables.ToList();
        }

        // Métodos privados de validación

        private List<string> ValidarStockCombo(Combo combo)
        {
            var productosSinStock = new List<string>();

            foreach (var comboProducto in combo.ComboProductos)
            {
                var producto = comboProducto.Producto;
                if (producto != null)
                {
                    int cantidadRequerida = comboProducto.Cantidad;

                    if (producto.Stock < cantidadRequerida)
                    {
                        productosSinStock.Add($"- {producto.Nombre} (Stock: {producto.Stock}, Requerido: {cantidadRequerida})");
                    }
                }
            }

            return productosSinStock;
        }

        private bool ValidarStockParaCantidad(Combo combo, int cantidad)
        {
            foreach (var comboProducto in combo.ComboProductos)
            {
                var producto = comboProducto.Producto;
                if (producto != null)
                {
                    int cantidadRequerida = comboProducto.Cantidad * cantidad;
                    if (producto.Stock < cantidadRequerida)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}