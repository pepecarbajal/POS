using POS.Interfaces;
using POS.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.ventanas
{
    public partial class DevolucionParcialWindow : Window
    {
        private readonly int _ventaId;
        private readonly IDevolucionService _devolucionService;
        private readonly ObservableCollection<DetalleDevolucion> _detalles;

        public DevolucionParcialWindow(int ventaId, List<DetalleVenta> detalles, IDevolucionService devolucionService)
        {
            InitializeComponent();

            _ventaId = ventaId;
            _devolucionService = devolucionService;
            _detalles = new ObservableCollection<DetalleDevolucion>();

            VentaIdText.Text = ventaId.ToString();

            // Convertir detalles a modelo de devolución
            foreach (var detalle in detalles)
            {
                var detalleDevolucion = new DetalleDevolucion
                {
                    Id = detalle.Id,
                    NombreItem = detalle.NombreParaMostrar,
                    CantidadTotal = detalle.Cantidad,
                    CantidadADevolver = 1, // Por defecto, 1 unidad
                    PrecioUnitario = detalle.PrecioUnitario,
                    Subtotal = detalle.Subtotal,
                    TipoItem = detalle.TipoItem,
                    IsSelected = false
                };

                detalleDevolucion.TipoItemTexto = detalle.TipoItem switch
                {
                    1 => "Producto",
                    2 => "Combo",
                    3 => "Tiempo",
                    _ => "Otro"
                };

                detalleDevolucion.PropertyChanged += DetalleDevolucion_PropertyChanged;
                _detalles.Add(detalleDevolucion);
            }

            ProductosListBox.ItemsSource = _detalles;
            ActualizarTotal();
        }

        private void DetalleDevolucion_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DetalleDevolucion.IsSelected) ||
                e.PropertyName == nameof(DetalleDevolucion.CantidadADevolver))
            {
                ActualizarTotal();
            }
        }

        private void IncrementarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DetalleDevolucion detalle)
            {
                if (detalle.CantidadADevolver < detalle.CantidadTotal)
                {
                    detalle.CantidadADevolver++;
                }
            }
        }

        private void DecrementarCantidad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DetalleDevolucion detalle)
            {
                if (detalle.CantidadADevolver > 1)
                {
                    detalle.CantidadADevolver--;
                }
            }
        }

        private void ProductoCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DetalleDevolucion detalle)
            {
                detalle.IsSelected = !detalle.IsSelected;
            }
        }

        private void ActualizarTotal()
        {
            decimal total = _detalles.Where(d => d.IsSelected).Sum(d => d.SubtotalDevolucion);
            TotalDevolucionText.Text = $"${total:N2}";
        }

        private async void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            var seleccionados = _detalles.Where(d => d.IsSelected).ToList();

            if (!seleccionados.Any())
            {
                MessageBox.Show("Por favor seleccione al menos un producto para devolver",
                    "Sin selección", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal totalDevolucion = seleccionados.Sum(d => d.SubtotalDevolucion);
            int totalItems = seleccionados.Sum(d => d.CantidadADevolver);

            var result = MessageBox.Show(
                $"¿Está seguro de devolver {totalItems} unidad(es) de {seleccionados.Count} producto(s)?\n\n" +
                $"Total a devolver: ${totalDevolucion:N2}\n\n" +
                "Esta acción:\n" +
                "• Restaurará el stock de los productos seleccionados\n" +
                "• Ajustará o eliminará estos productos de la venta\n" +
                "• Actualizará el total de la venta\n" +
                "• NO se podrá deshacer\n\n" +
                "¿Desea continuar?",
                "Confirmar Devolución",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // Crear diccionario con ID del detalle y cantidad a devolver
                var detallesConCantidades = new Dictionary<int, int>();

                foreach (var detalle in seleccionados)
                {
                    detallesConCantidades[detalle.Id] = detalle.CantidadADevolver;
                }

                bool exito = await _devolucionService.DevolverProductosParcialesAsync(_ventaId, detallesConCantidades);

                if (exito)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar devolución: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Modelo para representar un detalle de venta en la ventana de devolución
    /// </summary>
    public class DetalleDevolucion : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _cantidadADevolver;

        public int Id { get; set; }
        public string NombreItem { get; set; } = "";
        public int CantidadTotal { get; set; } // Cantidad total disponible
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public int TipoItem { get; set; }
        public string TipoItemTexto { get; set; } = "";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public int CantidadADevolver
        {
            get => _cantidadADevolver;
            set
            {
                if (_cantidadADevolver != value && value >= 1 && value <= CantidadTotal)
                {
                    _cantidadADevolver = value;
                    OnPropertyChanged(nameof(CantidadADevolver));
                    OnPropertyChanged(nameof(SubtotalDevolucion));
                }
            }
        }

        // Subtotal calculado basado en la cantidad a devolver
        public decimal SubtotalDevolucion
        {
            get
            {
                // Calcular precio unitario real basado en el subtotal y cantidad total
                decimal precioUnitarioReal = CantidadTotal > 0 ? Subtotal / CantidadTotal : 0;
                return precioUnitarioReal * CantidadADevolver;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}