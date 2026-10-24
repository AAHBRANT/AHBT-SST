using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarModuloUniforme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoUniformes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FotoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FotoContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_CatalogoUniformes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntregasUniforme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoUniformeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tamanho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    DataEntrega = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoTipo = table.Column<int>(type: "int", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_EntregasUniforme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregasUniforme_CatalogoUniformes_CatalogoUniformeId",
                        column: x => x.CatalogoUniformeId,
                        principalTable: "CatalogoUniformes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntregasUniforme_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EstoquesUniforme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoUniformeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tamanho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Saldo = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EstoquesUniforme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstoquesUniforme_CatalogoUniformes_CatalogoUniformeId",
                        column: x => x.CatalogoUniformeId,
                        principalTable: "CatalogoUniformes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EstoquesUniforme_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatrizUniformeFuncoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoUniformeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MatrizUniformeFuncoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrizUniformeFuncoes_CatalogoUniformes_CatalogoUniformeId",
                        column: x => x.CatalogoUniformeId,
                        principalTable: "CatalogoUniformes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatrizUniformeFuncoes_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrabalhadorTamanhosUniforme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoUniformeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tamanho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_TrabalhadorTamanhosUniforme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrabalhadorTamanhosUniforme_CatalogoUniformes_CatalogoUniformeId",
                        column: x => x.CatalogoUniformeId,
                        principalTable: "CatalogoUniformes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrabalhadorTamanhosUniforme_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesEstoqueUniforme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstoqueUniformeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    SaldoResultante = table.Column<int>(type: "int", nullable: false),
                    EntregaUniformeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_MovimentacoesEstoqueUniforme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueUniforme_EntregasUniforme_EntregaUniformeId",
                        column: x => x.EntregaUniformeId,
                        principalTable: "EntregasUniforme",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueUniforme_EstoquesUniforme_EstoqueUniformeId",
                        column: x => x.EstoqueUniformeId,
                        principalTable: "EstoquesUniforme",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntregasUniforme_CatalogoUniformeId",
                table: "EntregasUniforme",
                column: "CatalogoUniformeId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasUniforme_TrabalhadorId_DataEntrega",
                table: "EntregasUniforme",
                columns: new[] { "TrabalhadorId", "DataEntrega" });

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesUniforme_CatalogoUniformeId_ObraId_Tamanho",
                table: "EstoquesUniforme",
                columns: new[] { "CatalogoUniformeId", "ObraId", "Tamanho" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesUniforme_ObraId",
                table: "EstoquesUniforme",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizUniformeFuncoes_CatalogoUniformeId",
                table: "MatrizUniformeFuncoes",
                column: "CatalogoUniformeId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizUniformeFuncoes_FuncaoId_CatalogoUniformeId",
                table: "MatrizUniformeFuncoes",
                columns: new[] { "FuncaoId", "CatalogoUniformeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueUniforme_EntregaUniformeId",
                table: "MovimentacoesEstoqueUniforme",
                column: "EntregaUniformeId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueUniforme_EstoqueUniformeId_CreatedAtUtc",
                table: "MovimentacoesEstoqueUniforme",
                columns: new[] { "EstoqueUniformeId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TrabalhadorTamanhosUniforme_CatalogoUniformeId",
                table: "TrabalhadorTamanhosUniforme",
                column: "CatalogoUniformeId");

            migrationBuilder.CreateIndex(
                name: "IX_TrabalhadorTamanhosUniforme_TrabalhadorId_CatalogoUniformeId",
                table: "TrabalhadorTamanhosUniforme",
                columns: new[] { "TrabalhadorId", "CatalogoUniformeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatrizUniformeFuncoes");

            migrationBuilder.DropTable(
                name: "MovimentacoesEstoqueUniforme");

            migrationBuilder.DropTable(
                name: "TrabalhadorTamanhosUniforme");

            migrationBuilder.DropTable(
                name: "EntregasUniforme");

            migrationBuilder.DropTable(
                name: "EstoquesUniforme");

            migrationBuilder.DropTable(
                name: "CatalogoUniformes");
        }
    }
}
