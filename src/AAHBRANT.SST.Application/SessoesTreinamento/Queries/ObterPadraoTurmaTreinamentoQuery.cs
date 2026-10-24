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
    bool PodeEditar);

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
        return new PadraoTurmaTreinamentoDto(padrao.Norma, padrao.CargaHorariaMinima, padrao.ValidadeEmMeses, padrao.ConteudoProgramatico, padrao.Local, podeEditar);
    }
}
