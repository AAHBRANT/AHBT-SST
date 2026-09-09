using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TornarMatriculaOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_ObraId_Matricula",
                table: "Trabalhadores");

            migrationBuilder.AlterColumn<string>(
                name: "Matricula",
                table: "Trabalhadores",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_ObraId_Matricula",
                table: "Trabalhadores",
                columns: new[] { "ObraId", "Matricula" },
                unique: true,
                filter: "[Matricula] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_ObraId_Matricula",
                table: "Trabalhadores");

            migrationBuilder.AlterColumn<string>(
                name: "Matricula",
                table: "Trabalhadores",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_ObraId_Matricula",
                table: "Trabalhadores",
                columns: new[] { "ObraId", "Matricula" },
                unique: true);
        }
    }
}
