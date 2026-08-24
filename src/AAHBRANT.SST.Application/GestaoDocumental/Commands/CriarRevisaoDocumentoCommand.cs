using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Commands;

// Registra uma nova entrada no "histórico" (§31) do documento. Decisão própria: ao registrar uma
// revisão, os campos Versao/DataRevisao do documento (a versão/data de revisão VIGENTE) também são
// atualizados para refletir a nova revisão — mesmo princípio de UltimaRevisao carimbada
// automaticamente em AtualizarStatusRequisitoLegalCommand. NumeroRevisao é calculado automaticamente
// (quantidade de revisões existentes + 1), evitando que o usuário precise controlar a numeração.
public record CriarRevisaoDocumentoCommand(
    Guid DocumentoId,
    string Motivo,
    Guid? ResponsavelUsuarioId,
    string? NovaVersao) : IRequest<Guid>;

public class CriarRevisaoDocumentoCommandValidator : AbstractValidator<CriarRevisaoDocumentoCommand>
{
    public CriarRevisaoDocumentoCommandValidator()
    {
        RuleFor(x => x.DocumentoId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.NovaVersao).MaximumLength(50);
    }
}

public class CriarRevisaoDocumentoCommandHandler : IRequestHandler<CriarRevisaoDocumentoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarRevisaoDocumentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarRevisaoDocumentoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosGestao.FirstOrDefaultAsync(d => d.Id == request.DocumentoId, ct)
            ?? throw new KeyNotFoundException($"Documento {request.DocumentoId} não encontrado.");

        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        var proximoNumero = await _db.DocumentoRevisoes
            .Where(r => r.DocumentoId == request.DocumentoId)
            .CountAsync(ct) + 1;

        var agora = DateTime.UtcNow;

        var revisao = new DocumentoRevisao
        {
            DocumentoId = request.DocumentoId,
            NumeroRevisao = proximoNumero,
            DataRevisao = agora,
            Motivo = request.Motivo,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
        };

        _db.DocumentoRevisoes.Add(revisao);

        documento.DataRevisao = agora;
        if (!string.IsNullOrWhiteSpace(request.NovaVersao))
            documento.Versao = request.NovaVersao;

        await _db.SaveChangesAsync(ct);
        return revisao.Id;
    }
}
