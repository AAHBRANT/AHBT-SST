using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Evidência posterior (depois de resolvido o achado) — par de AnexarFotoItemInspecaoCommand (a
// evidência anterior), pedido "Patrulha de Segurança do Trabalho" (planilha do usuário, 31/08).
public record AnexarFotoDepoisItemInspecaoCommand(
    Guid RespostaId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest;

public class AnexarFotoDepoisItemInspecaoCommandValidator : AbstractValidator<AnexarFotoDepoisItemInspecaoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoDepoisItemInspecaoCommandValidator()
    {
        RuleFor(x => x.RespostaId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoDepoisItemInspecaoCommandHandler : IRequestHandler<AnexarFotoDepoisItemInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public AnexarFotoDepoisItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarFotoDepoisItemInspecaoCommand request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct)
            ?? throw new KeyNotFoundException($"Resposta {request.RespostaId} não encontrada.");

        resposta.FotoDepoisConteudo = request.FotoConteudo;
        resposta.FotoDepoisContentType = request.FotoContentType;

        await _db.SaveChangesAsync(ct);
    }
}
