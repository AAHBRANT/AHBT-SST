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
    IReadOnlyList<string> Avisos,
    IReadOnlyList<PessoaExternaSugeridaDto> PessoasExternasSugeridas);

/// <summary>Pessoa que a IA entendeu não ser funcionário da obra (terceiro, visitante, transeunte...).</summary>
public record PessoaExternaSugeridaDto(string Nome, VinculoPessoaExterna Vinculo, string? Trecho);

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
        var externasCitadas = NormalizarPessoasExternas(sugestao.PessoasExternasCitadas);
        var correspondencia = CorrespondenciaNomesTrabalhadores.Localizar(sugestao.NomesCitados, trabalhadores, externasCitadas);
        var pessoasExternas = correspondencia.Externas
            .Select(p => new PessoaExternaSugeridaDto(p.Nome!, NormalizarVinculo(p.VinculoProvavel), p.Trecho))
            .ToList();

        var atividadeId = string.IsNullOrWhiteSpace(sugestao.Atividade)
            ? null
            : atividades.FirstOrDefault(a => string.Equals(a.Nome, sugestao.Atividade.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

        var gravidade = Enum.TryParse<GravidadeAcidente>(sugestao.Gravidade, out var g) ? g : GravidadeAcidente.SemAfastamento;
        var houveAfastamento = sugestao.HouveAfastamento ?? false;

        // Até 4 perguntas, sem repetir — o técnico responde ali mesmo e o relato é reclassificado.
        // A IA às vezes volta a perguntar o que já foi respondido (visto ao vivo com o técnico): pergunta
        // sobre campo que esta sugestão já preenche, ou igual a uma já respondida, não volta para a tela.
        var jaRespondidas = complementos.Select(c => c.Pergunta.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var perguntas = sugestao.Perguntas
            .Where(p => !string.IsNullOrWhiteSpace(p.Pergunta))
            .Where(p => !jaRespondidas.Contains(p.Pergunta.Trim()))
            .Where(p => !CampoJaPreenchido(p.Campo, sugestao, atividadeId, correspondencia.Localizados.Count))
            .GroupBy(p => p.Pergunta.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PerguntaRelatoOcorrencia(g.First().Campo, LimitarOuNulo(g.Key, 300)!))
            .Take(4)
            .ToList();

        // Avisos não são perguntas: o técnico resolve direto no formulário (ex.: escolher o funcionário).
        var avisos = correspondencia.Pendencias.ToList();
        if (gravidade == GravidadeAcidente.IncapacidadePermanenteParcial)
            avisos.Add("Informe os dias debitados conforme o Quadro III da NBR 14280.");

        var tipo = Enum.TryParse<TipoOcorrencia>(sugestao.Tipo, out var t) ? t : TipoOcorrencia.Incidente;
        if (tipo == TipoOcorrencia.OcorrenciaComPessoaExterna)
        {
            if (correspondencia.LocalizadosDeExternas.Count > 0)
            {
                // A IA chamou de externo alguém que é funcionário da obra: ele provavelmente se feriu, então o
                // tipo vira Acidente (as demais pessoas externas continuam na lista) e afastamento fica como a IA disse.
                tipo = TipoOcorrencia.Acidente;
            }
            else if (pessoasExternas.Count == 0)
            {
                // Sem pessoa externa o tipo não se sustenta: o registro exigiria ao menos uma.
                tipo = correspondencia.Localizados.Count > 0 ? TipoOcorrencia.Acidente : TipoOcorrencia.Incidente;
            }
            else
            {
                // Pessoa externa não tem afastamento: dias e afastamento não são sugeridos.
                houveAfastamento = false;
                gravidade = GravidadeAcidente.SemAfastamento;
            }
        }

        // Mesmos limites do CriarAcidenteCommandValidator, para a sugestão nunca ser recusada ao registrar.
        return new RelatoOcorrenciaSugestaoDto(
            tipo,
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
            avisos,
            pessoasExternas);
    }

    private const int MaximoPessoasExternas = 10;

    // Limites de nome (200) e trecho (300); sem nome não há pessoa; duplicata por nome (sem acento/caixa) some.
    private static List<PessoaExternaCitadaIa> NormalizarPessoasExternas(IReadOnlyList<PessoaExternaCitadaIa>? citadas)
    {
        var resultado = new List<PessoaExternaCitadaIa>();
        var vistos = new HashSet<string>();
        foreach (var p in citadas ?? Array.Empty<PessoaExternaCitadaIa>())
        {
            var nome = LimitarOuNulo(p.Nome is null ? null : string.Join(' ', p.Nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), 200);
            if (nome is null) continue;
            if (!vistos.Add(SemAcentoECaixa(nome))) continue;
            resultado.Add(new PessoaExternaCitadaIa(nome, p.VinculoProvavel, LimitarOuNulo(p.Trecho, 300)));
            if (resultado.Count == MaximoPessoasExternas) break;
        }
        return resultado;
    }

    public static VinculoPessoaExterna NormalizarVinculo(string? texto)
    {
        var chave = SemAcentoECaixa(texto ?? string.Empty);
        return chave switch
        {
            "terceiro" or "terceirizado" => VinculoPessoaExterna.Terceiro,
            "visitante" => VinculoPessoaExterna.Visitante,
            "transeunte" or "pedestre" => VinculoPessoaExterna.Transeunte,
            _ => VinculoPessoaExterna.Outro,
        };
    }

    private static string SemAcentoECaixa(string texto)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // "outro" (circunstâncias da análise) não corresponde a um campo do formulário: sempre passa.
    private static bool CampoJaPreenchido(string? campo, OcorrenciaSugeridaIa s, Guid? atividadeId, int trabalhadoresLocalizados)
        => campo switch
        {
            "local" => !string.IsNullOrWhiteSpace(s.Local),
            "data" => !string.IsNullOrWhiteSpace(s.Data),
            "hora" => !string.IsNullOrWhiteSpace(s.Hora),
            "lesao" => !string.IsNullOrWhiteSpace(s.Lesao),
            "consequencia" => !string.IsNullOrWhiteSpace(s.Consequencia),
            "atendimento" => !string.IsNullOrWhiteSpace(s.Atendimento),
            "afastamento" => s.HouveAfastamento is not null,
            "funcionarios" => trabalhadoresLocalizados > 0,
            "atividade" => atividadeId is not null,
            _ => false,
        };

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
