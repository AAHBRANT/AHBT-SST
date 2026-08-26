using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFabricanteESaldoEstoqueCatalogoEpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EntregasEpi_TrabalhadorId",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "AssinaturaColetada",
                table: "EntregasEpi");

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "EntregasEpi",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "EntregasEpi",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantidade",
                table: "EntregasEpi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeDevolucao",
                table: "EntregasEpi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VistoConsorcioResponsavel",
                table: "EntregasEpi",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fabricante",
                table: "CatalogoEpis",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaldoEstoque",
                table: "CatalogoEpis",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EntregasEpi_TrabalhadorId_DataValidade",
                table: "EntregasEpi",
                columns: new[] { "TrabalhadorId", "DataValidade" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EntregasEpi_TrabalhadorId_DataValidade",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "Quantidade",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "QuantidadeDevolucao",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "VistoConsorcioResponsavel",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "Fabricante",
                table: "CatalogoEpis");

            migrationBuilder.DropColumn(
                name: "SaldoEstoque",
                table: "CatalogoEpis");

            migrationBuilder.AddColumn<bool>(
                name: "AssinaturaColetada",
                table: "EntregasEpi",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EntregasEpi_TrabalhadorId",
                table: "EntregasEpi",
                column: "TrabalhadorId");
        }
    }
}
