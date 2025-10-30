using System;
using System.IO;
using System.Text.Json;

namespace POS.Services
{
    public class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "POS",
            "config.json"
        );

        public class Config
        {
            public string ImpresoraNombre { get; set; } = "PDF Architect 9";
            public int AnchoTicket { get; set; } = 80;
        }

        public static Config CargarConfiguracion()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    return config ?? new Config();
                }
                else
                {
                    // Crear configuración por defecto si no existe
                    var configDefault = new Config();
                    GuardarConfiguracion(configDefault);
                    return configDefault;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cargar configuración: {ex.Message}");
            }
        }

        public static void GuardarConfiguracion(Config config)
        {
            try
            {
                string directorio = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directorio))
                {
                    Directory.CreateDirectory(directorio);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar configuración: {ex.Message}");
            }
        }
    }
}