using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AjustarIndiceInspecaoEmAndamentoIgnorarExcluidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes",
                column: "AlojamentoId",
                unique: true,
                filter: "[AlojamentoId] IS NOT NULL AND [Status] = 1 AND [Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes",
                column: "VeiculoId",
                unique: true,
                filter: "[VeiculoId] IS NOT NULL AND [Status] = 1 AND [Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes",
                column: "AlojamentoId",
                unique: true,
                filter: "[AlojamentoId] IS NOT NULL AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes",
                column: "VeiculoId",
                unique: true,
                filter: "[VeiculoId] IS NOT NULL AND [Status] = 1");
        }
    }
}
