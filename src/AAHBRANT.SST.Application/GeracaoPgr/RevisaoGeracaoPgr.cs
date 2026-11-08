using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Nova revisão do PGR (07/11/2026, mockup pgr-nova-revisao_2026-11-07_v1): parte da última geração aprovada da obra.
// - Mudanca: tudo o que já foi aprovado vem como "Mantido"; o técnico acrescenta itens e só eles passam pela IA.
// - Periodica: nada muda no inventário; o engenheiro confirma que reavaliou os riscos (NR-01) e a geração vai direto
//   ao passo de aprovação, com as mesmas assinaturas de um PGR normal.
public record CriarRevisaoGeracaoPgrCommand(
    Guid ObraId,
    string Tipo,
    DateTime DataElaboracao,
    DateTime? DataProximaRevisao,
    DateTime? DataTermino,
    string? Observacao,
    bool ReavaliacaoConfirmada,
    Guid? UsuarioId,
    string? NomeUsuario) : IRequest<Guid>;

public class CriarRevisaoGeracaoPgrCommandHandler : IRequestHandler<CriarRevisaoGeracaoPgrCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarRevisaoGeracaoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRevisaoGeracaoPgrCommand request, CancellationToken ct)
    {
        if (request.Tipo is not (RevisaoGeracaoPgr.Mudanca or RevisaoGeracaoPgr.Periodica))
            throw new InvalidOperationException("Escolha o motivo da revisão.");
        if (!_db.ObraNoEscopo(request.ObraId) || !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");
        // Próxima revisão é sempre 1 ano após a elaboração (regra da empresa, 07/11/2026): o servidor calcula.
        var proximaRevisao = request.DataElaboracao.Date.AddYears(1);
        if (request.Tipo == RevisaoGeracaoPgr.Periodica && !request.ReavaliacaoConfirmada)
            throw new InvalidOperationException("Na revisão periódica, confirme que reavaliou o inventário de riscos.");

        var emAndamento = await _db.GeracoesPgrEap.AnyAsync(x => x.ObraId == request.ObraId
            && (x.Status == StatusGeracaoPgrEap.Rascunho || x.Status == StatusGeracaoPgrEap.AguardandoAprovacao
                || x.Status == StatusGeracaoPgrEap.Devolvido), ct);
        if (emAndamento)
            throw new InvalidOperationException("Já existe uma geração ou revisão do PGR em andamento nesta obra. Conclua ou exclua antes de abrir outra.");

        var anterior = await _db.GeracoesPgrEap
            .Where(x => x.ObraId == request.ObraId && x.Status == StatusGeracaoPgrEap.Aprovado)
            .OrderByDescending(x => x.AprovadoEmUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Esta obra não tem PGR gerado pela EAP para revisar. Gere o PGR pela EAP.");

        var e = SerializacaoGeracao.Ler(anterior.EstruturaJson);
        foreach (var i in e.Itens) i.Herdado = true;
        foreach (var a in e.Ghes.SelectMany(x => x.Atividades)) a.Herdada = true;
        e.DataTermino = proximaRevisao;

        var nome = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        e.Revisao = new RevisaoGeracaoPgr
        {
            Tipo = request.Tipo,
            GeracaoAnteriorId = anterior.Id,
            Observacao = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim(),
            ReavaliacaoConfirmada = request.Tipo == RevisaoGeracaoPgr.Periodica && request.ReavaliacaoConfirmada,
            ReavaliadoPorNome = request.Tipo == RevisaoGeracaoPgr.Periodica ? nome : null,
            ReavaliadoEmUtc = request.Tipo == RevisaoGeracaoPgr.Periodica ? DateTime.UtcNow : null,
        };

        var g = new GeracaoPgrEap
        {
            ObraId = request.ObraId,
            Nome = anterior.Nome,
            DataElaboracao = request.DataElaboracao,
            DataProximaRevisao = proximaRevisao,
            ElaboradoPorUsuarioId = request.UsuarioId,
            ElaboradoPorNome = nome,
            NomeArquivoEap = anterior.NomeArquivoEap,
            OrigemEap = "Revisão",
            // Periódica abre no passo 4 (inventário já revisado, onde fica "Enviar para aprovação"); mudança abre na EAP (2).
            Passo = request.Tipo == RevisaoGeracaoPgr.Periodica ? 4 : 2,
            EstruturaJson = SerializacaoGeracao.Gravar(e),
        };
        _db.GeracoesPgrEap.Add(g);
        await _db.SaveChangesAsync(ct);
        return g.Id;
    }
}
