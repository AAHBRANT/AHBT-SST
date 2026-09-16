using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSincronizacaoGrhAso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataUltimaSincronizacao",
                table: "Asos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrhAsoId",
                table: "Asos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asos_GrhAsoId",
                table: "Asos",
                column: "GrhAsoId",
                unique: true,
                filter: "[GrhAsoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Asos_GrhAsoId",
                table: "Asos");

            migrationBuilder.DropColumn(
                name: "DataUltimaSincronizacao",
                table: "Asos");

            migrationBuilder.DropColumn(
                name: "GrhAsoId",
                table: "Asos");
        }
    }
}
