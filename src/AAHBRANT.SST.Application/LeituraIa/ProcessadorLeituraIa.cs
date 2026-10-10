using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.Ghes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.LeituraIa;

// Executa uma leitura com IA (chamado pelo BackgroundService da Api, um escopo por leitura). Falha vira
// Status=Falhou com mensagem para o usuário; nada é gravado na estrutura da obra aqui — isso só acontece
// no "Cadastrar" (CadastrarLeituraIaCommand), depois da revisão humana.
public class ProcessadorLeituraIa
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly IExtratorTextoPdf _extrator;
    private readonly ILeitorDocumentoSstIa _leitor;
    private readonly INotificacaoTeamsService _notificacao;
    private readonly ILogger<ProcessadorLeituraIa> _logger;

    public ProcessadorLeituraIa(
        IAppDbContext db, IExtratorTextoPdf extrator, ILeitorDocumentoSstIa leitor,
        INotificacaoTeamsService notificacao, ILogger<ProcessadorLeituraIa> logger)
    {
        _db = db;
        _extrator = extrator;
        _leitor = leitor;
        _notificacao = notificacao;
        _logger = logger;
    }

    public async Task ProcessarAsync(Guid leituraId, CancellationToken ct)
    {
        var leitura = await _db.LeiturasDocumentoIa.FirstOrDefaultAsync(l => l.Id == leituraId, ct);
        if (leitura is null || leitura.Status != StatusLeituraIa.Pendente) return;

        leitura.Status = StatusLeituraIa.Lendo;
        leitura.IniciadaEmUtc = DateTime.UtcNow;
        leitura.Etapa = "Extraindo o texto do PDF";
        await _db.SaveChangesAsync(ct);

        try
        {
            var pdf = leitura.Documento == DocumentoLeituraIa.Pgr
                ? await _db.Pgrs.Where(p => p.Id == leitura.DocumentoId).Select(p => p.DocumentoConteudo).FirstOrDefaultAsync(ct)
                : await _db.PcmsoDetalhes.Where(p => p.Id == leitura.DocumentoId).Select(p => p.DocumentoConteudo).FirstOrDefaultAsync(ct);
            if (pdf is not { Length: > 0 })
                throw new LeituraIaException("O documento não tem PDF anexado.");

            var paginas = _extrator.ExtrairPaginas(pdf);
            if (paginas.Sum(p => p.Trim().Length) < 500)
                throw new LeituraIaException("O PDF não tem texto legível (provavelmente foi digitalizado como imagem). Anexe o PDF original do documento.");

            async Task Progresso(string etapa, int feito, int total)
            {
                leitura.Etapa = etapa;
                leitura.PassosConcluidos = feito;
                leitura.PassosTotal = total;
                await _db.SaveChangesAsync(ct);
            }

            var resultado = leitura.Documento == DocumentoLeituraIa.Pgr
                ? await MontarPgrAsync(leitura, await _leitor.LerPgrAsync(paginas, Progresso, ct), ct)
                : await MontarPcmsoAsync(leitura, await _leitor.LerPcmsoAsync(paginas, Progresso, ct), ct);

            leitura.ResultadoJson = JsonSerializer.Serialize(resultado, Json);
            leitura.Status = StatusLeituraIa.Concluida;
            leitura.Etapa = "Pronta para revisão";
            leitura.ConcluidaEmUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await AvisarAsync(leitura, $"Leitura com IA do {NomeDocumento(leitura)} pronta",
                "Abra o documento no SST para revisar o que a IA leu e cadastrar.", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Falha na leitura com IA {LeituraId}", leituraId);
            leitura.Status = StatusLeituraIa.Falhou;
            leitura.Erro = ex is LeituraIaException ? ex.Message : "A IA não conseguiu ler o documento agora. Tente de novo em alguns minutos.";
            leitura.ConcluidaEmUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            await AvisarAsync(leitura, $"Leitura com IA do {NomeDocumento(leitura)} não terminou", leitura.Erro, CancellationToken.None);
        }
    }

    private async Task<ResultadoLeituraIa> MontarPgrAsync(LeituraDocumentoIa leitura, LeituraPgrIa lida, CancellationToken ct)
    {
        var matriz = await MatrizAsync(ct);
        var divergencias = MontagemLeituraIa.ConferirPgr(lida, matriz, DateTime.UtcNow.Date);
        var funcoes = MontagemLeituraIa.CasarFuncoes(
            lida.Ghes.SelectMany(g => g.Funcoes.Select(f => (f.Nome, f.Cbo, (int?)g.Numero, 0))),
            await FuncoesExistentesAsync(ct));

        var atuais = await new ListarGhesObraQueryHandler(_db).Handle(new ListarGhesObraQuery(leitura.ObraId), ct);
        var diferencas = atuais.Count == 0
            ? new List<DiferencaLeituraIa>()
            : MontagemLeituraIa.CompararPgr(
                lida.Ghes,
                atuais.ToDictionary(g => g.Numero, g => g.Funcoes.Select(f => new MontagemLeituraIa.FuncaoAtual(f.FuncaoId, f.FuncaoNome ?? "")).ToList()),
                atuais.SelectMany(g => g.Riscos.Select(r => new MontagemLeituraIa.RiscoAtual(g.Numero, r.Perigo ?? "", (int)r.NivelRisco))).ToList(),
                matriz,
                funcoes.ToDictionary(f => Common.FuncaoSstClassifier.Normalizar(f.NomeDocumento), f => f.FuncaoIdSugerida));

        return new ResultadoLeituraIa("PGR", lida.Cabecalho, null, lida.Ghes, lida.PlanoAcao,
            new List<ImportarEstruturaExamesFuncao>(), funcoes, divergencias, diferencas, atuais.Count > 0);
    }

    private async Task<ResultadoLeituraIa> MontarPcmsoAsync(LeituraDocumentoIa leitura, LeituraPcmsoIa lida, CancellationToken ct)
    {
        var (exames, divergencias) = MontagemLeituraIa.MontarExamesPcmso(lida);

        // Nomes em consulta separada: IgnoreQueryFilters na mesma consulta traria GHE desativados.
        var idsFuncoesGhe = await _db.GheFuncoes
            .Where(f => _db.Ghes.Any(g => g.Id == f.GheId && g.ObraId == leitura.ObraId))
            .Select(f => f.FuncaoId)
            .ToListAsync(ct);
        var nomesGhe = await Common.NomesPorId.FuncoesAsync(_db, idsFuncoesGhe, ct);
        var funcoesGhe = idsFuncoesGhe.Select(id => nomesGhe.TryGetValue(id, out var f) ? f.Nome : "").ToList();
        divergencias.AddRange(MontagemLeituraIa.ConferirPcmso(lida.Cabecalho, exames.Select(e => e.Funcao), funcoesGhe, DateTime.UtcNow.Date));

        var gheDaFuncao = lida.Quadros
            .GroupBy(q => q.Funcao.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => int.TryParse(new string((g.First().Ghe ?? "").Where(char.IsDigit).ToArray()), out var n) ? n : (int?)null,
                StringComparer.OrdinalIgnoreCase);
        var cboDaFuncao = lida.Quadros.GroupBy(q => q.Funcao.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(q => q.Cbo).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
        var funcoes = MontagemLeituraIa.CasarFuncoes(
            exames.Select(e => (e.Funcao, cboDaFuncao.GetValueOrDefault(e.Funcao), gheDaFuncao.GetValueOrDefault(e.Funcao), e.Exames.Count)),
            await FuncoesExistentesAsync(ct));

        var examesAtuais = await _db.ExamesFuncaoObra
            .Where(e => e.ObraId == leitura.ObraId)
            .Select(e => new { e.FuncaoId, e.Exame, e.PeriodicidadeMeses })
            .ToListAsync(ct);
        var nomesExames = await Common.NomesPorId.FuncoesAsync(_db, examesAtuais.Select(e => e.FuncaoId), ct);
        var atuais = examesAtuais
            .Select(e => new MontagemLeituraIa.ExameAtual(nomesExames.TryGetValue(e.FuncaoId, out var f) ? f.Nome : "", e.Exame, e.PeriodicidadeMeses))
            .ToList();
        var diferencas = atuais.Count == 0 ? new List<DiferencaLeituraIa>() : MontagemLeituraIa.CompararPcmso(exames, atuais);

        return new ResultadoLeituraIa("PCMSO", null, lida.Cabecalho, new List<ImportarEstruturaGhe>(), new List<string>(),
            exames, funcoes, divergencias, diferencas, atuais.Count > 0);
    }

    private async Task<Dictionary<(int P, int S), int>> MatrizAsync(CancellationToken ct)
    {
        var matriz = await _db.MatrizRiscoConfigs.Include(m => m.Celulas).FirstOrDefaultAsync(ct)
            ?? throw new LeituraIaException("Nenhuma matriz de risco cadastrada. Configure a matriz antes de ler o PGR.");
        return matriz.Celulas.ToDictionary(c => (c.Probabilidade, c.Severidade), c => (int)c.NivelRisco);
    }

    private async Task<List<MontagemLeituraIa.FuncaoExistente>> FuncoesExistentesAsync(CancellationToken ct) =>
        await _db.Funcoes.Select(f => new MontagemLeituraIa.FuncaoExistente(f.Id, f.Nome)).ToListAsync(ct);

    private async Task AvisarAsync(LeituraDocumentoIa leitura, string titulo, string? descricao, CancellationToken ct)
    {
        if (leitura.CreatedBy is not { } usuarioId) return;
        try
        {
            await _notificacao.EnviarAsync(usuarioId, titulo, descricao, ct);
        }
        catch (Exception ex)
        {
            // Sininho é conveniência: a leitura já está salva e o status aparece na própria tela.
            _logger.LogWarning(ex, "Não foi possível avisar no Teams o fim da leitura {LeituraId}", leitura.Id);
        }
    }

    private static string NomeDocumento(LeituraDocumentoIa l) => l.Documento == DocumentoLeituraIa.Pgr ? "PGR" : "PCMSO";
}

// Falha com mensagem própria para o usuário (as demais viram uma mensagem genérica).
public class LeituraIaException : Exception
{
    public LeituraIaException(string mensagem) : base(mensagem) { }
}
