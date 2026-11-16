using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Tarefa periódica do Worker: encerra, e apaga o quadro, das sessões ao vivo que pararam e ninguém mais tocou. Devolve quantas encerrou.
public record LimparSessoesAoVivoCameraParadasCommand : IRequest<int>;

public class LimparSessoesAoVivoCameraParadasCommandHandler : IRequestHandler<LimparSessoesAoVivoCameraParadasCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly AoVivoCameraOpcoes _opcoes;

    public LimparSessoesAoVivoCameraParadasCommandHandler(IAppDbContext db, IOptions<AoVivoCameraOpcoes>? opcoes = null)
    {
        _db = db;
        _opcoes = opcoes?.Value ?? new AoVivoCameraOpcoes();
    }

    public Task<int> Handle(LimparSessoesAoVivoCameraParadasCommand request, CancellationToken ct)
        => SessaoAoVivoCameraRegras.EncerrarParadasAsync(_db, _opcoes, agenteId: null, DateTime.UtcNow, ct);
}
