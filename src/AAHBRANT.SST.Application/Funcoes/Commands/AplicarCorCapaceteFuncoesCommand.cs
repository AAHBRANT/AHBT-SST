using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record AplicarCorCapaceteItem(Guid FuncaoId, CorCapacete Cor);

// Grava de uma vez a cor do capacete de várias funções (a conferência da tela "Aplicar padrão de cores").
// Tudo ou nada: função inexistente derruba o lote inteiro, nada é gravado.
public record AplicarCorCapaceteFuncoesCommand(List<AplicarCorCapaceteItem> Itens) : IRequest;

public class AplicarCorCapaceteFuncoesCommandValidator : AbstractValidator<AplicarCorCapaceteFuncoesCommand>
{
    public const int MaximoItens = 1000;

    public AplicarCorCapaceteFuncoesCommandValidator()
    {
        RuleFor(x => x.Itens).NotNull();
        RuleFor(x => x.Itens).Must(i => i.Count <= MaximoItens)
            .When(x => x.Itens is not null)
            .WithMessage($"Informe no máximo {MaximoItens} funções por vez.");
        RuleFor(x => x.Itens).Must(i => i.Select(x => x.FuncaoId).Distinct().Count() == i.Count)
            .When(x => x.Itens is not null)
            .WithMessage("A mesma função aparece mais de uma vez.");
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.FuncaoId).NotEmpty();
            item.RuleFor(i => i.Cor).IsInEnum();
        });
    }
}

public class AplicarCorCapaceteFuncoesCommandHandler : IRequestHandler<AplicarCorCapaceteFuncoesCommand>
{
    private readonly IAppDbContext _db;

    public AplicarCorCapaceteFuncoesCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AplicarCorCapaceteFuncoesCommand request, CancellationToken ct)
    {
        if (request.Itens.Count == 0) return;

        var ids = request.Itens.Select(i => i.FuncaoId).ToList();
        var funcoes = await _db.Funcoes.Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var ausente = ids.FirstOrDefault(id => !funcoes.ContainsKey(id));
        if (ausente != Guid.Empty) throw new KeyNotFoundException($"Função {ausente} não encontrada.");

        foreach (var item in request.Itens) funcoes[item.FuncaoId].CorCapacete = item.Cor;
        await _db.SaveChangesAsync(ct);
    }
}
