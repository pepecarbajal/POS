using System;
using System.IO;

namespace POS.Helpers
{
    public static class ImageHelper
    {
        private static readonly string ImageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "POS",
            "images"
        );

        static ImageHelper()
        {
            if (!Directory.Exists(ImageDirectory))
            {
                Directory.CreateDirectory(ImageDirectory);
            }
        }

        /// <summary>
        /// Copies an image to the AppData/POS/images folder with a unique name
        /// </summary>
        /// <param name="sourcePath">Original image file path</param>
        /// <returns>New image path in AppData folder, or empty string if source is null/empty</returns>
        public static string SaveImage(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return string.Empty;
            }

            try
            {
                string extension = Path.GetExtension(sourcePath);
                string newFileName = $"{Guid.NewGuid()}{extension}";
                string destinationPath = Path.Combine(ImageDirectory, newFileName);

                File.Copy(sourcePath, destinationPath, overwrite: true);

                return destinationPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar la imagen: {ex.Message}", ex);
            }
        }


        public static void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            try
            {
                if (imagePath.StartsWith(ImageDirectory))
                {
                    File.Delete(imagePath);
                }
            }
            catch
            {
                
            }
        }
    }
}
