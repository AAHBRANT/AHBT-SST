using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pgrs.Commands;

// Documento PDF original do PGR, consultado (não editado) na aba "PGR" da tela de detalhe — mesmo
// padrão de anexo binário direto na linha já usado por AnexarFotoTrabalhadorCommand.
public record AnexarDocumentoPgrCommand(
    Guid PgrId,
    byte[] Conteudo,
    string ContentType) : IRequest;

public class AnexarDocumentoPgrCommandValidator : AbstractValidator<AnexarDocumentoPgrCommand>
{
    private const int TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexarDocumentoPgrCommandValidator()
    {
        RuleFor(x => x.PgrId).NotEmpty();
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O documento deve ter no máximo 20 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ContentType).Equal("application/pdf").WithMessage("O documento deve ser um arquivo PDF.");
    }
}

public class AnexarDocumentoPgrCommandHandler : IRequestHandler<AnexarDocumentoPgrCommand>
{
    private readonly IAppDbContext _db;

    public AnexarDocumentoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarDocumentoPgrCommand request, CancellationToken ct)
    {
        var pgr = await _db.Pgrs.FirstOrDefaultAsync(p => p.Id == request.PgrId, ct)
            ?? throw new KeyNotFoundException($"PGR {request.PgrId} não encontrado.");

        pgr.DocumentoConteudo = request.Conteudo;
        pgr.DocumentoContentType = request.ContentType;
        await _db.SaveChangesAsync(ct);
    }
}
