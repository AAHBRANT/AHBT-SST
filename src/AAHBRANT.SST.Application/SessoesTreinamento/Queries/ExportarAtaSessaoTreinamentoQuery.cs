using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ExportarAtaSessaoTreinamentoQuery(Guid SessaoTreinamentoId) : IRequest<byte[]?>;

public class ExportarAtaSessaoTreinamentoQueryHandler : IRequestHandler<ExportarAtaSessaoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IAtaSessaoTreinamentoPdfService _pdf;

    public ExportarAtaSessaoTreinamentoQueryHandler(IAppDbContext db, IAtaSessaoTreinamentoPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarAtaSessaoTreinamentoQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.SessaoTreinamentoId, ct);
        if (sessao is null || sessao.Obra is null || sessao.CursoTreinamento is null) return null;

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .OrderBy(p => p.Trabalhador!.Nome)
            .Select(p => new AtaSessaoTreinamentoPdfParticipanteModelo(
                p.Trabalhador!.Nome, p.Trabalhador.Matricula, p.PresencaConfirmadaEm))
            .ToListAsync(ct);

        var fotos = await _db.FotosEvidenciaSessaoTreinamento
            .Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .Select(f => f.FotoConteudo)
            .ToListAsync(ct);

        var modelo = new AtaSessaoTreinamentoPdfModelo(
            sessao.Obra.Nome,
            sessao.Obra.LogoConteudo,
            sessao.CursoTreinamento.Nome,
            sessao.CursoTreinamento.NormaReferencia,
            sessao.DataRealizacao,
            sessao.CargaHorariaRealizada,
            sessao.InstituicaoInstrutor,
            sessao.NumeroCertificado,
            sessao.DataEncerramento,
            participantes,
            fotos);

        return _pdf.Gerar(modelo);
    }
}
