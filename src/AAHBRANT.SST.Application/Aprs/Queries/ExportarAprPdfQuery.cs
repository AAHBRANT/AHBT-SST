using AAHBRANT.SST.Domain.Enums;
using MediatR;

namespace AAHBRANT.SST.Application.Aprs.Queries;

public record ExportarAprPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarAprPdfQueryHandler : IRequestHandler<ExportarAprPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAprPdfService _pdf;

    public ExportarAprPdfQueryHandler(IMediator mediator, IAprPdfService pdf)
    {
        _mediator = mediator;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarAprPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterAprDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        return _pdf.Gerar(MontarModelo(detalhe));
    }

    public static AprPdfModelo MontarModelo(AprDetalheDto detalhe)
    {
        var assinaturasPorTrabalhador = detalhe.Assinaturas
            .Where(a => a.Papel == PapelAssinaturaApr.Envolvido)
            .Select(a => a.TrabalhadorId)
            .ToHashSet();

        var envolvidos = detalhe.Responsaveis
            .Select(r => new AprPdfEnvolvido(r.TrabalhadorNome, r.TrabalhadorFuncaoNome, assinaturasPorTrabalhador.Contains(r.TrabalhadorId)))
            .ToList();

        var riscos = detalhe.Etapas
            .SelectMany(e => e.Riscos.Select(r => new AprPdfRiscoLinha(
                e.Descricao,
                r.PerigoEventoPerigoso,
                r.FonteCircunstancia,
                r.PossiveisLesoes,
                r.TrabalhadoresExpostos,
                r.ProbabilidadeInicial,
                r.SeveridadeInicial,
                r.NivelRiscoInicial,
                r.MedidasPrevencao,
                r.Responsavel,
                r.ProbabilidadeResidual,
                r.SeveridadeResidual,
                r.NivelRiscoResidual)))
            .ToList();

        var elaboracao = detalhe.Assinaturas.Where(a => a.Papel == PapelAssinaturaApr.Elaboracao)
            .OrderByDescending(a => a.DataAssinatura).FirstOrDefault();
        var supervisao = detalhe.Assinaturas.Where(a => a.Papel == PapelAssinaturaApr.Supervisao)
            .OrderByDescending(a => a.DataAssinatura).FirstOrDefault();

        return new AprPdfModelo(
            detalhe.Apr.NumeroApr,
            detalhe.Apr.ObraNome,
            detalhe.Apr.AtividadeNome,
            detalhe.Apr.Local,
            detalhe.Apr.MaquinasEquipamentos,
            detalhe.Apr.PgrReferencia,
            detalhe.Apr.Data,
            envolvidos,
            riscos,
            new AprPdfAssinatura(elaboracao?.TrabalhadorNome, null, elaboracao?.DataAssinatura),
            new AprPdfAssinatura(supervisao?.TrabalhadorNome, null, supervisao?.DataAssinatura));
    }
}
