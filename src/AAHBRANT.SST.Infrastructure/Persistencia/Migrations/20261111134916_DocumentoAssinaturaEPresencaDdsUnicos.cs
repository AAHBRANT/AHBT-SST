using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DocumentoAssinaturaEPresencaDdsUnicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentosAssinatura_EntidadeTipo_EntidadeId",
                table: "DocumentosAssinatura");

            migrationBuilder.DropIndex(
                name: "IX_DdsParticipantes_DdsId_TrabalhadorId",
                table: "DdsParticipantes");

            // Antes do índice único, resolve os documentos duplicados que já existem (11 em produção em
            // 11/11/2026, criados em pares no mesmo segundo). Fica o documento finalizado, ou o de mais
            // assinaturas, ou o mais antigo. As assinaturas dos outros passam para ele quando ainda
            // está aberto e a pessoa não assinou nele do mesmo jeito; os outros são desativados, não
            // apagados. Em produção todos os duplicados estavam sem assinatura.
            migrationBuilder.Sql(@"
WITH ranqueados AS (
    SELECT d.Id, d.EntidadeTipo, d.EntidadeId,
           ROW_NUMBER() OVER (PARTITION BY d.EntidadeTipo, d.EntidadeId
               ORDER BY CASE WHEN d.FinalizadoEm IS NULL THEN 1 ELSE 0 END,
                        (SELECT COUNT(*) FROM DocumentoSignatarios s WHERE s.DocumentoAssinaturaId = d.Id AND s.Ativo = 1) DESC,
                        d.CreatedAtUtc, d.Id) AS ordem
    FROM DocumentosAssinatura d
    WHERE d.Ativo = 1
)
SELECT r.Id AS DuplicadoId, k.Id AS MantidoId
INTO #DocumentosDuplicados
FROM ranqueados r
JOIN ranqueados k ON k.EntidadeTipo = r.EntidadeTipo AND k.EntidadeId = r.EntidadeId AND k.ordem = 1
WHERE r.ordem > 1;

UPDATE s SET s.DocumentoAssinaturaId = x.MantidoId
FROM DocumentoSignatarios s
JOIN #DocumentosDuplicados x ON x.DuplicadoId = s.DocumentoAssinaturaId
JOIN DocumentosAssinatura mantido ON mantido.Id = x.MantidoId
WHERE s.Ativo = 1 AND mantido.FinalizadoEm IS NULL
  AND NOT EXISTS (SELECT 1 FROM DocumentoSignatarios o
                  WHERE o.DocumentoAssinaturaId = x.MantidoId AND o.TrabalhadorId = s.TrabalhadorId
                    AND o.MetodoAutenticacao = s.MetodoAutenticacao
                    AND (o.Papel = s.Papel OR (o.Papel IS NULL AND s.Papel IS NULL)));

UPDATE d SET d.Ativo = 0, d.UpdatedAtUtc = SYSUTCDATETIME()
FROM DocumentosAssinatura d
JOIN #DocumentosDuplicados x ON x.DuplicadoId = d.Id;

DROP TABLE #DocumentosDuplicados;
");

            // Mesma coisa para presença repetida no DDS (nenhuma em produção em 11/11/2026): fica a primeira.
            migrationBuilder.Sql(@"
WITH ranqueadas AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY DdsId, TrabalhadorId ORDER BY CreatedAtUtc, Id) AS ordem
    FROM DdsParticipantes
    WHERE Ativo = 1
)
UPDATE p SET p.Ativo = 0, p.UpdatedAtUtc = SYSUTCDATETIME()
FROM DdsParticipantes p
JOIN ranqueadas r ON r.Id = p.Id
WHERE r.ordem > 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAssinatura_EntidadeTipo_EntidadeId",
                table: "DocumentosAssinatura",
                columns: new[] { "EntidadeTipo", "EntidadeId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DdsParticipantes_DdsId_TrabalhadorId",
                table: "DdsParticipantes",
                columns: new[] { "DdsId", "TrabalhadorId" },
                unique: true,
                filter: "[Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentosAssinatura_EntidadeTipo_EntidadeId",
                table: "DocumentosAssinatura");

            migrationBuilder.DropIndex(
                name: "IX_DdsParticipantes_DdsId_TrabalhadorId",
                table: "DdsParticipantes");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAssinatura_EntidadeTipo_EntidadeId",
                table: "DocumentosAssinatura",
                columns: new[] { "EntidadeTipo", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DdsParticipantes_DdsId_TrabalhadorId",
                table: "DdsParticipantes",
                columns: new[] { "DdsId", "TrabalhadorId" });
        }
    }
}
