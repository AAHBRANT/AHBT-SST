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
    string? Uf) : IRequest;

public class AtualizarObraCommandValidator : AbstractValidator<AtualizarObraCommand>
{
    public AtualizarObraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Uf).MaximumLength(2);
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
        obra.Cliente = request.Cliente;
        obra.Status = request.Status;
        obra.DataInicio = request.DataInicio;
        obra.DataPrevisaoTermino = request.DataPrevisaoTermino;
        obra.DataTerminoReal = request.DataTerminoReal;
        obra.Endereco = request.Endereco;
        obra.Cidade = request.Cidade;
        obra.Uf = request.Uf;

        await _db.SaveChangesAsync(ct);
    }
}
