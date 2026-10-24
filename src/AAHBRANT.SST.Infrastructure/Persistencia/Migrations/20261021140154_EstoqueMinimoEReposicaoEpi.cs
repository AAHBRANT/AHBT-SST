using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class EstoqueMinimoEReposicaoEpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrazoReposicaoEpiDias",
                table: "Obras",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "EstoqueMinimo",
                table: "EstoquesEpi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstoqueMinimoDefinidoEm",
                table: "EstoquesEpi",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EstoqueMinimoDefinidoPorUsuarioId",
                table: "EstoquesEpi",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SolicitacoesReposicaoEpi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SolicitanteUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SolicitanteNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrazoReposicaoDias = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SolicitacoesReposicaoEpi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacoesReposicaoEpi_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitacoesReposicaoEpi_Usuarios_SolicitanteUsuarioId",
                        column: x => x.SolicitanteUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolicitacoesReposicaoEpiItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitacaoReposicaoEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpiNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CertificadoAprovacaoNumero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Saldo = table.Column<int>(type: "int", nullable: false),
                    Reservado = table.Column<int>(type: "int", nullable: false),
                    EstoqueMinimo = table.Column<int>(type: "int", nullable: false),
                    QuantidadeSugerida = table.Column<int>(type: "int", nullable: false),
                    TamanhosEmFalta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_SolicitacoesReposicaoEpiItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitacoesReposicaoEpiItens_SolicitacoesReposicaoEpi_SolicitacaoReposicaoEpiId",
                        column: x => x.SolicitacaoReposicaoEpiId,
                        principalTable: "SolicitacoesReposicaoEpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpi_EstoqueMinimoDefinidoPorUsuarioId",
                table: "EstoquesEpi",
                column: "EstoqueMinimoDefinidoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesReposicaoEpi_ObraId_CreatedAtUtc",
                table: "SolicitacoesReposicaoEpi",
                columns: new[] { "ObraId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesReposicaoEpi_SolicitanteUsuarioId",
                table: "SolicitacoesReposicaoEpi",
                column: "SolicitanteUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitacoesReposicaoEpiItens_SolicitacaoReposicaoEpiId",
                table: "SolicitacoesReposicaoEpiItens",
                column: "SolicitacaoReposicaoEpiId");

            migrationBuilder.AddForeignKey(
                name: "FK_EstoquesEpi_Usuarios_EstoqueMinimoDefinidoPorUsuarioId",
                table: "EstoquesEpi",
                column: "EstoqueMinimoDefinidoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EstoquesEpi_Usuarios_EstoqueMinimoDefinidoPorUsuarioId",
                table: "EstoquesEpi");

            migrationBuilder.DropTable(
                name: "SolicitacoesReposicaoEpiItens");

            migrationBuilder.DropTable(
                name: "SolicitacoesReposicaoEpi");

            migrationBuilder.DropIndex(
                name: "IX_EstoquesEpi_EstoqueMinimoDefinidoPorUsuarioId",
                table: "EstoquesEpi");

            migrationBuilder.DropColumn(
                name: "PrazoReposicaoEpiDias",
                table: "Obras");

            migrationBuilder.DropColumn(
                name: "EstoqueMinimo",
                table: "EstoquesEpi");

            migrationBuilder.DropColumn(
                name: "EstoqueMinimoDefinidoEm",
                table: "EstoquesEpi");

            migrationBuilder.DropColumn(
                name: "EstoqueMinimoDefinidoPorUsuarioId",
                table: "EstoquesEpi");
        }
    }
}
