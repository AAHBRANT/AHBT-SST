using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CursosObrigatoriosPgr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CursosObrigatoriosFuncaoObra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrigemSugestao = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PgrId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JustificativaRecusa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecididoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecididoPorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecididoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_CursosObrigatoriosFuncaoObra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CursosObrigatoriosFuncaoObra_CursosTreinamento_CursoTreinamentoId",
                        column: x => x.CursoTreinamentoId,
                        principalTable: "CursosTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CursosObrigatoriosFuncaoObra_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CursosObrigatoriosFuncaoObra_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegrasCursoObrigatorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Norma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CursoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AplicaTodasFuncoes = table.Column<bool>(type: "bit", nullable: false),
                    PalavrasChave = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_RegrasCursoObrigatorio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegrasCursoObrigatorio_CursosTreinamento_CursoTreinamentoId",
                        column: x => x.CursoTreinamentoId,
                        principalTable: "CursosTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CursosObrigatoriosFuncaoObra_CursoTreinamentoId",
                table: "CursosObrigatoriosFuncaoObra",
                column: "CursoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_CursosObrigatoriosFuncaoObra_FuncaoId",
                table: "CursosObrigatoriosFuncaoObra",
                column: "FuncaoId");

            migrationBuilder.CreateIndex(
                name: "IX_CursosObrigatoriosFuncaoObra_ObraId_FuncaoId_CursoTreinamentoId",
                table: "CursosObrigatoriosFuncaoObra",
                columns: new[] { "ObraId", "FuncaoId", "CursoTreinamentoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegrasCursoObrigatorio_CursoTreinamentoId",
                table: "RegrasCursoObrigatorio",
                column: "CursoTreinamentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CursosObrigatoriosFuncaoObra");

            migrationBuilder.DropTable(
                name: "RegrasCursoObrigatorio");
        }
    }
}
