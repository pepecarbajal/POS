using POS.Interfaces;
using POS.Models;
using POS.Data;
using POS.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System;

namespace POS.paginas.detalles_ventas
{
    public partial class DetallesVentasPag : Page
    {
        private readonly IVentaService _ventaService;
        private List<Venta> _todasLasVentas = new List<Venta>(); // Store all sales for filtering

        public DetallesVentasPag()
        {
            InitializeComponent();

            var context = new AppDbContext();
            _ventaService = new VentaService(context);

            FechaFiltro.SelectedDate = DateTime.Today;

            LoadVentas();
        }

        public DetallesVentasPag(IVentaService ventaService)
        {
            InitializeComponent();
            _ventaService = ventaService;
            LoadVentas();
        }

        private async void LoadVentas()
        {
            var ventas = await _ventaService.GetAllVentasAsync();
            _todasLasVentas = ventas.OrderByDescending(v => v.Fecha).ToList();
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (FechaFiltro.SelectedDate == null)
            {
                VentasItemsControl.ItemsSource = _todasLasVentas;
                ActualizarTotal(_todasLasVentas);
                return;
            }

            var fechaSeleccionada = FechaFiltro.SelectedDate.Value.Date;
            var ventasFiltradas = _todasLasVentas
                .Where(v => v.Fecha.Date == fechaSeleccionada)
                .ToList();

            VentasItemsControl.ItemsSource = ventasFiltradas;
            ActualizarTotal(ventasFiltradas);
        }

        private void ActualizarTotal(List<Venta> ventas)
        {
            var total = ventas.Sum(v => v.Total);
            TotalVentasTextBlock.Text = $"${total:N2}";
        }

        private void FechaFiltro_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void ToggleDetails_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // Find the parent Border (the card)
            var border = FindParent<Border>(button);
            if (border == null) return;

            // Find the details panel
            var detailsPanel = FindChild<Border>(border, "DetailsPanel");
            if (detailsPanel == null) return;

            // Find the button text
            var buttonText = FindChild<TextBlock>(button, "ExpandButtonText");

            // Toggle visibility
            if (detailsPanel.Visibility == Visibility.Collapsed)
            {
                detailsPanel.Visibility = Visibility.Visible;
                if (buttonText != null) buttonText.Text = "Ocultar Detalles";
            }
            else
            {
                detailsPanel.Visibility = Visibility.Collapsed;
                if (buttonText != null) buttonText.Text = "Ver Detalles";
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            T? parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        private T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            T? foundChild = null;

            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                T? childType = child as T;
                if (childType == null)
                {
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }
    }
}
