using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos;

// Teste E2E 21/10: a turma (CriarSessaoTreinamento) recusava NR-06 com 1h, mas o registro individual
// aceitava e emitia certificado AAHBRANT com a mesma carga — que, pela própria mensagem da turma, não
// tem validade legal. Mesma regra (PadraoTurmaTreinamento) agora vale aqui. Só para certificado
// emitido pela AAHBRANT: o Externo é lançamento do documento de outra instituição, que já vem pronto.
public static class CargaHorariaMinimaTreinamento
{
    public static async Task GarantirAsync(
        IAppDbContext db, Guid cursoTreinamentoId, int cargaHoraria, OrigemCertificadoTreinamento origem, CancellationToken ct)
    {
        if (origem != OrigemCertificadoTreinamento.Aahbrant) return;

        var curso = await db.CursosTreinamento.FirstOrDefaultAsync(c => c.Id == cursoTreinamentoId, ct)
            ?? throw new KeyNotFoundException("Curso de treinamento não encontrado.");
        var padrao = PadraoTurmaTreinamento.De(curso, null);
        if (cargaHoraria < padrao.CargaHorariaMinima)
            throw new InvalidOperationException(
                $"A carga horária mínima de {padrao.Norma ?? curso.Nome} é {padrao.CargaHorariaMinima}h. Abaixo disso o certificado não tem validade legal.");
    }
}
