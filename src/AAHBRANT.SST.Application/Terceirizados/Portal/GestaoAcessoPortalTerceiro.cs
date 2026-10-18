using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Terceirizados.Portal;

// Lado interno (técnico de SST, com login): gera, lista e revoga o link do portal de um contrato.
// _db.Contratos já vem filtrado pelo escopo de obra do usuário, então um técnico só gera/revoga
// acesso de contrato da própria obra.

public record AcessoPortalGeradoDto(Guid AcessoId, string Link, string Email, DateTime ExpiraEmUtc, bool EmailEnviado);

public record GerarAcessoPortalTerceiroCommand(Guid ContratoId, string? Email, Guid? UsuarioId) : IRequest<AcessoPortalGeradoDto>;

public class GerarAcessoPortalTerceiroCommandValidator : AbstractValidator<GerarAcessoPortalTerceiroCommand>
{
    public GerarAcessoPortalTerceiroCommandValidator()
    {
        RuleFor(x => x.ContratoId).NotEmpty();
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().WithMessage("Informe um e-mail válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class GerarAcessoPortalTerceiroCommandHandler : IRequestHandler<GerarAcessoPortalTerceiroCommand, AcessoPortalGeradoDto>
{
    private readonly IAppDbContext _db;
    private readonly IEmailService _email;
    private readonly IPortalTerceiroConfiguracao _configuracao;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<GerarAcessoPortalTerceiroCommandHandler> _logger;

    public GerarAcessoPortalTerceiroCommandHandler(
        IAppDbContext db, IEmailService email, IPortalTerceiroConfiguracao configuracao,
        IAuditoriaService auditoria, ILogger<GerarAcessoPortalTerceiroCommandHandler> logger)
    {
        _db = db;
        _email = email;
        _configuracao = configuracao;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<AcessoPortalGeradoDto> Handle(GerarAcessoPortalTerceiroCommand request, CancellationToken ct)
    {
        var contrato = await _db.Contratos.Include(c => c.Empresa)
            .FirstOrDefaultAsync(c => c.Id == request.ContratoId, ct)
            ?? throw new KeyNotFoundException("Contrato não encontrado.");
        if (contrato.Status != StatusContrato.Validado)
            throw new InvalidOperationException("Só é possível liberar o portal para contrato com status Validado.");

        var email = (string.IsNullOrWhiteSpace(request.Email) ? contrato.Empresa?.ContatoEmail : request.Email)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("A empresa não tem e-mail de contato cadastrado. Informe o e-mail que vai receber o acesso.");

        var obraNome = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == contrato.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;

        var agora = DateTime.UtcNow;

        // Um acesso ativo por contrato: gerar de novo (e-mail errado, link vazado) invalida o anterior.
        var anteriores = await _db.AcessosPortalTerceiro
            .Where(a => a.ContratoId == contrato.Id && a.RevogadoEmUtc == null)
            .ToListAsync(ct);
        foreach (var anterior in anteriores)
        {
            anterior.RevogadoEmUtc = agora;
            anterior.SessaoHash = null;
            anterior.CodigoHash = null;
        }

        var fimVigencia = contrato.DataFimVigencia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) + PortalTerceiroRegras.FolgaAposVigencia;
        var expira = fimVigencia > agora + PortalTerceiroRegras.ValidadeMinimaLink
            ? fimVigencia
            : agora + PortalTerceiroRegras.ValidadeMinimaLink;

        var token = SegredoPortal.GerarToken();
        var acesso = new AcessoPortalTerceiro
        {
            ContratoId = contrato.Id,
            Email = email,
            TokenHash = SegredoPortal.Hash(token),
            ExpiraEmUtc = expira,
            CreatedBy = request.UsuarioId,
        };
        _db.AcessosPortalTerceiro.Add(acesso);
        await _auditoria.RegistrarAsync("PortalTerceiro.AcessoGerado", nameof(Contrato), contrato.Id, request.UsuarioId, null,
            new { AcessoId = acesso.Id, Email = email, ExpiraEmUtc = expira, Revogados = anteriores.Count }, ct);
        await _db.SaveChangesAsync(ct);

        var link = $"{_configuracao.UrlBase.TrimEnd('/')}/#/portal-terceiros/{token}";

        // O link é devolvido mesmo se o e-mail falhar: o técnico pode repassá-lo por outro canal.
        var emailEnviado = true;
        try
        {
            await _email.EnviarAsync(email,
                EmailPortalTerceiro.AssuntoConvite(contrato.NumeroContrato, obraNome),
                EmailPortalTerceiro.HtmlConvite(contrato.Empresa?.RazaoSocial ?? string.Empty, contrato.NumeroContrato, obraNome, link, expira),
                ct);
        }
        catch (Exception ex)
        {
            emailEnviado = false;
            _logger.LogWarning(ex, "Falha ao enviar o convite do portal de terceiros do contrato {ContratoId}", contrato.Id);
        }

        return new AcessoPortalGeradoDto(acesso.Id, link, email, expira, emailEnviado);
    }
}

public record AcessoPortalTerceiroDto(
    Guid Id, string Email, DateTime CriadoEmUtc, DateTime ExpiraEmUtc, DateTime? RevogadoEmUtc,
    DateTime? UltimoAcessoEmUtc, DateTime? TermoAceitoEmUtc, bool Ativo);

public record ListarAcessosPortalTerceiroQuery(Guid ContratoId) : IRequest<List<AcessoPortalTerceiroDto>>;

public class ListarAcessosPortalTerceiroQueryHandler : IRequestHandler<ListarAcessosPortalTerceiroQuery, List<AcessoPortalTerceiroDto>>
{
    private readonly IAppDbContext _db;

    public ListarAcessosPortalTerceiroQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<AcessoPortalTerceiroDto>> Handle(ListarAcessosPortalTerceiroQuery request, CancellationToken ct)
    {
        if (!await _db.Contratos.AnyAsync(c => c.Id == request.ContratoId, ct))
            throw new KeyNotFoundException("Contrato não encontrado.");

        var agora = DateTime.UtcNow;
        var acessos = await _db.AcessosPortalTerceiro.AsNoTracking()
            .Where(a => a.ContratoId == request.ContratoId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);
        return acessos.Select(a => new AcessoPortalTerceiroDto(
            a.Id, a.Email, a.CreatedAtUtc, a.ExpiraEmUtc, a.RevogadoEmUtc, a.UltimoAcessoEmUtc, a.TermoAceitoEmUtc,
            a.EstaAtivo(agora))).ToList();
    }
}

public record RevogarAcessoPortalTerceiroCommand(Guid AcessoId, Guid? UsuarioId) : IRequest;

public class RevogarAcessoPortalTerceiroCommandHandler : IRequestHandler<RevogarAcessoPortalTerceiroCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public RevogarAcessoPortalTerceiroCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task Handle(RevogarAcessoPortalTerceiroCommand request, CancellationToken ct)
    {
        var acesso = await _db.AcessosPortalTerceiro
            .FirstOrDefaultAsync(a => a.Id == request.AcessoId && _db.Contratos.Any(c => c.Id == a.ContratoId), ct)
            ?? throw new KeyNotFoundException("Acesso não encontrado.");
        if (acesso.RevogadoEmUtc is not null)
            return;

        acesso.RevogadoEmUtc = DateTime.UtcNow;
        acesso.SessaoHash = null;
        acesso.CodigoHash = null;
        await _auditoria.RegistrarAsync("PortalTerceiro.AcessoRevogado", nameof(Contrato), acesso.ContratoId, request.UsuarioId, null,
            new { AcessoId = acesso.Id }, ct);
        await _db.SaveChangesAsync(ct);
    }
}
