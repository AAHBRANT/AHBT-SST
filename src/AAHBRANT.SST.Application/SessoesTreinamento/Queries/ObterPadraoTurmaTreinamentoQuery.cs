using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

// Preenche a tela de nova turma ao escolher o curso (19/10) e diz se quem está logado pode editar
// os dados do certificado ou só visualizar.
public record ObterPadraoTurmaTreinamentoQuery(Guid CursoTreinamentoId, Guid? ObraId, string? AzureAdObjectId, bool AutenticacaoHabilitada)
    : IRequest<PadraoTurmaTreinamentoDto?>;

public record PadraoTurmaTreinamentoDto(
    string? Norma,
    int CargaHorariaMinima,
    int ValidadeEmMeses,
    string? ConteudoProgramatico,
    string? Local,
    bool PodeEditar,
    Guid? InstrutorTrabalhadorId = null,
    string? InstrutorNome = null,
    string? InstrutorRegistro = null);

public class ObterPadraoTurmaTreinamentoQueryHandler : IRequestHandler<ObterPadraoTurmaTreinamentoQuery, PadraoTurmaTreinamentoDto?>
{
    private readonly IAppDbContext _db;
    public ObterPadraoTurmaTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PadraoTurmaTreinamentoDto?> Handle(ObterPadraoTurmaTreinamentoQuery request, CancellationToken ct)
    {
        var curso = await _db.CursosTreinamento.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CursoTreinamentoId, ct);
        if (curso is null) return null;

        var obraNome = request.ObraId is null
            ? null
            : await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct);

        var padrao = PadraoTurmaTreinamento.De(curso, obraNome);
        var podeEditar = await PerfilTecnicoSeguranca.PodeEditarDadosTreinamentoAsync(_db, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        Guid? instrutorTrabId = null;
        string? instrutorNome = null;
        string? instrutorRegistro = null;
        if (!string.IsNullOrWhiteSpace(request.AzureAdObjectId))
        {
            var usuario = await _db.Usuarios.AsNoTracking()
                .Where(u => u.AzureAdObjectId == request.AzureAdObjectId && u.Ativo && u.Status == Domain.Enums.StatusUsuario.Ativo)
                .Select(u => new { u.Nome, u.TrabalhadorId })
                .FirstOrDefaultAsync(ct);
            if (usuario is not null)
            {
                instrutorNome = usuario.Nome;
                instrutorTrabId = usuario.TrabalhadorId;
                if (usuario.TrabalhadorId is { } tid)
                {
                    var trabNome = await _db.Trabalhadores.IgnoreQueryFilters().Where(t => t.Id == tid).Select(t => t.Nome).FirstOrDefaultAsync(ct);
                    if (!string.IsNullOrWhiteSpace(trabNome)) instrutorNome = trabNome;
                    var perfil = await _db.PerfisInstrutor.AsNoTracking().FirstOrDefaultAsync(p => p.TrabalhadorId == tid, ct);
                    if (perfil is not null)
                        instrutorRegistro = AAHBRANT.SST.Application.Instrutores.DescricaoInstrutor.Registro(perfil.Conselho, perfil.NumeroRegistro, perfil.UfRegistro);
                }
            }
        }
        return new PadraoTurmaTreinamentoDto(
            padrao.Norma, padrao.CargaHorariaMinima, padrao.ValidadeEmMeses, padrao.ConteudoProgramatico, padrao.Local, podeEditar,
            instrutorTrabId, instrutorNome, instrutorRegistro);
    }
}
