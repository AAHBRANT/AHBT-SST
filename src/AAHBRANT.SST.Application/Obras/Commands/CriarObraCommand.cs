using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Obras.Commands;

public record CriarObraCommand(
    string Codigo,
    string Nome,
    string? Cliente,
    StatusObra Status,
    DateTime? DataInicio,
    DateTime? DataPrevisaoTermino,
    string? Endereco,
    string? Cidade,
    string? Uf,
    string? Cnpj) : IRequest<Guid>;

public class CriarObraCommandValidator : AbstractValidator<CriarObraCommand>
{
    public CriarObraCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Uf).MaximumLength(2);
        RuleFor(x => x.Cnpj).MaximumLength(18);
    }
}

public class CriarObraCommandHandler : IRequestHandler<CriarObraCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarObraCommand request, CancellationToken ct)
    {
        var obra = new Obra
        {
            Codigo = request.Codigo,
            Nome = request.Nome,
            Cliente = request.Cliente,
            Status = request.Status,
            DataInicio = request.DataInicio,
            DataPrevisaoTermino = request.DataPrevisaoTermino,
            Endereco = request.Endereco,
            Cidade = request.Cidade,
            Uf = request.Uf,
            Cnpj = request.Cnpj
        };

        _db.Obras.Add(obra);
        await _db.SaveChangesAsync(ct);
        return obra.Id;
    }
}
