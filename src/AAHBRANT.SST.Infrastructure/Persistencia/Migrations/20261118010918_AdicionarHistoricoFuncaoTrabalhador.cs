using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHistoricoFuncaoTrabalhador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricosFuncaoTrabalhador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoNovaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlteradaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_HistoricosFuncaoTrabalhador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricosFuncaoTrabalhador_Funcoes_FuncaoAnteriorId",
                        column: x => x.FuncaoAnteriorId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosFuncaoTrabalhador_Funcoes_FuncaoNovaId",
                        column: x => x.FuncaoNovaId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosFuncaoTrabalhador_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosFuncaoTrabalhador_FuncaoAnteriorId",
                table: "HistoricosFuncaoTrabalhador",
                column: "FuncaoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosFuncaoTrabalhador_FuncaoNovaId",
                table: "HistoricosFuncaoTrabalhador",
                column: "FuncaoNovaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosFuncaoTrabalhador_TrabalhadorId_AlteradaEm",
                table: "HistoricosFuncaoTrabalhador",
                columns: new[] { "TrabalhadorId", "AlteradaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricosFuncaoTrabalhador");
        }
    }
}
