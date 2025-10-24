using System.ComponentModel.DataAnnotations;

namespace POS.Models
{
    public class ComboProducto
    {
        public int ComboId { get; set; }

        public int ProductoId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; } = 1;

        public Combo? Combo { get; set; }

        public Producto? Producto { get; set; }
    }
}