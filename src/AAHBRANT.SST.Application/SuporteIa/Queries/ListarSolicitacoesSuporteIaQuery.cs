using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Queries;

public record ListarSolicitacoesSuporteIaQuery(
    StatusSolicitacaoSuporteIa? Status = null,
    Guid? SolicitanteUsuarioId = null,
    string? SolicitanteEmail = null,
    bool IncluirTodos = false) : IRequest<List<SuporteIaSolicitacaoDto>>;

public class ListarSolicitacoesSuporteIaQueryHandler : IRequestHandler<ListarSolicitacoesSuporteIaQuery, List<SuporteIaSolicitacaoDto>>
{
    private readonly IAppDbContext _db;

    public ListarSolicitacoesSuporteIaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SuporteIaSolicitacaoDto>> Handle(ListarSolicitacoesSuporteIaQuery request, CancellationToken ct)
    {
        var email = request.SolicitanteEmail?.Trim();

        var query = _db.SuporteIaSolicitacoes
            .Where(s => request.Status == null || s.Status == request.Status);

        if (!request.IncluirTodos)
        {
            query = query.Where(s =>
                (request.SolicitanteUsuarioId.HasValue && s.SolicitanteUsuarioId == request.SolicitanteUsuarioId) ||
                (!string.IsNullOrWhiteSpace(email) && s.SolicitanteEmail == email));
        }

        return await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(100)
            .Select(s => new SuporteIaSolicitacaoDto
            {
                Id = s.Id,
                Tipo = s.Tipo,
                SeveridadeInformada = s.SeveridadeInformada,
                Status = s.Status,
                Titulo = s.Titulo,
                Descricao = s.Descricao,
                Modulo = s.Modulo,
                UrlContexto = s.UrlContexto,
                SolicitanteUsuarioId = s.SolicitanteUsuarioId,
                SolicitanteNome = s.SolicitanteNome,
                SolicitanteEmail = s.SolicitanteEmail,
                ResultadoTriagem = s.ResultadoTriagem,
                RequerAlteracaoCodigo = s.RequerAlteracaoCodigo,
                RespostaAoUsuario = s.RespostaAoUsuario,
                DemandaReduzida = s.DemandaReduzida,
                SolucaoProposta = s.SolucaoProposta,
                EvidenciasTecnicas = s.EvidenciasTecnicas,
                CreatedAtUtc = s.CreatedAtUtc,
                TriadoEmUtc = s.TriadoEmUtc,
                EncaminhadoEmUtc = s.EncaminhadoEmUtc
            })
            .ToListAsync(ct);
    }
}
