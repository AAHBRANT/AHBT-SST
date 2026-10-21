using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MateriaisApoio.Commands;

// Atualiza só os dados do documento controlado (situação, revisão, forma de consulta...), sem trocar
// o arquivo. Ex.: procedimento que passou a Obsoleto sai das próximas Ordens de Serviço.
public record AtualizarDocumentoSstCommand(
    Guid Id,
    string Nome,
    string? Codigo,
    string? Revisao,
    SituacaoDocumentoSst? Situacao,
    string? FormaConsulta,
    bool IncluirEmTodasOs) : IRequest;

public class AtualizarDocumentoSstCommandValidator : AbstractValidator<AtualizarDocumentoSstCommand>
{
    public AtualizarDocumentoSstCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Codigo).MaximumLength(60);
        RuleFor(x => x.Revisao).MaximumLength(20);
        RuleFor(x => x.FormaConsulta).MaximumLength(300);
    }
}

public class AtualizarDocumentoSstCommandHandler : IRequestHandler<AtualizarDocumentoSstCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarDocumentoSstCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarDocumentoSstCommand request, CancellationToken ct)
    {
        var doc = await _db.MateriaisApoio.FirstOrDefaultAsync(m => m.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Documento {request.Id} não encontrado.");

        doc.Nome = request.Nome.Trim();
        doc.FormaConsulta = request.FormaConsulta?.Trim();
        if (doc.Tipo == TipoDocumentoSst.MaterialApoio) { await _db.SaveChangesAsync(ct); return; }

        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Revisao) || request.Situacao is null)
            throw new InvalidOperationException("Documento controlado precisa de código, revisão e situação.");

        doc.Codigo = request.Codigo.Trim();
        doc.Revisao = request.Revisao.Trim();
        doc.Situacao = request.Situacao;
        doc.IncluirEmTodasOs = doc.Tipo == TipoDocumentoSst.Politica && request.IncluirEmTodasOs;
        await _db.SaveChangesAsync(ct);
    }
}
