using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Commands;

public record AtualizarObraCommand(
    Guid Id,
    string Codigo,
    string Nome,
    string? Cliente,
    StatusObra Status,
    DateTime? DataInicio,
    DateTime? DataPrevisaoTermino,
    DateTime? DataTerminoReal,
    string? Endereco,
    string? Cidade,
    string? Uf,
    string? Cnpj = null,
    string? Cep = null,
    string? Cnae = null,
    int? GrauRisco = null) : IRequest;

public class AtualizarObraCommandValidator : AbstractValidator<AtualizarObraCommand>
{
    public AtualizarObraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Uf).MaximumLength(2);
        RuleFor(x => x.Cep).MaximumLength(10);
        RuleFor(x => x.Cnpj).MaximumLength(18);
        RuleFor(x => x.Cnae).MaximumLength(200);
        RuleFor(x => x.GrauRisco).InclusiveBetween(1, 4).When(x => x.GrauRisco.HasValue);
    }
}

public class AtualizarObraCommandHandler : IRequestHandler<AtualizarObraCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarObraCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Obra {request.Id} não encontrada.");

        obra.Codigo = request.Codigo;
        obra.Nome = request.Nome;
        if (request.Cliente is not null)
            obra.Cliente = string.IsNullOrWhiteSpace(request.Cliente) ? null : request.Cliente.Trim();
        obra.Status = request.Status;
        obra.DataInicio = request.DataInicio;
        obra.DataPrevisaoTermino = request.DataPrevisaoTermino;
        obra.DataTerminoReal = request.DataTerminoReal;
        obra.Endereco = string.IsNullOrWhiteSpace(request.Endereco) ? null : request.Endereco.Trim();
        obra.Cidade = string.IsNullOrWhiteSpace(request.Cidade) ? null : request.Cidade.Trim();
        obra.Uf = string.IsNullOrWhiteSpace(request.Uf) ? null : request.Uf.Trim();
        obra.Cep = string.IsNullOrWhiteSpace(request.Cep) ? null : request.Cep.Trim();
        obra.Cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim();
        obra.Cnae = string.IsNullOrWhiteSpace(request.Cnae) ? null : request.Cnae.Trim();
        obra.GrauRisco = request.GrauRisco;

        await _db.SaveChangesAsync(ct);
    }
}
