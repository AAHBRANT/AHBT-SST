using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

// Correção de emergência (22/09): reverte uma exclusão (sempre soft-delete, Ativo=false — ver
// SstDbContext.AplicarAuditoria) do botão "Excluir" de linha, que nunca verificou se a função ainda
// tinha trabalhador vinculado. Não existe "reativação em massa" de propósito — cada função reativada
// aqui é uma decisão específica, uma por vez, a partir da lista de ListarFuncoesInativasComTrabalhadorQuery.
public record ReativarFuncaoCommand(Guid FuncaoId) : IRequest;

public class ReativarFuncaoCommandValidator : AbstractValidator<ReativarFuncaoCommand>
{
    public ReativarFuncaoCommandValidator()
    {
        RuleFor(x => x.FuncaoId).NotEmpty();
    }
}

public class ReativarFuncaoCommandHandler : IRequestHandler<ReativarFuncaoCommand>
{
    private readonly IAppDbContext _db;

    public ReativarFuncaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ReativarFuncaoCommand request, CancellationToken ct)
    {
        var funcao = await _db.Funcoes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == request.FuncaoId, ct)
            ?? throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");

        funcao.Ativo = true;
        await _db.SaveChangesAsync(ct);
    }
}
