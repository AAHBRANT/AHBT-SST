using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

// SomenteTrabalhadorId (dossiê, LGPD): o certificado traz só a evidência desse funcionário.
public record ExportarDdsPdfQuery(Guid Id, Guid? SomenteTrabalhadorId = null) : IRequest<byte[]?>;

public class ExportarDdsPdfQueryHandler : IRequestHandler<ExportarDdsPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IDdsPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;
    private readonly IImagemBiometricaCriptografia? _cripto;
    private readonly IQrCodeDocumentoService? _qrCode;

    public ExportarDdsPdfQueryHandler(IMediator mediator, IAppDbContext db, IDdsPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade,
        IImagemBiometricaCriptografia? cripto = null, IQrCodeDocumentoService? qrCode = null)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
        _cripto = cripto;
        _qrCode = qrCode;
    }

    public async Task<byte[]?> Handle(ExportarDdsPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        var dds = await _db.Dds.FirstAsync(d => d.Id == request.Id, ct);
        var logoConteudo = await _db.Obras.Where(o => o.Id == detalhe.Dds.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.Dds), request.Id, ct);

        // Assinatura do responsável: o usuário responsável se liga a um Trabalhador, e é esse
        // trabalhador que assina pelo "Assinar DDS" (DocumentoSignatario). Sem vínculo ou sem
        // assinatura, o PDF mostra "Aguardando assinatura".
        var responsavel = await _db.Usuarios
            .Where(u => u.Id == dds.ResponsavelUsuarioId)
            .Select(u => new
            {
                u.TrabalhadorId,
                Funcao = u.Trabalhador != null && u.Trabalhador.Funcao != null ? u.Trabalhador.Funcao.Nome : null,
            })
            .FirstOrDefaultAsync(ct);
        var trabalhadorResponsavelId = responsavel?.TrabalhadorId;
        var assinaturaResponsavel = trabalhadorResponsavelId is null
            ? null
            : await _db.DocumentoSignatarios
                .Where(s => s.TrabalhadorId == trabalhadorResponsavelId
                    && s.DocumentoAssinatura!.EntidadeTipo == nameof(Domain.Entidades.Dds)
                    && s.DocumentoAssinatura.EntidadeId == request.Id)
                .OrderBy(s => s.AssinadoEm)
                .Select(s => new { s.AssinadoEm, s.MetodoAutenticacao })
                .FirstOrDefaultAsync(ct);

        var fotosBanco = await _db.DdsFotosEvidencia
            .Where(f => f.DdsId == request.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .Select(f => new { f.FotoConteudo, f.FotoMetadadosJson })
            .ToListAsync(ct);
        var fotos = fotosBanco.Select(f =>
        {
            var d = Common.DadosCapturaFoto.Ler(f.FotoMetadadosJson);
            return new DdsPdfFotoModelo(f.FotoConteudo, d?.CapturadaEm, d?.FusoMinutos, d?.Local, d?.Latitude, d?.Longitude);
        }).ToList();

        // 05/11: selo na linha (função · CBO) e Certificado de Assinaturas com as assinaturas gravadas.
        var documento = await _db.DocumentosAssinatura.AsNoTracking()
            .Include(d => d.Signatarios)
            .Where(d => d.EntidadeTipo == nameof(Domain.Entidades.Dds) && d.EntidadeId == request.Id && d.Status != StatusDocumentoAssinatura.Cancelado)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        var assinaturas = documento?.Signatarios.Where(s => s.Ativo).ToList() ?? new List<DocumentoSignatario>();
        var signatarios = await MontadorCertificadoAssinaturas.CarregarSignatariosAsync(_db,
            assinaturas.Select(s => s.TrabalhadorId).Concat(detalhe.Participantes.Select(p => p.TrabalhadorId)), ct);
        var assinaturaPorTrabalhador = assinaturas.GroupBy(s => s.TrabalhadorId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.AssinadoEm).First());

        var participantes = detalhe.Participantes.Select(p =>
        {
            signatarios.TryGetValue(p.TrabalhadorId, out var dados);
            assinaturaPorTrabalhador.TryGetValue(p.TrabalhadorId, out var sig);
            var selo = p.AssinadoEm is null ? null
                : new SeloAssinaturaPdf(p.TrabalhadorNome, dados?.Funcao, sig is null ? null : MontadorCertificadoAssinaturas.CodigoSelo(sig));
            return new DdsPdfParticipanteModelo(p.TrabalhadorNome, p.AssinadoEm, p.MetodoAssinatura, dados?.Funcao, selo);
        }).ToList();

        var temas = detalhe.Dds.TemasAtividades.Select(t => t.AtividadeNome).ToList();
        if (!string.IsNullOrWhiteSpace(detalhe.Dds.TemaLivreNome)) temas.Add(detalhe.Dds.TemaLivreNome!);
        var painel = new PainelCertificado("DDS", $"Diálogo Diário de Segurança · {detalhe.Dds.Data:dd/MM/yyyy}", new List<SecaoPainelCertificado>
        {
            new("DDS", new List<LinhaPainelCertificado>
            {
                new("Data", detalhe.Dds.Data.ToString("dd/MM/yyyy")),
                new("Nº", dds.NumeroDocumento ?? "—"),
                new("Tema(s)", temas.Count == 0 ? "—" : string.Join("; ", temas)),
            }),
            new("Responsável", new List<LinhaPainelCertificado>
            {
                new("Nome", detalhe.Dds.ResponsavelUsuarioNome),
                new("Função", responsavel?.Funcao ?? "—"),
            }),
        });
        var certificado = documento is null ? null : await MontadorCertificadoAssinaturas.MontarAsync(
            _db, _cripto, _qrCode, documento,
            request.SomenteTrabalhadorId is { } so ? assinaturas.Where(a => a.TrabalhadorId == so).ToList() : assinaturas, signatarios,
            s => s.TrabalhadorId == trabalhadorResponsavelId ? "DDS — assinatura do responsável" : "DDS — presença do participante",
            "DDS", $"DDS · {detalhe.Dds.Data:dd/MM/yyyy} · {detalhe.Dds.ObraNome}",
            detalhe.Dds.ObraNome, logoConteudo, rastreio, painel, compacto: true, ct);

        var modelo = MontarModelo(detalhe, logoConteudo, dds.NumeroDocumento, rastreio) with
        {
            Participantes = participantes,
            Certificado = certificado,
            Fotos = fotos,
            ResponsavelAssinadoEm = assinaturaResponsavel?.AssinadoEm,
            ResponsavelMetodo = assinaturaResponsavel?.MetodoAutenticacao,
            ResponsavelFuncao = responsavel?.Funcao,
        };
        var pdf = _pdf.Gerar(modelo);
        // Guarda a cópia exata emitida e o SHA-256 dela — é o que permite conferir, depois,
        // que o arquivo em mãos não foi adulterado (ver HashArquivoCalculador).
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

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
        detalhe.Participantes.Select(p => new DdsPdfParticipanteModelo(p.TrabalhadorNome, p.AssinadoEm, p.MetodoAssinatura)).ToList(),
        protocolo,
        rastreio.ConteudoHash,
        rastreio.UrlValidacaoPublica,
        rastreio.QrCodePng,
        rastreio.TemAssinatura);
}
