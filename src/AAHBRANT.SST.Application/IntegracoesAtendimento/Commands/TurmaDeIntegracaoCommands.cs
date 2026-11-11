using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// Token de captura de foto da turma de uma etapa em andamento (uso único, 5 min, ligado à turma e ao usuário). A foto só é
// aceita com ele, e a hora da captura passa a ser a do servidor no consumo.
public record TokenCapturaFotoDto(string Token, DateTime ExpiraEmUtc);

public record EmitirTokenCapturaFotoCommand(Guid SessaoTreinamentoId) : IRequest<TokenCapturaFotoDto>;

public class EmitirTokenCapturaFotoCommandHandler : IRequestHandler<EmitirTokenCapturaFotoCommand, TokenCapturaFotoDto>
{
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService? _usuario;

    public EmitirTokenCapturaFotoCommandHandler(IAppDbContext db, ICurrentUserService? usuario = null)
    {
        _db = db;
        _usuario = usuario;
    }

    public async Task<TokenCapturaFotoDto> Handle(EmitirTokenCapturaFotoCommand request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento.Where(s => s.Id == request.SessaoTreinamentoId)
            .Select(s => new { s.Id, s.ObraId }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Turma não encontrada.");
        // Só turma de etapa de integração em andamento (recusa as demais).
        var etapa = await ProtecaoTurmaIntegracao.EtapaEmAndamentoDaTurmaAsync(_db, sessao.Id, ct)
            ?? throw new InvalidOperationException("O token de captura é só para turmas de etapa de integração.");

        var token = new TokenCapturaFoto
        {
            ObraId = sessao.ObraId,
            SessaoTreinamentoId = sessao.Id,
            UsuarioId = _usuario?.UsuarioId,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ExpiraEmUtc = DateTime.UtcNow.Add(Validade),
        };
        _ = etapa;
        _db.TokensCapturaFoto.Add(token);
        await _db.SaveChangesAsync(ct);
        return new TokenCapturaFotoDto(token.Token, token.ExpiraEmUtc);
    }
}

// B1: nos endpoints reaproveitados de turma (presença, fotos, instrutor, token), quando a turma pertence a uma integração, só
// Técnico/Admin com vínculo na obra passa (senão 404). Turma comum: nada a conferir.
public record GarantirAcessoTurmaIntegracaoQuery(
    Guid? SessaoTreinamentoId, Guid? FotoId, string? AzureAdObjectId, bool AutenticacaoHabilitada) : IRequest<bool>;

public class GarantirAcessoTurmaIntegracaoQueryHandler : IRequestHandler<GarantirAcessoTurmaIntegracaoQuery, bool>
{
    private readonly IAppDbContext _db;

    public GarantirAcessoTurmaIntegracaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(GarantirAcessoTurmaIntegracaoQuery request, CancellationToken ct)
    {
        var sessaoId = request.SessaoTreinamentoId;
        if (sessaoId is null && request.FotoId is { } fotoId)
            sessaoId = await _db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters()
                .Where(f => f.Id == fotoId).Select(f => (Guid?)f.SessaoTreinamentoId).FirstOrDefaultAsync(ct);
        if (sessaoId is null) return false;

        var turma = await _db.SessoesTreinamento.IgnoreQueryFilters().Where(s => s.Id == sessaoId)
            .Select(s => new { s.IntegracaoAtendimentoId, s.ObraId }).FirstOrDefaultAsync(ct);
        if (turma?.IntegracaoAtendimentoId is null) return false;

        await AutorizacaoIntegracaoAtendimento.AutorizarAsync(_db, turma.ObraId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        return true;
    }
}
