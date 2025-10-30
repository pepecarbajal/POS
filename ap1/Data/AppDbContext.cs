using Microsoft.EntityFrameworkCore;
using POS.Models;
using System;
using System.IO;

namespace POS.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Combo> Combos { get; set; }
        public DbSet<ComboProducto> ComboProductos { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetallesVenta { get; set; }
        public DbSet<Tiempo> Tiempos { get; set; }
        public DbSet<PrecioTiempo> PreciosTiempo { get; set; }

        public DbSet<CorteCaja> CorteCajas { get; set; }
        public DbSet<MovimientoCaja> MovimientosCaja { get; set; }

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
            base.OnModelCreating(modelBuilder);

            // ========== CONFIGURACIÓN DE COMBO Y PRODUCTOS ==========

            // Configurar la llave primaria compuesta para ComboProducto
            modelBuilder.Entity<ComboProducto>()
                .HasKey(cp => new { cp.ComboId, cp.ProductoId });

            // Relación Combo -> ComboProducto
            modelBuilder.Entity<ComboProducto>()
                .HasOne(cp => cp.Combo)
                .WithMany(c => c.ComboProductos)
                .HasForeignKey(cp => cp.ComboId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación Producto -> ComboProducto
            modelBuilder.Entity<ComboProducto>()
                .HasOne(cp => cp.Producto)
                .WithMany(p => p.ComboProductos)
                .HasForeignKey(cp => cp.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación muchos a muchos entre Combo y Producto (a través de ComboProducto)
            modelBuilder.Entity<Combo>()
                .HasMany(c => c.Productos)
                .WithMany(p => p.Combos)
                .UsingEntity<ComboProducto>(
                    j => j
                        .HasOne(cp => cp.Producto)
                        .WithMany(p => p.ComboProductos)
                        .HasForeignKey(cp => cp.ProductoId),
                    j => j
                        .HasOne(cp => cp.Combo)
                        .WithMany(c => c.ComboProductos)
                        .HasForeignKey(cp => cp.ComboId),
                    j =>
                    {
                        j.HasKey(cp => new { cp.ComboId, cp.ProductoId });
                        j.Property(cp => cp.Cantidad).HasDefaultValue(1);
                    }
                );

            // Índices para ComboProducto (mejor rendimiento)
            modelBuilder.Entity<ComboProducto>()
                .HasIndex(cp => cp.ComboId);

            modelBuilder.Entity<ComboProducto>()
                .HasIndex(cp => cp.ProductoId);

            modelBuilder.Entity<Combo>()
    .HasOne(c => c.PrecioTiempo)
    .WithMany()
    .HasForeignKey(c => c.PrecioTiempoId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.Restrict);

            // Índice para mejorar consultas
            modelBuilder.Entity<Combo>()
                .HasIndex(c => c.PrecioTiempoId);

            // ========== CONFIGURACIÓN DE VENTA CON ESTADO Y NFC ==========

            // Índice para IdNfc en Venta
            modelBuilder.Entity<Venta>()
                .HasIndex(v => v.IdNfc);

            // Índice para Estado en Venta
            modelBuilder.Entity<Venta>()
                .HasIndex(v => v.Estado);

            // ========== CONFIGURACIÓN DE VENTAS ==========

            // Configurar la relación uno a muchos entre Venta y DetalleVenta
            modelBuilder.Entity<Venta>()
                .HasMany(v => v.DetallesVenta)
                .WithOne(dv => dv.Venta)
                .HasForeignKey(dv => dv.VentaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar ProductoId como nullable en DetalleVenta
            modelBuilder.Entity<DetalleVenta>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .IsRequired(false) // Permitir null para tiempos
                .OnDelete(DeleteBehavior.Restrict); // No eliminar detalle si se elimina producto

            modelBuilder.Entity<Venta>()
                .Property(v => v.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<DetalleVenta>()
                .Property(dv => dv.Id)
                .ValueGeneratedOnAdd();

            // ========== CONFIGURACIÓN DE PRECISIÓN DECIMAL ==========

            // Precios de productos
            modelBuilder.Entity<Producto>()
                .Property(p => p.Precio)
                .HasColumnType("decimal(18,2)");

            // Precios de combos
            modelBuilder.Entity<Combo>()
                .Property(c => c.Precio)
                .HasColumnType("decimal(18,2)");

            // Total de ventas
            modelBuilder.Entity<Venta>()
                .Property(v => v.Total)
                .HasColumnType("decimal(18,2)");

            // Detalles de venta
            modelBuilder.Entity<DetalleVenta>()
                .Property(d => d.PrecioUnitario)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<DetalleVenta>()
                .Property(d => d.Subtotal)
                .HasColumnType("decimal(18,2)");

            // ========== CONFIGURACIÓN ADICIONAL DE DETALLE VENTA ==========

            // Índice para mejorar consultas por tipo de item
            modelBuilder.Entity<DetalleVenta>()
                .HasIndex(d => d.TipoItem);

            // Índice para ItemReferenciaId (Combo/Tiempo)
            modelBuilder.Entity<DetalleVenta>()
                .HasIndex(d => d.ItemReferenciaId);

            // Índice para ProductoId (ahora nullable)
            modelBuilder.Entity<DetalleVenta>()
                .HasIndex(d => d.ProductoId);

            // ========== CONFIGURACIÓN DE ÍNDICES ==========

            modelBuilder.Entity<Producto>()
                .HasIndex(p => p.CategoriaId);

            modelBuilder.Entity<Producto>()
                .HasIndex(p => p.Estado);

            // ========== CONFIGURACIÓN DE TIEMPO ==========

            modelBuilder.Entity<Tiempo>()
                .ToTable("Tiempo");

            modelBuilder.Entity<Tiempo>()
                .Property(t => t.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Tiempo>()
                .Property(t => t.Total)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Tiempo>()
                .HasIndex(t => t.IdNfc);

            modelBuilder.Entity<Tiempo>()
                .HasIndex(t => t.Estado);

            // ========== CONFIGURACIÓN DE PRECIO TIEMPO ==========

            modelBuilder.Entity<PrecioTiempo>()
                .ToTable("PrecioTiempo");

            modelBuilder.Entity<PrecioTiempo>()
                .Property(pt => pt.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<PrecioTiempo>()
                .Property(pt => pt.Precio)
                .HasColumnType("decimal(18,2)");

            // Configurar valor por defecto para TipoPago
            modelBuilder.Entity<Venta>()
                .Property(v => v.TipoPago)
                .HasDefaultValue(1); // 1 = Efectivo por defecto

            // Índice para mejorar consultas por tipo de pago
            modelBuilder.Entity<Venta>()
                .HasIndex(v => v.TipoPago);
            // Configuración de CorteCaja
            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.EfectivoInicial)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.EfectivoFinal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.TotalVentasEfectivo)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.TotalVentasTarjeta)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.TotalDepositos)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.TotalRetiros)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.Diferencia)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CorteCaja>()
                .Property(c => c.EfectivoEsperado)
                .HasColumnType("decimal(18,2)");

            // Índices para CorteCaja
            modelBuilder.Entity<CorteCaja>()
                .HasIndex(c => c.FechaApertura);

            modelBuilder.Entity<CorteCaja>()
                .HasIndex(c => c.FechaCierre);

            modelBuilder.Entity<CorteCaja>()
                .HasIndex(c => c.EstaCerrado);

            // Configuración de MovimientoCaja
            modelBuilder.Entity<MovimientoCaja>()
                .Property(m => m.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<MovimientoCaja>()
                .Property(m => m.Monto)
                .HasColumnType("decimal(18,2)");

            // Relación CorteCaja -> MovimientoCaja
            modelBuilder.Entity<MovimientoCaja>()
                .HasOne(m => m.CorteCaja)
                .WithMany()
                .HasForeignKey(m => m.CorteCajaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices para MovimientoCaja
            modelBuilder.Entity<MovimientoCaja>()
                .HasIndex(m => m.CorteCajaId);

            modelBuilder.Entity<MovimientoCaja>()
                .HasIndex(m => m.Fecha);

            modelBuilder.Entity<MovimientoCaja>()
                .HasIndex(m => m.TipoMovimiento);


            // ========== SEED DATA: PRECIOS DE TIEMPO PREDEFINIDOS ==========

            modelBuilder.Entity<PrecioTiempo>().HasData(
                new PrecioTiempo
                {
                    Id = 1,
                    Nombre = "1 hora",
                    Minutos = 60,
                    Precio = 100.00m,
                    Orden = 1,
                    Estado = "Activo"
                },
                new PrecioTiempo
                {
                    Id = 2,
                    Nombre = "80 minutos",
                    Minutos = 80,
                    Precio = 130.00m,
                    Orden = 2,
                    Estado = "Activo"
                },
                new PrecioTiempo
                {
                    Id = 3,
                    Nombre = "2 horas",
                    Minutos = 120,
                    Precio = 165.00m,
                    Orden = 3,
                    Estado = "Activo"
                },
                new PrecioTiempo
                {
                    Id = 4,
                    Nombre = "140 minutos",
                    Minutos = 140,
                    Precio = 180.00m,
                    Orden = 4,
                    Estado = "Activo"
                }
            );
        }
    }
}