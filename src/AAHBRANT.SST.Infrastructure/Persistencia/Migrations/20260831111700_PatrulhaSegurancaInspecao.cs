using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PatrulhaSegurancaInspecao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescricaoPersonalizada",
                table: "InspecaoItemRespostas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Local",
                table: "InspecaoItemRespostas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanoDeAcao",
                table: "InspecaoItemRespostas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FotoDepoisConteudo",
                table: "InspecaoItemRespostas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoDepoisContentType",
                table: "InspecaoItemRespostas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescricaoPersonalizada",
                table: "InspecaoItemRespostas");

            migrationBuilder.DropColumn(
                name: "Local",
                table: "InspecaoItemRespostas");

            migrationBuilder.DropColumn(
                name: "PlanoDeAcao",
                table: "InspecaoItemRespostas");

            migrationBuilder.DropColumn(
                name: "FotoDepoisConteudo",
                table: "InspecaoItemRespostas");

            migrationBuilder.DropColumn(
                name: "FotoDepoisContentType",
                table: "InspecaoItemRespostas");
        }
    }
}
