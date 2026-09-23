using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCertificadoRetroativoTreinamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue 1 = OrigemCertificadoTreinamento.Aahbrant (o enum começa em 1; o 0 que o
            // EF gera sozinho não é valor válido). Assim todo treinamento já cadastrado continua
            // sendo tratado como interno e segue emitindo o certificado no modelo AAHBRANT.
            migrationBuilder.AddColumn<int>(
                name: "OrigemCertificado",
                table: "Treinamentos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "AtendeNr6",
                table: "CursosTreinamento",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ArquivosCertificadoTreinamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Conteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_ArquivosCertificadoTreinamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArquivosCertificadoTreinamento_Treinamentos_TreinamentoId",
                        column: x => x.TreinamentoId,
                        principalTable: "Treinamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArquivosCertificadoTreinamento_TreinamentoId",
                table: "ArquivosCertificadoTreinamento",
                column: "TreinamentoId",
                unique: true);

            // Marca sozinho os cursos que a entrega de EPI já reconhecia como NR-06 pela heurística
            // de texto do frontend (EntregasTab.ehNormaNr6: sobravam só os dígitos e comparava com
            // "6"). Sem este UPDATE, o deploy desta migration passaria a bloquear a entrega de EPI
            // de todo mundo, porque nenhum curso estaria marcado ainda. Cobre NR-06, NR 06, NR06,
            // NR-6, "6" e "06".
            migrationBuilder.Sql(@"
                UPDATE CursosTreinamento
                SET AtendeNr6 = 1
                WHERE REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ISNULL(NormaReferencia, '')), 'NR', ''), '-', ''), ' ', ''), '.', '') IN ('6', '06');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArquivosCertificadoTreinamento");

            migrationBuilder.DropColumn(
                name: "OrigemCertificado",
                table: "Treinamentos");

            migrationBuilder.DropColumn(
                name: "AtendeNr6",
                table: "CursosTreinamento");
        }
    }
}
