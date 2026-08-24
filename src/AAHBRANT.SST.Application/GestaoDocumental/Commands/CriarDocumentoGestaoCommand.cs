using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Commands;

public record CriarDocumentoGestaoCommand(
    string Nome,
    string? Tipo,
    string? Categoria,
    string? OrigemDocumento,
    Guid? ResponsavelUsuarioId,
    string? Versao,
    DateTime? Validade,
    DateTime DataEmissao,
    DateTime? DataRevisao,
    Guid? RequisitoLegalId,
    Guid? ObraId,
    Guid? SetorId,
    string? Arquivo) : IRequest<Guid>;

public class CriarDocumentoGestaoCommandValidator : AbstractValidator<CriarDocumentoGestaoCommand>
{
    public CriarDocumentoGestaoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Tipo).MaximumLength(100);
        RuleFor(x => x.Categoria).MaximumLength(100);
        RuleFor(x => x.OrigemDocumento).MaximumLength(200);
        RuleFor(x => x.Versao).MaximumLength(50);
        RuleFor(x => x.Arquivo).MaximumLength(500);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class CriarDocumentoGestaoCommandHandler : IRequestHandler<CriarDocumentoGestaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDocumentoGestaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDocumentoGestaoCommand request, CancellationToken ct)
    {
        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        if (request.RequisitoLegalId.HasValue &&
            !await _db.RequisitosLegais.AnyAsync(r => r.Id == request.RequisitoLegalId, ct))
            throw new KeyNotFoundException($"Requisito legal {request.RequisitoLegalId} não encontrado.");

        if (request.ObraId.HasValue &&
            !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.SetorId.HasValue &&
            !await _db.Setores.AnyAsync(s => s.Id == request.SetorId, ct))
            throw new KeyNotFoundException($"Setor {request.SetorId} não encontrado.");

        var documento = new DocumentoGestao
        {
            Nome = request.Nome,
            Tipo = request.Tipo,
            Categoria = request.Categoria,
            OrigemDocumento = request.OrigemDocumento,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Versao = request.Versao,
            Validade = request.Validade,
            DataEmissao = request.DataEmissao,
            DataRevisao = request.DataRevisao,
            RequisitoLegalId = request.RequisitoLegalId,
            ObraId = request.ObraId,
            SetorId = request.SetorId,
            Arquivo = request.Arquivo,
        };

        _db.DocumentosGestao.Add(documento);
        await _db.SaveChangesAsync(ct);
        return documento.Id;
    }
}
