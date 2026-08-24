using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTelegramTrabalhador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Trabalhadores",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramCodigoVinculo",
                table: "Trabalhadores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramVinculadoEm",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TelegramCodigoVinculo",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TelegramVinculadoEm",
                table: "Trabalhadores");
        }
    }
}
