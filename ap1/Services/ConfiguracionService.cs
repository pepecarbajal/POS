using System;
using System.IO;
using System.Text.Json;
using System.Drawing.Printing;
using System.Collections.Generic;
using System.Linq;

namespace POS.Services
{
    public class ConfiguracionService
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "POS",
            "config.json"
        );

        public class ConfiguracionImpresora
        {
            public string ImpresoraNombre { get; set; } = "";
            public int AnchoTicket { get; set; } = 80; // mm
        }

        public static ConfiguracionImpresora CargarConfiguracion()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<ConfiguracionImpresora>(json) ?? new ConfiguracionImpresora();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar configuración: {ex.Message}");
            }

            return new ConfiguracionImpresora();
        }

        public static void GuardarConfiguracion(ConfiguracionImpresora config)
        {
            try
            {
                var directorio = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directorio))
                {
                    Directory.CreateDirectory(directorio!);
                }

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar configuración: {ex.Message}", ex);
            }
        }

        public static List<string> ObtenerImpresorasDisponibles()
        {
            var impresoras = new List<string>();

            try
            {
                foreach (string impresora in PrinterSettings.InstalledPrinters)
                {
                    impresoras.Add(impresora);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener impresoras: {ex.Message}");
            }

            return impresoras;
        }

        public static string ObtenerImpresoraPredeterminada()
        {
            try
            {
                var printerSettings = new PrinterSettings();
                return printerSettings.PrinterName;
            }
            catch
            {
                return "";
            }
        }
    }
}