using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Instrutores;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Quem pode ser escolhido no cartão "Quem assina": usuários ativos com perfil de técnico (ou engenheiro) de segurança na obra do
// PGR. Vinculado = o usuário já está ligado a um cadastro de trabalhador, condição para assinar pelo motor. Registro = o
// registro profissional cadastrado na edição do usuário (perfil de instrutor), que a tela usa para preencher o campo (04/11/2026).
public record ListarAssinantesGeracaoPgrQuery(Guid GeracaoId) : IRequest<AssinantesGeracaoPgrDto>;

public record AssinanteDisponivelDto(Guid UsuarioId, string Nome, bool Vinculado, string? Registro = null);

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
            var trabalhadores = linhas.Where(u => u.TrabalhadorId != null).Select(u => u.TrabalhadorId!.Value).ToList();
            var registros = (await _db.PerfisInstrutor.Where(p => trabalhadores.Contains(p.TrabalhadorId))
                    .Select(p => new { p.TrabalhadorId, p.Conselho, p.NumeroRegistro, p.UfRegistro }).ToListAsync(ct))
                .ToDictionary(p => p.TrabalhadorId, p => DescricaoInstrutor.Registro(p.Conselho, p.NumeroRegistro, p.UfRegistro));
            return linhas.Select(u => new AssinanteDisponivelDto(u.Id, u.Nome, u.TrabalhadorId != null,
                u.TrabalhadorId is { } t && registros.TryGetValue(t, out var r) ? r : null)).ToList();
        }

        return new AssinantesGeracaoPgrDto(await Listar(TipoPerfilAcesso.TecnicoSeguranca), await Listar(TipoPerfilAcesso.EngenheiroSeguranca));
    }
}
