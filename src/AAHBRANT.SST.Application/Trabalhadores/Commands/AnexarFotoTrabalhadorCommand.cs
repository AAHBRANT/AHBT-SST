using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Foto real do trabalhador, substituindo o avatar de iniciais no cadastro — mesmo padrão de
// AnexarLogoObraCommand (Obras/Commands): binário guardado direto na linha do Trabalhador.
public record AnexarFotoTrabalhadorCommand(
    Guid TrabalhadorId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest;

public class AnexarFotoTrabalhadorCommandValidator : AbstractValidator<AnexarFotoTrabalhadorCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoTrabalhadorCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoTrabalhadorCommandHandler : IRequestHandler<AnexarFotoTrabalhadorCommand>
{
    private readonly IAppDbContext _db;

    public AnexarFotoTrabalhadorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarFotoTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        trabalhador.FotoConteudo = request.FotoConteudo;
        trabalhador.FotoContentType = request.FotoContentType;
        await _db.SaveChangesAsync(ct);
    }
}
