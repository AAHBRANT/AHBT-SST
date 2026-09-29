using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record AtualizarFuncionariosDdsCommand(Guid DdsId, List<Guid> TrabalhadoresIds) : IRequest;

public class AtualizarFuncionariosDdsCommandValidator : AbstractValidator<AtualizarFuncionariosDdsCommand>
{
    public AtualizarFuncionariosDdsCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.TrabalhadoresIds).NotNull();
        RuleForEach(x => x.TrabalhadoresIds).NotEmpty();
    }
}

public class AtualizarFuncionariosDdsCommandHandler(IAppDbContext db)
    : IRequestHandler<AtualizarFuncionariosDdsCommand>
{
    public async Task Handle(AtualizarFuncionariosDdsCommand request, CancellationToken ct)
    {
        var dds = await db.Dds.Include(d => d.DdsSemanal).FirstOrDefaultAsync(d => d.Id == request.DdsId, ct)
            ?? throw new KeyNotFoundException("DDS não encontrado.");
        if (dds.SemExpediente || dds.Status != StatusDds.EmAndamento || dds.DdsSemanal?.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("A lista de funcionários só pode ser alterada em um DDS em andamento.");

        // Confirmados nunca são excluídos por uma edição da lista, inclusive em chamadas concorrentes.
        var confirmados = await db.DdsParticipantes.Where(p => p.DdsId == dds.Id && p.Ativo)
            .Select(p => p.TrabalhadorId).ToListAsync(ct);
        var ids = request.TrabalhadoresIds.Distinct().Except(confirmados).ToHashSet();
        var atuais = await db.DdsFuncionariosSelecionados.Where(s => s.DdsId == dds.Id).ToListAsync(ct);
        var existentes = atuais.Select(s => s.TrabalhadorId).ToHashSet();
        var novos = ids.Except(existentes).ToList();

        var totalValidos = await db.Trabalhadores.CountAsync(t => novos.Contains(t.Id)
            && t.ObraId == dds.ObraId && t.Ativo && t.Situacao == SituacaoTrabalhador.Ativo
            && (!t.DataDemissao.HasValue || t.DataDemissao.Value.Date > dds.Data.Date), ct);
        if (totalValidos != novos.Count)
            throw new InvalidOperationException("Selecione apenas funcionários ativos da obra deste DDS.");

        // Valida o lote inteiro antes de alterar a seleção. Remover usa a exclusão lógica do contexto.
        db.DdsFuncionariosSelecionados.RemoveRange(atuais.Where(s => !ids.Contains(s.TrabalhadorId)));
        db.DdsFuncionariosSelecionados.AddRange(novos.Select(id => new DdsFuncionarioSelecionado
        {
            DdsId = dds.Id,
            TrabalhadorId = id,
        }));
        await db.SaveChangesAsync(ct);
    }
}
