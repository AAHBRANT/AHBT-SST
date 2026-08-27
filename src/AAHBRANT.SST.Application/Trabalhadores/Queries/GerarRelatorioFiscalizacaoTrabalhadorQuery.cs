using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Relatório único de fiscalização (MTE) — reaproveita a mesma agregação de
// ObterPerfilCompletoTrabalhadorQuery injetando o handler diretamente (não IMediator, que não é usado
// dentro de handlers da Application neste projeto — ver nota de arquitetura do módulo), para não
// duplicar as 6 seções do perfil num segundo lugar.
public record GerarRelatorioFiscalizacaoTrabalhadorQuery(Guid Id) : IRequest<byte[]?>;

public class GerarRelatorioFiscalizacaoTrabalhadorQueryHandler : IRequestHandler<GerarRelatorioFiscalizacaoTrabalhadorQuery, byte[]?>
{
    private readonly IRequestHandler<ObterPerfilCompletoTrabalhadorQuery, PerfilCompletoTrabalhadorDto?> _obterPerfil;
    private readonly IRelatorioFiscalizacaoPdfService _pdf;

    public GerarRelatorioFiscalizacaoTrabalhadorQueryHandler(
        IRequestHandler<ObterPerfilCompletoTrabalhadorQuery, PerfilCompletoTrabalhadorDto?> obterPerfil,
        IRelatorioFiscalizacaoPdfService pdf)
    {
        _obterPerfil = obterPerfil;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(GerarRelatorioFiscalizacaoTrabalhadorQuery request, CancellationToken ct)
    {
        var perfil = await _obterPerfil.Handle(new ObterPerfilCompletoTrabalhadorQuery(request.Id), ct);
        return perfil is null ? null : _pdf.Gerar(perfil);
    }
}
