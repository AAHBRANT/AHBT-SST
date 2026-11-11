using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

// Prévia da exclusão definitiva: o que vai junto (inscrições, presenças, fotos) e se a turma pode ser excluída.
// Não altera nada. Devolve null se a turma não existe ou está fora do escopo de obras do usuário.
public record ObterPreviaExclusaoSessaoTreinamentoQuery(Guid Id) : IRequest<PreviaExclusaoSessaoTreinamentoDto?>;

public class PreviaExclusaoSessaoTreinamentoDto
{
    public Guid Id { get; set; }
    public string? NumeroCertificado { get; set; }
    public string CursoNome { get; set; } = string.Empty;
    public string ObraNome { get; set; } = string.Empty;
    public DateTime DataRealizacao { get; set; }
    public int Inscricoes { get; set; }
    public int Presencas { get; set; }
    public int FotosEvidencia { get; set; }
    public int CertificadosEmitidos { get; set; }
    public bool PodeExcluir { get; set; }
    public string? Motivo { get; set; }
}

public class ObterPreviaExclusaoSessaoTreinamentoQueryHandler : IRequestHandler<ObterPreviaExclusaoSessaoTreinamentoQuery, PreviaExclusaoSessaoTreinamentoDto?>
{
    private readonly IAppDbContext _db;
    public ObterPreviaExclusaoSessaoTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PreviaExclusaoSessaoTreinamentoDto?> Handle(ObterPreviaExclusaoSessaoTreinamentoQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento.AsNoTracking()
            .Include(s => s.Obra).Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (sessao is null) return null;

        var presencas = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Select(p => p.PresencaConfirmadaEm).ToListAsync(ct);
        var fotos = await _db.FotosEvidenciaSessaoTreinamento.CountAsync(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo, ct);
        var (certificados, motivo) = await ExclusaoSessaoTreinamento.AvaliarBloqueioAsync(_db, sessao.Id, ct);

        return new PreviaExclusaoSessaoTreinamentoDto
        {
            Id = sessao.Id,
            NumeroCertificado = sessao.NumeroCertificado,
            CursoNome = sessao.CursoTreinamento?.Nome ?? string.Empty,
            ObraNome = sessao.Obra?.Nome ?? string.Empty,
            DataRealizacao = sessao.DataRealizacao,
            Inscricoes = presencas.Count,
            Presencas = presencas.Count(p => p is not null),
            FotosEvidencia = fotos,
            CertificadosEmitidos = certificados,
            PodeExcluir = motivo is null,
            Motivo = motivo,
        };
    }
}
