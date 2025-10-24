using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public enum EstadoVenta
    {
        Finalizada = 1,
        Pendiente = 2
    }

    public class Venta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El total debe ser mayor que cero.")]
        public decimal Total { get; set; }

        // NUEVO: Estado de la venta
        [Required]
        public int Estado { get; set; } = (int)EstadoVenta.Finalizada;

        // NUEVO: ID de la tarjeta NFC asociada (solo para ventas pendientes con tiempo)
        [StringLength(50)]
        public string? IdNfc { get; set; }

        // NUEVO: Hora de entrada (para combos con tiempo)
        public DateTime? HoraEntrada { get; set; }

        // NUEVO: Minutos de tiempo incluidos en el combo
        public int? MinutosTiempoCombo { get; set; }

        public ICollection<DetalleVenta> DetallesVenta { get; set; } = new List<DetalleVenta>();

        // Propiedades helper
        [NotMapped]
        public bool EsPendiente => Estado == (int)EstadoVenta.Pendiente;

        [NotMapped]
        public bool EsFinalizada => Estado == (int)EstadoVenta.Finalizada;
    }
}