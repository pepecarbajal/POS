using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace POS.Services
{
    public class SumatraPrintService
    {
        private static readonly string[] PosibleRutasSumatra = new[]
        {
            @"C:\Program Files\SumatraPDF\SumatraPDF.exe",
            @"C:\Program Files (x86)\SumatraPDF\SumatraPDF.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"SumatraPDF\SumatraPDF.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"SumatraPDF\SumatraPDF.exe")
        };

        public static string? EncontrarSumatra()
        {
            foreach (var ruta in PosibleRutasSumatra)
            {
                if (File.Exists(ruta))
                {
                    return ruta;
                }
            }
            return null;
        }

        public static async Task<bool> ImprimirPdfAsync(byte[] pdfBytes, string nombreImpresora, int anchoMm)
        {
            var rutaSumatra = EncontrarSumatra();

            if (string.IsNullOrEmpty(rutaSumatra))
            {
                throw new Exception("No se encontró SumatraPDF. Por favor, instale SumatraPDF o especifique su ubicación.");
            }

            // Guardar PDF temporalmente
            var tempPath = Path.Combine(Path.GetTempPath(), $"ticket_{Guid.NewGuid()}.pdf");

            try
            {
                await File.WriteAllBytesAsync(tempPath, pdfBytes);

                // Preparar argumentos para Sumatra
                var argumentos = $"-print-to \"{nombreImpresora}\" -silent \"{tempPath}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = rutaSumatra,
                    Arguments = argumentos,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        // Esperar a que termine la impresión (timeout de 10 segundos)
                        await Task.Run(() => process.WaitForExit(10000));

                        // Pequeña espera adicional para asegurar que el archivo se liberó
                        await Task.Delay(1000);

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al imprimir: {ex.Message}", ex);
            }
            finally
            {
                // Intentar eliminar el archivo temporal
                try
                {
                    if (File.Exists(tempPath))
                    {
                        // Esperar un poco más antes de intentar eliminar
                        await Task.Delay(500);
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Si no se puede eliminar, no es crítico
                }
            }
        }
    }
}