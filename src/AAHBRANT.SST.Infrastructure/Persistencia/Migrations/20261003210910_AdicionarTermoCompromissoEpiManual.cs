using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTermoCompromissoEpiManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TermosCompromissoEpiManual",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataAssinaturaPapel = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegistradoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArquivoNome = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ArquivoContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ArquivoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ArquivoTamanhoBytes = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TermosCompromissoEpiManual", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermosCompromissoEpiManual_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TermosCompromissoEpiManual_Usuarios_RegistradoPorUsuarioId",
                        column: x => x.RegistradoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TermosCompromissoEpiManual_RegistradoPorUsuarioId",
                table: "TermosCompromissoEpiManual",
                column: "RegistradoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TermosCompromissoEpiManual_TrabalhadorId",
                table: "TermosCompromissoEpiManual",
                column: "TrabalhadorId",
                unique: true,
                filter: "[Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TermosCompromissoEpiManual");
        }
    }
}
