using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ContratoManualSemGJuri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contratos_GJuriContratoId",
                table: "Contratos");

            migrationBuilder.AlterColumn<string>(
                name: "GJuriContratoId",
                table: "Contratos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_GJuriContratoId",
                table: "Contratos",
                column: "GJuriContratoId",
                unique: true,
                filter: "[GJuriContratoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contratos_GJuriContratoId",
                table: "Contratos");

            migrationBuilder.AlterColumn<string>(
                name: "GJuriContratoId",
                table: "Contratos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_GJuriContratoId",
                table: "Contratos",
                column: "GJuriContratoId",
                unique: true);
        }
    }
}
