using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace POS.paginas.ventas
{
    /// <summary>
    /// Modelo para representar un producto o combo en la vista
    /// </summary>
    public class ProductoVenta
    {
        public required int Id { get; set; }
        public required string Nombre { get; set; }
        public required decimal Precio { get; set; }
        public required int Stock { get; set; }
        public string? ImagenUrl { get; set; }
        public bool EsCombo { get; set; } = false;
        public int ComboId { get; set; } = 0;
        public bool TieneTiempo { get; set; } = false;
        public int? PrecioTiempoId { get; set; }
        public int MinutosTiempo { get; set; } = 0;
    }

    /// <summary>
    /// Modelo para representar un item en el carrito
    /// </summary>
    public class ItemCarrito : INotifyPropertyChanged
    {
        private int _cantidad;
        private decimal _total;

        public required int ProductoId { get; set; }
        public required string Nombre { get; set; }
        public required decimal PrecioUnitario { get; set; }

        public int Cantidad
        {
            get => _cantidad;
            set
            {
                _cantidad = value;
                OnPropertyChanged(nameof(Cantidad));
            }
        }

        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged(nameof(Total));
            }
        }

        public string NombreProducto => Nombre;
        public decimal Subtotal => Total;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Modelo para representar una categoría en el combobox
    /// </summary>
    public class CategoriaItem
    {
        public required int Id { get; set; }
        public required string Nombre { get; set; }
    }

    /// <summary>
    /// Modelo para representar un tiempo activo (individual o combo)
    /// </summary>
    public class TiempoActivo : INotifyPropertyChanged
    {
        public required int Id { get; set; }
        public required string IdNfc { get; set; }
        public required DateTime HoraEntrada { get; set; }
        public required string Estado { get; set; }
        public bool EsCombo { get; set; } = false;
        public string? NombreCombo { get; set; }
        public int MinutosIncluidos { get; set; } = 0;
        public decimal MontoTotal { get; set; } = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Modelo para almacenar items recuperables cuando se cancela una venta/tiempo
    /// </summary>
    public class VentaTiempoRecuperable
    {
        public int Id { get; set; }
        public bool EsCombo { get; set; }
        public string IdNfc { get; set; } = "";
        public List<ItemCarrito> Items { get; set; } = new List<ItemCarrito>();
        public DateTime HoraEntrada { get; set; }
        public int MinutosTiempo { get; set; }
    }
}