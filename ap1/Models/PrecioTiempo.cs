using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public class PrecioTiempo
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no debe superar los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los minutos son obligatorios.")]
        [Range(1, int.MaxValue, ErrorMessage = "Los minutos deben ser mayor a cero.")]
        public int Minutos { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor que cero.")]
        public decimal Precio { get; set; }

        [StringLength(200)]
        public string Descripcion { get; set; } = string.Empty;

        public string Estado { get; set; } = "Activo";

        [Required]
        public int Orden { get; set; }
    }
}