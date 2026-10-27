using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Só o rascunho que nunca foi enviado para aprovação pode ser excluído. Quem já foi para o engenheiro (aguardando,
// devolvido ou aprovado) fica no histórico. A permissão (só Administrador) é checada no controller.
public record ExcluirGeracaoPgrCommand(Guid Id) : IRequest;

public class ExcluirGeracaoPgrCommandHandler : IRequestHandler<ExcluirGeracaoPgrCommand>
{
    private readonly IAppDbContext _db;

    public ExcluirGeracaoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ExcluirGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.Rascunho)
            throw new InvalidOperationException("Só dá para excluir um PGR que ainda não foi enviado para aprovação.");

        _db.GeracoesPgrEap.Remove(g);
        await _db.SaveChangesAsync(ct);
    }
}
