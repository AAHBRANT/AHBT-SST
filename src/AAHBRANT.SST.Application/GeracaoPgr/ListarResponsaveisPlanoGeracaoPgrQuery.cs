using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Quem pode ser responsável por uma ação do plano do PGR (23/11/2026). A tela usava a lista de usuários, que exige
// usuario:ver; técnico sem essa permissão via a lista vazia e não conseguia escolher. Esta consulta vale para quem vê o
// PGR e devolve só id e nome dos usuários ativos (nada de e-mail, perfil ou CPF).
public record ResponsavelPlanoDto(Guid Id, string Nome);

public record ListarResponsaveisPlanoGeracaoPgrQuery(Guid GeracaoId) : IRequest<List<ResponsavelPlanoDto>>;

public class ListarResponsaveisPlanoGeracaoPgrQueryHandler : IRequestHandler<ListarResponsaveisPlanoGeracaoPgrQuery, List<ResponsavelPlanoDto>>
{
    private readonly IAppDbContext _db;

    public ListarResponsaveisPlanoGeracaoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ResponsavelPlanoDto>> Handle(ListarResponsaveisPlanoGeracaoPgrQuery request, CancellationToken ct)
    {
        // Garante que a geração existe e está no escopo de obras de quem pergunta.
        await SerializacaoGeracao.CarregarAsync(_db, request.GeracaoId, ct);
        return await _db.Usuarios.Where(u => u.Status == StatusUsuario.Ativo)
            .OrderBy(u => u.Nome)
            .Select(u => new ResponsavelPlanoDto(u.Id, u.Nome))
            .ToListAsync(ct);
    }
}
