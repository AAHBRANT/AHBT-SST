using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Relatorios;

// Monta os dados da lista de presença de um DDS a partir do detalhe que o próprio sistema já mostra
// (participantes com a hora da assinatura e funcionários selecionados que não confirmaram = ausentes).
public record ConstruirListaPresencaQuery(Guid DdsId) : IRequest<ListaPresencaDados?>;

public class ConstruirListaPresencaQueryHandler : IRequestHandler<ConstruirListaPresencaQuery, ListaPresencaDados?>
{
    // Até quantos DDS anteriores olhar para contar faltas seguidas.
    private const int JanelaDeFaltasSeguidas = 10;

    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public ConstruirListaPresencaQueryHandler(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public async Task<ListaPresencaDados?> Handle(ConstruirListaPresencaQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.DdsId), ct);
        if (detalhe is null) return null;

        var dds = await _db.Dds.AsNoTracking().FirstAsync(d => d.Id == request.DdsId, ct);

        var matriculas = await _db.Trabalhadores
            .Where(t => detalhe.Participantes.Select(p => p.TrabalhadorId).Contains(t.Id))
            .Select(t => new { t.Id, t.Matricula })
            .ToDictionaryAsync(t => t.Id, t => t.Matricula, ct);

        var presentes = detalhe.Participantes
            .OrderBy(p => p.AssinadoEm ?? DateTime.MaxValue)
            .ThenBy(p => p.TrabalhadorNome)
            .Select(p => new PresencaLinha(
                p.TrabalhadorNome,
                matriculas.GetValueOrDefault(p.TrabalhadorId),
                p.AssinadoEm is { } quando ? FusoBrasilia.ParaBrasilia(quando) : null))
            .ToList();

        var faltas = await ContarFaltasSeguidasAsync(dds.ObraId, dds.Data, detalhe.FuncionariosSelecionados.Select(f => f.TrabalhadorId).ToList(), ct);
        var ausentes = detalhe.FuncionariosSelecionados
            .OrderBy(f => f.Nome)
            .Select(f => new AusenteLinha(f.Nome, f.Matricula, faltas.GetValueOrDefault(f.TrabalhadorId, 1)))
            .ToList();

        // Duração: 1ª à última assinatura de participante (precisa de pelo menos duas).
        var assinaturas = detalhe.Participantes.Where(p => p.AssinadoEm is not null).Select(p => p.AssinadoEm!.Value).OrderBy(a => a).ToList();
        DateTime? primeira = assinaturas.Count > 0 ? FusoBrasilia.ParaBrasilia(assinaturas[0]) : null;
        DateTime? ultima = assinaturas.Count > 0 ? FusoBrasilia.ParaBrasilia(assinaturas[^1]) : null;
        int? duracao = assinaturas.Count >= 2 ? (int)Math.Round((assinaturas[^1] - assinaturas[0]).TotalMinutes) : null;

        DateTime? fechadoEm = dds.EncerradoEm is { } enc ? FusoBrasilia.ParaBrasilia(enc) : null;
        int? ateFechar = dds.EncerradoEm is { } encerrado && assinaturas.Count > 0
            ? (int)Math.Round((encerrado - assinaturas[0]).TotalMinutes) : null;

        var tema = detalhe.Dds.TemaLivreNome
            ?? detalhe.Dds.TemasAtividades.Select(t => t.PerigoNome ?? t.AtividadeNome).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        return new ListaPresencaDados(
            dds.Id, detalhe.Dds.ObraNome, dds.Data, tema, FusoBrasilia.Agora(),
            presentes.Count + ausentes.Count, presentes, ausentes, primeira, ultima, duracao, fechadoEm, ateFechar);
    }

    // Para cada ausente: quantos DDS seguidos (incluindo este) ficou de fora estando na lista do DDS.
    private async Task<Dictionary<Guid, int>> ContarFaltasSeguidasAsync(Guid obraId, DateTime dataAtual, List<Guid> ausentesIds, CancellationToken ct)
    {
        var resultado = ausentesIds.ToDictionary(id => id, _ => 1);
        if (ausentesIds.Count == 0) return resultado;

        var anteriores = await _db.Dds
            .Where(d => d.ObraId == obraId && d.Data < dataAtual && d.Status == StatusDds.Concluido && !d.SemExpediente)
            .OrderByDescending(d => d.Data)
            .Take(JanelaDeFaltasSeguidas)
            .Select(d => d.Id)
            .ToListAsync(ct);
        if (anteriores.Count == 0) return resultado;

        var presencas = await _db.DdsParticipantes
            .Where(p => anteriores.Contains(p.DdsId) && ausentesIds.Contains(p.TrabalhadorId))
            .Select(p => new { p.DdsId, p.TrabalhadorId })
            .ToListAsync(ct);
        var listados = await _db.DdsFuncionariosSelecionados
            .Where(s => anteriores.Contains(s.DdsId) && ausentesIds.Contains(s.TrabalhadorId))
            .Select(s => new { s.DdsId, s.TrabalhadorId })
            .ToListAsync(ct);

        foreach (var id in ausentesIds)
        {
            var seguidas = 1;
            foreach (var anteriorId in anteriores) // do mais recente para o mais antigo
            {
                var compareceu = presencas.Any(p => p.DdsId == anteriorId && p.TrabalhadorId == id);
                var estavaNaLista = listados.Any(s => s.DdsId == anteriorId && s.TrabalhadorId == id);
                if (compareceu || !estavaNaLista) break;
                seguidas++;
            }
            resultado[id] = seguidas;
        }
        return resultado;
    }
}
