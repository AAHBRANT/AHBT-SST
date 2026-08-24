using MediatR;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ExportarDdsPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarDdsPdfQueryHandler : IRequestHandler<ExportarDdsPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IDdsPdfService _pdf;

    public ExportarDdsPdfQueryHandler(IMediator mediator, IDdsPdfService pdf)
    {
        _mediator = mediator;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarDdsPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        return _pdf.Gerar(MontarModelo(detalhe));
    }

    // Reaproveitado por EnviarDdsTelegramCommandHandler para não duplicar a montagem do modelo do PDF.
    public static DdsPdfModelo MontarModelo(DdsDetalheDto detalhe) => new(
        detalhe.Dds.ObraNome,
        detalhe.Dds.Data,
        detalhe.Dds.ResponsavelUsuarioNome,
        detalhe.Dds.TopicoPrincipal,
        detalhe.Dds.AtividadesNomes,
        detalhe.ItensChecklist.Select(i => (i.Descricao, i.Verificado)).ToList(),
        detalhe.Participantes.Select(p => p.TrabalhadorNome).ToList());
}
