using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarObraIdDocumentoAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ObraId",
                table: "DocumentosAssinatura",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAssinatura_ObraId",
                table: "DocumentosAssinatura",
                column: "ObraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentosAssinatura_ObraId",
                table: "DocumentosAssinatura");

            migrationBuilder.DropColumn(
                name: "ObraId",
                table: "DocumentosAssinatura");
        }
    }
}
