using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RevisoesComDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentoContentType",
                table: "PgrRevisoes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "DocumentoConteudo",
                table: "PgrRevisoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoNomeArquivo",
                table: "PgrRevisoes",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PcmsoDocumentoRevisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PcmsoDetalheId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroRevisao = table.Column<int>(type: "int", nullable: false),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    DocumentoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentoNomeArquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
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
                    table.PrimaryKey("PK_PcmsoDocumentoRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcmsoDocumentoRevisoes_PcmsoDetalhes_PcmsoDetalheId",
                        column: x => x.PcmsoDetalheId,
                        principalTable: "PcmsoDetalhes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoDocumentoRevisoes_PcmsoDetalheId_NumeroRevisao",
                table: "PcmsoDocumentoRevisoes",
                columns: new[] { "PcmsoDetalheId", "NumeroRevisao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcmsoDocumentoRevisoes");

            migrationBuilder.DropColumn(
                name: "DocumentoContentType",
                table: "PgrRevisoes");

            migrationBuilder.DropColumn(
                name: "DocumentoConteudo",
                table: "PgrRevisoes");

            migrationBuilder.DropColumn(
                name: "DocumentoNomeArquivo",
                table: "PgrRevisoes");
        }
    }
}
