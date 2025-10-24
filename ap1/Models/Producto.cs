using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace POS.Models
{
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string UrlImage { get; set; } = string.Empty;

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0.")]
        public decimal Precio { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
        public int Stock { get; set; }

        public string Estado { get; set; } = "Activo";

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public int CategoriaId { get; set; }

        public Categoria? Categoria { get; set; }

        public ICollection<Combo> Combos { get; set; } = new List<Combo>();

        public ICollection<ComboProducto> ComboProductos { get; set; } = new List<ComboProducto>();
    }
}