using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// PDF do PGR gerado pela EAP (próxima etapa após a aprovação, 23/10/2026). Só sai depois da aprovação do
// Engenheiro de Segurança: antes disso o conteúdo ainda é sugestão. Gera a partir da estrutura aprovada
// (GeracaoPgrEap.EstruturaJson), sem guardar um segundo arquivo.
public interface IPgrPdfService
{
    byte[] Gerar(DadosPgrPdf dados, byte[]? logoObra);
}

public record DadosPgrPdf(
    string ObraNome,
    string? ObraCnpj,
    string Nome,
    DateTime DataElaboracao,
    DateTime? DataProximaRevisao,
    string? ElaboradoPorNome,
    string NomeArquivoEap,
    string? AprovadoPorNome,
    DateTime? AprovadoEmUtc,
    EstruturaGeracaoPgr Estrutura,
    DateTime GeradoEmUtc);

public record ArquivoPgrPdf(byte[] Conteudo, string NomeArquivo);

public record GerarPdfGeracaoPgrEapQuery(Guid Id) : IRequest<ArquivoPgrPdf>;

public class GerarPdfGeracaoPgrEapQueryHandler : IRequestHandler<GerarPdfGeracaoPgrEapQuery, ArquivoPgrPdf>
{
    private readonly IAppDbContext _db;
    private readonly IPgrPdfService _pdf;

    public GerarPdfGeracaoPgrEapQueryHandler(IAppDbContext db, IPgrPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    public async Task<ArquivoPgrPdf> Handle(GerarPdfGeracaoPgrEapQuery request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.Aprovado)
            throw new InvalidOperationException("O PDF do PGR só é gerado depois da aprovação do Engenheiro de Segurança.");

        var obra = await _db.Obras.Where(o => o.Id == g.ObraId)
            .Select(o => new { o.Nome, o.Cnpj, o.LogoConteudo })
            .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Obra não encontrada.");

        var dados = new DadosPgrPdf(
            obra.Nome,
            obra.Cnpj,
            g.Nome,
            g.DataElaboracao,
            g.DataProximaRevisao,
            g.ElaboradoPorNome,
            g.NomeArquivoEap,
            g.AprovadoPorNome,
            g.AprovadoEmUtc,
            SerializacaoGeracao.Ler(g.EstruturaJson),
            DateTime.UtcNow);

        var nomeArquivo = $"PGR - {obra.Nome} - {g.DataElaboracao:yyyyMMdd}.pdf";
        return new ArquivoPgrPdf(_pdf.Gerar(dados, obra.LogoConteudo), nomeArquivo);
    }
}
