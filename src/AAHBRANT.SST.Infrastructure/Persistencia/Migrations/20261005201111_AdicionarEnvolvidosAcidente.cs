using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEnvolvidosAcidente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcidentesEnvolvidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcidenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AcidentesEnvolvidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcidentesEnvolvidos_Acidentes_AcidenteId",
                        column: x => x.AcidenteId,
                        principalTable: "Acidentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcidentesEnvolvidos_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcidentesEnvolvidos_AcidenteId_TrabalhadorId",
                table: "AcidentesEnvolvidos",
                columns: new[] { "AcidenteId", "TrabalhadorId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcidentesEnvolvidos_TrabalhadorId",
                table: "AcidentesEnvolvidos",
                column: "TrabalhadorId");

            // Cada acidente existente já tinha no máximo um funcionário: vira o primeiro envolvido.
            migrationBuilder.Sql(@"
INSERT INTO AcidentesEnvolvidos (Id, AcidenteId, TrabalhadorId, CreatedAtUtc, Origem, Ativo)
SELECT NEWID(), a.Id, a.TrabalhadorId, SYSUTCDATETIME(), 0, 1
FROM Acidentes a
WHERE a.TrabalhadorId IS NOT NULL AND a.Ativo = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcidentesEnvolvidos");
        }
    }
}
