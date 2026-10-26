using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizRisco.Commands;

public record NovaVersaoMatrizRiscoCelula(int Probabilidade, int Severidade, NivelRisco NivelRisco);

// A matriz de risco não se edita nem se exclui (25/10/2026): toda mudança é uma nova versão que substitui a vigente.
// A primeira matriz da empresa é a versão 1; a partir da 2 o motivo da revisão é obrigatório. A versão anterior
// fica guardada como histórico e os riscos já avaliados mantêm o nível que receberam na época.
// UsuarioId e NomeUsuario vêm sempre do token (nunca do corpo da requisição).
public record NovaVersaoMatrizRiscoCommand(
    string Nome,
    int NumNiveisProbabilidade,
    int NumNiveisSeveridade,
    List<NovaVersaoMatrizRiscoCelula> Celulas,
    string? Motivo,
    Guid? UsuarioId,
    string? NomeUsuario) : IRequest<Guid>;

public class NovaVersaoMatrizRiscoCommandValidator : AbstractValidator<NovaVersaoMatrizRiscoCommand>
{
    public NovaVersaoMatrizRiscoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NumNiveisProbabilidade).InclusiveBetween(1, 10);
        RuleFor(x => x.NumNiveisSeveridade).InclusiveBetween(1, 10);
        RuleFor(x => x.Motivo).MaximumLength(500);
        RuleFor(x => x.Celulas).NotEmpty();
        RuleForEach(x => x.Celulas).ChildRules(c => c.RuleFor(x => x.NivelRisco).IsInEnum());

        // Toda combinação probabilidade × severidade precisa ter exatamente um nível: sem buraco, sem repetição,
        // sem célula fora da escala.
        RuleFor(x => x).Custom((comando, contexto) =>
        {
            if (comando.Celulas is null || comando.NumNiveisProbabilidade is < 1 or > 10 || comando.NumNiveisSeveridade is < 1 or > 10) return;
            var esperado = comando.NumNiveisProbabilidade * comando.NumNiveisSeveridade;
            var foraDaEscala = comando.Celulas.Any(c =>
                c.Probabilidade < 1 || c.Probabilidade > comando.NumNiveisProbabilidade ||
                c.Severidade < 1 || c.Severidade > comando.NumNiveisSeveridade);
            var distintas = comando.Celulas.Select(c => (c.Probabilidade, c.Severidade)).Distinct().Count();
            if (foraDaEscala || distintas != comando.Celulas.Count || comando.Celulas.Count != esperado)
                contexto.AddFailure(nameof(comando.Celulas),
                    $"A matriz precisa de uma célula para cada cruzamento ({comando.NumNiveisProbabilidade} × {comando.NumNiveisSeveridade} = {esperado}), sem repetir nem sair da escala.");
        });
    }
}

public class NovaVersaoMatrizRiscoCommandHandler : IRequestHandler<NovaVersaoMatrizRiscoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public NovaVersaoMatrizRiscoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(NovaVersaoMatrizRiscoCommand request, CancellationToken ct)
    {
        var atual = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct);
        if (atual is not null && string.IsNullOrWhiteSpace(request.Motivo))
            throw new InvalidOperationException("Informe o motivo da revisão da matriz de risco.");

        var maiorVersao = await _db.MatrizRiscoConfigs.MaxAsync(m => (int?)m.Versao, ct) ?? 0;
        var agora = DateTime.UtcNow;
        var nome = request.UsuarioId is { } id
            ? await _db.Usuarios.Where(u => u.Id == id).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        // Num único SaveChanges: a anterior deixa de ser vigente no mesmo instante em que a nova passa a valer.
        if (atual is not null)
        {
            atual.Vigente = false;
            atual.SubstituidaEmUtc = agora;
        }

        var nova = new MatrizRiscoConfig
        {
            Nome = request.Nome.Trim(),
            NumNiveisProbabilidade = request.NumNiveisProbabilidade,
            NumNiveisSeveridade = request.NumNiveisSeveridade,
            Versao = maiorVersao + 1,
            Vigente = true,
            VigenteDesdeUtc = agora,
            MotivoRevisao = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim(),
            RevisadaPorUsuarioId = request.UsuarioId,
            RevisadaPorNome = nome ?? request.NomeUsuario,
        };
        foreach (var celula in request.Celulas)
            nova.Celulas.Add(new MatrizRiscoCelula { Probabilidade = celula.Probabilidade, Severidade = celula.Severidade, NivelRisco = celula.NivelRisco });

        _db.MatrizRiscoConfigs.Add(nova);
        await _db.SaveChangesAsync(ct);
        return nova.Id;
    }
}
