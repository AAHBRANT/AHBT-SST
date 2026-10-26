using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class VersionarMatrizRisco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRevisao",
                table: "MatrizRiscoConfigs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisadaPorNome",
                table: "MatrizRiscoConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisadaPorUsuarioId",
                table: "MatrizRiscoConfigs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubstituidaEmUtc",
                table: "MatrizRiscoConfigs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "MatrizRiscoConfigs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Vigente",
                table: "MatrizRiscoConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VigenteDesdeUtc",
                table: "MatrizRiscoConfigs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // As matrizes que já existem viram as versões 1, 2, 3... na ordem de criação, e a mais recente entre as
            // ativas passa a ser a vigente (as anteriores viram histórico e registram quando foram substituídas).
            // Precisa rodar antes do índice único de Versao: as colunas novas entram com 0 em todas as linhas.
            migrationBuilder.Sql(@"
WITH ordenadas AS (
    SELECT Id,
           Ativo,
           CreatedAtUtc,
           ROW_NUMBER() OVER (ORDER BY CreatedAtUtc, Id) AS numero,
           ROW_NUMBER() OVER (PARTITION BY Ativo ORDER BY CreatedAtUtc DESC, Id DESC) AS recencia,
           LEAD(CreatedAtUtc) OVER (PARTITION BY Ativo ORDER BY CreatedAtUtc, Id) AS proxima
    FROM MatrizRiscoConfigs
)
UPDATE m
SET m.Versao = o.numero,
    m.Vigente = CASE WHEN o.Ativo = 1 AND o.recencia = 1 THEN 1 ELSE 0 END,
    m.VigenteDesdeUtc = o.CreatedAtUtc,
    m.SubstituidaEmUtc = CASE WHEN o.Ativo = 1 AND o.recencia = 1 THEN NULL ELSE o.proxima END
FROM MatrizRiscoConfigs AS m
INNER JOIN ordenadas AS o ON o.Id = m.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizRiscoConfigs_Versao",
                table: "MatrizRiscoConfigs",
                column: "Versao",
                unique: true,
                filter: "[Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatrizRiscoConfigs_Versao",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "MotivoRevisao",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "RevisadaPorNome",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "RevisadaPorUsuarioId",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "SubstituidaEmUtc",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "Vigente",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "VigenteDesdeUtc",
                table: "MatrizRiscoConfigs");
        }
    }
}
