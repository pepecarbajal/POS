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

    public enum TipoPago
    {
        Efectivo = 1,
        Tarjeta = 2
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

        // Estado de la venta
        [Required]
        public int Estado { get; set; } = (int)EstadoVenta.Finalizada;

        // NUEVO: Tipo de pago (inicializado en el constructor)
        [Required]
        public int TipoPago { get; set; }

        // ID de la tarjeta NFC asociada (solo para ventas pendientes con tiempo)
        [StringLength(50)]
        public string? IdNfc { get; set; }

        // Hora de entrada (para combos con tiempo)
        public DateTime? HoraEntrada { get; set; }

        // Minutos de tiempo incluidos en el combo
        public int? MinutosTiempoCombo { get; set; }

        // Nombre del cliente/referencia
        [StringLength(100)]
        public string? NombreCliente { get; set; }

        public ICollection<DetalleVenta> DetallesVenta { get; set; } = new List<DetalleVenta>();

        // Constructor para inicializar valores por defecto
        public Venta()
        {
            TipoPago = (int)Models.TipoPago.Efectivo;
        }

        // Propiedades helper
        [NotMapped]
        public bool EsPendiente => Estado == (int)EstadoVenta.Pendiente;

        [NotMapped]
        public bool EsFinalizada => Estado == (int)EstadoVenta.Finalizada;

        [NotMapped]
        public string TipoPagoTexto => TipoPago == (int)Models.TipoPago.Efectivo ? "Efectivo" : "Tarjeta";
    }
}