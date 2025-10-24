using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace POS.Models
{
    public class Combo
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del combo es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string UrlImage { get; set; } = string.Empty;

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0.")]
        public decimal Precio { get; set; }

        // ID del PrecioTiempo asociado (null = sin tiempo)
        public int? PrecioTiempoId { get; set; }

        // Estado del combo: "Activo" o "Inactivo"
        public string Estado { get; set; } = "Activo";

        // Relación con PrecioTiempo
        public PrecioTiempo? PrecioTiempo { get; set; }

        public ICollection<Producto> Productos { get; set; } = new List<Producto>();

        public ICollection<ComboProducto> ComboProductos { get; set; } = new List<ComboProducto>();
    }
}