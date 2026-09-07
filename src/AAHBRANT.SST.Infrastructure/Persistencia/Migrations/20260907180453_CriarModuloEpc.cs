using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarModuloEpc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoEpcs",
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
                    table.PrimaryKey("PK_CatalogoEpcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntregasEpc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_EntregasEpc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregasEpc_CatalogoEpcs_CatalogoEpcId",
                        column: x => x.CatalogoEpcId,
                        principalTable: "CatalogoEpcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntregasEpc_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EstoquesEpc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_EstoquesEpc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstoquesEpc_CatalogoEpcs_CatalogoEpcId",
                        column: x => x.CatalogoEpcId,
                        principalTable: "CatalogoEpcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EstoquesEpc_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatrizEpcFuncoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MatrizEpcFuncoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrizEpcFuncoes_CatalogoEpcs_CatalogoEpcId",
                        column: x => x.CatalogoEpcId,
                        principalTable: "CatalogoEpcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatrizEpcFuncoes_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesEstoqueEpc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstoqueEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    SaldoResultante = table.Column<int>(type: "int", nullable: false),
                    EntregaEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MovimentacoesEstoqueEpc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueEpc_EntregasEpc_EntregaEpcId",
                        column: x => x.EntregaEpcId,
                        principalTable: "EntregasEpc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueEpc_EstoquesEpc_EstoqueEpcId",
                        column: x => x.EstoqueEpcId,
                        principalTable: "EstoquesEpc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntregasEpc_CatalogoEpcId",
                table: "EntregasEpc",
                column: "CatalogoEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasEpc_TrabalhadorId_DataEntrega",
                table: "EntregasEpc",
                columns: new[] { "TrabalhadorId", "DataEntrega" });

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpc_CatalogoEpcId_ObraId",
                table: "EstoquesEpc",
                columns: new[] { "CatalogoEpcId", "ObraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpc_ObraId",
                table: "EstoquesEpc",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizEpcFuncoes_CatalogoEpcId",
                table: "MatrizEpcFuncoes",
                column: "CatalogoEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizEpcFuncoes_FuncaoId_CatalogoEpcId",
                table: "MatrizEpcFuncoes",
                columns: new[] { "FuncaoId", "CatalogoEpcId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueEpc_EntregaEpcId",
                table: "MovimentacoesEstoqueEpc",
                column: "EntregaEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueEpc_EstoqueEpcId_CreatedAtUtc",
                table: "MovimentacoesEstoqueEpc",
                columns: new[] { "EstoqueEpcId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatrizEpcFuncoes");

            migrationBuilder.DropTable(
                name: "MovimentacoesEstoqueEpc");

            migrationBuilder.DropTable(
                name: "EntregasEpc");

            migrationBuilder.DropTable(
                name: "EstoquesEpc");

            migrationBuilder.DropTable(
                name: "CatalogoEpcs");
        }
    }
}
