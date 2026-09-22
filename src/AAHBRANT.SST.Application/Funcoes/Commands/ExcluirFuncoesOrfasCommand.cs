using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes.Queries;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Funcoes.Commands;

public record ExcluirFuncoesOrfasCommand(List<Guid> FuncaoIds) : IRequest<int>;

public class ExcluirFuncoesOrfasCommandValidator : AbstractValidator<ExcluirFuncoesOrfasCommand>
{
    public ExcluirFuncoesOrfasCommandValidator()
    {
        RuleFor(x => x.FuncaoIds).NotNull();
        RuleForEach(x => x.FuncaoIds).NotEmpty();
    }
}

// Pedido do usuário (22/09): exclusão em massa das funções "lixo" detectadas por
// ListarFuncoesOrfasQuery (sem CBO, sem EPI/Treinamento/Uniforme na matriz, sem trabalhador
// vinculado). Reconfirma as 5 condições aqui — não confia soltamente na lista que a tela mandou —
// porque alguém pode ter vinculado um trabalhador ou configurado uma matriz nessa função entre a
// tela carregar e o clique em "Excluir"; a que não bate mais os critérios é ignorada em vez de
// excluída à força. "Excluir" aqui é soft-delete (Ativo=false, ver SstDbContext.AplicarAuditoria),
// como todo o resto do sistema — nunca some fisicamente.
public class ExcluirFuncoesOrfasCommandHandler : IRequestHandler<ExcluirFuncoesOrfasCommand, int>
{
    private readonly IAppDbContext _db;

    public ExcluirFuncoesOrfasCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(ExcluirFuncoesOrfasCommand request, CancellationToken ct)
    {
        var idsSolicitados = request.FuncaoIds.Distinct().ToHashSet();
        var funcoes = await _db.Funcoes
            .Where(f => idsSolicitados.Contains(f.Id) && (f.CboCodigo == null || f.CboCodigo == ""))
            .ToListAsync(ct);
        if (funcoes.Count == 0) return 0;

        var idsProtegidos = await DeteccaoFuncaoOrfa.IdsProtegidosAsync(_db, ct);

        var paraExcluir = funcoes.Where(f => !idsProtegidos.Contains(f.Id)).ToList();
        foreach (var funcao in paraExcluir) _db.Funcoes.Remove(funcao);

        await _db.SaveChangesAsync(ct);
        return paraExcluir.Count;
    }
}
