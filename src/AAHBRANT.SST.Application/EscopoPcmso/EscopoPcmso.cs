using System.Net;
using System.Text;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.CursosObrigatorios;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EscopoPcmso;

// Escopo para o médico elaborar o PCMSO (Etapa 2, desenho aprovado em 21/10): tudo sai do PGR
// vigente da obra — identificação, funções por GHE, inventário de riscos e atividades críticas.
// Exames, periodicidade e critérios de aptidão ficam com o médico (decisão do usuário: o sistema
// não sugere exame).
public record EscopoPcmsoFuncaoDto(int GheNumero, string? GheSetor, string Funcao, int Trabalhadores, string? Atividades);

public record EscopoPcmsoRiscoDto(int GheNumero, string? Tipo, string Perigo, string? Fonte, string Atividade, NivelRisco Nivel);

public record EscopoPcmsoAtividadeCriticaDto(string Descricao, List<string> Funcoes);

public record EscopoPcmsoDto(
    Guid ObraId,
    string ObraNome,
    string? Cnpj,
    string? Endereco,
    DateTime? DataInicio,
    DateTime? DataPrevisaoTermino,
    int TrabalhadoresAtivos,
    string? PgrNome,
    DateTime? PgrDataElaboracao,
    DateTime GeradoEmUtc,
    List<string> Pendencias,
    List<EscopoPcmsoFuncaoDto> Funcoes,
    List<EscopoPcmsoRiscoDto> Riscos,
    List<EscopoPcmsoAtividadeCriticaDto> AtividadesCriticas);

public record ObterEscopoPcmsoQuery(Guid ObraId) : IRequest<EscopoPcmsoDto?>;

public class ObterEscopoPcmsoQueryHandler : IRequestHandler<ObterEscopoPcmsoQuery, EscopoPcmsoDto?>
{
    // NRs cujo curso obrigatório indica atividade que pede aptidão específica no ASO.
    private static readonly Dictionary<int, string> NrsCriticas = new()
    {
        [35] = "Trabalho em altura (NR-35)",
        [33] = "Espaço confinado (NR-33)",
        [10] = "Serviços em eletricidade (NR-10)",
        [11] = "Operação de equipamentos de movimentação de carga (NR-11)",
    };

    private readonly IAppDbContext _db;
    public ObterEscopoPcmsoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EscopoPcmsoDto?> Handle(ObterEscopoPcmsoQuery request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId)) return null;
        var obra = await _db.Obras.Where(o => o.Id == request.ObraId)
            .Select(o => new { o.Id, o.Nome, o.Cnpj, o.Endereco, o.Cidade, o.Uf, o.DataInicio, o.DataPrevisaoTermino })
            .FirstOrDefaultAsync(ct);
        if (obra is null) return null;

        // Tabelas isoladas e junção em memória (soft delete + INNER JOIN esconde linha).
        var pgr = await _db.Pgrs.Where(p => p.ObraId == obra.Id && p.Status == StatusPgr.Vigente)
            .OrderByDescending(p => p.DataElaboracao).Select(p => new { p.Nome, p.DataElaboracao }).FirstOrDefaultAsync(ct);
        var ghes = await _db.Ghes.Where(g => g.ObraId == obra.Id).Select(g => new { g.Id, g.Numero, g.Setor }).ToListAsync(ct);
        var idsGhes = ghes.Select(g => g.Id).ToList();
        var gheFuncoes = await _db.GheFuncoes.Where(f => idsGhes.Contains(f.GheId))
            .Select(f => new { f.GheId, f.FuncaoId, f.DescricaoAtividades }).ToListAsync(ct);
        var idsFuncoes = gheFuncoes.Select(f => f.FuncaoId).Distinct().ToList();
        var funcoes = await _db.Funcoes.IgnoreQueryFilters().Where(f => idsFuncoes.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var trabalhadores = await _db.Trabalhadores.Where(t => t.ObraId == obra.Id && t.DataDemissao == null).Select(t => t.FuncaoId).ToListAsync(ct);
        var porFuncao = trabalhadores.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());

        // Só atividades dos GHE em uso: revisão nova do PGR desativa os GHE antigos, mas as
        // atividades deles continuam apontando para eles.
        var atividades = await _db.Atividades.Where(a => a.ObraId == obra.Id && a.GheId != null && idsGhes.Contains(a.GheId.Value))
            .Select(a => new { a.Id, a.GheId, a.Nome }).ToListAsync(ct);
        var idsAtividades = atividades.Select(a => a.Id).ToList();
        var riscos = await _db.Riscos.Where(r => idsAtividades.Contains(r.AtividadeId))
            .Select(r => new { r.AtividadeId, r.PerigoId, r.NivelRisco, r.Exposicao }).ToListAsync(ct);
        var idsPerigos = riscos.Select(r => r.PerigoId).Distinct().ToList();
        var perigos = await _db.Perigos.IgnoreQueryFilters().Where(p => idsPerigos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Nome, p.Agente, p.Fonte }, ct);

        int NumeroGhe(Guid id) => ghes.First(g => g.Id == id).Numero;

        var listaFuncoes = gheFuncoes
            .Select(f => new EscopoPcmsoFuncaoDto(NumeroGhe(f.GheId), ghes.First(g => g.Id == f.GheId).Setor,
                funcoes.GetValueOrDefault(f.FuncaoId) ?? "(função excluída)", porFuncao.GetValueOrDefault(f.FuncaoId), f.DescricaoAtividades))
            .OrderBy(f => f.GheNumero).ThenBy(f => f.Funcao).ToList();

        var listaRiscos = riscos
            .Where(r => perigos.ContainsKey(r.PerigoId))
            .Select(r =>
            {
                var a = atividades.First(x => x.Id == r.AtividadeId);
                var p = perigos[r.PerigoId];
                var tipo = p.Agente ?? (r.Exposicao?.StartsWith("Tipo: ") == true ? r.Exposicao[6..] : null);
                // Perigo.Fonte não entra: o catálogo de perigos é de todas as obras e a fonte gravada nele
                // pode ser de outra obra (achado na verificação local, 21/10). A fonte aqui é a atividade.
                return new EscopoPcmsoRiscoDto(NumeroGhe(a.GheId!.Value), tipo, p.Nome, null, a.Nome, r.NivelRisco);
            })
            // Mesmo perigo em várias atividades do GHE vira uma linha só, com o maior nível.
            .GroupBy(r => (r.GheNumero, r.Perigo))
            .Select(g => g.OrderByDescending(r => r.Nivel).First() with
            {
                Atividade = string.Join("; ", g.Select(r => r.Atividade).Distinct().Take(3)) + (g.Select(r => r.Atividade).Distinct().Count() > 3 ? "…" : ""),
            })
            .OrderBy(r => r.GheNumero).ThenByDescending(r => r.Nivel).ThenBy(r => r.Perigo).ToList();

        // Atividades críticas: cursos obrigatórios (Etapa 1) não recusados das NRs que pedem aptidão
        // específica no ASO.
        var cursosObrigatorios = await _db.CursosObrigatoriosFuncaoObra
            .Where(c => c.ObraId == obra.Id && c.Status != StatusCursoObrigatorio.Recusado)
            .Select(c => new { c.FuncaoId, c.CursoTreinamentoId }).ToListAsync(ct);
        var idsCursos = cursosObrigatorios.Select(c => c.CursoTreinamentoId).Distinct().ToList();
        var normaCurso = await _db.CursosTreinamento.IgnoreQueryFilters().Where(c => idsCursos.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => GeradorCursosObrigatorios.NumeroNr(c.NormaReferencia), ct);
        var criticas = cursosObrigatorios
            .Select(c => (Nr: normaCurso.GetValueOrDefault(c.CursoTreinamentoId), c.FuncaoId))
            .Where(c => c.Nr is { } n && NrsCriticas.ContainsKey(n))
            .GroupBy(c => c.Nr!.Value)
            .OrderBy(g => g.Key == 35 ? 0 : g.Key)
            .Select(g => new EscopoPcmsoAtividadeCriticaDto(NrsCriticas[g.Key],
                g.Select(c => funcoes.GetValueOrDefault(c.FuncaoId)).OfType<string>().Distinct().OrderBy(n => n).ToList()))
            .Where(c => c.Funcoes.Count > 0)
            .ToList();

        var endereco = string.Join(", ", new[] { obra.Endereco, string.Join("/", new[] { obra.Cidade, obra.Uf }.Where(s => !string.IsNullOrWhiteSpace(s))) }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        var pendencias = new List<string>();
        if (pgr is null) pendencias.Add("A obra não tem PGR vigente — o escopo sai sem base.");
        if (listaFuncoes.Count == 0) pendencias.Add("O PGR não tem GHE com funções cadastradas.");
        if (listaRiscos.Count == 0) pendencias.Add("O PGR não tem riscos cadastrados nas atividades dos GHE.");
        if (string.IsNullOrWhiteSpace(obra.Cnpj)) pendencias.Add("CNPJ da obra não preenchido.");
        if (endereco.Length == 0) pendencias.Add("Endereço da obra não preenchido.");

        return new EscopoPcmsoDto(obra.Id, obra.Nome, obra.Cnpj, endereco.Length == 0 ? null : endereco, obra.DataInicio, obra.DataPrevisaoTermino,
            trabalhadores.Count, pgr?.Nome, pgr?.DataElaboracao, DateTime.UtcNow, pendencias, listaFuncoes, listaRiscos, criticas);
    }
}

public interface IEscopoPcmsoPdfService
{
    byte[] Gerar(EscopoPcmsoDto escopo, byte[]? logoObra);
}

public record ExportarEscopoPcmsoPdfQuery(Guid ObraId) : IRequest<byte[]?>;

public class ExportarEscopoPcmsoPdfQueryHandler : IRequestHandler<ExportarEscopoPcmsoPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IEscopoPcmsoPdfService _pdf;

    public ExportarEscopoPcmsoPdfQueryHandler(IMediator mediator, IAppDbContext db, IEscopoPcmsoPdfService pdf)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarEscopoPcmsoPdfQuery request, CancellationToken ct)
    {
        var escopo = await _mediator.Send(new ObterEscopoPcmsoQuery(request.ObraId), ct);
        if (escopo is null) return null;
        var logo = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        return _pdf.Gerar(escopo, logo);
    }
}

// "Enviar ao médico por e-mail": sai pela caixa sst@aahbrant.com com o PDF anexo.
public record EnviarEscopoPcmsoCommand(Guid ObraId, string EmailMedico, string? NomeMedico, string? Mensagem, string? NomeRemetente) : IRequest;

public class EnviarEscopoPcmsoCommandValidator : AbstractValidator<EnviarEscopoPcmsoCommand>
{
    public EnviarEscopoPcmsoCommandValidator()
    {
        RuleFor(x => x.EmailMedico).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.NomeMedico).MaximumLength(200);
        RuleFor(x => x.Mensagem).MaximumLength(2000);
    }
}

public class EnviarEscopoPcmsoCommandHandler : IRequestHandler<EnviarEscopoPcmsoCommand>
{
    private readonly IMediator _mediator;
    private readonly IEmailService _email;

    public EnviarEscopoPcmsoCommandHandler(IMediator mediator, IEmailService email)
    {
        _mediator = mediator;
        _email = email;
    }

    public async Task Handle(EnviarEscopoPcmsoCommand request, CancellationToken ct)
    {
        var escopo = await _mediator.Send(new ObterEscopoPcmsoQuery(request.ObraId), ct)
            ?? throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");
        var pdf = await _mediator.Send(new ExportarEscopoPcmsoPdfQuery(request.ObraId), ct)
            ?? throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var nomeArquivo = $"Escopo PCMSO - {escopo.ObraNome}.pdf";
        await _email.EnviarAsync(request.EmailMedico.Trim(), $"Escopo para elaboração do PCMSO — Obra {escopo.ObraNome}",
            CorpoEmail(escopo, request), new[] { new AnexoEmail(nomeArquivo, "application/pdf", pdf) }, ct);
    }

    private static string CorpoEmail(EscopoPcmsoDto e, EnviarEscopoPcmsoCommand r)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        const string Just = "text-align:justify;";
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"></head>");
        sb.Append("<body style=\"margin:0;background:#f4f3ef;font-family:'Segoe UI',Roboto,Arial,sans-serif;color:#1c1c1c;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f4f3ef;padding:24px 0;\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border:1px solid #e2ded6;\">");
        sb.Append($"<tr><td style=\"padding:18px 28px;border-bottom:1px solid #e2ded6;\"><img src=\"cid:{IEmailService.ContentIdLogo}\" alt=\"AAHBRANT Engenharia &amp; Construções\" height=\"38\" style=\"display:block;height:38px;\"></td></tr>");
        sb.Append("<tr><td style=\"background:#670000;color:#ffffff;padding:20px 28px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:.06em;text-transform:uppercase;opacity:.85;\">SST · Saúde Ocupacional</div>");
        sb.Append($"<div style=\"font-size:20px;font-weight:600;margin-top:6px;\">Escopo para elaboração do PCMSO — Obra {H(e.ObraNome)}</div></td></tr>");
        sb.Append($"<tr><td style=\"padding:24px 28px;font-size:14px;line-height:1.55;{Just}\">");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Prezado(a) {H(string.IsNullOrWhiteSpace(r.NomeMedico) ? "Dr(a)." : r.NomeMedico)},</p>");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Segue anexo o escopo para elaboração do PCMSO da obra {H(e.ObraNome)}, montado a partir do inventário de riscos do PGR vigente");
        if (e.PgrNome is not null) sb.Append($" ({H(e.PgrNome)})");
        sb.Append($". O documento traz {e.Funcoes.Count} função(ões) por GHE, {e.Riscos.Count} risco(s) ocupacional(is) e as atividades críticas que exigem aptidão específica no ASO.</p>");
        if (!string.IsNullOrWhiteSpace(r.Mensagem))
            sb.Append($"<p style=\"margin:0 0 16px;white-space:pre-line;{Just}\">{H(r.Mensagem.Trim())}</p>");
        sb.Append($"<p style=\"margin:0 0 16px;{Just}\">Ficam a cargo do médico responsável a definição dos exames por função, a periodicidade, os critérios de aptidão e a assinatura do PCMSO. Pedimos a devolução do documento concluído para anexação na obra.</p>");
        sb.Append($"<p style=\"margin:0;{Just}\">Atenciosamente,<br>{H(r.NomeRemetente ?? "Equipe de SST")}<br>AAHBRANT Engenharia &amp; Construções</p>");
        sb.Append("</td></tr></table></td></tr></table></body></html>");
        return sb.ToString();
    }
}
