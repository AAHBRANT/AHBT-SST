using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.RegrasAlerta.Queries;

// Retorna todas as regras de todos os módulos — o frontend agrupa por Modulo (TipoModuloAlerta) na
// tela de Configurações (AlertasConfiguracaoTab). Não filtra por módulo aqui porque a tela sempre
// mostra o painel completo (poucas dezenas de linhas no total, um módulo por vez seria uma
// otimização prematura).
public record ListarRegrasAlertaQuery : IRequest<List<RegraAlertaDto>>;

public class ListarRegrasAlertaQueryHandler : IRequestHandler<ListarRegrasAlertaQuery, List<RegraAlertaDto>>
{
    private readonly IAppDbContext _db;

    public ListarRegrasAlertaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<RegraAlertaDto>> Handle(ListarRegrasAlertaQuery request, CancellationToken ct)
    {
        return await _db.RegrasAlerta
            .OrderBy(r => r.Modulo)
            .ThenBy(r => r.DiasAntecedencia)
            .Select(r => new RegraAlertaDto
            {
                Id = r.Id,
                Modulo = r.Modulo,
                DiasAntecedencia = r.DiasAntecedencia,
                Severidade = r.Severidade,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = r.ResponsavelUsuario != null ? r.ResponsavelUsuario.Nome : null
            })
            .ToListAsync(ct);
    }
}
