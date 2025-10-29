using POS.Data;
using POS.Interfaces;
using POS.Models;
using POS.Services;
using POS.ventanas;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace POS.paginas.devoluciones
{
    public partial class DevolucionesPag : Page
    {
        private readonly AppDbContext _context;
        private readonly IDevolucionService _devolucionService;
        private ObservableCollection<VentaDevolucion> _todasLasVentas;
        private ObservableCollection<VentaDevolucion> _ventasFiltradas;
        private bool _mostrandoTodasLasFechas = false;

        public DevolucionesPag()
        {
            InitializeComponent();

            _context = new AppDbContext();
            _devolucionService = new DevolucionService(_context);
            _todasLasVentas = new ObservableCollection<VentaDevolucion>();
            _ventasFiltradas = new ObservableCollection<VentaDevolucion>();

            VentasItemsControl.ItemsSource = _ventasFiltradas;

            _ = CargarVentasAsync();
        }

        private async Task CargarVentasAsync()
        {
            try
            {
                var ventas = await _context.Ventas
                    .Include(v => v.DetallesVenta)
                    .Where(v => v.Estado == (int)EstadoVenta.Finalizada)
                    .OrderByDescending(v => v.Fecha)
                    .AsNoTracking()
                    .ToListAsync();

                _todasLasVentas.Clear();

                foreach (var venta in ventas)
                {
                    _todasLasVentas.Add(new VentaDevolucion
                    {
                        Id = venta.Id,
                        Fecha = venta.Fecha,
                        Total = venta.Total,
                        CantidadItems = venta.DetallesVenta.Count,
                        PuedeSerDevuelta = true, // Todas las ventas finalizadas pueden devolverse
                        TieneProductosDevolvibles = true, // Todas tienen items devolvibles (incluyendo tiempo)
                        NoTieneProductosDevolvibles = false // Ninguna tiene este problema ahora
                    });
                }

                AplicarFiltros();
                ActualizarContadores();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar ventas: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AplicarFiltros()
        {
            _ventasFiltradas.Clear();

            var ventasParaMostrar = _todasLasVentas.AsEnumerable();

            // Por defecto, mostrar solo las ventas de hoy
            if (!_mostrandoTodasLasFechas)
            {
                var hoy = DateTime.Today;
                ventasParaMostrar = ventasParaMostrar.Where(v => v.Fecha.Date == hoy);
            }

            foreach (var venta in ventasParaMostrar)
            {
                _ventasFiltradas.Add(venta);
            }

            ActualizarContadores();
        }

        private void ActualizarContadores()
        {
            int total = _ventasFiltradas.Count;
            int devolvibles = _ventasFiltradas.Count(v => v.TieneProductosDevolvibles);

            string textoFecha = _mostrandoTodasLasFechas ? "" : " de hoy";
            ContadorTextBlock.Text = $"Mostrando {total} venta{(total != 1 ? "s" : "")}{textoFecha}";
            DevolviblesCountText.Text = $"{devolvibles} Devolvible{(devolvibles != 1 ? "s" : "")}";
        }

        private void MostrarTodas_Click(object sender, RoutedEventArgs e)
        {
            _mostrandoTodasLasFechas = !_mostrandoTodasLasFechas;

            if (_mostrandoTodasLasFechas)
            {
                MostrarTodasButton.Content = "📅 Mostrar Solo Hoy";
            }
            else
            {
                MostrarTodasButton.Content = "📅 Mostrar Todas";
            }

            AplicarFiltros();
        }

        private async void DevolucionParcial_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not VentaDevolucion venta)
                return;

            try
            {
                var detalles = await _devolucionService.GetDetallesDevolviblesAsync(venta.Id);

                if (!detalles.Any())
                {
                    MessageBox.Show("Esta venta no tiene items devolvibles",
                        "Sin items", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var ventana = new DevolucionParcialWindow(venta.Id, detalles, _devolucionService);
                ventana.Owner = Window.GetWindow(this);

                if (ventana.ShowDialog() == true)
                {
                    MessageBox.Show("Devolución procesada correctamente.\nEl stock ha sido restaurado (si aplica).",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    await CargarVentasAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar devolución: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DevolucionCompleta_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not VentaDevolucion venta)
                return;

            var result = MessageBox.Show(
                $"¿Está seguro de devolver completamente la venta #{venta.Id}?\n\n" +
                $"Total: ${venta.Total:N2}\n" +
                $"Fecha: {venta.Fecha:dd/MM/yyyy HH:mm}\n\n" +
                "Esta acción:\n" +
                "• Restaurará el stock de todos los productos (si aplica)\n" +
                "• Eliminará la venta del sistema\n" +
                "• NO se podrá deshacer\n\n" +
                "¿Desea continuar?",
                "Confirmar Devolución Completa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                bool exito = await _devolucionService.DevolverVentaCompletaAsync(venta.Id);

                if (exito)
                {
                    MessageBox.Show(
                        "Devolución completa procesada correctamente.\n\n" +
                        "• Stock restaurado (si aplica)\n" +
                        "• Venta eliminada del sistema",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await CargarVentasAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar devolución: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class VentaDevolucion
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public int CantidadItems { get; set; }
        public bool PuedeSerDevuelta { get; set; } = true; // Siempre true para ventas finalizadas
        public bool TieneProductosDevolvibles { get; set; } = true; // Siempre true para ventas finalizadas
        public bool NoTieneProductosDevolvibles { get; set; } = false; // Siempre false (ya se puede devolver tiempo)
    }
}