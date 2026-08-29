using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Bloqueia encerramento enquanto houver item sem resposta — aplicação do mesmo princípio de
// bloqueio preventivo já usado em AutorizarPermissaoTrabalhoCommand; não é uma regra literal do
// §23 isoladamente, mas garante que "cada inspeção deverá gerar evidência" (§23) tenha, no
// mínimo, todo item do checklist respondido antes de fechar.
//
// Motor de Assinatura Eletrônica — mesma decisão tomada em EncerrarNaoConformidadeCommand: garante
// (idempotente) um DocumentoAssinatura para a inspeção, sem bloquear o encerramento por ele ainda
// não estar Finalizado (mesmo padrão não-bloqueante já usado por EncerrarDdsCommand).
public record EncerrarInspecaoCommand(Guid Id) : IRequest;

public class EncerrarInspecaoCommandValidator : AbstractValidator<EncerrarInspecaoCommand>
{
    public EncerrarInspecaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarInspecaoCommandHandler : IRequestHandler<EncerrarInspecaoCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes
            .Include(i => i.Respostas)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Inspeção {request.Id} não encontrada.");

        var pendentes = inspecao.Respostas.Where(r => r.Ativo && r.StatusItem == null).ToList();
        if (pendentes.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível encerrar: {pendentes.Count} item(ns) do checklist ainda não respondido(s).");

        inspecao.Status = StatusInspecao.Concluida;

        var documentoExistente = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == nameof(Domain.Entidades.Inspecao) && d.EntidadeId == inspecao.Id, ct);
        if (documentoExistente is null)
            _db.DocumentosAssinatura.Add(new DocumentoAssinatura
            {
                EntidadeTipo = nameof(Domain.Entidades.Inspecao),
                EntidadeId = inspecao.Id,
            });

        await _db.SaveChangesAsync(ct);
    }
}
