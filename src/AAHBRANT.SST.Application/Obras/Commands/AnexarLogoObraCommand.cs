using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Obras;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Commands;

public record AnexarLogoObraCommand(
    Guid ObraId,
    byte[] LogoConteudo,
    string LogoContentType) : IRequest;

public class AnexarLogoObraCommandValidator : AbstractValidator<AnexarLogoObraCommand>
{
    public AnexarLogoObraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.LogoConteudo)
            .NotEmpty().WithMessage("O logo é obrigatório.")
            .Must(f => f.Length <= ValidacaoLogoObra.TamanhoMaximoBytes).WithMessage("O logo deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.LogoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.LogoContentType)
            .Must(t => ValidacaoLogoObra.TiposPermitidos.Contains(t)).WithMessage("O logo deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarLogoObraCommandHandler : IRequestHandler<AnexarLogoObraCommand>
{
    private readonly IAppDbContext _db;

    public AnexarLogoObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarLogoObraCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.ObraId, ct)
            ?? throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        obra.LogoConteudo = request.LogoConteudo;
        obra.LogoContentType = request.LogoContentType;
        await _db.SaveChangesAsync(ct);
    }
}
