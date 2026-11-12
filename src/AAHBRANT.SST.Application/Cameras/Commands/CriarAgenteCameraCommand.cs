using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// O segredo em texto só existe neste retorno: no banco fica apenas o hash.
public record RegistroAgenteCamera(Guid Id, string Segredo);

public record CriarAgenteCameraCommand(Guid ObraId, string Nome) : IRequest<RegistroAgenteCamera>;

public class CriarAgenteCameraCommandValidator : AbstractValidator<CriarAgenteCameraCommand>
{
    public CriarAgenteCameraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
    }
}

public class CriarAgenteCameraCommandHandler : IRequestHandler<CriarAgenteCameraCommand, RegistroAgenteCamera>
{
    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public CriarAgenteCameraCommandHandler(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<RegistroAgenteCamera> Handle(CriarAgenteCameraCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var nome = request.Nome.Trim();
        if (await NomeJaEmUsoAsync(request.ObraId, nome, ct))
            throw new InvalidOperationException(MensagemNomeEmUso(nome));

        // 32 bytes aleatórios (o gerador do hasher) em base64url: sem '+', '/' nem '=', seguro em cabeçalho HTTP.
        // O hash é do texto final, o mesmo que o agente enviará.
        var segredo = _hasher.GerarSegredo().Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var agente = new AgenteCamera
        {
            ObraId = request.ObraId,
            Nome = nome,
            SegredoHash = _hasher.GerarHash(segredo),
        };
        _db.AgentesCamera.Add(agente);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            // Dois cadastros simultâneos com o mesmo nome passam pela checagem acima e um deles bate no índice único.
            // Se o nome agora está em uso, é o mesmo erro de negócio do caminho normal; qualquer outra falha segue.
            _db.DescartarAlteracoesPendentes();
            if (await NomeJaEmUsoAsync(request.ObraId, nome, ct))
                throw new InvalidOperationException(MensagemNomeEmUso(nome), ex);
            throw;
        }

        return new RegistroAgenteCamera(agente.Id, segredo);
    }

    private Task<bool> NomeJaEmUsoAsync(Guid obraId, string nome, CancellationToken ct)
        => _db.AgentesCamera.AnyAsync(a => a.ObraId == obraId && a.Nome == nome && a.Ativo, ct);

    private static string MensagemNomeEmUso(string nome) => $"Já existe um agente chamado \"{nome}\" nesta obra.";
}
