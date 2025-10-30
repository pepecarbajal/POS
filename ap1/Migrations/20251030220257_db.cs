using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace POS.Migrations
{
    /// <inheritdoc />
    public partial class db : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorteCajas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaApertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EfectivoInicial = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EfectivoFinal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVentasEfectivo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVentasTarjeta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDepositos = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRetiros = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EfectivoEsperado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UsuarioCierre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EstaCerrado = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorteCajas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrecioTiempo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Minutos = table.Column<int>(type: "INTEGER", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecioTiempo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tiempo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdNfc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HoraEntrada = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HoraSalida = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tiempo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoPago = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    IdNfc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    HoraEntrada = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MinutosTiempoCombo = table.Column<int>(type: "INTEGER", nullable: true),
                    NombreCliente = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UrlImage = table.Column<string>(type: "TEXT", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stock = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CorteCajaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TipoMovimiento = table.Column<int>(type: "INTEGER", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Concepto = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_CorteCajas_CorteCajaId",
                        column: x => x.CorteCajaId,
                        principalTable: "CorteCajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Combos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UrlImage = table.Column<string>(type: "TEXT", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioTiempoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Estado = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Combos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Combos_PrecioTiempo_PrecioTiempoId",
                        column: x => x.PrecioTiempoId,
                        principalTable: "PrecioTiempo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: true),
                    TipoItem = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemReferenciaId = table.Column<int>(type: "INTEGER", nullable: true),
                    NombreItem = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesVenta_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallesVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComboProductos",
                columns: table => new
                {
                    ComboId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboProductos", x => new { x.ComboId, x.ProductoId });
                    table.ForeignKey(
                        name: "FK_ComboProductos_Combos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Combos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComboProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PrecioTiempo",
                columns: new[] { "Id", "Descripcion", "Estado", "Minutos", "Nombre", "Orden", "Precio" },
                values: new object[,]
                {
                    { 1, "", "Activo", 60, "1 hora", 1, 100.00m },
                    { 2, "", "Activo", 80, "80 minutos", 2, 130.00m },
                    { 3, "", "Activo", 120, "2 horas", 3, 165.00m },
                    { 4, "", "Activo", 140, "140 minutos", 4, 180.00m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComboProductos_ComboId",
                table: "ComboProductos",
                column: "ComboId");

            migrationBuilder.CreateIndex(
                name: "IX_ComboProductos_ProductoId",
                table: "ComboProductos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Combos_PrecioTiempoId",
                table: "Combos",
                column: "PrecioTiempoId");

            migrationBuilder.CreateIndex(
                name: "IX_CorteCajas_EstaCerrado",
                table: "CorteCajas",
                column: "EstaCerrado");

            migrationBuilder.CreateIndex(
                name: "IX_CorteCajas_FechaApertura",
                table: "CorteCajas",
                column: "FechaApertura");

            migrationBuilder.CreateIndex(
                name: "IX_CorteCajas_FechaCierre",
                table: "CorteCajas",
                column: "FechaCierre");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_ItemReferenciaId",
                table: "DetallesVenta",
                column: "ItemReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_ProductoId",
                table: "DetallesVenta",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_TipoItem",
                table: "DetallesVenta",
                column: "TipoItem");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_VentaId",
                table: "DetallesVenta",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_CorteCajaId",
                table: "MovimientosCaja",
                column: "CorteCajaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_Fecha",
                table: "MovimientosCaja",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_TipoMovimiento",
                table: "MovimientosCaja",
                column: "TipoMovimiento");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Estado",
                table: "Productos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Tiempo_Estado",
                table: "Tiempo",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Tiempo_IdNfc",
                table: "Tiempo",
                column: "IdNfc");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Estado",
                table: "Ventas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_IdNfc",
                table: "Ventas",
                column: "IdNfc");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TipoPago",
                table: "Ventas",
                column: "TipoPago");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComboProductos");

            migrationBuilder.DropTable(
                name: "DetallesVenta");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "Tiempo");

            migrationBuilder.DropTable(
                name: "Combos");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "CorteCajas");

            migrationBuilder.DropTable(
                name: "PrecioTiempo");

            migrationBuilder.DropTable(
                name: "Categorias");
        }
    }
}
