using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.OrdensServico;

public record OrdemServicoPdfModelo(
    MinutaOrdemServicoDto Conteudo,
    string Numero,
    int Revisao,
    DateTime EmitidaEm,
    byte[]? ObraLogoConteudo,
    string Situacao,
    AssinaturaOsDto? Funcionario,
    AssinaturaOsDto? Tecnico,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng);

public interface IOrdemServicoPdfService
{
    byte[] Gerar(OrdemServicoPdfModelo modelo);
}

// PDF da OS emitida, montado da cópia congelada na emissão (não da minuta atual). Cabeçalho só com a
// logo da obra (decisão de 03/11). Depois de finalizada (as duas assinaturas), quem baixa recebe o
// arquivo guardado no Motor — ver OrdensServicoController.
public record ExportarOrdemServicoPdfQuery(Guid OrdemServicoId) : IRequest<byte[]?>;

public class ExportarOrdemServicoPdfQueryHandler : IRequestHandler<ExportarOrdemServicoPdfQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IOrdemServicoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarOrdemServicoPdfQueryHandler(IAppDbContext db, IOrdemServicoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarOrdemServicoPdfQuery request, CancellationToken ct)
    {
        var os = await _db.OrdensServico.FirstOrDefaultAsync(o => o.Id == request.OrdemServicoId, ct);
        if (os is null) return null;

        var conteudo = JsonSerializer.Deserialize<MinutaOrdemServicoDto>(os.ConteudoJson)!;
        var logo = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == os.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);
        var (_, funcionario, tecnico) = (await AssinaturasOrdemServico.CarregarAsync(_db, new[] { os }, ct))[os.Id];
        var rastreio = await _rastreabilidade.GarantirAsync(OrdemServico.EntidadeTipoAssinatura, os.Id, ct);

        var pdf = _pdf.Gerar(new OrdemServicoPdfModelo(
            conteudo, os.Numero, os.Revisao, os.EmitidaEm, logo,
            AssinaturasOrdemServico.Situacao(funcionario, tecnico), funcionario, tecnico,
            rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng));

        var finalizado = await _db.DocumentosAssinatura.AnyAsync(
            d => d.Id == rastreio.DocumentoId && d.Status == StatusDocumentoAssinatura.Finalizado, ct);
        if (!finalizado)
            await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }
}
