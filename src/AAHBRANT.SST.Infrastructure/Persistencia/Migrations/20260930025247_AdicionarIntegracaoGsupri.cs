using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIntegracaoGsupri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GsupriObras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GsupriObras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GsupriObras_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GsupriProdutos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Unidade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tamanho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FatorConversao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GsupriProdutos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GsupriRecebimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecebimentoExternoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ObraCodigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DadosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Pendencia = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GsupriRecebimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GsupriRecebimentos_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GsupriEventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventoExternoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RecebimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DadosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GsupriEventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GsupriEventos_GsupriRecebimentos_RecebimentoId",
                        column: x => x.RecebimentoId,
                        principalTable: "GsupriRecebimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GsupriRecebimentoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecebimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemExternoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProdutoCodigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Unidade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProdutoVinculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuantidadeAplicada = table.Column<int>(type: "int", nullable: false),
                    Pendencia = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GsupriRecebimentoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GsupriRecebimentoItens_GsupriProdutos_ProdutoVinculoId",
                        column: x => x.ProdutoVinculoId,
                        principalTable: "GsupriProdutos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GsupriRecebimentoItens_GsupriRecebimentos_RecebimentoId",
                        column: x => x.RecebimentoId,
                        principalTable: "GsupriRecebimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GsupriEventos_EventoExternoId",
                table: "GsupriEventos",
                column: "EventoExternoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GsupriEventos_RecebimentoId",
                table: "GsupriEventos",
                column: "RecebimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_GsupriObras_CodigoExterno",
                table: "GsupriObras",
                column: "CodigoExterno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GsupriObras_ObraId",
                table: "GsupriObras",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_GsupriProdutos_CodigoExterno_Unidade",
                table: "GsupriProdutos",
                columns: new[] { "CodigoExterno", "Unidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GsupriRecebimentoItens_ProdutoVinculoId",
                table: "GsupriRecebimentoItens",
                column: "ProdutoVinculoId");

            migrationBuilder.CreateIndex(
                name: "IX_GsupriRecebimentoItens_RecebimentoId_ItemExternoId",
                table: "GsupriRecebimentoItens",
                columns: new[] { "RecebimentoId", "ItemExternoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GsupriRecebimentos_ObraId",
                table: "GsupriRecebimentos",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_GsupriRecebimentos_RecebimentoExternoId",
                table: "GsupriRecebimentos",
                column: "RecebimentoExternoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GsupriEventos");

            migrationBuilder.DropTable(
                name: "GsupriObras");

            migrationBuilder.DropTable(
                name: "GsupriRecebimentoItens");

            migrationBuilder.DropTable(
                name: "GsupriProdutos");

            migrationBuilder.DropTable(
                name: "GsupriRecebimentos");
        }
    }
}
