using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMatrizTreinamentoFuncao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatrizTreinamentoFuncoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MatrizTreinamentoFuncoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrizTreinamentoFuncoes_CursosTreinamento_CursoTreinamentoId",
                        column: x => x.CursoTreinamentoId,
                        principalTable: "CursosTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatrizTreinamentoFuncoes_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatrizTreinamentoFuncoes_CursoTreinamentoId",
                table: "MatrizTreinamentoFuncoes",
                column: "CursoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizTreinamentoFuncoes_FuncaoId_CursoTreinamentoId",
                table: "MatrizTreinamentoFuncoes",
                columns: new[] { "FuncaoId", "CursoTreinamentoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatrizTreinamentoFuncoes");
        }
    }
}
