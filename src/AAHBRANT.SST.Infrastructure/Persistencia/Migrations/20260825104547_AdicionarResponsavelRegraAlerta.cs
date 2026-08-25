using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarResponsavelRegraAlerta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelUsuarioId",
                table: "RegrasAlerta",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegrasAlerta_ResponsavelUsuarioId",
                table: "RegrasAlerta",
                column: "ResponsavelUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegrasAlerta_Usuarios_ResponsavelUsuarioId",
                table: "RegrasAlerta",
                column: "ResponsavelUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegrasAlerta_Usuarios_ResponsavelUsuarioId",
                table: "RegrasAlerta");

            migrationBuilder.DropIndex(
                name: "IX_RegrasAlerta_ResponsavelUsuarioId",
                table: "RegrasAlerta");

            migrationBuilder.DropColumn(
                name: "ResponsavelUsuarioId",
                table: "RegrasAlerta");
        }
    }
}
