namespace POS.Models
{
    public class ComboProducto
    {
        public int ComboId { get; set; }
        public int ProductoId { get; set; }
        public int Cantidad { get; set; } = 1;

        public Combo? Combo { get; set; }
        public Producto? Producto { get; set; }
    }
}
