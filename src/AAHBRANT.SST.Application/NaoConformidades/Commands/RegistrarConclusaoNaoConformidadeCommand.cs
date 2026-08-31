using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Procedimento de Inspeção Técnica de Campo (§6.6) — o responsável, ao concluir a execução, registra
// a conclusão e envia para validação do inspetor. Marca a AcaoPlano vinculada mais recente ainda
// pendente como concluída (mesmo efeito de ValidarAcaoPlanoCommand sobre o campo DataConclusao, mas
// sem marcar validação — essa é etapa separada, do inspetor, em EncerrarNaoConformidadeCommand) e
// move a NC para AguardandoValidacao. Evidência da execução (§6.6: "anexar fotografia(s) ou
// documento(s)"): mesmo gap pré-existente já registrado em NaoConformidade/AcaoPlano — sem
// mecanismo de anexo genérico implementado, fora de escopo aqui.
public record RegistrarConclusaoNaoConformidadeCommand(Guid NaoConformidadeId, string? DescricaoConclusao) : IRequest;

public class RegistrarConclusaoNaoConformidadeCommandValidator : AbstractValidator<RegistrarConclusaoNaoConformidadeCommand>
{
    public RegistrarConclusaoNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.NaoConformidadeId).NotEmpty();
        RuleFor(x => x.DescricaoConclusao).MaximumLength(1000);
    }
}

public class RegistrarConclusaoNaoConformidadeCommandHandler : IRequestHandler<RegistrarConclusaoNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;

    public RegistrarConclusaoNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarConclusaoNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades.FirstOrDefaultAsync(n => n.Id == request.NaoConformidadeId, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.NaoConformidadeId} não encontrada.");

        if (nc.Status is not (StatusNaoConformidade.EmAndamento or StatusNaoConformidade.Devolvida))
            throw new InvalidOperationException(
                "Só é possível registrar conclusão de uma ocorrência Em andamento ou Devolvida.");

        var acao = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Domain.Entidades.NaoConformidade) && a.OrigemId == nc.Id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(a => a.Status != StatusControleRisco.Concluido, ct)
            ?? throw new InvalidOperationException(
                "Não há ação de plano pendente vinculada a esta ocorrência para concluir.");

        var agora = DateTime.UtcNow;
        acao.Status = StatusControleRisco.Concluido;
        acao.DataConclusao = agora;
        if (request.DescricaoConclusao is { Length: > 0 })
            acao.Descricao = $"{acao.Descricao} — Conclusão: {request.DescricaoConclusao}";

        nc.Status = StatusNaoConformidade.AguardandoValidacao;

        await _db.SaveChangesAsync(ct);
    }
}
