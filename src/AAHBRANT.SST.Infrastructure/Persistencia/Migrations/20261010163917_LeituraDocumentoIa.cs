using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LeituraDocumentoIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeiturasDocumentoIa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Documento = table.Column<int>(type: "int", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Etapa = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PassosConcluidos = table.Column<int>(type: "int", nullable: false),
                    PassosTotal = table.Column<int>(type: "int", nullable: false),
                    ResultadoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Erro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IniciadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CadastradaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_LeiturasDocumentoIa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasDocumentoIa_Documento_DocumentoId",
                table: "LeiturasDocumentoIa",
                columns: new[] { "Documento", "DocumentoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeiturasDocumentoIa");
        }
    }
}
