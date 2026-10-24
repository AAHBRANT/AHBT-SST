using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Veiculos.Commands;

// Cadastro manual de veículo/equipamento por obra (sem integração com outro sistema). Quem
// cadastra é o Técnico (permissão inspecao:criar); exclusão é só do Administrador.
public record CriarVeiculoCommand(
    Guid ObraId,
    TipoVeiculo Tipo,
    string PlacaPrefixo,
    string? MarcaModelo,
    int? Ano,
    string? Cor,
    string? Empresa,
    string? Responsavel) : IRequest<Guid>;

public class CriarVeiculoCommandValidator : AbstractValidator<CriarVeiculoCommand>
{
    public CriarVeiculoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.PlacaPrefixo).NotEmpty().MaximumLength(60);
        RuleFor(x => x.MarcaModelo).MaximumLength(150);
        RuleFor(x => x.Ano).InclusiveBetween(1950, 2100).When(x => x.Ano.HasValue);
        RuleFor(x => x.Cor).MaximumLength(40);
        RuleFor(x => x.Empresa).MaximumLength(150);
        RuleFor(x => x.Responsavel).MaximumLength(150);
    }
}

public class CriarVeiculoCommandHandler : IRequestHandler<CriarVeiculoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarVeiculoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarVeiculoCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var placa = request.PlacaPrefixo.Trim();
        if (await _db.Veiculos.AnyAsync(v => v.PlacaPrefixo == placa, ct))
            throw new InvalidOperationException($"Já existe um veículo cadastrado com a placa/prefixo \"{placa}\".");

        var veiculo = new Veiculo
        {
            ObraId = request.ObraId,
            Tipo = request.Tipo,
            PlacaPrefixo = placa,
            MarcaModelo = request.MarcaModelo?.Trim(),
            Ano = request.Ano,
            Cor = request.Cor?.Trim(),
            Empresa = request.Empresa?.Trim(),
            Responsavel = request.Responsavel?.Trim(),
        };
        _db.Veiculos.Add(veiculo);
        await _db.SaveChangesAsync(ct);
        return veiculo.Id;
    }
}
