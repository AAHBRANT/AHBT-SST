using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ghes.Commands;

// Importa de uma vez a estrutura de SST de uma obra, transcrita do PGR e do PCMSO oficiais (pedido de
// 09/10/2026, primeira obra: Parque Roger): GHEs com funções, inventário de riscos (Atividade por GHE
// → Perigo → Risco), quadro de exames por função, plano de ação e pendências de validação técnica.
// Tudo numa transação: ou entra a estrutura inteira, ou nada. Recusa obra que já tem GHE, para não
// duplicar inventário — reimportar exige limpar antes.
public record ImportarEstruturaFuncao(string Nome, string? Cbo, string? DescricaoAtividades, int? QuantidadeExpostos, List<string>? Apelidos);

public record ImportarEstruturaRisco(
    string Tipo,
    string Agente,
    string? Danos,
    string? Avaliacao,
    string? Epc,
    string? Epi,
    int Probabilidade,
    int Severidade,
    string? ClassificacaoDocumento,
    string? Monitoramento);

public record ImportarEstruturaGhe(
    int Numero,
    string? Setor,
    string? JornadaTrabalho,
    string? DescricaoAmbiente,
    string? AtividadesCriticas,
    string? FonteGeradora,
    string? MedidasProtecaoExistentes,
    List<ImportarEstruturaFuncao> Funcoes,
    List<ImportarEstruturaRisco> Riscos);

public record ImportarEstruturaExame(
    string Exame,
    string? Codigo,
    bool Admissional,
    bool Periodico,
    bool RetornoTrabalho,
    bool MudancaRisco,
    bool Demissional,
    int? PeriodicidadeMeses,
    string? Observacao);

public record ImportarEstruturaExamesFuncao(string Funcao, List<ImportarEstruturaExame> Exames);

public record ImportarEstruturaPgr(string Nome, DateTime DataElaboracao, DateTime? DataProximaRevisao, DateTime? DataTermino, string? Descricao);

public record ImportarEstruturaPcmso(string Nome, DateTime DataEmissao, DateTime? Validade, string? Versao, string? MedicoNome, string? MedicoCrm);

public record ImportarEstruturaSstObraCommand(
    Guid ObraId,
    ImportarEstruturaPgr? Pgr,
    ImportarEstruturaPcmso? Pcmso,
    List<ImportarEstruturaGhe> Ghes,
    List<ImportarEstruturaExamesFuncao>? ExamesPorFuncao,
    List<string>? PlanoAcao,
    List<string>? PendenciasValidacao) : IRequest<ImportarEstruturaSstObraResultado>;

public record ImportarEstruturaSstObraResultado(
    Guid? PgrId,
    Guid? PcmsoId,
    int Ghes,
    int Riscos,
    int RiscosSemExposicaoIgnorados,
    int PerigosCriados,
    int Exames,
    int ItensPlanoAcao,
    List<string> FuncoesReaproveitadas,
    List<string> FuncoesCriadas);

public class ImportarEstruturaSstObraCommandValidator : AbstractValidator<ImportarEstruturaSstObraCommand>
{
    public ImportarEstruturaSstObraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Ghes).NotEmpty();
        RuleFor(x => x.Ghes).Must(g => g.Select(x => x.Numero).Distinct().Count() == g.Count)
            .WithMessage("Há GHE com número repetido.");
        RuleForEach(x => x.Ghes).ChildRules(g =>
        {
            g.RuleFor(x => x.Numero).GreaterThan(0);
            g.RuleFor(x => x.Funcoes).NotEmpty();
            g.RuleForEach(x => x.Funcoes).ChildRules(f => f.RuleFor(x => x.Nome).NotEmpty().MaximumLength(200));
            g.RuleForEach(x => x.Riscos).ChildRules(r =>
            {
                r.RuleFor(x => x.Agente).NotEmpty().MaximumLength(200);
                r.RuleFor(x => x.Probabilidade).GreaterThanOrEqualTo(0);
                r.RuleFor(x => x.Severidade).GreaterThanOrEqualTo(0);
            });
        });
        RuleForEach(x => x.ExamesPorFuncao).ChildRules(f =>
        {
            f.RuleFor(x => x.Funcao).NotEmpty();
            f.RuleForEach(x => x.Exames).ChildRules(e => e.RuleFor(x => x.Exame).NotEmpty().MaximumLength(200));
        });
        RuleForEach(x => x.PlanoAcao).NotEmpty().MaximumLength(500);
        RuleForEach(x => x.PendenciasValidacao).NotEmpty().MaximumLength(480);
    }
}

public class ImportarEstruturaSstObraCommandHandler : IRequestHandler<ImportarEstruturaSstObraCommand, ImportarEstruturaSstObraResultado>
{
    // Prefixo dos itens de plano de ação que são pendência de validação técnica (decisão A de
    // 09/10/2026: cadastrar como está no documento e abrir as divergências como pendência do QSMS).
    public const string PrefixoPendencia = "[Validação técnica] ";

    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public ImportarEstruturaSstObraCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<ImportarEstruturaSstObraResultado> Handle(ImportarEstruturaSstObraCommand request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId) || !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        if (await _db.Ghes.AnyAsync(g => g.ObraId == request.ObraId, ct))
            throw new InvalidOperationException("Esta obra já tem GHE cadastrado. Exclua a estrutura atual antes de importar de novo.");

        var matriz = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhuma matriz de risco cadastrada.");

        // Função é global: casa pelo nome normalizado (sem acento/caixa/espaço extra) e pelos apelidos,
        // para não duplicar a função que o G-RH já criou pelo nome do cargo.
        var funcoesPorNome = new Dictionary<string, Funcao>();
        foreach (var f in await _db.Funcoes.ToListAsync(ct))
            funcoesPorNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), f);

        var reaproveitadas = new SortedSet<string>();
        var criadas = new SortedSet<string>();
        Funcao ResolverFuncao(string nome, string? cbo, IEnumerable<string>? apelidos)
        {
            foreach (var candidato in new[] { nome }.Concat(apelidos ?? Enumerable.Empty<string>()))
            {
                if (funcoesPorNome.TryGetValue(FuncaoSstClassifier.Normalizar(candidato), out var existente))
                {
                    if (!criadas.Contains(existente.Nome)) reaproveitadas.Add(existente.Nome);
                    if (string.IsNullOrWhiteSpace(existente.CboCodigo) && !string.IsNullOrWhiteSpace(cbo))
                        existente.CboCodigo = cbo;
                    funcoesPorNome.TryAdd(FuncaoSstClassifier.Normalizar(nome), existente);
                    return existente;
                }
            }
            var nova = new Funcao { Nome = nome.Trim(), CboCodigo = string.IsNullOrWhiteSpace(cbo) ? null : cbo };
            _db.Funcoes.Add(nova);
            funcoesPorNome[FuncaoSstClassifier.Normalizar(nome)] = nova;
            criadas.Add(nova.Nome);
            return nova;
        }

        var perigosPorNome = await _db.Perigos.ToDictionaryAsync(p => p.Nome, p => p, StringComparer.OrdinalIgnoreCase, ct);
        var perigosCriados = 0;
        var riscos = 0;
        var semExposicao = 0;

        foreach (var g in request.Ghes.OrderBy(x => x.Numero))
        {
            var ghe = new Ghe
            {
                ObraId = request.ObraId,
                Numero = g.Numero,
                Setor = g.Setor,
                JornadaTrabalho = g.JornadaTrabalho,
                DescricaoAmbiente = g.DescricaoAmbiente,
                AtividadesCriticas = g.AtividadesCriticas,
                FonteGeradora = g.FonteGeradora,
                MedidasProtecaoExistentes = g.MedidasProtecaoExistentes,
            };
            _db.Ghes.Add(ghe);

            foreach (var f in g.Funcoes)
            {
                ghe.Funcoes.Add(new GheFuncao
                {
                    Funcao = ResolverFuncao(f.Nome, f.Cbo, f.Apelidos),
                    QuantidadeExpostos = f.QuantidadeExpostos,
                    DescricaoAtividades = f.DescricaoAtividades,
                });
            }

            var nomesFuncoes = string.Join(", ", g.Funcoes.Select(f => f.Nome));
            var atividade = new Atividade
            {
                ObraId = request.ObraId,
                Ghe = ghe,
                Nome = Limitar($"GHE {g.Numero:00} · {nomesFuncoes}", 200),
                Descricao = string.Join("\n", g.Funcoes.Where(f => !string.IsNullOrWhiteSpace(f.DescricaoAtividades))
                    .Select(f => $"{f.Nome}: {f.DescricaoAtividades}")),
            };
            _db.Atividades.Add(atividade);

            foreach (var r in g.Riscos)
            {
                // Linha "Não há exposição" do inventário: não é risco, não entra (P/G zerados no documento).
                if (r.Probabilidade == 0 || r.Severidade == 0)
                {
                    semExposicao++;
                    continue;
                }

                var nomePerigo = Limitar(r.Agente.Trim(), 200);
                if (!perigosPorNome.TryGetValue(nomePerigo, out var perigo))
                {
                    perigo = new Perigo { Nome = nomePerigo, Agente = r.Tipo };
                    _db.Perigos.Add(perigo);
                    perigosPorNome[nomePerigo] = perigo;
                    perigosCriados++;
                }

                var celula = matriz.Celulas.FirstOrDefault(c => c.Probabilidade == r.Probabilidade && c.Severidade == r.Severidade)
                    ?? throw new InvalidOperationException(
                        $"A matriz de risco '{matriz.Nome}' não tem célula para P={r.Probabilidade}/S={r.Severidade} (GHE {g.Numero:00} · {r.Agente}).");

                _db.Riscos.Add(new Risco
                {
                    Atividade = atividade,
                    Perigo = perigo,
                    Ambiente = g.DescricaoAmbiente,
                    Exposicao = Juntar(("Tipo", r.Tipo), ("Avaliação", r.Avaliacao), ("Classificação no documento", r.ClassificacaoDocumento)),
                    Consequencia = r.Danos,
                    Probabilidade = r.Probabilidade,
                    Severidade = r.Severidade,
                    NivelRisco = celula.NivelRisco,
                    ControlesExistentes = Juntar(("EPC", r.Epc), ("EPI", r.Epi)),
                    ControlesAdicionais = Juntar(("Monitoramento", r.Monitoramento)),
                });
                riscos++;
            }
        }

        Pgr? pgr = null;
        if (request.Pgr is { } p)
        {
            pgr = new Pgr
            {
                ObraId = request.ObraId,
                Nome = p.Nome,
                Descricao = p.Descricao,
                DataElaboracao = p.DataElaboracao,
                DataProximaRevisao = p.DataProximaRevisao,
                DataTermino = p.DataTermino,
                Status = StatusPgr.Vigente,
            };
            _db.Pgrs.Add(pgr);
        }

        var itensPlano = 0;
        var descricoesPlano = (request.PlanoAcao ?? new())
            .Concat((request.PendenciasValidacao ?? new()).Select(x => PrefixoPendencia + x));
        foreach (var descricao in descricoesPlano)
        {
            if (pgr is null)
                throw new InvalidOperationException("Plano de ação e pendências precisam do PGR na importação.");
            pgr.PlanoDeAcao.Add(new PlanoAcaoItem { Descricao = Limitar(descricao, 500), Status = StatusControleRisco.Pendente });
            itensPlano++;
        }

        PcmsoDetalhe? pcmso = null;
        if (request.Pcmso is { } c)
        {
            pcmso = new PcmsoDetalhe
            {
                NumeroDocumento = await _geradorNumero.GerarAsync("PCMSO", ct),
                ObraId = request.ObraId,
                Nome = c.Nome,
                DataEmissao = c.DataEmissao,
                Validade = c.Validade,
                Versao = c.Versao,
                MedicoResponsavelNome = c.MedicoNome,
                MedicoResponsavelCrm = c.MedicoCrm,
                Status = StatusPcmsoDocumento.EmAprovacao,
            };
            _db.PcmsoDetalhes.Add(pcmso);
        }

        var exames = 0;
        foreach (var ef in request.ExamesPorFuncao ?? new())
        {
            var funcao = ResolverFuncao(ef.Funcao, null, null);
            foreach (var e in ef.Exames)
            {
                _db.ExamesFuncaoObra.Add(new ExameFuncaoObra
                {
                    ObraId = request.ObraId,
                    Funcao = funcao,
                    PcmsoDetalhe = pcmso,
                    Exame = e.Exame.Trim(),
                    CodigoExame = e.Codigo,
                    Admissional = e.Admissional,
                    Periodico = e.Periodico,
                    RetornoTrabalho = e.RetornoTrabalho,
                    MudancaRisco = e.MudancaRisco,
                    Demissional = e.Demissional,
                    PeriodicidadeMeses = e.PeriodicidadeMeses,
                    Observacao = e.Observacao,
                });
                exames++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ImportarEstruturaSstObraResultado(
            pgr?.Id, pcmso?.Id, request.Ghes.Count, riscos, semExposicao, perigosCriados, exames, itensPlano,
            reaproveitadas.ToList(), criadas.ToList());
    }

    private static string Limitar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string? Juntar(params (string Rotulo, string? Valor)[] partes)
    {
        var texto = string.Join("\n", partes.Where(p => !string.IsNullOrWhiteSpace(p.Valor)).Select(p => $"{p.Rotulo}: {p.Valor!.Trim()}"));
        return texto.Length == 0 ? null : texto;
    }
}
