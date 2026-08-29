using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ModuloRequisitosLegaisFase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItensQuestionarioAplicabilidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pergunta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TextoApoio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ItensQuestionarioAplicabilidade", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "RequisitosLegais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Norma = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Artigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Fonte = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RequisitosLegais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RespostasQuestionarioAplicabilidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemQuestionarioAplicabilidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resposta = table.Column<bool>(type: "bit", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RespostasQuestionarioAplicabilidade", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasQuestionarioAplicabilidade_ItensQuestionarioAplicabilidade_ItemQuestionarioAplicabilidadeId",
                        column: x => x.ItemQuestionarioAplicabilidadeId,
                        principalTable: "ItensQuestionarioAplicabilidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespostasQuestionarioAplicabilidade_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequisitoLegalCriterios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequisitoLegalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PerigoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoEquipamento = table.Column<int>(type: "int", nullable: true),
                    ItemQuestionarioAplicabilidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_RequisitoLegalCriterios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitoLegalCriterios_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequisitoLegalCriterios_ItensQuestionarioAplicabilidade_ItemQuestionarioAplicabilidadeId",
                        column: x => x.ItemQuestionarioAplicabilidadeId,
                        principalTable: "ItensQuestionarioAplicabilidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequisitoLegalCriterios_Perigos_PerigoId",
                        column: x => x.PerigoId,
                        principalTable: "Perigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequisitoLegalCriterios_RequisitosLegais_RequisitoLegalId",
                        column: x => x.RequisitoLegalId,
                        principalTable: "RequisitosLegais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "IX_RequisitoLegalCriterios_FuncaoId",
                table: "RequisitoLegalCriterios",
                column: "FuncaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitoLegalCriterios_ItemQuestionarioAplicabilidadeId",
                table: "RequisitoLegalCriterios",
                column: "ItemQuestionarioAplicabilidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitoLegalCriterios_PerigoId",
                table: "RequisitoLegalCriterios",
                column: "PerigoId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitoLegalCriterios_RequisitoLegalId",
                table: "RequisitoLegalCriterios",
                column: "RequisitoLegalId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_Categoria",
                table: "RequisitosLegais",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasQuestionarioAplicabilidade_ItemQuestionarioAplicabilidadeId",
                table: "RespostasQuestionarioAplicabilidade",
                column: "ItemQuestionarioAplicabilidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasQuestionarioAplicabilidade_ObraId_ItemQuestionarioAplicabilidadeId",
                table: "RespostasQuestionarioAplicabilidade",
                columns: new[] { "ObraId", "ItemQuestionarioAplicabilidadeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatrizTreinamentoFuncoes");

            migrationBuilder.DropTable(
                name: "RequisitoLegalCriterios");

            migrationBuilder.DropTable(
                name: "RespostasQuestionarioAplicabilidade");

            migrationBuilder.DropTable(
                name: "RequisitosLegais");

            migrationBuilder.DropTable(
                name: "ItensQuestionarioAplicabilidade");
        }
    }
}
