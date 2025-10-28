using System;
using System.Threading.Tasks;
using System.Windows;
using POS.Interfaces;

namespace POS.paginas.ventas.Managers
{
    /// <summary>
    /// Manager para gestionar operaciones con el lector NFC
    /// </summary>
    public class NFCManager
    {
        private readonly INFCReaderService _nfcReaderService;

        // Estados de espera
        private bool _esperandoTarjeta;
        private string _accionEsperada = "";

        public bool EsperandoTarjeta => _esperandoTarjeta;
        public string AccionEsperada => _accionEsperada;

        // Eventos
        public event EventHandler<string>? TarjetaEscaneada;
        public event EventHandler? EstadoEsperaCambiado;

        public NFCManager(INFCReaderService nfcReaderService)
        {
            _nfcReaderService = nfcReaderService;
        }

        /// <summary>
        /// Verifica si el lector está conectado
        /// </summary>
        public bool EstaConectado()
        {
            return _nfcReaderService.IsConnected;
        }

        /// <summary>
        /// Inicia la espera de una tarjeta NFC para una acción específica
        /// </summary>
        public bool IniciarEsperaTarjeta(string accion, string mensajeEspera)
        {
            if (!_nfcReaderService.IsConnected)
            {
                MessageBox.Show("El lector NFC no está conectado. Verifique la conexión.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            _esperandoTarjeta = true;
            _accionEsperada = accion;

            // Suscribirse al evento
            _nfcReaderService.CardScanned += OnCardScanned;

            // Notificar cambio de estado
            EstadoEsperaCambiado?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Cancela la espera de tarjeta
        /// </summary>
        public void CancelarEsperaTarjeta()
        {
            _esperandoTarjeta = false;
            _accionEsperada = "";
            _nfcReaderService.CardScanned -= OnCardScanned;

            EstadoEsperaCambiado?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Maneja el evento de tarjeta escaneada
        /// </summary>
        private void OnCardScanned(object? sender, string cardId)
        {
            if (!_esperandoTarjeta) return;

            // Limpiar estado
            _esperandoTarjeta = false;
            _nfcReaderService.CardScanned -= OnCardScanned;

            // Notificar
            TarjetaEscaneada?.Invoke(this, cardId);
            EstadoEsperaCambiado?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Obtiene mensaje apropiado según la acción
        /// </summary>
        public string ObtenerMensajeEspera(string accion)
        {
            return accion switch
            {
                "combo_tiempo" => "Esperando tarjeta NFC para registrar combo con tiempo...",
                "finalizar_combo_tiempo" => "Esperando tarjeta NFC para finalizar venta...",
                "recuperar_venta" => "Esperando tarjeta NFC para recuperar venta pendiente...",
                _ => "Esperando tarjeta NFC..."
            };
        }
    }
}