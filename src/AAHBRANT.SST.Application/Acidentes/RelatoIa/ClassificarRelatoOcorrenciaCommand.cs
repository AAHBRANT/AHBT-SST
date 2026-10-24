using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Transforma o relato de uma ocorrência na sugestão de preenchimento do formulário de registro.
/// Não grava nada: o técnico revisa a sugestão e registra pelo fluxo normal (CriarAcidenteCommand).
/// </summary>
// Complementos: respostas do técnico às perguntas da rodada anterior — o relato é reclassificado
// com elas, e a tela só aplica o resultado nos campos que o técnico ainda não editou à mão.
public record ClassificarRelatoOcorrenciaCommand(
    string Relato,
    Guid ObraId,
    IReadOnlyList<RespostaPerguntaRelato>? Complementos = null) : IRequest<RelatoOcorrenciaSugestaoDto>;

public record RelatoOcorrenciaSugestaoDto(
    TipoOcorrencia Tipo,
    GravidadeAcidente Gravidade,
    string? Local,
    string? Data,
    string? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    Guid? AtividadeId,
    IReadOnlyList<Guid> TrabalhadoresIds,
    IReadOnlyList<PerguntaRelatoOcorrencia> Perguntas,
    IReadOnlyList<string> Avisos);

public class ClassificarRelatoOcorrenciaCommandValidator : AbstractValidator<ClassificarRelatoOcorrenciaCommand>
{
    public ClassificarRelatoOcorrenciaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty().WithMessage("Selecione a obra antes de relatar a ocorrência.");
        RuleFor(x => x.Relato).NotEmpty().WithMessage("Descreva o que aconteceu.").MaximumLength(8000);
        RuleForEach(x => x.Complementos).ChildRules(c =>
        {
            c.RuleFor(r => r.Pergunta).MaximumLength(500);
            c.RuleFor(r => r.Resposta).MaximumLength(2000);
        });
    }
}

public class ClassificarRelatoOcorrenciaCommandHandler : IRequestHandler<ClassificarRelatoOcorrenciaCommand, RelatoOcorrenciaSugestaoDto>
{
    private readonly IAppDbContext _db;
    private readonly IClassificadorRelatoOcorrencia _classificador;

    public ClassificarRelatoOcorrenciaCommandHandler(IAppDbContext db, IClassificadorRelatoOcorrencia classificador)
    {
        _db = db;
        _classificador = classificador;
    }

    public async Task<RelatoOcorrenciaSugestaoDto> Handle(ClassificarRelatoOcorrenciaCommand request, CancellationToken ct)
    {
        var atividades = await _db.Atividades.EmUso(_db)
            .Where(a => a.ObraId == request.ObraId)
            .OrderBy(a => a.Nome)
            .Select(a => new { a.Id, a.Nome })
            .ToListAsync(ct);

        var complementos = (request.Complementos ?? Array.Empty<RespostaPerguntaRelato>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Resposta))
            .ToList();

        var sugestao = await _classificador.ClassificarAsync(
            request.Relato.Trim(),
            complementos,
            AgoraEmBrasilia(),
            atividades.Select(a => a.Nome).ToList(),
            ct);

        var trabalhadores = await _db.Trabalhadores
            .Where(t => t.ObraId == request.ObraId)
            .Select(t => new CorrespondenciaNomesTrabalhadores.Candidato(t.Id, t.Nome))
            .ToListAsync(ct);
        var correspondencia = CorrespondenciaNomesTrabalhadores.Localizar(sugestao.NomesCitados, trabalhadores);

        var atividadeId = string.IsNullOrWhiteSpace(sugestao.Atividade)
            ? null
            : atividades.FirstOrDefault(a => string.Equals(a.Nome, sugestao.Atividade.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

        var gravidade = Enum.TryParse<GravidadeAcidente>(sugestao.Gravidade, out var g) ? g : GravidadeAcidente.SemAfastamento;
        var houveAfastamento = sugestao.HouveAfastamento ?? false;

        // Até 4 perguntas, sem repetir — o técnico responde ali mesmo e o relato é reclassificado.
        var perguntas = sugestao.Perguntas
            .Where(p => !string.IsNullOrWhiteSpace(p.Pergunta))
            .GroupBy(p => p.Pergunta.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PerguntaRelatoOcorrencia(g.First().Campo, LimitarOuNulo(g.Key, 300)!))
            .Take(4)
            .ToList();

        // Avisos não são perguntas: o técnico resolve direto no formulário (ex.: escolher o funcionário).
        var avisos = correspondencia.Pendencias.ToList();
        if (gravidade == GravidadeAcidente.IncapacidadePermanenteParcial)
            avisos.Add("Informe os dias debitados conforme o Quadro III da NBR 14280.");

        // Mesmos limites do CriarAcidenteCommandValidator, para a sugestão nunca ser recusada ao registrar.
        return new RelatoOcorrenciaSugestaoDto(
            Enum.TryParse<TipoOcorrencia>(sugestao.Tipo, out var tipo) ? tipo : TipoOcorrencia.Incidente,
            gravidade,
            LimitarOuNulo(sugestao.Local, 200),
            DateOnly.TryParseExact(sugestao.Data, "yyyy-MM-dd", out var data) ? data.ToString("yyyy-MM-dd") : null,
            TimeOnly.TryParseExact(sugestao.Hora, "HH:mm", out var hora) ? hora.ToString("HH:mm") : null,
            LimitarOuNulo(sugestao.Descricao, 2000) ?? LimitarOuNulo(request.Relato, 2000)!,
            LimitarOuNulo(sugestao.Lesao, 500),
            LimitarOuNulo(sugestao.Consequencia, 500),
            LimitarOuNulo(sugestao.Atendimento, 500),
            houveAfastamento,
            houveAfastamento && sugestao.DiasAfastamento is >= 0 ? sugestao.DiasAfastamento : null,
            atividadeId,
            correspondencia.Localizados,
            perguntas,
            avisos);
    }

    private static string? LimitarOuNulo(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = texto.Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo].TrimEnd();
    }

    // O relato fala em "hoje", "ontem às 9h" — a referência é o horário da obra, não UTC.
    private static DateTime AgoraEmBrasilia()
    {
        TimeZoneInfo fuso;
        try { fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { fuso = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);
    }
}
