using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFalhasReconhecimentoFacial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FalhasReconhecimentoFacial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    Confianca = table.Column<double>(type: "float", nullable: true),
                    OcorridaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_FalhasReconhecimentoFacial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FalhasReconhecimentoFacial_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FalhasReconhecimentoFacial_ObraId_OcorridaEm",
                table: "FalhasReconhecimentoFacial",
                columns: new[] { "ObraId", "OcorridaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_FalhasReconhecimentoFacial_TrabalhadorId_OcorridaEm",
                table: "FalhasReconhecimentoFacial",
                columns: new[] { "TrabalhadorId", "OcorridaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FalhasReconhecimentoFacial");
        }
    }
}
