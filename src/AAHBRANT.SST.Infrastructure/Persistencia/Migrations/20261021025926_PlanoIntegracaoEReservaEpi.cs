using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PlanoIntegracaoEReservaEpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanosIntegracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EncerradoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssumidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PlanosIntegracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanosIntegracao_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanosIntegracao_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanosIntegracao_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanosIntegracao_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservasEpi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    Tamanho = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EntregaEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_ReservasEpi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasEpi_CatalogoEpis_CatalogoEpiId",
                        column: x => x.CatalogoEpiId,
                        principalTable: "CatalogoEpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasEpi_EntregasEpi_EntregaEpiId",
                        column: x => x.EntregaEpiId,
                        principalTable: "EntregasEpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasEpi_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasEpi_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanosIntegracao_FuncaoId",
                table: "PlanosIntegracao",
                column: "FuncaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanosIntegracao_ObraId_Status",
                table: "PlanosIntegracao",
                columns: new[] { "ObraId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanosIntegracao_ResponsavelUsuarioId",
                table: "PlanosIntegracao",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanosIntegracao_TrabalhadorId_Status",
                table: "PlanosIntegracao",
                columns: new[] { "TrabalhadorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEpi_CatalogoEpiId",
                table: "ReservasEpi",
                column: "CatalogoEpiId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEpi_EntregaEpiId",
                table: "ReservasEpi",
                column: "EntregaEpiId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEpi_ObraId_CatalogoEpiId_Status_CreatedAtUtc",
                table: "ReservasEpi",
                columns: new[] { "ObraId", "CatalogoEpiId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEpi_TrabalhadorId_Status",
                table: "ReservasEpi",
                columns: new[] { "TrabalhadorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanosIntegracao");

            migrationBuilder.DropTable(
                name: "ReservasEpi");
        }
    }
}
