using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarModuloEpc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoEpcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fabricante = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificadoAprovacaoNumero = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificadoAprovacaoValidade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VidaUtilEmMeses = table.Column<int>(type: "int", nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EstoquesEpc_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstalacoesEpc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalInstalacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    DataInstalacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataUltimaInspecao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusUltimaInspecao = table.Column<int>(type: "int", nullable: true),
                    ObservacoesInspecao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataRemocao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_InstalacoesEpc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstalacoesEpc_CatalogoEpcs_CatalogoEpcId",
                        column: x => x.CatalogoEpcId,
                        principalTable: "CatalogoEpcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstalacoesEpc_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    InstalacaoEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                        name: "FK_MovimentacoesEstoqueEpc_EstoquesEpc_EstoqueEpcId",
                        column: x => x.EstoqueEpcId,
                        principalTable: "EstoquesEpc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueEpc_InstalacoesEpc_InstalacaoEpcId",
                        column: x => x.InstalacaoEpcId,
                        principalTable: "InstalacoesEpc",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpc_CatalogoEpcId",
                table: "EstoquesEpc",
                column: "CatalogoEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpc_ObraId",
                table: "EstoquesEpc",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_InstalacoesEpc_CatalogoEpcId",
                table: "InstalacoesEpc",
                column: "CatalogoEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_InstalacoesEpc_ObraId",
                table: "InstalacoesEpc",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueEpc_EstoqueEpcId",
                table: "MovimentacoesEstoqueEpc",
                column: "EstoqueEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueEpc_InstalacaoEpcId",
                table: "MovimentacoesEstoqueEpc",
                column: "InstalacaoEpcId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentacoesEstoqueEpc");

            migrationBuilder.DropTable(
                name: "EstoquesEpc");

            migrationBuilder.DropTable(
                name: "InstalacoesEpc");

            migrationBuilder.DropTable(
                name: "CatalogoEpcs");
        }
    }
}
