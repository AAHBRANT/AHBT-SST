using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarValidacaoRequisitoLegal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ValidadoEmUtc",
                table: "RequisitosLegais",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidadoPorNome",
                table: "RequisitosLegais",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidadoPorUsuarioId",
                table: "RequisitosLegais",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidadoEmUtc",
                table: "RequisitosLegais");

            migrationBuilder.DropColumn(
                name: "ValidadoPorNome",
                table: "RequisitosLegais");

            migrationBuilder.DropColumn(
                name: "ValidadoPorUsuarioId",
                table: "RequisitosLegais");
        }
    }
}
