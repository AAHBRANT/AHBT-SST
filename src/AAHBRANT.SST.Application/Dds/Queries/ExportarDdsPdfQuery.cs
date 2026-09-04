using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ExportarDdsPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarDdsPdfQueryHandler : IRequestHandler<ExportarDdsPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IDdsPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarDdsPdfQueryHandler(IMediator mediator, IAppDbContext db, IDdsPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarDdsPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        var dds = await _db.Dds.FirstAsync(d => d.Id == request.Id, ct);
        var logoConteudo = await _db.Obras.Where(o => o.Id == detalhe.Dds.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.Dds), request.Id, ct);

        return _pdf.Gerar(MontarModelo(detalhe, logoConteudo, dds.NumeroDocumento, rastreio));
    }

    // Reaproveitado por EnviarDdsTelegramCommandHandler para não duplicar a montagem do modelo do PDF.
    public static DdsPdfModelo MontarModelo(DdsDetalheDto detalhe, byte[]? obraLogoConteudo, string? protocolo, RastreabilidadeDocumentoResultado rastreio) => new(
        detalhe.Dds.ObraNome,
        obraLogoConteudo,
        detalhe.Dds.Data,
        detalhe.Dds.ResponsavelUsuarioNome,
        detalhe.Dds.TemasAtividades.Select(t => new DdsPdfTemaModelo(
            t.AtividadeNome, t.PerigoNome, t.PerigoDescricao, t.Consequencia, t.ControlesExistentes, t.ControlesAdicionais)).ToList(),
        detalhe.Dds.TemaLivreNome,
        detalhe.Dds.TemaLivreDescricao,
        detalhe.ItensChecklist.Select(i => (i.Descricao, i.Verificado)).ToList(),
        detalhe.Participantes.Select(p => p.TrabalhadorNome).ToList(),
        protocolo,
        rastreio.ConteudoHash,
        rastreio.UrlValidacaoPublica,
        rastreio.QrCodePng,
        rastreio.TemAssinatura);
}
