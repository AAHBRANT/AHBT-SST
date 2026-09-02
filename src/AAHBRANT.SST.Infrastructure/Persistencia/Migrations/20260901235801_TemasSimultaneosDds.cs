using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TemasSimultaneosDds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrigemTema",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "TopicoPrincipal",
                table: "Dds");

            migrationBuilder.AddColumn<string>(
                name: "Consequencia",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlesAdicionais",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlesExistentes",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerigoDescricao",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerigoNome",
                table: "DdsAtividades",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemaLivreDescricao",
                table: "Dds",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemaLivreNome",
                table: "Dds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Consequencia",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "ControlesAdicionais",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "ControlesExistentes",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "PerigoDescricao",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "PerigoNome",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "TemaLivreDescricao",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "TemaLivreNome",
                table: "Dds");

            migrationBuilder.AddColumn<int>(
                name: "OrigemTema",
                table: "Dds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TopicoPrincipal",
                table: "Dds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
