using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GestaoDocumental.Commands;

public record AtualizarDocumentoGestaoCommand(
    Guid Id,
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
    string? Arquivo) : IRequest;

public class AtualizarDocumentoGestaoCommandValidator : AbstractValidator<AtualizarDocumentoGestaoCommand>
{
    public AtualizarDocumentoGestaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Tipo).MaximumLength(100);
        RuleFor(x => x.Categoria).MaximumLength(100);
        RuleFor(x => x.OrigemDocumento).MaximumLength(200);
        RuleFor(x => x.Versao).MaximumLength(50);
        RuleFor(x => x.Arquivo).MaximumLength(500);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class AtualizarDocumentoGestaoCommandHandler : IRequestHandler<AtualizarDocumentoGestaoCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarDocumentoGestaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarDocumentoGestaoCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosGestao.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Documento {request.Id} não encontrado.");

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

        documento.Nome = request.Nome;
        documento.Tipo = request.Tipo;
        documento.Categoria = request.Categoria;
        documento.OrigemDocumento = request.OrigemDocumento;
        documento.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
        documento.Versao = request.Versao;
        documento.Validade = request.Validade;
        documento.DataEmissao = request.DataEmissao;
        documento.DataRevisao = request.DataRevisao;
        documento.RequisitoLegalId = request.RequisitoLegalId;
        documento.ObraId = request.ObraId;
        documento.SetorId = request.SetorId;
        documento.Arquivo = request.Arquivo;

        await _db.SaveChangesAsync(ct);
    }
}
