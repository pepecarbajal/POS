using System.ComponentModel.DataAnnotations;

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
        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}