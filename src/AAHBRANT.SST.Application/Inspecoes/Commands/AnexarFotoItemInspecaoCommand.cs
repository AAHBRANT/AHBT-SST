using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

public record AnexarFotoItemInspecaoCommand(
    Guid RespostaId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest;

public class AnexarFotoItemInspecaoCommandValidator : AbstractValidator<AnexarFotoItemInspecaoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoItemInspecaoCommandValidator()
    {
        RuleFor(x => x.RespostaId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoItemInspecaoCommandHandler : IRequestHandler<AnexarFotoItemInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public AnexarFotoItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarFotoItemInspecaoCommand request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct)
            ?? throw new KeyNotFoundException($"Resposta {request.RespostaId} não encontrada.");

        resposta.FotoConteudo = request.FotoConteudo;
        resposta.FotoContentType = request.FotoContentType;

        await _db.SaveChangesAsync(ct);
    }
}
