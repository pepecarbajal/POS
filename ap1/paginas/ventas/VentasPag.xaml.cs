using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace WPF_ProductTable.paginas.ventas
{
    public partial class VentasPag : Page
    {
        public ObservableCollection<ProductoVenta> Productos { get; set; }

        public VentasPag()
        {
            InitializeComponent();

            // Inicializar productos de ejemplo
            Productos = new ObservableCollection<ProductoVenta>
            {
                new ProductoVenta { Id = 1, Nombre = "Hamburguesa Clásica", Precio = 89.00m, Stock = 25, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 2, Nombre = "Pizza Pepperoni", Precio = 149.00m, Stock = 15, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 3, Nombre = "Refresco Grande", Precio = 35.00m, Stock = 50, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 4, Nombre = "Papas Fritas", Precio = 45.00m, Stock = 30, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 5, Nombre = "Hot Dog Especial", Precio = 65.00m, Stock = 20, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 6, Nombre = "Ensalada César", Precio = 95.00m, Stock = 12, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 7, Nombre = "Tacos al Pastor", Precio = 75.00m, Stock = 40, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 8, Nombre = "Café Americano", Precio = 30.00m, Stock = 60, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 9, Nombre = "Sandwich Club", Precio = 85.00m, Stock = 18, ImagenUrl = "/placeholder.svg?height=140&width=180" },
                new ProductoVenta { Id = 10, Nombre = "Helado Sundae", Precio = 55.00m, Stock = 22, ImagenUrl = "/placeholder.svg?height=140&width=180" }
            };

            ProductosItemsControl.ItemsSource = Productos;
        }
    }

    public class ProductoVenta
    {
        public required int Id { get; set; }
        public required string Nombre { get; set; }
        public required decimal Precio { get; set; }
        public required int Stock { get; set; }
        public required string ImagenUrl { get; set; }
    }
}
