using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record AnexarDocumentoPcmsoCommand(
    Guid PcmsoId,
    byte[] Conteudo,
    string ContentType) : IRequest;

public class AnexarDocumentoPcmsoCommandValidator : AbstractValidator<AnexarDocumentoPcmsoCommand>
{
    private const int TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexarDocumentoPcmsoCommandValidator()
    {
        RuleFor(x => x.PcmsoId).NotEmpty();
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O documento deve ter no máximo 20 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ContentType).Equal("application/pdf").WithMessage("O documento deve ser um arquivo PDF.");
    }
}

public class AnexarDocumentoPcmsoCommandHandler : IRequestHandler<AnexarDocumentoPcmsoCommand>
{
    private readonly IAppDbContext _db;

    public AnexarDocumentoPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarDocumentoPcmsoCommand request, CancellationToken ct)
    {
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.PcmsoId, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.PcmsoId} não encontrado.");

        pcmso.DocumentoConteudo = request.Conteudo;
        pcmso.DocumentoContentType = request.ContentType;
        await _db.SaveChangesAsync(ct);
    }
}
