using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Higienizacao.Commands;

public record RegistrarHigienizacaoCommand(
    Guid ItemHigienizacaoId,
    Guid TrabalhadorId,
    string? Observacoes,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest<Guid>;

public class RegistrarHigienizacaoCommandValidator : AbstractValidator<RegistrarHigienizacaoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public RegistrarHigienizacaoCommandValidator()
    {
        RuleFor(x => x.ItemHigienizacaoId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto do local higienizado é obrigatória para registrar a limpeza.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class RegistrarHigienizacaoCommandHandler : IRequestHandler<RegistrarHigienizacaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public RegistrarHigienizacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(RegistrarHigienizacaoCommand request, CancellationToken ct)
    {
        var itemExiste = await _db.ItensHigienizacao.AnyAsync(i => i.Id == request.ItemHigienizacaoId, ct);
        if (!itemExiste)
            throw new KeyNotFoundException($"Item de higienização {request.ItemHigienizacaoId} não encontrado.");

        var trabalhadorExiste = await _db.Trabalhadores.AnyAsync(t => t.Id == request.TrabalhadorId, ct);
        if (!trabalhadorExiste)
            throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        var registro = new Domain.Entidades.RegistroHigienizacao
        {
            ItemHigienizacaoId = request.ItemHigienizacaoId,
            TrabalhadorId = request.TrabalhadorId,
            DataHora = DateTime.UtcNow,
            Observacoes = request.Observacoes,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
        };
        _db.RegistrosHigienizacao.Add(registro);
        await _db.SaveChangesAsync(ct);
        return registro.Id;
    }
}
