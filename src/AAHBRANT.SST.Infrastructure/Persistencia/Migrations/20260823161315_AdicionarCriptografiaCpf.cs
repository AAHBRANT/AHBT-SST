using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCriptografiaCpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_Cpf",
                table: "Trabalhadores");

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "Trabalhadores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);

            migrationBuilder.AddColumn<string>(
                name: "CpfHash",
                table: "Trabalhadores",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_CpfHash",
                table: "Trabalhadores",
                column: "CpfHash",
                unique: true,
                filter: "[CpfHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_CpfHash",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "CpfHash",
                table: "Trabalhadores");

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "Trabalhadores",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_Cpf",
                table: "Trabalhadores",
                column: "Cpf",
                unique: true);
        }
    }
}
