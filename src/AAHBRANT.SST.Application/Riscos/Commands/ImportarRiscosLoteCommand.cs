using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Riscos;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Riscos.Commands;

// Importação em lote (Base de Conhecimento §14/§36): usada para transcrever inventários de risco
// inteiros (ex.: PGR de uma obra) sem precisar cadastrar Atividade → Perigo → Risco um por um pela
// tela. Resolve Atividade e Perigo por nome (cria se não existir) e reaproveita se já existirem —
// mesma lógica de "achar ou criar" que um operador faria manualmente.
public record ImportarRiscoLoteItem(
    string NomeAtividade,
    string? DescricaoAtividade,
    string NomePerigo,
    string? AgentePerigo,
    string? Ambiente,
    string? Exposicao,
    string? Consequencia,
    int Probabilidade,
    int Severidade,
    string? ControlesExistentes,
    string? ControlesAdicionais);

public record ImportarRiscosLoteCommand(Guid ObraId, List<ImportarRiscoLoteItem> Itens) : IRequest<ImportarRiscosLoteResultado>;

public record ImportarRiscosLoteResultado(int AtividadesCriadas, int PerigosCriados, int RiscosCriados);

public class ImportarRiscosLoteCommandValidator : AbstractValidator<ImportarRiscosLoteCommand>
{
    public ImportarRiscosLoteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Itens).NotEmpty();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(x => x.NomeAtividade).NotEmpty().MaximumLength(200);
            item.RuleFor(x => x.NomePerigo).NotEmpty().MaximumLength(200);
            item.RuleFor(x => x.Probabilidade).GreaterThan(0);
            item.RuleFor(x => x.Severidade).GreaterThan(0);
        });
    }
}

public class ImportarRiscosLoteCommandHandler : IRequestHandler<ImportarRiscosLoteCommand, ImportarRiscosLoteResultado>
{
    private readonly IAppDbContext _db;

    public ImportarRiscosLoteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ImportarRiscosLoteResultado> Handle(ImportarRiscosLoteCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
        {
            throw new KeyNotFoundException("Obra não encontrada.");
        }

        var atividadesExistentes = await _db.Atividades
            .Where(a => a.ObraId == request.ObraId)
            .ToDictionaryAsync(a => a.Nome, a => a, StringComparer.OrdinalIgnoreCase, ct);

        var perigosExistentes = await _db.Perigos
            .ToDictionaryAsync(p => p.Nome, p => p, StringComparer.OrdinalIgnoreCase, ct);

        var matriz = await _db.MatrizRiscoConfigs
            .Include(c => c.Celulas)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Nenhuma MatrizRiscoConfig cadastrada.");

        var atividadesCriadas = 0;
        var perigosCriados = 0;
        var riscosCriados = 0;

        foreach (var item in request.Itens)
        {
            if (!atividadesExistentes.TryGetValue(item.NomeAtividade, out var atividade))
            {
                atividade = new Atividade
                {
                    ObraId = request.ObraId,
                    Nome = item.NomeAtividade,
                    Descricao = item.DescricaoAtividade,
                };
                _db.Atividades.Add(atividade);
                atividadesExistentes[item.NomeAtividade] = atividade;
                atividadesCriadas++;
            }

            if (!perigosExistentes.TryGetValue(item.NomePerigo, out var perigo))
            {
                perigo = new Perigo
                {
                    Nome = item.NomePerigo,
                    Agente = item.AgentePerigo,
                };
                _db.Perigos.Add(perigo);
                perigosExistentes[item.NomePerigo] = perigo;
                perigosCriados++;
            }

            var celula = matriz.Celulas.FirstOrDefault(c => c.Probabilidade == item.Probabilidade && c.Severidade == item.Severidade)
                ?? throw new InvalidOperationException(
                    $"A matriz de risco '{matriz.Nome}' não tem célula para Probabilidade={item.Probabilidade}/Severidade={item.Severidade} (item '{item.NomeAtividade}' / '{item.NomePerigo}').");

            _db.Riscos.Add(new Risco
            {
                Atividade = atividade,
                Perigo = perigo,
                Ambiente = item.Ambiente,
                Exposicao = item.Exposicao,
                Consequencia = item.Consequencia,
                Probabilidade = item.Probabilidade,
                Severidade = item.Severidade,
                NivelRisco = celula.NivelRisco,
                ControlesExistentes = item.ControlesExistentes,
                ControlesAdicionais = item.ControlesAdicionais,
            });
            riscosCriados++;
        }

        await _db.SaveChangesAsync(ct);

        return new ImportarRiscosLoteResultado(atividadesCriadas, perigosCriados, riscosCriados);
    }
}
