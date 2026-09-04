using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ObterSessaoTreinamentoDetalheQuery(Guid Id) : IRequest<SessaoTreinamentoDetalheDto?>;

public class ObterSessaoTreinamentoDetalheQueryHandler : IRequestHandler<ObterSessaoTreinamentoDetalheQuery, SessaoTreinamentoDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterSessaoTreinamentoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SessaoTreinamentoDetalheDto?> Handle(ObterSessaoTreinamentoDetalheQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (sessao is null) return null;

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .ToListAsync(ct);

        var fotosEvidencia = await _db.FotosEvidenciaSessaoTreinamento
            .Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .ToListAsync(ct);

        sessao.Participantes = participantes;
        sessao.FotosEvidencia = fotosEvidencia;

        // Assinaturas dos certificados já gerados (04/09) — um DocumentoAssinatura por Treinamento
        // (EntidadeTipo="Treinamento"), com até 2 signatários: o trabalhador (Biometria, reaproveita
        // a presença) e o instrutor (SessaoLogada). Carregado em lote para não fazer 1 query por linha.
        var treinamentosIds = participantes.Where(p => p.TreinamentoGeradoId is not null).Select(p => p.TreinamentoGeradoId!.Value).ToList();
        var assinaturasBrutas = treinamentosIds.Count == 0
            ? new List<(Guid EntidadeId, Guid TrabalhadorId, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm)>()
            : (await _db.DocumentosAssinatura
                .Where(d => d.EntidadeTipo == nameof(Treinamento) && treinamentosIds.Contains(d.EntidadeId))
                .SelectMany(d => d.Signatarios.Select(s => new { d.EntidadeId, s.TrabalhadorId, s.MetodoAutenticacao, s.AssinadoEm }))
                .ToListAsync(ct))
                .Select(x => (x.EntidadeId, x.TrabalhadorId, x.MetodoAutenticacao, x.AssinadoEm))
                .ToList();
        var assinaturasPorTreinamento = assinaturasBrutas
            .GroupBy(a => a.EntidadeId)
            .ToDictionary(g => g.Key, g => g.Select(a => (a.TrabalhadorId, a.MetodoAutenticacao, a.AssinadoEm)).ToList());

        return new SessaoTreinamentoDetalheDto
        {
            Sessao = ListarSessoesTreinamentoQueryHandler.MapearParaDto(sessao),
            Participantes = participantes
                .OrderBy(p => p.Trabalhador?.Nome)
                .Select(p =>
                {
                    List<(Guid TrabalhadorId, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm)>? assinaturas = null;
                    if (p.TreinamentoGeradoId is not null)
                        assinaturasPorTreinamento.TryGetValue(p.TreinamentoGeradoId.Value, out assinaturas);

                    DateTime? assinadoEmPorMetodo(MetodoAutenticacaoAssinatura metodo)
                    {
                        if (assinaturas is null) return null;
                        foreach (var a in assinaturas)
                        {
                            if (a.MetodoAutenticacao == metodo) return a.AssinadoEm;
                        }
                        return null;
                    }

                    return new ParticipanteSessaoTreinamentoDto
                    {
                        Id = p.Id,
                        TrabalhadorId = p.TrabalhadorId,
                        TrabalhadorNome = p.Trabalhador?.Nome ?? string.Empty,
                        TrabalhadorMatricula = p.Trabalhador?.Matricula,
                        PresencaConfirmadaEm = p.PresencaConfirmadaEm,
                        ScoreConfianca = p.ScoreConfianca,
                        TreinamentoGeradoId = p.TreinamentoGeradoId,
                        CertificadoAssinadoPeloTrabalhadorEm = assinadoEmPorMetodo(MetodoAutenticacaoAssinatura.Biometria),
                        CertificadoAssinadoPeloInstrutorEm = assinadoEmPorMetodo(MetodoAutenticacaoAssinatura.SessaoLogada),
                    };
                }).ToList(),
            FotosEvidencia = fotosEvidencia.Select(f => new FotoEvidenciaSessaoTreinamentoDto { Id = f.Id, Ordem = f.Ordem }).ToList(),
        };
    }
}
