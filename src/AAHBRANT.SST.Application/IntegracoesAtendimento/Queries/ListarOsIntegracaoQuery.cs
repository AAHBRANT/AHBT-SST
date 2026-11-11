using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;

// Etapa da OS: uma linha por pessoa (ler, emitir, assinar) e as pendências iguais agrupadas (uma pendência de 20
// pessoas é UMA linha com os nomes, não 20). LGPD: a pendência de ASO mostra só "Sem ASO apto".
public record ListarOsIntegracaoQuery(Guid IntegracaoId, string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false)
    : IRequest<OsIntegracaoDto>;

public static class SituacaoOsIntegracao
{
    public const string NaoEmitida = "naoEmitida";
    public const string AguardandoFuncionario = "aguardandoFuncionario";
    public const string AguardandoTecnico = "aguardandoTecnico";
    public const string Assinada = "assinada";
    // Há pendência (ex.: Sem ASO apto) e nada foi emitido: "OS pendente", não segura os demais (Q2).
    public const string Pendente = "pendente";
    // Pendência de PGR/engenheiro/obra (não é do RH): segura a conclusão até ser resolvida.
    public const string Bloqueada = "bloqueada";
}

// Regra Q2 (padrão adotado em 11/11/2026): "Concluir" libera quando todas as OS EMITÍVEIS estão assinadas. Quem ficou com
// pendência externa fica "OS pendente" e recebe o registro "Integração realizada" ao assinar a OS depois, pelo perfil.
public static class AvaliadorConclusaoIntegracao
{
    public static bool PodeConcluir(IEnumerable<LinhaOsIntegracaoDto> pessoas)
    {
        var lista = pessoas.ToList();
        // Só pendência individual resolvida pelo RH (ex.: Sem ASO apto) deixa de segurar; PGR/engenheiro/obra seguram.
        // E ao menos uma OS assinada.
        return lista.Any(p => p.Situacao == SituacaoOsIntegracao.Assinada)
            && lista.Where(p => p.Situacao != SituacaoOsIntegracao.Pendente).All(p => p.Situacao == SituacaoOsIntegracao.Assinada);
    }
}

public class ListarOsIntegracaoQueryHandler : IRequestHandler<ListarOsIntegracaoQuery, OsIntegracaoDto>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;

    public ListarOsIntegracaoQueryHandler(IAppDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<OsIntegracaoDto> Handle(ListarOsIntegracaoQuery request, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var participantes = await _db.ParticipantesIntegracao.Where(p => p.IntegracaoAtendimentoId == at.Id).OrderBy(p => p.Nome).ToListAsync(ct);
        var etapaOs = await _db.IntegracaoEtapas.FirstOrDefaultAsync(e => e.IntegracaoAtendimentoId == at.Id && e.Tipo == TipoEtapaIntegracao.OrdemServico, ct);

        var ids = participantes.Select(p => p.TrabalhadorId).ToList();
        var ordens = await _db.OrdensServico.Where(o => ids.Contains(o.TrabalhadorId) && o.ObraId == at.ObraId).ToListAsync(ct);
        var assinaturas = ordens.Count == 0
            ? new Dictionary<Guid, (Guid?, AssinaturaOsDto?, AssinaturaOsDto?)>()
            : await AssinaturasOrdemServico.CarregarAsync(_db, ordens, ct);

        var linhas = new List<LinhaOsIntegracaoDto>();
        var pendenciasPorPessoa = new List<(Guid TrabalhadorId, string Nome, string Tipo, string Titulo, string QuemResolve)>();
        foreach (var p in participantes)
        {
            // Só a OS vigente (a revisão mais recente da obra) conta.
            var vigente = ordens.Where(o => o.TrabalhadorId == p.TrabalhadorId).OrderByDescending(o => o.Revisao).FirstOrDefault();
            if (vigente is not null)
            {
                var (_, funcionario, tecnico) = assinaturas[vigente.Id];
                var situacao = AssinaturasOrdemServico.Situacao(funcionario, tecnico);
                // OS já válida de antes desta integração não vale: cada pessoa lê e assina a própria OS agora.
                var antiga = situacao == SituacaoOrdemServico.Valida && (funcionario is null || funcionario.AssinadoEm < at.IniciadaEmUtc);
                if (!antiga)
                {
                    linhas.Add(new LinhaOsIntegracaoDto(p.TrabalhadorId, p.Nome, p.FuncaoNome,
                        situacao switch
                        {
                            SituacaoOrdemServico.Valida => SituacaoOsIntegracao.Assinada,
                            SituacaoOrdemServico.AguardandoTecnico => SituacaoOsIntegracao.AguardandoTecnico,
                            _ => SituacaoOsIntegracao.AguardandoFuncionario,
                        },
                        vigente.Id, vigente.Numero, funcionario?.AssinadoEm, true, Array.Empty<string>()));
                    continue;
                }
            }

            var minuta = await _mediator.Send(new ObterMinutaOrdemServicoQuery(p.TrabalhadorId), ct);
            var pendencias = minuta?.Pendencias ?? new List<PendenciaOsDto>();
            foreach (var pend in pendencias)
            {
                var (tipo, titulo) = Normalizar(pend);
                pendenciasPorPessoa.Add((p.TrabalhadorId, p.Nome, tipo, titulo, pend.QuemResolve));
            }
            var podeEmitir = minuta is not null && minuta.PodeEmitir;
            var daPessoa = pendenciasPorPessoa.Where(x => x.TrabalhadorId == p.TrabalhadorId).ToList();
            var bloqueia = daPessoa.Any(x => x.QuemResolve != "RH");
            linhas.Add(new LinhaOsIntegracaoDto(p.TrabalhadorId, p.Nome, p.FuncaoNome,
                bloqueia ? SituacaoOsIntegracao.Bloqueada : podeEmitir ? SituacaoOsIntegracao.NaoEmitida : SituacaoOsIntegracao.Pendente,
                null, null, null, podeEmitir,
                daPessoa.Select(x => x.QuemResolve == "RH" ? x.Titulo : $"{x.Titulo} (resolve: {x.QuemResolve})").Distinct().ToList()));
        }

        var agrupadas = pendenciasPorPessoa
            .GroupBy(x => (x.Tipo, x.Titulo))
            .Select(g => new PendenciaAgrupadaOsDto(
                g.Key.Tipo, g.Key.Titulo, g.First().QuemResolve,
                g.Select(x => x.TrabalhadorId).Distinct().ToList(),
                g.Select(x => x.Nome).Distinct().OrderBy(n => n).ToList()))
            .OrderByDescending(g => g.TrabalhadoresIds.Count).ThenBy(g => g.Titulo)
            .ToList();

        var assinadas = linhas.Count(l => l.Situacao == SituacaoOsIntegracao.Assinada);
        return new OsIntegracaoDto(
            at.Id, etapaOs?.Status == StatusEtapaIntegracao.EmAndamento, linhas.Count, assinadas,
            linhas.Count(l => l.Situacao != SituacaoOsIntegracao.Assinada),
            at.Status == StatusIntegracaoAtendimento.EmAndamento && etapaOs?.Status == StatusEtapaIntegracao.EmAndamento
                && AvaliadorConclusaoIntegracao.PodeConcluir(linhas),
            linhas, agrupadas);
    }

    // LGPD: ASO sem aptidão aparece como "Sem ASO apto", sem resultado clínico nem datas.
    private static (string Tipo, string Titulo) Normalizar(PendenciaOsDto p) =>
        p.Tipo is "SemAso" or "AsoInvalido" ? ("SemAsoApto", "Sem ASO apto") : (p.Tipo, p.Titulo);
}
