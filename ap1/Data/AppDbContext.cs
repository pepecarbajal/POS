
using Microsoft.EntityFrameworkCore;
using POS.Models;
using System.IO;

namespace POS.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Combo> Combos { get; set; }
        public DbSet<ComboProducto> ComboProductos { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 1. Obtener la ruta a AppData
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // 2. Definir la carpeta específica para tu aplicación
            string appFolder = Path.Combine(appDataPath, "POS");

            // 3. Asegurarse de que el directorio exista
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            // 4. Combinar para obtener la ruta completa de la base de datos
            string dbPath = Path.Combine(appFolder, "pos.db");

            // 5. Configurar SQLite
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configurar la llave primaria compuesta para la tabla de unión ComboProducto
            modelBuilder.Entity<ComboProducto>()
                .HasKey(cp => new { cp.ComboId, cp.ProductoId });

            // Configurar la relación muchos a muchos entre Combo y Producto
            modelBuilder.Entity<Combo>()
                .HasMany(c => c.Productos) // Un combo tiene muchos productos
                .WithMany(p => p.Combos)   // Un producto puede estar en muchos combos
                .UsingEntity<ComboProducto>(); // A través de la tabla ComboProducto
        }
    }
}