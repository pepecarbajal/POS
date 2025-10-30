using Microsoft.EntityFrameworkCore;
using POS.Data;
using POS.Models;
using POS.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace POS.ventanas
{
    public partial class CorteCajaWindow : Window
    {
        private readonly AppDbContext _context;
        private readonly CorteCajaService _service;
        private CorteCaja? _corteActual;
        private ResumenCorteCaja? _resumenActual;

        public CorteCajaWindow()
        {
            InitializeComponent();
            _context = new AppDbContext();
            _service = new CorteCajaService(_context);

            Loaded += CorteCajaWindow_Loaded;
        }

        private async void CorteCajaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar si hay un corte abierto
                _corteActual = await _service.ObtenerCorteAbierto();

                if (_corteActual == null)
                {
                    // Si no hay corte abierto, solicitar apertura
                    var aperturaWindow = new AperturaCajaWindow();
                    if (aperturaWindow.ShowDialog() == true)
                    {
                        _corteActual = await _service.AbrirCorteCaja(
                            aperturaWindow.EfectivoInicial,
                            aperturaWindow.Observaciones
                        );
                    }
                    else
                    {
                        MessageBox.Show("Debe abrir un corte de caja para continuar.",
                            "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                        Close();
                        return;
                    }
                }

                await CargarDatos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el corte de caja: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async System.Threading.Tasks.Task CargarDatos()
        {
            try
            {
                if (_corteActual == null) return;

                // Obtener resumen actualizado
                _resumenActual = await _service.CalcularResumenCorte();

                // Actualizar UI
                txtFecha.Text = $"Fecha: {_corteActual.FechaApertura:dd/MM/yyyy HH:mm}";
                txtEfectivoInicial.Text = _corteActual.EfectivoInicial.ToString("C2");

                txtTotalVentasEfectivo.Text = _resumenActual.TotalVentasEfectivo.ToString("C2");
                txtCantidadVentasEfectivo.Text = $"({_resumenActual.CantidadVentasEfectivo})";

                txtTotalVentasTarjeta.Text = _resumenActual.TotalVentasTarjeta.ToString("C2");
                txtCantidadVentasTarjeta.Text = $"({_resumenActual.CantidadVentasTarjeta})";

                txtTotalVentas.Text = _resumenActual.TotalVentas.ToString("C2");

                txtTotalDepositos.Text = _resumenActual.TotalDepositos.ToString("C2");
                txtTotalRetiros.Text = _resumenActual.TotalRetiros.ToString("C2");
                txtEfectivoEsperado.Text = _resumenActual.EfectivoEsperado.ToString("C2");

                // Cargar movimientos
                dgMovimientos.ItemsSource = _resumenActual.Movimientos;

                // Si el corte ya está cerrado, mostrar diferencia
                if (_corteActual.EstaCerrado)
                {
                    txtEfectivoFinal.Text = _corteActual.EfectivoFinal.ToString("C2");
                    MostrarDiferencia(_corteActual.Diferencia);
                    btnCerrarCorte.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar los datos: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MostrarDiferencia(decimal diferencia)
        {
            txtDiferencia.Text = diferencia.ToString("C2");

            if (Math.Abs(diferencia) < 0.01m)
            {
                txtLabelDiferencia.Text = "Diferencia (Sin diferencia) ✓";
                txtDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            }
            else if (diferencia > 0)
            {
                txtLabelDiferencia.Text = "Diferencia (Sobrante) ⬆";
                txtDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
            }
            else
            {
                txtLabelDiferencia.Text = "Diferencia (Faltante) ⬇";
                txtDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            }
        }

        private async void BtnDeposito_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var movimientoWindow = new MovimientoCajaWindow(TipoMovimiento.Deposito);
                if (movimientoWindow.ShowDialog() == true)
                {
                    await _service.RegistrarDeposito(
                        movimientoWindow.Monto,
                        movimientoWindow.Concepto,
                        movimientoWindow.Observaciones,
                        movimientoWindow.Usuario
                    );

                    await CargarDatos();
                    MessageBox.Show("Depósito registrado correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar el depósito: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRetiro_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var movimientoWindow = new MovimientoCajaWindow(TipoMovimiento.Retiro);
                if (movimientoWindow.ShowDialog() == true)
                {
                    await _service.RegistrarRetiro(
                        movimientoWindow.Monto,
                        movimientoWindow.Concepto,
                        movimientoWindow.Observaciones,
                        movimientoWindow.Usuario
                    );

                    await CargarDatos();
                    MessageBox.Show("Retiro registrado correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar el retiro: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCerrarCorte_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_corteActual == null || _resumenActual == null) return;

                // CORRECCIÓN: Pasar los 3 parámetros al constructor de CierreCajaWindow
                var cierreWindow = new CierreCajaWindow(
                    _resumenActual.EfectivoEsperado,  // efectivoEsperado
                    _corteActual,                      // corteCaja
                    _resumenActual                     // resumenCorteCaja
                );

                if (cierreWindow.ShowDialog() == true)
                {
                    var resultado = MessageBox.Show(
                        $"¿Está seguro de cerrar el corte de caja?\n\n" +
                        $"Efectivo esperado: {_resumenActual.EfectivoEsperado:C2}\n" +
                        $"Efectivo contado: {cierreWindow.EfectivoFinal:C2}\n" +
                        $"Diferencia: {(cierreWindow.EfectivoFinal - _resumenActual.EfectivoEsperado):C2}",
                        "Confirmar Cierre",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (resultado == MessageBoxResult.Yes)
                    {
                        _corteActual = await _service.CerrarCorteCaja(
                            cierreWindow.EfectivoFinal,
                            cierreWindow.Usuario,
                            cierreWindow.Observaciones
                        );

                        await CargarDatos();

                        MessageBox.Show("Corte de caja cerrado correctamente.",
                            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cerrar el corte: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_corteActual == null || _resumenActual == null)
                {
                    MessageBox.Show("No hay información para imprimir.",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var ticketService = new TicketService();
                ticketService.ImprimirCorteCaja(_corteActual, _resumenActual);

                MessageBox.Show("Ticket enviado a la impresora.",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al imprimir: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _context?.Dispose();
        }
    }
}