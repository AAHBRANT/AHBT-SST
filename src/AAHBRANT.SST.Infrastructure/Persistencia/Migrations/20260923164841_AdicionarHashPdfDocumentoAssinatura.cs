using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHashPdfDocumentoAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArquivoAtualizadoEm",
                table: "DocumentosAssinatura",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashPdf",
                table: "DocumentosAssinatura",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArquivoAtualizadoEm",
                table: "DocumentosAssinatura");

            migrationBuilder.DropColumn(
                name: "HashPdf",
                table: "DocumentosAssinatura");
        }
    }
}
