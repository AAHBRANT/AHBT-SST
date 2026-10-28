using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Instrutores;
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
            ? new List<(Guid EntidadeId, Guid TrabalhadorId, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm, PapelAssinatura? Papel)>()
            : (await _db.DocumentosAssinatura
                .Where(d => d.EntidadeTipo == nameof(Treinamento) && treinamentosIds.Contains(d.EntidadeId))
                .SelectMany(d => d.Signatarios.Select(s => new { d.EntidadeId, s.TrabalhadorId, s.MetodoAutenticacao, s.AssinadoEm, s.Papel }))
                .ToListAsync(ct))
                .Select(x => (x.EntidadeId, x.TrabalhadorId, x.MetodoAutenticacao, x.AssinadoEm, x.Papel))
                .ToList();
        var assinaturasPorTreinamento = assinaturasBrutas
            .GroupBy(a => a.EntidadeId)
            .ToDictionary(g => g.Key, g => g.Select(a => (a.TrabalhadorId, a.MetodoAutenticacao, a.AssinadoEm, a.Papel)).ToList());
        var temInstrutorEscolhido = sessao.InstrutorTrabalhadorId is not null;

        InstrutorSessaoTreinamentoDto? instrutor = null;
        if (sessao.InstrutorTrabalhadorId is { } instrutorId)
        {
            // IgnoreQueryFilters: o instrutor pode ser de outra obra e o escopo de obra o esconderia.
            var nomeInstrutor = await _db.Trabalhadores.IgnoreQueryFilters()
                .Where(t => t.Id == instrutorId).Select(t => t.Nome).FirstOrDefaultAsync(ct);
            var perfil = await _db.PerfisInstrutor.FirstOrDefaultAsync(pf => pf.TrabalhadorId == instrutorId, ct);
            instrutor = new InstrutorSessaoTreinamentoDto
            {
                TrabalhadorId = instrutorId,
                Nome = nomeInstrutor ?? sessao.InstituicaoInstrutor ?? string.Empty,
                FormacaoDescricao = DescricaoInstrutor.Formacao(perfil?.Formacao ?? FormacaoInstrutor.TecnicoSegurancaTrabalho),
                RegistroDescricao = perfil is null ? null : DescricaoInstrutor.Registro(perfil.Conselho, perfil.NumeroRegistro, perfil.UfRegistro),
                PodeAssinarEmOutrasObras = perfil?.PodeAssinarEmOutrasObras ?? false,
                TotalCertificados = treinamentosIds.Count,
                CertificadosAssinados = assinaturasBrutas
                    .Where(a => a.Papel == PapelAssinatura.Instrutor && a.TrabalhadorId == instrutorId)
                    .Select(a => a.EntidadeId).Distinct().Count(),
            };
        }

        return new SessaoTreinamentoDetalheDto
        {
            Instrutor = instrutor,
            Sessao = ListarSessoesTreinamentoQueryHandler.MapearParaDto(sessao),
            Participantes = participantes
                .OrderBy(p => p.Trabalhador?.Nome)
                .Select(p =>
                {
                    List<(Guid TrabalhadorId, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm, PapelAssinatura? Papel)>? assinaturas = null;
                    if (p.TreinamentoGeradoId is not null)
                        assinaturasPorTreinamento.TryGetValue(p.TreinamentoGeradoId.Value, out assinaturas);

                    // Trabalhador: a assinatura do próprio participante (a presença vira assinatura). Instrutor:
                    // o papel Instrutor; nas turmas antigas, sem instrutor escolhido, vale a sessão logada de
                    // quem encerrou a turma, como sempre foi.
                    DateTime? assinadoPeloTrabalhador = assinaturas?
                        .Where(a => a.TrabalhadorId == p.TrabalhadorId && a.Papel != PapelAssinatura.Instrutor)
                        .Select(a => (DateTime?)a.AssinadoEm).FirstOrDefault();
                    DateTime? assinadoPeloInstrutor = assinaturas?
                        .Where(a => a.Papel == PapelAssinatura.Instrutor
                            || (!temInstrutorEscolhido && a.Papel is null && a.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada))
                        .Select(a => (DateTime?)a.AssinadoEm).FirstOrDefault();

                    return new ParticipanteSessaoTreinamentoDto
                    {
                        Id = p.Id,
                        TrabalhadorId = p.TrabalhadorId,
                        TrabalhadorNome = p.Trabalhador?.Nome ?? string.Empty,
                        TrabalhadorMatricula = p.Trabalhador?.Matricula,
                        PresencaConfirmadaEm = p.PresencaConfirmadaEm,
                        ScoreConfianca = p.ScoreConfianca,
                        TreinamentoGeradoId = p.TreinamentoGeradoId,
                        CertificadoAssinadoPeloTrabalhadorEm = assinadoPeloTrabalhador,
                        CertificadoAssinadoPeloInstrutorEm = assinadoPeloInstrutor,
                    };
                }).ToList(),
            FotosEvidencia = fotosEvidencia.Select(f => new FotoEvidenciaSessaoTreinamentoDto { Id = f.Id, Ordem = f.Ordem, DadosFoto = AAHBRANT.SST.Application.Common.DadosCapturaFoto.Ler(f.FotoMetadadosJson) }).ToList(),
        };
    }
}
