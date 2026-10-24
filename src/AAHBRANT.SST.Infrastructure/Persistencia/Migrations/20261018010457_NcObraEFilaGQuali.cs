using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class NcObraEFilaGQuali : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ObraId",
                table: "NaoConformidades",
                type: "uniqueidentifier",
                nullable: true);

            // Preenche a obra das NCs existentes: atividade → risco (atividade do risco) → item de inspeção.
            migrationBuilder.Sql(@"
UPDATE n SET ObraId = a.ObraId FROM NaoConformidades n JOIN Atividades a ON a.Id = n.AtividadeId WHERE n.ObraId IS NULL;
UPDATE n SET ObraId = a.ObraId FROM NaoConformidades n JOIN Riscos r ON r.Id = n.RiscoId JOIN Atividades a ON a.Id = r.AtividadeId WHERE n.ObraId IS NULL;
UPDATE n SET ObraId = i.ObraId FROM NaoConformidades n JOIN InspecaoItemRespostas ir ON ir.Id = n.InspecaoItemRespostaId JOIN Inspecoes i ON i.Id = ir.InspecaoId WHERE n.ObraId IS NULL;");

            migrationBuilder.CreateTable(
                name: "EventosNcGQuali",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NaoConformidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Revisao = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Situacao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnviandoDesdeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnviadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespostaGQuali = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UltimoErro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosNcGQuali", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NaoConformidades_ObraId",
                table: "NaoConformidades",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosNcGQuali_EventId",
                table: "EventosNcGQuali",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosNcGQuali_NaoConformidadeId",
                table: "EventosNcGQuali",
                column: "NaoConformidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosNcGQuali_Situacao_ProximaTentativaEmUtc",
                table: "EventosNcGQuali",
                columns: new[] { "Situacao", "ProximaTentativaEmUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_NaoConformidades_Obras_ObraId",
                table: "NaoConformidades",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NaoConformidades_Obras_ObraId",
                table: "NaoConformidades");

            migrationBuilder.DropTable(
                name: "EventosNcGQuali");

            migrationBuilder.DropIndex(
                name: "IX_NaoConformidades_ObraId",
                table: "NaoConformidades");

            migrationBuilder.DropColumn(
                name: "ObraId",
                table: "NaoConformidades");
        }
    }
}
