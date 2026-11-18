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
    byte[] QrCodePng,
    CertificadoAssinaturasModelo? Certificado = null);

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
    private readonly IImagemBiometricaCriptografia? _cripto;
    private readonly IQrCodeDocumentoService? _qrCode;

    public ExportarOrdemServicoPdfQueryHandler(IAppDbContext db, IOrdemServicoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade,
        IImagemBiometricaCriptografia? cripto = null, IQrCodeDocumentoService? qrCode = null)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
        _cripto = cripto;
        _qrCode = qrCode;
    }

    public async Task<byte[]?> Handle(ExportarOrdemServicoPdfQuery request, CancellationToken ct)
    {
        var os = await _db.OrdensServico.FirstOrDefaultAsync(o => o.Id == request.OrdemServicoId, ct);
        if (os is null) return null;

        var conteudo = JsonSerializer.Deserialize<MinutaOrdemServicoDto>(os.ConteudoJson)!;
        // CPF completo da OS (também das já emitidas, que congelaram o CPF mascarado): lido do cadastro na hora de gerar,
        // sem alterar o conteúdo congelado. O endpoint exige trabalhador:ver; o valor não vai para log.
        var cpfAtual = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Id == os.TrabalhadorId).Select(t => t.Cpf).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(cpfAtual))
            conteudo.Trabalhador.Cpf = AAHBRANT.SST.Application.Common.CpfMascarador.Formatar(cpfAtual);
        var logo = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == os.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);
        var (_, funcionario, tecnico) = (await AssinaturasOrdemServico.CarregarAsync(_db, new[] { os }, ct))[os.Id];
        var rastreio = await _rastreabilidade.GarantirAsync(OrdemServico.EntidadeTipoAssinatura, os.Id, ct);

        var docAssinatura = await _db.DocumentosAssinatura.AsNoTracking()
            .Include(d => d.Signatarios)
            .Where(d => d.EntidadeTipo == OrdemServico.EntidadeTipoAssinatura && d.EntidadeId == os.Id && d.Status != StatusDocumentoAssinatura.Cancelado)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        var assinaturasAtivas = docAssinatura?.Signatarios.Where(s => s.Ativo).ToList() ?? new List<DocumentoSignatario>();
        var dadosSignatarios = assinaturasAtivas.Count > 0
            ? await MontadorCertificadoAssinaturas.CarregarSignatariosAsync(_db, assinaturasAtivas.Select(s => s.TrabalhadorId), ct)
            : new Dictionary<Guid, MontadorCertificadoAssinaturas.DadosSignatario>();
        var painel = new PainelCertificado("Ordem de Serviço", $"OS Nº {os.Numero} (Rev. {os.Revisao}) · {conteudo.Trabalhador.Nome}", new List<SecaoPainelCertificado>
        {
            new("Ordem de Serviço", new List<LinhaPainelCertificado>
            {
                new("Nº", $"{os.Numero} (Rev. {os.Revisao})"),
                new("Emissão", os.EmitidaEm.ToString("dd/MM/yyyy")),
                new("Trabalhador", conteudo.Trabalhador.Nome),
                new("Função", conteudo.Trabalhador.Funcao),
            }),
        });
        var certificado = docAssinatura is null || assinaturasAtivas.Count == 0
            ? null
            : await MontadorCertificadoAssinaturas.MontarAsync(
                _db, _cripto, _qrCode, docAssinatura, assinaturasAtivas, dadosSignatarios,
                s => s.Papel == PapelAssinatura.TecnicoSegurancaOs ? "Ordem de Serviço — assinatura do Técnico de Segurança" : "Ordem de Serviço — ciência do Trabalhador",
                "Ordem de Serviço de SST",
                $"OS Nº {os.Numero} · {conteudo.Trabalhador.Nome} · {conteudo.Obra.Nome}",
                conteudo.Obra.Nome, logo, rastreio, painel, compacto: false, ct);

        var pdf = _pdf.Gerar(new OrdemServicoPdfModelo(
            conteudo, os.Numero, os.Revisao, os.EmitidaEm, logo,
            AssinaturasOrdemServico.Situacao(funcionario, tecnico), funcionario, tecnico,
            rastreio.ConteudoHash, rastreio.UrlValidacaoPublica, rastreio.QrCodePng, certificado));

        var finalizado = await _db.DocumentosAssinatura.AnyAsync(
            d => d.Id == rastreio.DocumentoId && d.Status == StatusDocumentoAssinatura.Finalizado, ct);
        if (!finalizado)
            await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }
}
