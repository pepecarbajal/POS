using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    /// <summary>
    /// Enumeración para tipos de items en DetalleVenta
    /// </summary>
    public enum TipoItemVenta
    {
        Producto = 1,
        Combo = 2,
        Tiempo = 3
    }

    public class DetalleVenta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int VentaId { get; set; }

        // CAMBIO: ProductoId ahora es nullable para permitir tiempos sin producto asociado
        public int? ProductoId { get; set; }

        // Tipo de item: 1=Producto, 2=Combo, 3=Tiempo
        [Required]
        public int TipoItem { get; set; } = (int)TipoItemVenta.Producto;

        // ID específico del item según su tipo
        // Si es Combo: ID del Combo
        // Si es Tiempo: ID del Tiempo
        public int? ItemReferenciaId { get; set; }

        // Nombre descriptivo del item
        [Required] // CAMBIO: Ahora es requerido ya que puede no haber producto
        [StringLength(300)]
        public string NombreItem { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecioUnitario { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Subtotal { get; set; }

        // Relaciones
        [ForeignKey("VentaId")]
        public Venta Venta { get; set; } = null!;

        // CAMBIO: Ahora es nullable para reflejar que ProductoId es nullable
        [ForeignKey("ProductoId")]
        public Producto? Producto { get; set; }

        // Propiedades de ayuda para verificar el tipo
        [NotMapped]
        public bool EsProducto => TipoItem == (int)TipoItemVenta.Producto;

        [NotMapped]
        public bool EsCombo => TipoItem == (int)TipoItemVenta.Combo;

        [NotMapped]
        public bool EsTiempo => TipoItem == (int)TipoItemVenta.Tiempo;

        // NUEVA PROPIEDAD: Nombre para mostrar en la UI
        /// <summary>
        /// Obtiene el nombre apropiado para mostrar en la interfaz de usuario.
        /// Si existe un Producto asociado, usa su nombre.
        /// Si no, usa NombreItem (para Tiempos y Combos).
        /// Si ambos son null, retorna "Tiempo" por defecto.
        /// </summary>
        [NotMapped]
        public string NombreParaMostrar => Producto?.Nombre ?? NombreItem ?? "Tiempo";
    }
}