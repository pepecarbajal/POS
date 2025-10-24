using PCSC;
using PCSC.Exceptions;
using PCSC.Monitoring;
using POS.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace POS.Services
{
    public class NFCReaderService : INFCReaderService, IDisposable
    {
        private ISCardContext? _context;
        private ISCardMonitor? _monitor;
        private bool _isConnected;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _reconnectTask;

        public event EventHandler<string>? CardScanned;

        public NFCReaderService()
        {
            _isConnected = false;
            Initialize(); // 🔁 intenta conectar automáticamente al iniciar
        }

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Inicializa el lector NFC y activa reconexión automática si no hay lector disponible.
        /// </summary>
        public void Initialize()
        {
            if (_reconnectTask != null && !_reconnectTask.IsCompleted)
                return; // ya hay un proceso corriendo

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _reconnectTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (!_isConnected)
                    {
                        try
                        {
                            Console.WriteLine("[NFC] Intentando conectar lector...");
                            Connect();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[NFC] Error al intentar conectar: {ex.Message}");
                        }
                    }

                    await Task.Delay(5000, token); // ⏳ reintentar cada 5 segundos
                }
            }, token);
        }

        /// <summary>
        /// Establece conexión con el lector NFC.
        /// </summary>
        public bool Connect()
        {
            try
            {
                if (_isConnected)
                    return true;

                _context = ContextFactory.Instance.Establish(SCardScope.System);
                string[] readerNames = _context.GetReaders();

                if (readerNames == null || readerNames.Length == 0)
                {
                    Console.WriteLine("[NFC] No se encontraron lectores NFC/RFID.");
                    _isConnected = false;
                    return false;
                }

                Console.WriteLine($"[NFC] Lectores detectados: {string.Join(", ", readerNames)}");

                _monitor = MonitorFactory.Instance.Create(SCardScope.System);
                _monitor.CardInserted += OnCardInserted;
                _monitor.MonitorException += OnMonitorException;
                _monitor.Start(readerNames);

                _isConnected = true;
                Console.WriteLine("[NFC] Lector conectado exitosamente.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NFC] Error al conectar lector: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Desconecta y limpia recursos del lector.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_monitor != null)
                {
                    _monitor.CardInserted -= OnCardInserted;
                    _monitor.MonitorException -= OnMonitorException;
                    _monitor.Cancel();
                    _monitor.Dispose();
                    _monitor = null;
                }

                _context?.Dispose();
                _context = null;

                _isConnected = false;
                Console.WriteLine("[NFC] Lector desconectado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NFC] Error al desconectar lector: {ex.Message}");
            }
        }

        private void OnCardInserted(object sender, CardStatusEventArgs e)
        {
            try
            {
                Console.WriteLine($"[NFC] Tarjeta detectada en {e.ReaderName}");
                string cardId = ReadCardUID(e.ReaderName);

                if (!string.IsNullOrWhiteSpace(cardId))
                {
                    Console.WriteLine($"[NFC] Tarjeta escaneada: {cardId}");
                    CardScanned?.Invoke(this, cardId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NFC] Error al procesar tarjeta: {ex.Message}");
            }
        }

        private string ReadCardUID(string readerName)
        {
            try
            {
                using (var context = ContextFactory.Instance.Establish(SCardScope.System))
                using (var reader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
                {
                    var apdu = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
                    var receiveBuffer = new byte[256];

                    var bytesReceived = reader.Transmit(apdu, receiveBuffer);

                    if (bytesReceived >= 2)
                    {
                        byte sw1 = receiveBuffer[bytesReceived - 2];
                        byte sw2 = receiveBuffer[bytesReceived - 1];

                        if (sw1 == 0x90 && sw2 == 0x00)
                        {
                            var uidBytes = receiveBuffer.Take(bytesReceived - 2).ToArray();
                            return BitConverter.ToString(uidBytes).Replace("-", "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NFC] Error al leer UID: {ex.Message}");
            }

            return string.Empty;
        }

        private void OnMonitorException(object sender, PCSCException ex)
        {
            Console.WriteLine($"[NFC] Error en monitor: {ex.Message}");
            _isConnected = false;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
