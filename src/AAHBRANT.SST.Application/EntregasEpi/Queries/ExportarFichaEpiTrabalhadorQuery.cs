using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Queries;

public record ExportarFichaEpiTrabalhadorQuery(Guid TrabalhadorId) : IRequest<byte[]?>;

public class ExportarFichaEpiTrabalhadorQueryHandler : IRequestHandler<ExportarFichaEpiTrabalhadorQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IFichaEpiPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarFichaEpiTrabalhadorQueryHandler(IAppDbContext db, IFichaEpiPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarFichaEpiTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores
            .Include(t => t.Obra)
            .Include(t => t.Funcao)
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);
        if (trabalhador is null) return null;

        // Ficha NR-6 tem valor de prova em fiscalização — uma entrega apenas reservada
        // (Confirmada=false, automação de EPI do módulo Terceirizado) nunca deve aparecer como se
        // tivesse sido fisicamente entregue. Decisão do usuário (revisão final do módulo
        // Terceirizado, 2026-09-18): excluir da ficha em vez de marcar como "reservada".
        var entregas = await _db.EntregasEpi
            .Include(e => e.CatalogoEpi)
            .Where(e => e.TrabalhadorId == request.TrabalhadorId && e.Confirmada)
            .OrderBy(e => e.DataEntrega)
            .ToListAsync(ct);

        var entregaIds = entregas.Select(e => e.Id).ToList();

        // Um DocumentoAssinatura por entrega/devolução (EntidadeTipo="EntregaEpi"/"DevolucaoEpi",
        // EntidadeId=EntregaEpi.Id) — ver docs/Motor-Assinatura-Eletronica.md. Carrega tudo de uma vez
        // e agrupa em memória em vez de uma query por linha da ficha.
        var documentos = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .Where(d => entregaIds.Contains(d.EntidadeId) && (d.EntidadeTipo == "EntregaEpi" || d.EntidadeTipo == "DevolucaoEpi"))
            .ToListAsync(ct);

        // GroupBy em vez de ToDictionary: dados de produção anteriores ao fix de idempotência do
        // CriarDocumentoAssinaturaCommand podem ter mais de um DocumentoAssinatura para a mesma
        // (EntidadeTipo, EntidadeId) — nesse caso fica com o mais "completo" (Finalizado antes de
        // EmAndamento) e, empatando, o mais recente, em vez de derrubar a ficha inteira com exceção.
        var documentosEntrega = documentos
            .Where(d => d.EntidadeTipo == "EntregaEpi")
            .GroupBy(d => d.EntidadeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Status == StatusDocumentoAssinatura.Finalizado)
                .ThenByDescending(d => d.CreatedAtUtc).First());
        var documentosDevolucao = documentos
            .Where(d => d.EntidadeTipo == "DevolucaoEpi")
            .GroupBy(d => d.EntidadeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Status == StatusDocumentoAssinatura.Finalizado)
                .ThenByDescending(d => d.CreatedAtUtc).First());

        var linhasEntrega = new List<LinhaEntregaEpiPdf>();
        var linhasDevolucao = new List<LinhaDevolucaoEpiPdf>();
        var numero = 0;

        foreach (var entrega in entregas)
        {
            numero++;

            var assinaturaEmpregado = documentosEntrega.TryGetValue(entrega.Id, out var docEntrega)
                ? docEntrega.Signatarios
                    .Where(s => s.TrabalhadorId == entrega.TrabalhadorId)
                    .OrderBy(s => s.AssinadoEm)
                    .FirstOrDefault()
                : null;
            var assinaturaResponsavel = docEntrega?.Signatarios
                .Where(s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada)
                .OrderBy(s => s.AssinadoEm)
                .FirstOrDefault();

            linhasEntrega.Add(new LinhaEntregaEpiPdf(
                numero,
                entrega.CatalogoEpi?.Nome ?? string.Empty,
                entrega.CatalogoEpi?.CertificadoAprovacaoNumero,
                entrega.MotivoTipo,
                entrega.Motivo,
                entrega.Quantidade,
                entrega.DataEntrega,
                assinaturaEmpregado is not null,
                assinaturaResponsavel is not null,
                assinaturaEmpregado?.AssinadoEm,
                assinaturaResponsavel?.AssinadoEm));

            if (entrega.DataDevolucao is null) continue;

            var assinaturaDevolucaoEmpregado = documentosDevolucao.TryGetValue(entrega.Id, out var docDevolucao)
                ? docDevolucao.Signatarios
                    .Where(s => s.TrabalhadorId == entrega.TrabalhadorId)
                    .OrderBy(s => s.AssinadoEm)
                    .FirstOrDefault()
                : null;

            linhasDevolucao.Add(new LinhaDevolucaoEpiPdf(
                numero,
                entrega.CatalogoEpi?.Nome ?? string.Empty,
                entrega.QuantidadeDevolucao ?? entrega.Quantidade,
                entrega.DataDevolucao.Value,
                assinaturaDevolucaoEmpregado is not null,
                assinaturaDevolucaoEmpregado?.AssinadoEm,
                entrega.VistoConsorcioResponsavel));
        }

        // Chave sintética "FichaEpiTrabalhador"/TrabalhadorId: a Ficha agrega N entregas, cada uma já
        // individualmente rastreável (DocumentoAssinatura por entrega, acima) — ninguém assina a Ficha
        // em si, esta rastreabilidade é só pra atestar integridade do PDF impresso como um todo.
        var rastreio = await _rastreabilidade.GarantirAsync("FichaEpiTrabalhador", request.TrabalhadorId, ct);

        var modelo = new FichaEpiPdfModelo(
            trabalhador.Obra?.Nome ?? string.Empty,
            trabalhador.Obra?.Cliente,
            trabalhador.Obra?.Cnpj,
            trabalhador.Obra?.LogoConteudo,
            trabalhador.Obra?.LogoContentType,
            trabalhador.Nome,
            CpfMascarador.Mascarar(trabalhador.Cpf),
            trabalhador.Matricula ?? string.Empty,
            trabalhador.Funcao?.Nome ?? string.Empty,
            trabalhador.Turno,
            trabalhador.DataAdmissao,
            linhasEntrega,
            linhasDevolucao,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        var pdf = _pdf.Gerar(modelo);
        // Guarda a cópia exata emitida e o SHA-256 dela — é o que permite conferir, depois,
        // que o arquivo em mãos não foi adulterado (ver HashArquivoCalculador).
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }
}
