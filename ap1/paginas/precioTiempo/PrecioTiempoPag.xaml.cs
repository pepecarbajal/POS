using Microsoft.EntityFrameworkCore;
using POS.Data;
using POS.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.paginas.precios
{
    public partial class PrecioTiempoPag : Page
    {
        private readonly AppDbContext _context;

        public PrecioTiempoPag()
        {
            InitializeComponent();
            _context = new AppDbContext();
            CargarPrecios();
        }

        private async void CargarPrecios()
        {
            try
            {
                var precios = await _context.PreciosTiempo
                    .OrderBy(p => p.Orden)
                    .ToListAsync();

                foreach (var precio in precios)
                {
                    switch (precio.Minutos)
                    {
                        case 60:
                            Precio60TextBox.Text = precio.Nombre;
                            PrecioValor60TextBox.Text = precio.Precio.ToString("F2");
                            Precio60TextBox.Tag = precio.Id;
                            break;

                        case 80:
                            Precio80TextBox.Text = precio.Nombre;
                            PrecioValor80TextBox.Text = precio.Precio.ToString("F2");
                            Precio80TextBox.Tag = precio.Id;
                            break;

                        case 120:
                            Precio120TextBox.Text = precio.Nombre;
                            PrecioValor120TextBox.Text = precio.Precio.ToString("F2");
                            Precio120TextBox.Tag = precio.Id;
                            break;

                        case 140:
                            Precio140TextBox.Text = precio.Nombre;
                            PrecioValor140TextBox.Text = precio.Precio.ToString("F2");
                            Precio140TextBox.Tag = precio.Id;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar los precios: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ActualizarPrecio60_Click(object sender, RoutedEventArgs e)
        {
            await ActualizarPrecio((int?)Precio60TextBox.Tag, PrecioValor60TextBox.Text);
        }

        private async void ActualizarPrecio80_Click(object sender, RoutedEventArgs e)
        {
            await ActualizarPrecio((int?)Precio80TextBox.Tag, PrecioValor80TextBox.Text);
        }

        private async void ActualizarPrecio120_Click(object sender, RoutedEventArgs e)
        {
            await ActualizarPrecio((int?)Precio120TextBox.Tag, PrecioValor120TextBox.Text);
        }

        private async void ActualizarPrecio140_Click(object sender, RoutedEventArgs e)
        {
            await ActualizarPrecio((int?)Precio140TextBox.Tag, PrecioValor140TextBox.Text);
        }

        private async System.Threading.Tasks.Task ActualizarPrecio(int? id, string precioTexto)
        {
            try
            {
                if (id == null)
                {
                    MessageBox.Show("No se encontró el registro de precio.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(precioTexto))
                {
                    MessageBox.Show("El precio no puede estar vacío.",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(precioTexto, out decimal precio))
                {
                    MessageBox.Show("El precio debe ser un número válido.",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (precio <= 0)
                {
                    MessageBox.Show("El precio debe ser mayor a 0.",
                        "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var precioTiempo = await _context.PreciosTiempo.FindAsync(id.Value);

                if (precioTiempo == null)
                {
                    MessageBox.Show("No se encontró el registro de precio.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                precioTiempo.Precio = precio;

                await _context.SaveChangesAsync();

                MessageBox.Show("Precio actualizado correctamente.",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar el precio: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrecioTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(e.Text);
        }
    }
}
