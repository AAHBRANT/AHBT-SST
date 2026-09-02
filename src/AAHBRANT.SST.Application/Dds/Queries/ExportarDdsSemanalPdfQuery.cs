using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ExportarDdsSemanalPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarDdsSemanalPdfQueryHandler : IRequestHandler<ExportarDdsSemanalPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IDdsSemanalPdfService _pdf;

    public ExportarDdsSemanalPdfQueryHandler(IMediator mediator, IAppDbContext db, IDdsSemanalPdfService pdf)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarDdsSemanalPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsSemanalDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        var logoConteudo = await _db.Obras
            .Where(o => o.Id == detalhe.Semanal.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var dias = detalhe.Dias
            .Select(d => new DdsSemanalPdfDiaModelo(d.DiaSemana, d.Data, d.AtividadesNomes, d.TemaLivreNome))
            .ToList();

        // Presença: união de todos os trabalhadores que participaram de QUALQUER dia da semana,
        // com uma coluna booleana por dia (mesma grade Seg-Sex do papel) — não é o mesmo trabalhador
        // repetido por dia, é uma linha só com 5 marcações.
        var diasComRegistro = detalhe.Dias.Where(d => d.DdsId.HasValue).ToList();
        var ddsIds = diasComRegistro.Select(d => d.DdsId!.Value).ToList();

        var participantes = await _db.DdsParticipantes
            .Where(p => ddsIds.Contains(p.DdsId) && p.Ativo)
            .Include(p => p.Trabalhador).ThenInclude(t => t!.Funcao)
            .ToListAsync(ct);

        var presencas = participantes
            .GroupBy(p => p.TrabalhadorId)
            .Select(g =>
            {
                var trabalhador = g.First().Trabalhador;
                var diasParticipados = g.Select(p => p.DdsId).ToHashSet();
                var presencaPorDia = diasComRegistro.Select(d => diasParticipados.Contains(d.DdsId!.Value)).ToList();
                return new DdsSemanalPdfLinhaPresenca(
                    trabalhador?.Nome ?? string.Empty,
                    trabalhador?.Funcao?.Nome ?? string.Empty,
                    trabalhador?.Matricula ?? string.Empty,
                    presencaPorDia);
            })
            .OrderBy(p => p.Nome)
            .ToList();

        var modelo = new DdsSemanalPdfModelo(
            detalhe.Semanal.ObraNome,
            logoConteudo,
            detalhe.Semanal.Tipo == TipoDdsSemanal.Terceirizados ? "Empregados Terceirizados" : "Empregados Próprios",
            detalhe.Semanal.EmpresaTerceirizada,
            detalhe.Semanal.NumeroDocumento,
            detalhe.Semanal.LocalFrenteServico,
            detalhe.Semanal.ResponsavelUsuarioNome,
            detalhe.Semanal.DataInicioSemana,
            detalhe.Semanal.DataFimSemana,
            dias,
            presencas,
            detalhe.Semanal.ResponsavelObraSstNome,
            detalhe.Semanal.ResponsavelEmpresaTerceirizadaNome,
            detalhe.Semanal.ResponsavelEmpresaTerceirizadaFuncao);

        return _pdf.Gerar(modelo);
    }
}
