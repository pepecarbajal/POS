using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using POS.Interfaces;

namespace POS.Services
{
    public class NFCReaderService : INFCReaderService, IDisposable
    {
        private SerialPort? _serialPort;
        private int _baudRate;
        private bool _isConnected;

        public event EventHandler<string>? CardScanned;

        public NFCReaderService(int baudRate = 9600)
        {
            _baudRate = baudRate;
            _isConnected = false;
        }

        public bool Connect()
        {
            try
            {
                if (_isConnected && _serialPort?.IsOpen == true)
                {
                    return true;
                }

                // Get all available COM ports
                string[] availablePorts = SerialPort.GetPortNames();

                if (availablePorts.Length == 0)
                {
                    Console.WriteLine("[v0] No se encontraron puertos COM disponibles");
                    return false;
                }

                Console.WriteLine($"[v0] Buscando lector NFC en {availablePorts.Length} puerto(s)...");

                // Try each port until we find one that works
                foreach (string portName in availablePorts)
                {
                    try
                    {
                        Console.WriteLine($"[v0] Intentando conectar en {portName}...");

                        var testPort = new SerialPort(portName, _baudRate, Parity.None, 8, StopBits.One);
                        testPort.ReadTimeout = 500;
                        testPort.WriteTimeout = 500;
                        testPort.Open();

                        // If we can open the port, use it for the NFC reader
                        _serialPort = testPort;
                        _serialPort.DataReceived += SerialPort_DataReceived;
                        _isConnected = true;

                        Console.WriteLine($"[v0] NFC Reader conectado exitosamente en {portName}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[v0] No se pudo conectar en {portName}: {ex.Message}");
                        // Continue trying other ports
                    }
                }

                Console.WriteLine("[v0] No se pudo encontrar el lector NFC en ningún puerto");
                _isConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[v0] Error al buscar NFC Reader: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }
                _isConnected = false;
                Console.WriteLine("[v0] NFC Reader desconectado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[v0] Error al desconectar NFC Reader: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    string data = _serialPort.ReadLine().Trim();

                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        Console.WriteLine($"[v0] Tarjeta NFC escaneada: {data}");
                        CardScanned?.Invoke(this, data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[v0] Error al leer datos del NFC Reader: {ex.Message}");
            }
        }

        public bool IsConnected => _isConnected && _serialPort?.IsOpen == true;

        public void Dispose()
        {
            Disconnect();
        }
    }
}
