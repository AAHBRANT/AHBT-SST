using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarParteImagemEAvisarTelegramCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParteImagem",
                table: "TestesConexaoCamera",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AvisarTelegram",
                table: "Cameras",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ParteImagem",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParteImagem",
                table: "TestesConexaoCamera");

            migrationBuilder.DropColumn(
                name: "AvisarTelegram",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ParteImagem",
                table: "Cameras");
        }
    }
}
