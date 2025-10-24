using System;
using System.Collections.ObjectModel;
using System.Linq;
using POS.paginas.ventas;

namespace POS.Services
{
    public class CarritoService
    {
        private static CarritoService? _instance;
        private static readonly object _lock = new object();

        public ObservableCollection<ItemCarrito> Items { get; private set; }

        // Evento para notificar cambios en el carrito
        public event EventHandler? CarritoActualizado;

        private CarritoService()
        {
            Items = new ObservableCollection<ItemCarrito>();
            Items.CollectionChanged += (s, e) => CarritoActualizado?.Invoke(this, EventArgs.Empty);
        }

        public static CarritoService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CarritoService();
                        }
                    }
                }
                return _instance;
            }
        }

        public void AgregarItem(ItemCarrito item)
        {
            var itemExistente = Items.FirstOrDefault(i => i.ProductoId == item.ProductoId);

            if (itemExistente != null)
            {
                itemExistente.Cantidad += item.Cantidad;
                itemExistente.Total = itemExistente.Cantidad * itemExistente.PrecioUnitario;
            }
            else
            {
                Items.Add(item);
            }
        }

        public void ActualizarCantidad(int productoId, int nuevaCantidad)
        {
            var item = Items.FirstOrDefault(i => i.ProductoId == productoId);
            if (item != null)
            {
                item.Cantidad = nuevaCantidad;
                item.Total = item.Cantidad * item.PrecioUnitario;
            }
        }

        public void EliminarItem(ItemCarrito item)
        {
            Items.Remove(item);
        }

        public void LimpiarCarrito()
        {
            Items.Clear();
        }

        public decimal ObtenerTotal()
        {
            return Items.Sum(i => i.Total);
        }

        public int ObtenerCantidadItems()
        {
            return Items.Count;
        }

        public int ObtenerCantidadTotal()
        {
            return Items.Sum(i => i.Cantidad);
        }

        public bool TieneItems()
        {
            return Items.Any();
        }

        // Método para verificar si un producto ya está en el carrito
        public ItemCarrito? ObtenerItem(int productoId)
        {
            return Items.FirstOrDefault(i => i.ProductoId == productoId);
        }

        // Método para verificar stock disponible considerando lo que ya está en el carrito
        public int ObtenerCantidadEnCarrito(int productoId)
        {
            var item = Items.FirstOrDefault(i => i.ProductoId == productoId);
            return item?.Cantidad ?? 0;
        }
    }
}