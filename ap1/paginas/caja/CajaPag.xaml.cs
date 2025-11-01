using Microsoft.EntityFrameworkCore;
using POS.Data;
using POS.Models;
using POS.Services;
using POS.ventanas;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace POS.paginas.caja
{
    public partial class CajaPag : Page
    {
        private readonly AppDbContext _context;
        private readonly CorteCajaService _service;
        private CorteCaja? _corteActual;
        private DispatcherTimer _timer;

        public CajaPag()
        {
            InitializeComponent();
            _context = new AppDbContext();
            _service = new CorteCajaService(_context);

            Loaded += CajaPag_Loaded;
        }

        private async void CajaPag_Loaded(object sender, RoutedEventArgs e)
        {
            // Actualizar fecha
            txtFechaActual.Text = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy");

            // Cargar datos iniciales
            await CargarDatos();

            // Configurar timer para actualización automática cada 30 segundos
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += async (s, args) => await CargarDatos();
            _timer.Start();
        }

        private async System.Threading.Tasks.Task CargarDatos()
        {
            try
            {
                // Obtener corte actual
                _corteActual = await _service.ObtenerCorteAbierto();

                if (_corteActual == null)
                {
                    // No hay corte abierto
                    MostrarEstadoCerrado();
                    return;
                }

                // Hay corte abierto
                MostrarEstadoAbierto();

                // Calcular resumen
                var resumen = await _service.CalcularResumenCorte();

                // Actualizar UI con los datos
                txtEfectivoInicial.Text = _corteActual.EfectivoInicial.ToString("N2");
                txtEfectivoEsperado.Text = resumen.EfectivoEsperado.ToString("N2");

                // Ventas
                txtTotalVentasEfectivo.Text = resumen.TotalVentasEfectivo.ToString("N2");
                txtCantidadVentasEfectivo.Text = $"({resumen.CantidadVentasEfectivo})";

                txtTotalVentasTarjeta.Text = resumen.TotalVentasTarjeta.ToString("N2");
                txtCantidadVentasTarjeta.Text = $"({resumen.CantidadVentasTarjeta})";

                txtTotalVentas.Text = resumen.TotalVentas.ToString("N2");

                // Movimientos
                txtTotalDepositos.Text = resumen.TotalDepositos.ToString("N2");
                txtTotalRetiros.Text = resumen.TotalRetiros.ToString("N2");

                // Diferencia (solo si el corte está cerrado)
                if (_corteActual.EstaCerrado)
                {
                    txtDiferencia.Text = _corteActual.Diferencia.ToString("N2");
                    MostrarTipoDiferencia(_corteActual.Diferencia);
                }
                else
                {
                    txtDiferencia.Text = "$0.00";
                    txtTipoDiferencia.Text = "Pendiente de cierre";
                    txtTipoDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af"));
                }

                // Cargar movimientos recientes
                dgMovimientos.ItemsSource = resumen.Movimientos
                    .OrderByDescending(m => m.Fecha)
                    .Take(10)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MostrarEstadoAbierto()
        {
            txtEstadoCorte.Text = "ABIERTO";
            txtEstadoCorte.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
        }

        private void MostrarEstadoCerrado()
        {
            txtEstadoCorte.Text = "CERRADO";
            txtEstadoCorte.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));

            // Limpiar valores
            txtEfectivoInicial.Text = "$0.00";
            txtEfectivoEsperado.Text = "$0.00";
            txtTotalVentasEfectivo.Text = "$0.00";
            txtCantidadVentasEfectivo.Text = "(0)";
            txtTotalVentasTarjeta.Text = "$0.00";
            txtCantidadVentasTarjeta.Text = "(0)";
            txtTotalVentas.Text = "$0.00";
            txtTotalDepositos.Text = "$0.00";
            txtTotalRetiros.Text = "$0.00";
            txtDiferencia.Text = "$0.00";
            txtTipoDiferencia.Text = "Sin corte abierto";
            dgMovimientos.ItemsSource = null;
        }

        private void MostrarTipoDiferencia(decimal diferencia)
        {
            if (Math.Abs(diferencia) < 0.01m)
            {
                txtTipoDiferencia.Text = "✓ Sin diferencia";
                txtTipoDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
                txtDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
            }
            else if (diferencia > 0)
            {
                txtTipoDiferencia.Text = "⬆ Sobrante";
                txtTipoDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
                txtDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
            }
            else
            {
                txtTipoDiferencia.Text = "⬇ Faltante";
                txtTipoDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
                txtDiferencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
            }
        }

        // ==================== EVENTOS DE BOTONES ====================

        private async void BtnAbrirCaja_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar si ya hay un corte abierto
                var corteExistente = await _service.ObtenerCorteAbierto();
                if (corteExistente != null)
                {
                    MessageBox.Show("Ya existe un corte de caja abierto.",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Abrir ventana de apertura
                var aperturaWindow = new AperturaCajaWindow();
                if (aperturaWindow.ShowDialog() == true)
                {
                    await _service.AbrirCorteCaja(
                        aperturaWindow.EfectivoInicial,
                        aperturaWindow.Observaciones
                    );

                    MessageBox.Show("Caja abierta correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    await CargarDatos();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir la caja: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeposito_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar que haya corte abierto
                if (_corteActual == null)
                {
                    MessageBox.Show("No hay un corte de caja abierto. Debe abrir la caja primero.",
                        "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var movimientoWindow = new MovimientoCajaWindow(TipoMovimiento.Deposito);
                if (movimientoWindow.ShowDialog() == true)
                {
                    await _service.RegistrarDeposito(
                        movimientoWindow.Monto,
                        movimientoWindow.Concepto,
                        movimientoWindow.Observaciones,
                        movimientoWindow.Usuario
                    );

                    MessageBox.Show("Depósito registrado correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    await CargarDatos();
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
                // Verificar que haya corte abierto
                if (_corteActual == null)
                {
                    MessageBox.Show("No hay un corte de caja abierto. Debe abrir la caja primero.",
                        "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var movimientoWindow = new MovimientoCajaWindow(TipoMovimiento.Retiro);
                if (movimientoWindow.ShowDialog() == true)
                {
                    await _service.RegistrarRetiro(
                        movimientoWindow.Monto,
                        movimientoWindow.Concepto,
                        movimientoWindow.Observaciones,
                        movimientoWindow.Usuario
                    );

                    MessageBox.Show("Retiro registrado correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    await CargarDatos();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar el retiro: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCerrarCaja_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar que haya corte abierto
                if (_corteActual == null)
                {
                    MessageBox.Show("No hay un corte de caja abierto.",
                        "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Verificar que el corte no esté ya cerrado
                if (_corteActual.EstaCerrado)
                {
                    MessageBox.Show("Este corte de caja ya está cerrado.",
                        "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Calcular resumen
                var resumen = await _service.CalcularResumenCorte();

                if (resumen == null)
                {
                    MessageBox.Show("Error al calcular el resumen del corte.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Abrir ventana de cierre CON LOS TRES PARÁMETROS REQUERIDOS
                CierreCajaWindow cierreWindow = null;

                try
                {
                    // CORRECCIÓN: Pasar corteCaja y resumenCorteCaja como parámetros
                    cierreWindow = new CierreCajaWindow(
                        resumen.EfectivoEsperado,  // efectivoEsperado
                        _corteActual,               // corteCaja
                        resumen                     // resumenCorteCaja
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al abrir la ventana de cierre:\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialogResult = cierreWindow.ShowDialog();

                if (dialogResult == true)
                {
                    var diferencia = cierreWindow.EfectivoFinal - resumen.EfectivoEsperado;

                    var resultado = MessageBox.Show(
                        $"¿Está seguro de cerrar el corte de caja?\n\n" +
                        $"Efectivo esperado: {resumen.EfectivoEsperado:N2}\n" +
                        $"Efectivo contado: {cierreWindow.EfectivoFinal:N2}\n" +
                        $"Diferencia: {diferencia:N2}\n\n" +
                        $"{(diferencia == 0 ? "✓ Sin diferencia" : diferencia > 0 ? "⚠️ Sobrante" : "⚠️ Faltante")}",
                        "Confirmar Cierre",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (resultado == MessageBoxResult.Yes)
                    {
                        await _service.CerrarCorteCaja(
                            cierreWindow.EfectivoFinal,
                            cierreWindow.Usuario,
                            cierreWindow.Observaciones
                        );

                        MessageBox.Show("Corte de caja cerrado correctamente.",
                            "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                        await CargarDatos();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cerrar la caja:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            await CargarDatos();
        }

        // Limpiar recursos al cerrar
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _context?.Dispose();
        }
    }
}