using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Acidentes.RelatoIa;

/// <summary>
/// Análise preliminar de causas + plano de ação da ocorrência ainda não registrada, a partir do
/// formulário já revisado pelo técnico. Nada é gravado: as ações vão junto no CriarAcidenteCommand.
/// </summary>
public record SugerirPlanoAcaoOcorrenciaCommand(
    Guid ObraId,
    Guid? AtividadeId,
    TipoOcorrencia Tipo,
    GravidadeAcidente Gravidade,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    Guid? UsuarioAtualId) : IRequest<PlanoAcaoSugeridoDto>;

public record PlanoAcaoSugeridoDto(
    MetodologiaInvestigacao? Metodologia,
    string? Causas,
    IReadOnlyList<AcaoPlanoSugeridaDto> Acoes,
    TemaDdsSugeridoDto TemaDds);

// Tema obrigatório do DDS do próximo dia útil da obra (entra no plano como ação de DDS).
public record TemaDdsSugeridoDto(string Nome, string Roteiro, DateTime Data);

public record AcaoPlanoSugeridaDto(
    TipoAcaoPlano Tipo,
    string Descricao,
    PrioridadeAcao Prioridade,
    DateTime Prazo,
    Guid? ResponsavelUsuarioId,
    string? ResponsavelNome,
    string PapelResponsavel,
    string? AvisoResponsavel,
    string Fundamentacao,
    bool BaseConfirmada);

public class SugerirPlanoAcaoOcorrenciaCommandValidator : AbstractValidator<SugerirPlanoAcaoOcorrenciaCommand>
{
    public SugerirPlanoAcaoOcorrenciaCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Descricao).NotEmpty().WithMessage("Preencha a descrição antes de gerar o plano de ação.").MaximumLength(4000);
    }
}

public class SugerirPlanoAcaoOcorrenciaCommandHandler : IRequestHandler<SugerirPlanoAcaoOcorrenciaCommand, PlanoAcaoSugeridoDto>
{
    // Papel que a IA pode indicar → perfil de acesso do sistema. Gestor QSMS costuma ter escopo
    // global (UsuarioPerfilObra.ObraId nulo); os demais são vinculados à obra.
    private static readonly Dictionary<string, (TipoPerfilAcesso Perfil, string Rotulo)> Papeis = new()
    {
        ["TecnicoSeguranca"] = (TipoPerfilAcesso.TecnicoSeguranca, "Técnico de Segurança"),
        ["EngenheiroSeguranca"] = (TipoPerfilAcesso.EngenheiroSeguranca, "Engenheiro de Segurança"),
        ["GestorDeObra"] = (TipoPerfilAcesso.GestorDeObra, "Gestor de Obra"),
        ["Encarregado"] = (TipoPerfilAcesso.Encarregado, "Encarregado"),
        ["GestorQsms"] = (TipoPerfilAcesso.GestorQsms, "Gestor QSMS"),
    };

    private readonly IAppDbContext _db;
    private readonly IAnalistaPlanoOcorrencia _analista;
    private readonly ILogger<SugerirPlanoAcaoOcorrenciaCommandHandler> _logger;

    public SugerirPlanoAcaoOcorrenciaCommandHandler(
        IAppDbContext db,
        IAnalistaPlanoOcorrencia analista,
        ILogger<SugerirPlanoAcaoOcorrenciaCommandHandler> logger)
    {
        _db = db;
        _analista = analista;
        _logger = logger;
    }

    public async Task<PlanoAcaoSugeridoDto> Handle(SugerirPlanoAcaoOcorrenciaCommand request, CancellationToken ct)
    {
        var atividade = request.AtividadeId is null
            ? null
            : await _db.Atividades.Where(a => a.Id == request.AtividadeId).Select(a => a.Nome).FirstOrDefaultAsync(ct);

        var riscos = request.AtividadeId is null
            ? new List<RiscoPgrResumo>()
            : await _db.Riscos
                .Where(r => r.AtividadeId == request.AtividadeId)
                .Select(r => new RiscoPgrResumo(r.Perigo!.Nome, (int)r.NivelRisco, r.Consequencia, r.ControlesExistentes, r.ControlesAdicionais))
                .Distinct()
                .ToListAsync(ct);

        var requisitos = await RequisitosAplicaveisAsync(request.AtividadeId, ct);

        var analise = await _analista.AnalisarAsync(new ContextoAnaliseOcorrencia(
            request.Tipo.ToString(),
            request.Gravidade.ToString(),
            request.Descricao.Trim(),
            request.Lesao,
            request.Consequencia,
            request.Atendimento,
            atividade,
            riscos,
            requisitos), ct);

        var responsaveis = await CarregarResponsaveisAsync(request.ObraId, ct);
        var agora = DateTime.UtcNow;

        var acoes = analise.Acoes
            .Where(a => !string.IsNullOrWhiteSpace(a.Descricao))
            .Take(5)
            .Select(a =>
            {
                var prioridade = Enum.TryParse<PrioridadeAcao>(a.Prioridade, out var p) ? p : PrioridadeAcao.Media;
                var (responsavelId, responsavelNome, papel, aviso) = EscolherResponsavel(a.PapelResponsavel, responsaveis, request.UsuarioAtualId);
                var base_ = FundamentacaoAcaoPlano.Conferir(a, atividade, riscos, requisitos);
                return new AcaoPlanoSugeridaDto(
                    Enum.TryParse<TipoAcaoPlano>(a.Tipo, out var t) ? t : TipoAcaoPlano.Corretiva,
                    Limitar(a.Descricao!, 500),
                    prioridade,
                    // Prazo pela regra da própria empresa (Procedimento de Inspeção Técnica de Campo §7).
                    SlaPrioridadeCalculator.CalcularPrazoSugerido(prioridade, agora),
                    responsavelId,
                    responsavelNome,
                    papel,
                    aviso,
                    base_.Texto,
                    base_.Confirmada);
            })
            .ToList();

        return new PlanoAcaoSugeridoDto(
            Enum.TryParse<MetodologiaInvestigacao>(analise.Metodologia, out var m) ? m : null,
            string.IsNullOrWhiteSpace(analise.Causas) ? null : Limitar(analise.Causas, 2000),
            acoes,
            new TemaDdsSugeridoDto(
                Limitar(string.IsNullOrWhiteSpace(analise.TemaDdsNome) ? $"Prevenção: {atividade ?? "lições da ocorrência"}" : analise.TemaDdsNome, 200),
                Limitar(analise.TemaDdsRoteiro ?? string.Empty, 1000),
                CalendarioObra.ProximoDiaUtil(CalendarioObra.AgoraEmBrasilia())));
    }

    private async Task<List<RequisitoLegalResumo>> RequisitosAplicaveisAsync(Guid? atividadeId, CancellationToken ct)
    {
        if (atividadeId is null) return new();
        try
        {
            var perigos = _db.Riscos.Where(r => r.AtividadeId == atividadeId).Select(r => r.PerigoId);
            return await _db.RequisitoLegalCriterios
                .Where(c => c.Tipo == TipoCriterioAplicabilidade.Perigo && c.PerigoId != null && perigos.Contains(c.PerigoId.Value))
                .Where(c => c.RequisitoLegal!.Status == StatusRequisitoLegal.Ativo)
                .Select(c => new RequisitoLegalResumo(c.RequisitoLegal!.Norma, c.RequisitoLegal.Artigo, c.RequisitoLegal.Titulo, c.RequisitoLegal.Descricao))
                .Distinct()
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Em hml a tabela RequisitosLegais está ausente (divergência de schema, 08/10/2026). Sem
            // requisitos, o plano segue ancorado só no PGR e no método — não pode derrubar o registro.
            _logger.LogWarning(ex, "Requisitos legais indisponíveis; plano de ação gerado sem essa base.");
            return new();
        }
    }

    private sealed record Responsavel(Guid UsuarioId, string Nome, TipoPerfilAcesso Perfil);

    private async Task<List<Responsavel>> CarregarResponsaveisAsync(Guid obraId, CancellationToken ct)
        => await _db.UsuariosPerfilObra
            .Where(v => v.PerfilAcesso!.Tipo != null &&
                        (v.ObraId == obraId || (v.ObraId == null && v.PerfilAcesso.Tipo == TipoPerfilAcesso.GestorQsms)) &&
                        v.Usuario!.Status == StatusUsuario.Ativo)
            .OrderBy(v => v.Usuario!.Nome)
            .Select(v => new Responsavel(v.UsuarioId, v.Usuario!.Nome, v.PerfilAcesso!.Tipo!.Value))
            .ToListAsync(ct);

    private static (Guid? Id, string? Nome, string Papel, string? Aviso) EscolherResponsavel(
        string? papelIa, List<Responsavel> responsaveis, Guid? usuarioAtualId)
    {
        var (perfil, rotulo) = Papeis.TryGetValue(papelIa ?? string.Empty, out var p) ? p : Papeis["TecnicoSeguranca"];

        // O técnico que está registrando é o responsável natural das ações de Técnico de Segurança.
        if (perfil == TipoPerfilAcesso.TecnicoSeguranca && usuarioAtualId is not null)
            return (usuarioAtualId, responsaveis.FirstOrDefault(r => r.UsuarioId == usuarioAtualId)?.Nome ?? "Você", rotulo, null);

        var escolhido = responsaveis.FirstOrDefault(r => r.Perfil == perfil);
        if (escolhido is not null)
            return (escolhido.UsuarioId, escolhido.Nome, rotulo, null);

        return usuarioAtualId is not null
            ? (usuarioAtualId, "Você", rotulo, $"Nenhum {rotulo} vinculado a esta obra: a ação ficou com você.")
            : (null, null, rotulo, $"Nenhum {rotulo} vinculado a esta obra: escolha o responsável.");
    }

    private static string Limitar(string texto, int maximo)
    {
        var limpo = texto.Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo].TrimEnd();
    }
}
