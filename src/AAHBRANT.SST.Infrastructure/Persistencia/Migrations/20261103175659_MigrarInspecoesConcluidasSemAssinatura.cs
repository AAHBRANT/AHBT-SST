using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MigrarInspecoesConcluidasSemAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Regra de 03/11/2026: inspeção só fica Concluída depois de assinada. As antigas que foram
            // "concluídas" sem nenhuma assinatura voltam para "Aguardando assinatura" (Status 2 -> 3),
            // para poderem ser assinadas. Só entram as que têm documento de assinatura em andamento e
            // sem signatários. Como o índice único permite uma inspeção em andamento/aguardando por
            // alojamento, veículo ou item, só a mais recente de cada um é migrada, e nenhuma é migrada
            // se já existe outra em andamento ou aguardando. As demais continuam Concluídas.
            migrationBuilder.Sql(@"
;WITH Pendentes AS (
    SELECT i.Id, i.AlojamentoId, i.VeiculoId, i.ItemInspecionavelId,
           ROW_NUMBER() OVER (
               PARTITION BY COALESCE(i.AlojamentoId, i.VeiculoId, i.ItemInspecionavelId)
               ORDER BY i.Data DESC, i.CreatedAtUtc DESC) AS rn
    FROM Inspecoes i
    WHERE i.Status = 2 AND i.Ativo = 1
      AND EXISTS (SELECT 1 FROM DocumentosAssinatura d
                  WHERE d.EntidadeTipo = 'Inspecao' AND d.EntidadeId = i.Id AND d.Ativo = 1 AND d.Status = 1
                    AND NOT EXISTS (SELECT 1 FROM DocumentoSignatarios s WHERE s.DocumentoAssinaturaId = d.Id AND s.Ativo = 1))
      AND NOT EXISTS (SELECT 1 FROM DocumentosAssinatura f
                      WHERE f.EntidadeTipo = 'Inspecao' AND f.EntidadeId = i.Id AND f.Ativo = 1 AND f.Status = 2)
)
UPDATE i SET i.Status = 3
FROM Inspecoes i
JOIN Pendentes p ON p.Id = i.Id
WHERE (COALESCE(p.AlojamentoId, p.VeiculoId, p.ItemInspecionavelId) IS NULL OR p.rn = 1)
  AND NOT EXISTS (
        SELECT 1 FROM Inspecoes o
        WHERE o.Id <> i.Id AND o.Ativo = 1 AND o.Status IN (1, 3)
          AND ((i.AlojamentoId IS NOT NULL AND o.AlojamentoId = i.AlojamentoId)
            OR (i.VeiculoId IS NOT NULL AND o.VeiculoId = i.VeiculoId)
            OR (i.ItemInspecionavelId IS NOT NULL AND o.ItemInspecionavelId = i.ItemInspecionavelId)));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: não dá para distinguir, depois, as que já eram "Aguardando" por fluxo normal.
        }
    }
}
