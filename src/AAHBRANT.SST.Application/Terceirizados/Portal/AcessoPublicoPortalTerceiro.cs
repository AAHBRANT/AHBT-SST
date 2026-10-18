using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Terceirizados.Portal;

// Lado público (empresa terceirizada, sem login Entra ID): link do contrato → código por e-mail →
// sessão. A requisição é anônima, então o filtro global de escopo por obra nega tudo
// (EscopoPorObraMiddleware): as consultas aqui usam IgnoreQueryFilters e repetem o Ativo à mão, mesmo
// cuidado de ResolverTrabalhadorPublicoQuery.
//
// Toda falha de link (inexistente, revogado, expirado, contrato encerrado) vira a MESMA mensagem: não
// dizer a quem tem um link velho se ele já existiu.
internal static class AcessoPortalConsulta
{
    public const string MensagemLinkInvalido = "Link de acesso inválido ou expirado. Peça um novo link à equipe de Segurança do Trabalho da AAHBRANT.";

    public static async Task<(AcessoPortalTerceiro Acesso, Contrato Contrato)> ObterAcessoValidoPorTokenAsync(
        IAppDbContext db, string token, CancellationToken ct)
    {
        var hash = SegredoPortal.Hash(token);
        var acesso = await db.AcessosPortalTerceiro.FirstOrDefaultAsync(a => a.TokenHash == hash, ct);
        if (acesso is null || !acesso.EstaAtivo(DateTime.UtcNow))
            throw new KeyNotFoundException(MensagemLinkInvalido);

        var contrato = await ObterContratoValidoAsync(db, acesso.ContratoId, ct)
            ?? throw new KeyNotFoundException(MensagemLinkInvalido);
        return (acesso, contrato);
    }

    public static Task<Contrato?> ObterContratoValidoAsync(IAppDbContext db, Guid contratoId, CancellationToken ct) =>
        db.Contratos.IgnoreQueryFilters().Include(c => c.Empresa)
            .FirstOrDefaultAsync(c => c.Id == contratoId && c.Ativo && c.Status == StatusContrato.Validado, ct);
}

public record CodigoPortalSolicitadoDto(string EmailMascarado, string EmpresaNome, string NumeroContrato, string ObraNome);

public record SolicitarCodigoPortalCommand(string Token) : IRequest<CodigoPortalSolicitadoDto>;

public class SolicitarCodigoPortalCommandValidator : AbstractValidator<SolicitarCodigoPortalCommand>
{
    public SolicitarCodigoPortalCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
    }
}

public class SolicitarCodigoPortalCommandHandler : IRequestHandler<SolicitarCodigoPortalCommand, CodigoPortalSolicitadoDto>
{
    private readonly IAppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<SolicitarCodigoPortalCommandHandler> _logger;

    public SolicitarCodigoPortalCommandHandler(IAppDbContext db, IEmailService email, ILogger<SolicitarCodigoPortalCommandHandler> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task<CodigoPortalSolicitadoDto> Handle(SolicitarCodigoPortalCommand request, CancellationToken ct)
    {
        var (acesso, contrato) = await AcessoPortalConsulta.ObterAcessoValidoPorTokenAsync(_db, request.Token, ct);
        var obraNome = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == contrato.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;
        var resposta = new CodigoPortalSolicitadoDto(
            SegredoPortal.MascararEmail(acesso.Email), contrato.Empresa?.RazaoSocial ?? string.Empty, contrato.NumeroContrato, obraNome);

        // Recarregar a página ou clicar duas vezes não dispara outro e-mail (nem inunda a caixa da empresa):
        // dentro do intervalo, o código já enviado continua valendo.
        var agora = DateTime.UtcNow;
        if (acesso.CodigoHash is not null && acesso.CodigoEnviadoEmUtc > agora - PortalTerceiroRegras.IntervaloMinimoEntreCodigos)
            return resposta;

        var codigo = SegredoPortal.GerarCodigo();
        acesso.CodigoHash = SegredoPortal.HashCodigo(acesso.Id, codigo);
        acesso.CodigoExpiraEmUtc = agora + PortalTerceiroRegras.ValidadeCodigo;
        acesso.CodigoEnviadoEmUtc = agora;
        acesso.TentativasCodigo = 0;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.EnviarAsync(acesso.Email, EmailPortalTerceiro.AssuntoCodigo, EmailPortalTerceiro.HtmlCodigo(codigo), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar o código do portal de terceiros do acesso {AcessoId}", acesso.Id);
            acesso.CodigoHash = null;
            acesso.CodigoEnviadoEmUtc = null;
            await _db.SaveChangesAsync(ct);
            throw new ServicoIndisponivelException("Não foi possível enviar o código agora. Tente novamente em alguns minutos.", ex);
        }

        return resposta;
    }
}

public record SessaoPortalDto(string Sessao, DateTime ExpiraEmUtc);

public record VerificarCodigoPortalCommand(string Token, string Codigo) : IRequest<SessaoPortalDto>;

public class VerificarCodigoPortalCommandValidator : AbstractValidator<VerificarCodigoPortalCommand>
{
    public VerificarCodigoPortalCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Codigo).NotEmpty().Matches("^[0-9]{6}$").WithMessage("O código tem 6 números.");
    }
}

public class VerificarCodigoPortalCommandHandler : IRequestHandler<VerificarCodigoPortalCommand, SessaoPortalDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public VerificarCodigoPortalCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<SessaoPortalDto> Handle(VerificarCodigoPortalCommand request, CancellationToken ct)
    {
        var (acesso, _) = await AcessoPortalConsulta.ObterAcessoValidoPorTokenAsync(_db, request.Token, ct);
        var agora = DateTime.UtcNow;

        if (acesso.CodigoHash is null || acesso.CodigoExpiraEmUtc <= agora)
            throw new InvalidOperationException("Código expirado. Solicite um novo código.");

        if (!SegredoPortal.HashConfere(acesso.CodigoHash, SegredoPortal.HashCodigo(acesso.Id, request.Codigo)))
        {
            // 6 dígitos são adivinháveis por força bruta: poucas tentativas e o código morre.
            acesso.TentativasCodigo += 1;
            var esgotou = acesso.TentativasCodigo >= PortalTerceiroRegras.MaximoTentativasCodigo;
            if (esgotou)
                acesso.CodigoHash = null;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException(esgotou
                ? "Código incorreto. Limite de tentativas atingido — solicite um novo código."
                : "Código incorreto.");
        }

        var sessao = SegredoPortal.GerarToken();
        acesso.SessaoHash = SegredoPortal.Hash(sessao);
        acesso.SessaoExpiraEmUtc = agora + PortalTerceiroRegras.ValidadeSessao;
        acesso.CodigoHash = null;
        acesso.TentativasCodigo = 0;
        acesso.UltimoAcessoEmUtc = agora;
        await _auditoria.RegistrarAsync("PortalTerceiro.Entrada", nameof(AcessoPortalTerceiro), acesso.Id, null, null,
            new { acesso.ContratoId }, ct);
        await _db.SaveChangesAsync(ct);

        return new SessaoPortalDto(sessao, acesso.SessaoExpiraEmUtc.Value);
    }
}

public record SessaoPortalResolvida(Guid AcessoId, Guid ContratoId, Guid ObraId);

// Usada pela API a cada requisição do portal (cabeçalho X-Portal-Sessao). Só leitura.
public record ResolverSessaoPortalQuery(string Sessao) : IRequest<SessaoPortalResolvida?>;

public class ResolverSessaoPortalQueryHandler : IRequestHandler<ResolverSessaoPortalQuery, SessaoPortalResolvida?>
{
    private readonly IAppDbContext _db;

    public ResolverSessaoPortalQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SessaoPortalResolvida?> Handle(ResolverSessaoPortalQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Sessao) || request.Sessao.Length > 128)
            return null;

        var hash = SegredoPortal.Hash(request.Sessao);
        var agora = DateTime.UtcNow;
        var acesso = await _db.AcessosPortalTerceiro.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SessaoHash == hash, ct);
        if (acesso is null || !acesso.EstaAtivo(agora) || acesso.SessaoExpiraEmUtc <= agora)
            return null;

        var contrato = await AcessoPortalConsulta.ObterContratoValidoAsync(_db, acesso.ContratoId, ct);
        return contrato is null ? null : new SessaoPortalResolvida(acesso.Id, contrato.Id, contrato.ObraId);
    }
}
