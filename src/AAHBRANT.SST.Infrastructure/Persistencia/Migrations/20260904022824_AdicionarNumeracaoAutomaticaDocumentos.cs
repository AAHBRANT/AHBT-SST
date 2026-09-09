using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNumeracaoAutomaticaDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumento",
                table: "PcmsoDetalhes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContadoresDocumento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefixo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    UltimoNumero = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContadoresDocumento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContadoresDocumento_Prefixo_Ano",
                table: "ContadoresDocumento",
                columns: new[] { "Prefixo", "Ano" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContadoresDocumento");

            migrationBuilder.DropColumn(
                name: "NumeroDocumento",
                table: "PcmsoDetalhes");
        }
    }
}
