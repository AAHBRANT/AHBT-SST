using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFotoParticipanteDds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoContentType",
                table: "DdsParticipantes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "FotoConteudo",
                table: "DdsParticipantes",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "FotoTipo",
                table: "DdsParticipantes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoContentType",
                table: "DdsParticipantes");

            migrationBuilder.DropColumn(
                name: "FotoConteudo",
                table: "DdsParticipantes");

            migrationBuilder.DropColumn(
                name: "FotoTipo",
                table: "DdsParticipantes");
        }
    }
}
