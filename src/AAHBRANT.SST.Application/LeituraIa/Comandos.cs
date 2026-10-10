using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.LeituraIa;

public record LeituraIaDto(
    Guid Id,
    DocumentoLeituraIa Documento,
    Guid DocumentoId,
    Guid ObraId,
    StatusLeituraIa Status,
    string? Etapa,
    int PassosConcluidos,
    int PassosTotal,
    string? Erro,
    DateTime CriadaEmUtc,
    DateTime? ConcluidaEmUtc,
    DateTime? CadastradaEmUtc,
    ResultadoLeituraIa? Resultado);

internal static class LeituraIaMapeamento
{
    public static LeituraIaDto ParaDto(LeituraDocumentoIa l, bool comResultado) => new(
        l.Id, l.Documento, l.DocumentoId, l.ObraId, l.Status, l.Etapa, l.PassosConcluidos, l.PassosTotal, l.Erro,
        l.CreatedAtUtc, l.ConcluidaEmUtc, l.CadastradaEmUtc,
        comResultado && l.ResultadoJson is not null
            ? JsonSerializer.Deserialize<ResultadoLeituraIa>(l.ResultadoJson, ProcessadorLeituraIa.Json)
            : null);
}

// ---------- Iniciar ----------

public record IniciarLeituraIaCommand(DocumentoLeituraIa Documento, Guid DocumentoId) : IRequest<Guid>;

public class IniciarLeituraIaCommandHandler : IRequestHandler<IniciarLeituraIaCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IFilaLeituraIa _fila;

    public IniciarLeituraIaCommandHandler(IAppDbContext db, IFilaLeituraIa fila)
    {
        _db = db;
        _fila = fila;
    }

    public async Task<Guid> Handle(IniciarLeituraIaCommand request, CancellationToken ct)
    {
        // Pgrs/PcmsoDetalhes têm filtro por obra: documento de obra de fora não é encontrado (404).
        Guid obraId;
        bool temPdf;
        Guid? revisaoId;
        if (request.Documento == DocumentoLeituraIa.Pgr)
        {
            var pgr = await _db.Pgrs.Where(p => p.Id == request.DocumentoId)
                .Select(p => new { p.ObraId, TemPdf = p.DocumentoConteudo != null })
                .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("PGR não encontrado.");
            (obraId, temPdf) = (pgr.ObraId, pgr.TemPdf);
            revisaoId = await _db.PgrRevisoes.Where(r => r.PgrId == request.DocumentoId && r.DocumentoConteudo != null)
                .OrderByDescending(r => r.NumeroRevisao).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        }
        else
        {
            var pcmso = await _db.PcmsoDetalhes.Where(p => p.Id == request.DocumentoId)
                .Select(p => new { p.ObraId, TemPdf = p.DocumentoConteudo != null })
                .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("PCMSO não encontrado.");
            if (pcmso.ObraId is null)
                throw new InvalidOperationException("Vincule o PCMSO a uma obra antes de ler com IA.");
            (obraId, temPdf) = (pcmso.ObraId.Value, pcmso.TemPdf);
            revisaoId = await _db.PcmsoRevisoes.Where(r => r.PcmsoDetalheId == request.DocumentoId && r.DocumentoConteudo != null)
                .OrderByDescending(r => r.NumeroRevisao).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        }
        if (!temPdf)
            throw new InvalidOperationException("Anexe o PDF do documento antes de ler com IA.");

        // Uma leitura por vez por documento: pedir de novo enquanto lê devolve a que está em andamento.
        var emAndamento = await _db.LeiturasDocumentoIa
            .Where(l => l.Documento == request.Documento && l.DocumentoId == request.DocumentoId
                && (l.Status == StatusLeituraIa.Pendente || l.Status == StatusLeituraIa.Lendo))
            .Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
        if (emAndamento is { } existente) return existente;

        var leitura = new LeituraDocumentoIa
        {
            Documento = request.Documento,
            DocumentoId = request.DocumentoId,
            RevisaoId = revisaoId,
            ObraId = obraId,
            Etapa = "Na fila",
        };
        _db.LeiturasDocumentoIa.Add(leitura);
        await _db.SaveChangesAsync(ct);
        await _fila.EnfileirarAsync(leitura.Id, ct);
        return leitura.Id;
    }
}

// ---------- Consultar ----------

public record ObterLeituraIaQuery(Guid Id, DocumentoLeituraIa Documento) : IRequest<LeituraIaDto?>;

public class ObterLeituraIaQueryHandler : IRequestHandler<ObterLeituraIaQuery, LeituraIaDto?>
{
    private readonly IAppDbContext _db;

    public ObterLeituraIaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<LeituraIaDto?> Handle(ObterLeituraIaQuery request, CancellationToken ct)
    {
        var l = await _db.LeiturasDocumentoIa.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.Documento == request.Documento, ct);
        return l is null ? null : LeituraIaMapeamento.ParaDto(l, comResultado: true);
    }
}

// Última leitura do documento (para o painel da aba PGR/PCMSO), sem o resultado.
public record UltimaLeituraIaQuery(DocumentoLeituraIa Documento, Guid DocumentoId) : IRequest<LeituraIaDto?>;

public class UltimaLeituraIaQueryHandler : IRequestHandler<UltimaLeituraIaQuery, LeituraIaDto?>
{
    private readonly IAppDbContext _db;

    public UltimaLeituraIaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<LeituraIaDto?> Handle(UltimaLeituraIaQuery request, CancellationToken ct)
    {
        var l = await _db.LeiturasDocumentoIa.AsNoTracking()
            .Where(x => x.Documento == request.Documento && x.DocumentoId == request.DocumentoId && x.Status != StatusLeituraIa.Descartada)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new LeituraDocumentoIa
            {
                Id = x.Id, Documento = x.Documento, DocumentoId = x.DocumentoId, ObraId = x.ObraId, Status = x.Status,
                Etapa = x.Etapa, PassosConcluidos = x.PassosConcluidos, PassosTotal = x.PassosTotal, Erro = x.Erro,
                CreatedAtUtc = x.CreatedAtUtc, ConcluidaEmUtc = x.ConcluidaEmUtc, CadastradaEmUtc = x.CadastradaEmUtc,
            })
            .FirstOrDefaultAsync(ct);
        return l is null ? null : LeituraIaMapeamento.ParaDto(l, comResultado: false);
    }
}

// ---------- Descartar ----------

public record DescartarLeituraIaCommand(Guid Id, DocumentoLeituraIa Documento) : IRequest;

public class DescartarLeituraIaCommandHandler : IRequestHandler<DescartarLeituraIaCommand>
{
    private readonly IAppDbContext _db;

    public DescartarLeituraIaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DescartarLeituraIaCommand request, CancellationToken ct)
    {
        var l = await _db.LeiturasDocumentoIa.FirstOrDefaultAsync(x => x.Id == request.Id && x.Documento == request.Documento, ct)
            ?? throw new KeyNotFoundException("Leitura não encontrada.");
        if (l.Status is not (StatusLeituraIa.Concluida or StatusLeituraIa.Falhou))
            throw new InvalidOperationException("Só dá para descartar uma leitura concluída ou que falhou.");
        l.Status = StatusLeituraIa.Descartada;
        await _db.SaveChangesAsync(ct);
    }
}

// ---------- Cadastrar ----------

// FuncaoId nulo = criar a função com o nome do documento.
public record MapeamentoFuncaoLeituraIa(string NomeDocumento, Guid? FuncaoId);

public record CadastrarLeituraIaCommand(Guid Id, DocumentoLeituraIa Documento, List<MapeamentoFuncaoLeituraIa> Funcoes)
    : IRequest<ImportarEstruturaSstObraResultado>;

public class CadastrarLeituraIaCommandHandler : IRequestHandler<CadastrarLeituraIaCommand, ImportarEstruturaSstObraResultado>
{
    private readonly IAppDbContext _db;
    private readonly ISender _mediator;

    public CadastrarLeituraIaCommandHandler(IAppDbContext db, ISender mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<ImportarEstruturaSstObraResultado> Handle(CadastrarLeituraIaCommand request, CancellationToken ct)
    {
        var leitura = await _db.LeiturasDocumentoIa.FirstOrDefaultAsync(x => x.Id == request.Id && x.Documento == request.Documento, ct)
            ?? throw new KeyNotFoundException("Leitura não encontrada.");
        if (leitura.Status != StatusLeituraIa.Concluida || leitura.ResultadoJson is null)
            throw new InvalidOperationException("Esta leitura não está pronta para cadastro.");

        var resultado = JsonSerializer.Deserialize<ResultadoLeituraIa>(leitura.ResultadoJson, ProcessadorLeituraIa.Json)!;
        var escolha = request.Funcoes
            .GroupBy(m => Common.FuncaoSstClassifier.Normalizar(m.NomeDocumento))
            .ToDictionary(g => g.Key, g => g.First().FuncaoId);
        Guid? FuncaoEscolhida(string nome) => escolha.GetValueOrDefault(Common.FuncaoSstClassifier.Normalizar(nome));

        var pendencias = resultado.Divergencias.Select(d => d.Texto).ToList();
        ImportarEstruturaSstObraCommand comando;
        if (leitura.Documento == DocumentoLeituraIa.Pgr)
        {
            var ghes = resultado.Ghes
                .Select(g => g with { Funcoes = g.Funcoes.Select(f => f with { FuncaoId = FuncaoEscolhida(f.Nome) }).ToList() })
                .ToList();
            comando = new ImportarEstruturaSstObraCommand(
                leitura.ObraId, null, null, ghes, null, resultado.PlanoAcao, pendencias,
                PgrIdExistente: leitura.DocumentoId, SubstituirEstruturaAtual: true);

            // Vigência lida do documento atualiza o cadastro do PGR (é o mesmo documento, nova revisão).
            var pgr = await _db.Pgrs.FirstAsync(p => p.Id == leitura.DocumentoId, ct);
            if (resultado.CabecalhoPgr?.Termino is { } termino) pgr.DataTermino = termino;
            if (resultado.CabecalhoPgr?.RevisaoSugerida is { } revisao) pgr.DataProximaRevisao = revisao;
        }
        else
        {
            var exames = resultado.ExamesPorFuncao.Select(e => e with { FuncaoId = FuncaoEscolhida(e.Funcao) }).ToList();
            comando = new ImportarEstruturaSstObraCommand(
                leitura.ObraId, null, null, new List<ImportarEstruturaGhe>(), exames, null, pendencias,
                PcmsoIdExistente: leitura.DocumentoId, SubstituirEstruturaAtual: true);

            var pcmso = await _db.PcmsoDetalhes.FirstAsync(p => p.Id == leitura.DocumentoId, ct);
            if (resultado.CabecalhoPcmso is { } cab)
            {
                if (cab.Validade is { } validade) pcmso.Validade = validade;
                if (!string.IsNullOrWhiteSpace(cab.MedicoNome)) pcmso.MedicoResponsavelNome = cab.MedicoNome;
                if (!string.IsNullOrWhiteSpace(cab.MedicoCrm)) pcmso.MedicoResponsavelCrm = cab.MedicoCrm;
            }
        }

        leitura.Status = StatusLeituraIa.Cadastrada;
        leitura.CadastradaEmUtc = DateTime.UtcNow;
        // O importador grava tudo (inclusive as alterações acima) num único SaveChanges.
        return await _mediator.Send(comando, ct);
    }
}
