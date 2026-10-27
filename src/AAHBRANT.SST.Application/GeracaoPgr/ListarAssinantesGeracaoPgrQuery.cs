using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Quem pode ser escolhido no cartão "Quem assina": usuários ativos com perfil de técnico (ou engenheiro) de segurança na obra do
// PGR. Vinculado = o usuário já está ligado a um cadastro de trabalhador, condição para assinar pelo motor.
public record ListarAssinantesGeracaoPgrQuery(Guid GeracaoId) : IRequest<AssinantesGeracaoPgrDto>;

public record AssinanteDisponivelDto(Guid UsuarioId, string Nome, bool Vinculado);

public record AssinantesGeracaoPgrDto(IReadOnlyList<AssinanteDisponivelDto> Tecnicos, IReadOnlyList<AssinanteDisponivelDto> Engenheiros);

public class ListarAssinantesGeracaoPgrQueryHandler : IRequestHandler<ListarAssinantesGeracaoPgrQuery, AssinantesGeracaoPgrDto>
{
    private readonly IAppDbContext _db;

    public ListarAssinantesGeracaoPgrQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AssinantesGeracaoPgrDto> Handle(ListarAssinantesGeracaoPgrQuery request, CancellationToken ct)
    {
        var obraId = await _db.GeracoesPgrEap.Where(g => g.Id == request.GeracaoId).Select(g => (Guid?)g.ObraId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Geração de PGR não encontrada.");

        async Task<List<AssinanteDisponivelDto>> Listar(TipoPerfilAcesso tipo)
        {
            var linhas = await _db.Usuarios
                .Where(u => u.Status == StatusUsuario.Ativo
                    && u.PerfisPorObra.Any(pp => pp.PerfilAcesso!.Tipo == tipo && (pp.ObraId == obraId || pp.ObraId == null)))
                .OrderBy(u => u.Nome)
                .Select(u => new { u.Id, u.Nome, u.TrabalhadorId })
                .ToListAsync(ct);
            return linhas.Select(u => new AssinanteDisponivelDto(u.Id, u.Nome, u.TrabalhadorId != null)).ToList();
        }

        return new AssinantesGeracaoPgrDto(await Listar(TipoPerfilAcesso.TecnicoSeguranca), await Listar(TipoPerfilAcesso.EngenheiroSeguranca));
    }
}
