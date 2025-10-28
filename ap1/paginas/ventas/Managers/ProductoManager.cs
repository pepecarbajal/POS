using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using POS.Data;
using POS.Models;
using Microsoft.EntityFrameworkCore;
using POS.paginas.ventas;

namespace POS.paginas.ventas.Managers
{
    /// <summary>
    /// Manager para gestionar la visualización y carga de productos/combos
    /// </summary>
    public class ProductoManager
    {
        private readonly AppDbContext _context;

        // Colecciones observables
        public ObservableCollection<ProductoVenta> ProductosVisibles { get; }
        private readonly ObservableCollection<ProductoVenta> _todosLosProductos;

        // Estados de visualización
        private bool _mostrandoCombos;
        private bool _mostrandoTiempo;

        public bool MostrandoCombos => _mostrandoCombos;
        public bool MostrandoTiempo => _mostrandoTiempo;

        public ProductoManager(AppDbContext context)
        {
            _context = context;
            ProductosVisibles = new ObservableCollection<ProductoVenta>();
            _todosLosProductos = new ObservableCollection<ProductoVenta>();
        }

        /// <summary>
        /// Carga todos los productos activos con stock
        /// </summary>
        public async Task CargarProductosAsync(int? categoriaId = null)
        {
            try
            {
                _mostrandoCombos = false;
                _mostrandoTiempo = false;

                var todosQuery = _context.Productos
                    .Where(p => p.Estado == "Activo" && p.Stock > 0);

                var todosProductosDb = await todosQuery.AsNoTracking().ToListAsync();

                _todosLosProductos.Clear();
                foreach (var producto in todosProductosDb)
                {
                    _todosLosProductos.Add(MapearProductoAVenta(producto));
                }

                ProductosVisibles.Clear();
                var productosAMostrar = _todosLosProductos.AsEnumerable();

                if (categoriaId.HasValue && categoriaId.Value > 0)
                {
                    var productosEnCategoria = await _context.Productos
                        .Where(p => p.CategoriaId == categoriaId.Value && p.Estado == "Activo" && p.Stock > 0)
                        .Select(p => p.Id)
                        .ToListAsync();

                    productosAMostrar = _todosLosProductos.Where(p => productosEnCategoria.Contains(p.Id));
                }

                foreach (var producto in productosAMostrar)
                {
                    ProductosVisibles.Add(producto);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar productos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Carga todos los combos activos
        /// </summary>
        public async Task CargarCombosAsync()
        {
            try
            {
                _mostrandoCombos = true;
                _mostrandoTiempo = false;

                var combosDb = await _context.Combos
                    .Include(c => c.ComboProductos)
                    .ThenInclude(cp => cp.Producto)
                    .Include(c => c.PrecioTiempo)
                    .Where(c => c.Estado == "Activo")
                    .AsNoTracking()
                    .ToListAsync();

                _todosLosProductos.Clear();
                ProductosVisibles.Clear();

                foreach (var combo in combosDb)
                {
                    var productoVenta = MapearComboAVenta(combo);
                    _todosLosProductos.Add(productoVenta);
                    ProductosVisibles.Add(productoVenta);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar combos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Filtra productos por texto de búsqueda
        /// </summary>
        public void FiltrarProductos(string searchText, int? categoriaId = null)
        {
            ProductosVisibles.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                if (categoriaId.HasValue && categoriaId.Value > 0)
                {
                    // Filtrar por categoría (necesitaría cargar productos con categoría)
                    foreach (var producto in _todosLosProductos)
                    {
                        ProductosVisibles.Add(producto);
                    }
                }
                else
                {
                    foreach (var producto in _todosLosProductos)
                    {
                        ProductosVisibles.Add(producto);
                    }
                }
            }
            else
            {
                var productosFiltrados = _todosLosProductos
                    .Where(p => p.Nombre.ToLower().Contains(searchText.ToLower()))
                    .ToList();

                foreach (var producto in productosFiltrados)
                {
                    ProductosVisibles.Add(producto);
                }
            }
        }

        /// <summary>
        /// Establece el modo de visualización de tiempo
        /// </summary>
        public void MostrarPanelTiempo()
        {
            _mostrandoCombos = false;
            _mostrandoTiempo = true;
        }

        // Métodos privados de mapeo

        private ProductoVenta MapearProductoAVenta(Producto producto)
        {
            string? imagenUrl = null;
            if (!string.IsNullOrEmpty(producto.UrlImage))
            {
                string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", producto.UrlImage);

                if (File.Exists(imagePath))
                {
                    imagenUrl = imagePath;
                }
                else if (Uri.TryCreate(producto.UrlImage, UriKind.Absolute, out Uri? uri))
                {
                    imagenUrl = producto.UrlImage;
                }
            }

            return new ProductoVenta
            {
                Id = producto.Id,
                Nombre = producto.Nombre,
                Precio = producto.Precio,
                Stock = producto.Stock,
                ImagenUrl = imagenUrl,
                EsCombo = false,
                ComboId = 0,
                TieneTiempo = false,
                PrecioTiempoId = null,
                MinutosTiempo = 0
            };
        }

        private ProductoVenta MapearComboAVenta(Combo combo)
        {
            string? imagenUrl = null;
            if (!string.IsNullOrEmpty(combo.UrlImage))
            {
                string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", combo.UrlImage);

                if (File.Exists(imagePath))
                {
                    imagenUrl = imagePath;
                }
                else if (Uri.TryCreate(combo.UrlImage, UriKind.Absolute, out Uri? uri))
                {
                    imagenUrl = combo.UrlImage;
                }
            }

            // Calcular stock disponible basado en productos del combo
            int stockDisponible = int.MaxValue;
            if (combo.ComboProductos.Any())
            {
                foreach (var comboProducto in combo.ComboProductos)
                {
                    if (comboProducto.Producto != null)
                    {
                        int stockPosible = comboProducto.Producto.Stock / comboProducto.Cantidad;
                        stockDisponible = Math.Min(stockDisponible, stockPosible);
                    }
                }
            }
            else
            {
                stockDisponible = 0;
            }

            var nombreCompleto = combo.Nombre;
            if (combo.PrecioTiempo != null)
            {
                nombreCompleto += $" + {combo.PrecioTiempo.Nombre}";
            }

            return new ProductoVenta
            {
                Id = combo.Id,
                Nombre = nombreCompleto,
                Precio = combo.Precio,
                Stock = stockDisponible,
                ImagenUrl = imagenUrl,
                EsCombo = true,
                ComboId = combo.Id,
                TieneTiempo = combo.PrecioTiempoId.HasValue,
                PrecioTiempoId = combo.PrecioTiempoId,
                MinutosTiempo = combo.PrecioTiempo?.Minutos ?? 0
            };
        }
    }
}