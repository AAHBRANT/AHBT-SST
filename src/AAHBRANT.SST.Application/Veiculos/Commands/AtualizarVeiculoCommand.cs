using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Veiculos.Commands;

// Editar veículo, inclusive trocar de obra. O histórico de inspeções concluídas fica como está
// (cada uma guarda a obra onde foi feita); só a inspeção em andamento acompanha a nova obra.
// Trocar o tipo é recusado enquanto houver inspeção em andamento, porque o checklist dela é o do
// tipo anterior.
public record AtualizarVeiculoCommand(
    Guid Id,
    Guid ObraId,
    TipoVeiculo Tipo,
    string PlacaPrefixo,
    string? MarcaModelo,
    int? Ano,
    string? Cor,
    string? Empresa,
    string? Responsavel) : IRequest;

public class AtualizarVeiculoCommandValidator : AbstractValidator<AtualizarVeiculoCommand>
{
    public AtualizarVeiculoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class AtualizarVeiculoCommandHandler : IRequestHandler<AtualizarVeiculoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarVeiculoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarVeiculoCommand request, CancellationToken ct)
    {
        var veiculo = await _db.Veiculos.FirstOrDefaultAsync(v => v.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Veículo {request.Id} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var placa = request.PlacaPrefixo.Trim();
        if (await _db.Veiculos.AnyAsync(v => v.Id != veiculo.Id && v.PlacaPrefixo == placa, ct))
            throw new InvalidOperationException($"Já existe outro veículo cadastrado com a placa/prefixo \"{placa}\".");

        var emAndamento = await _db.Inspecoes
            .Where(i => i.VeiculoId == veiculo.Id && i.Status == StatusInspecao.EmAndamento)
            .ToListAsync(ct);

        if (emAndamento.Count > 0 && request.Tipo != veiculo.Tipo)
            throw new InvalidOperationException(
                "Não é possível trocar o tipo do veículo com uma inspeção em andamento. Conclua ou exclua a inspeção primeiro.");

        foreach (var inspecao in emAndamento)
            inspecao.ObraId = request.ObraId;

        veiculo.ObraId = request.ObraId;
        veiculo.Tipo = request.Tipo;
        veiculo.PlacaPrefixo = placa;
        veiculo.MarcaModelo = request.MarcaModelo?.Trim();
        veiculo.Ano = request.Ano;
        veiculo.Cor = request.Cor?.Trim();
        veiculo.Empresa = request.Empresa?.Trim();
        veiculo.Responsavel = request.Responsavel?.Trim();

        await _db.SaveChangesAsync(ct);
    }
}
