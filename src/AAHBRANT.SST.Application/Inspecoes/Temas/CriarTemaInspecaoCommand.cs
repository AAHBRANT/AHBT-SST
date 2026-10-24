using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Temas;

public record CriarTemaInspecaoItemInput(string Descricao, string? Secao = null);

// Tema de inspeção criado pelo usuário (ex.: "Andaime"): nasce com o checklist próprio, versão 1.
// Como nos Veículos, o item do checklist só exige foto quando marcado Não Conforme (ver
// EncerrarInspecaoCommand), por isso ExigeFotografia fica falso no modelo.
public record CriarTemaInspecaoCommand(string Nome, List<CriarTemaInspecaoItemInput> Itens) : IRequest<Guid>;

public class CriarTemaInspecaoCommandValidator : AbstractValidator<CriarTemaInspecaoCommand>
{
    public CriarTemaInspecaoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("Informe o nome do tema (ex.: Andaime).").MaximumLength(120);
        RuleFor(x => x.Itens).NotEmpty().WithMessage("Adicione ao menos um item ao checklist.");
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Descricao).NotEmpty().WithMessage("Todo item do checklist precisa de uma descrição.").MaximumLength(500);
            item.RuleFor(i => i.Secao).MaximumLength(150);
        });
    }
}

public class CriarTemaInspecaoCommandHandler : IRequestHandler<CriarTemaInspecaoCommand, Guid>
{
    // Alojamento e Veículos já são cards fixos da tela; um tema com o mesmo nome confundiria.
    private static readonly string[] NomesReservados = { "alojamento", "alojamentos", "veículo", "veiculo", "veículos", "veiculos" };

    private readonly IAppDbContext _db;
    public CriarTemaInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarTemaInspecaoCommand request, CancellationToken ct)
    {
        var nome = request.Nome.Trim();
        if (NomesReservados.Contains(nome.ToLowerInvariant()))
            throw new InvalidOperationException($"\"{nome}\" já é um card fixo de Inspeções. Escolha outro nome para o tema.");

        // Comparação sem diferenciar maiúsculas feita em memória: a lista de temas é pequena.
        var existentes = await _db.TemasInspecao.Select(t => t.Nome).ToListAsync(ct);
        if (existentes.Any(n => string.Equals(n, nome, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Já existe um tema chamado \"{nome}\".");

        var tema = new TemaInspecao { Nome = nome };
        var checklist = new ChecklistModelo
        {
            Nome = $"Checklist de {nome}",
            TipoInspecao = TipoInspecao.Tema,
            TemaInspecaoId = tema.Id,
            Versao = 1,
        };
        var ordem = 1;
        foreach (var item in request.Itens)
        {
            checklist.Itens.Add(new ChecklistModeloItem
            {
                Ordem = ordem++,
                Descricao = item.Descricao.Trim(),
                Secao = string.IsNullOrWhiteSpace(item.Secao) ? null : item.Secao.Trim(),
                ExigeFotografia = false,
                ExigeResponsavel = false,
                ExigePrazo = false,
            });
        }

        _db.TemasInspecao.Add(tema);
        _db.ChecklistModelos.Add(checklist);
        await _db.SaveChangesAsync(ct);
        return tema.Id;
    }
}
