using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TornarAzureAdObjectIdOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_AzureAdObjectId",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "AzureAdObjectId",
                table: "Usuarios",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AzureAdObjectId",
                table: "Usuarios",
                column: "AzureAdObjectId",
                unique: true,
                filter: "[AzureAdObjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_AzureAdObjectId",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "AzureAdObjectId",
                table: "Usuarios",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_AzureAdObjectId",
                table: "Usuarios",
                column: "AzureAdObjectId",
                unique: true);
        }
    }
}
