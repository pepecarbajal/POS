using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public enum TipoMovimiento
    {
        Deposito = 1,
        Retiro = 2
    }

    public class CorteCaja
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime FechaApertura { get; set; }

        public DateTime? FechaCierre { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal EfectivoInicial { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal EfectivoFinal { get; set; }

        // Totales calculados de ventas
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalVentasEfectivo { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalVentasTarjeta { get; set; }

        // Movimientos del día
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalDepositos { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalRetiros { get; set; }

        // Diferencia (sobrante o faltante)
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Diferencia { get; set; }

        // Efectivo esperado en caja
        [Column(TypeName = "decimal(18, 2)")]
        public decimal EfectivoEsperado { get; set; }

        [StringLength(500)]
        public string? Observaciones { get; set; }

        [StringLength(100)]
        public string? UsuarioCierre { get; set; }

        public bool EstaCerrado { get; set; } = false;

        // Propiedades calculadas
        [NotMapped]
        public decimal TotalVentas => TotalVentasEfectivo + TotalVentasTarjeta;

        [NotMapped]
        public bool HayDiferencia => Math.Abs(Diferencia) > 0.01m;

        [NotMapped]
        public string TipoDiferencia
        {
            get
            {
                if (Math.Abs(Diferencia) < 0.01m) return "Sin diferencia";
                return Diferencia > 0 ? "Sobrante" : "Faltante";
            }
        }
    }

    public class MovimientoCaja
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CorteCajaId { get; set; }

        [ForeignKey("CorteCajaId")]
        public CorteCaja CorteCaja { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required]
        public int TipoMovimiento { get; set; } // 1=Depósito, 2=Retiro

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0.01, double.MaxValue)]
        public decimal Monto { get; set; }

        [Required]
        [StringLength(200)]
        public string Concepto { get; set; }

        [StringLength(500)]
        public string? Observaciones { get; set; }

        [StringLength(100)]
        public string? Usuario { get; set; }

        [NotMapped]
        public string TipoMovimientoTexto => TipoMovimiento == (int)Models.TipoMovimiento.Deposito ? "Depósito" : "Retiro";
    }
}