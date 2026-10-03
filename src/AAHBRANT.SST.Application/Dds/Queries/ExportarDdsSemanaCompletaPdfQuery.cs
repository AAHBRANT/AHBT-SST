using MediatR;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ExportarDdsSemanaCompletaPdfQuery(Guid Id) : IRequest<byte[]?>;

// "Baixar semana" (pedido de 02/10): um único PDF com o DDS semanal primeiro e, na sequência, o
// DDS diário (com a lista de presença) de cada dia da semana, em ordem de data. Dias sem DDS ou
// marcados como sem expediente não têm lista de presença e ficam de fora.
public class ExportarDdsSemanaCompletaPdfQueryHandler : IRequestHandler<ExportarDdsSemanaCompletaPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IPdfMesclador _mesclador;

    public ExportarDdsSemanaCompletaPdfQueryHandler(IMediator mediator, IPdfMesclador mesclador)
    {
        _mediator = mediator;
        _mesclador = mesclador;
    }

    public async Task<byte[]?> Handle(ExportarDdsSemanaCompletaPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsSemanalDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        var semanal = await _mediator.Send(new ExportarDdsSemanalPdfQuery(request.Id), ct);
        if (semanal is null) return null;

        var pdfs = new List<byte[]> { semanal };
        foreach (var dia in detalhe.Dias.Where(d => d.DdsId.HasValue && !d.SemExpediente).OrderBy(d => d.Data))
        {
            var diario = await _mediator.Send(new ExportarDdsPdfQuery(dia.DdsId!.Value), ct);
            if (diario is not null) pdfs.Add(diario);
        }

        return _mesclador.Mesclar(pdfs);
    }
}
