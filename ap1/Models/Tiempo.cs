using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public class Tiempo
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El IdNfc es obligatorio.")]
        [StringLength(50, ErrorMessage = "El IdNfc no puede tener más de 50 caracteres.")]
        public string IdNfc { get; set; } = string.Empty;

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        public DateTime HoraEntrada { get; set; }

        public DateTime? HoraSalida { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "El total no puede ser negativo.")]
        public decimal Total { get; set; }

        public string Estado { get; set; } = "Activo";
    }
}