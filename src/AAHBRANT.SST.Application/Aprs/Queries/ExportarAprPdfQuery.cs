using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Queries;

// CertificadoAssinaturas (05/11): true = só o Certificado de Assinaturas Eletrônicas, em PDF à parte; a APR
// em si sai sem ele (vai para a frente de serviço sem biometria exposta).
// SomenteTrabalhadorId (dossiê, LGPD): o certificado traz só a evidência desse funcionário.
public record ExportarAprPdfQuery(Guid Id, bool CertificadoAssinaturas = false, Guid? SomenteTrabalhadorId = null) : IRequest<byte[]?>;

public class ExportarAprPdfQueryHandler : IRequestHandler<ExportarAprPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IAprPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;
    private readonly IImagemBiometricaCriptografia? _cripto;
    private readonly IQrCodeDocumentoService? _qrCode;
    private readonly ICertificadoAssinaturasPdfService? _certificadoPdf;

    public ExportarAprPdfQueryHandler(IMediator mediator, IAppDbContext db, IAprPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade,
        IImagemBiometricaCriptografia? cripto = null, IQrCodeDocumentoService? qrCode = null,
        ICertificadoAssinaturasPdfService? certificadoPdf = null)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
        _cripto = cripto;
        _qrCode = qrCode;
        _certificadoPdf = certificadoPdf;
    }

    public async Task<byte[]?> Handle(ExportarAprPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterAprDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        byte[]? logoConteudo = detalhe.Apr.ObraId is { } obraId
            ? await _db.Obras.Where(o => o.Id == obraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct)
            : null;

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Domain.Entidades.Apr), request.Id, ct);

        // 05/11: assinaturas biométricas do quiosque (documento "Apr") — selo na linha, blocos e certificado.
        var documento = await _db.DocumentosAssinatura.AsNoTracking()
            .Include(d => d.Signatarios)
            .Where(d => d.EntidadeTipo == nameof(Domain.Entidades.Apr) && d.EntidadeId == request.Id && d.Status != StatusDocumentoAssinatura.Cancelado)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        var assinaturas = documento?.Signatarios.Where(s => s.Ativo).ToList() ?? new List<DocumentoSignatario>();
        var signatarios = await MontadorCertificadoAssinaturas.CarregarSignatariosAsync(_db,
            assinaturas.Select(s => s.TrabalhadorId).Concat(detalhe.Responsaveis.Select(r => r.TrabalhadorId)), ct);

        var modelo = MontarModelo(detalhe, logoConteudo, rastreio, assinaturas, signatarios);
        var painel = new PainelCertificado("APR", $"APR {detalhe.Apr.NumeroApr ?? "—"} · {detalhe.Apr.AtividadeNome}", new List<SecaoPainelCertificado>
        {
            new("Atividade", new List<LinhaPainelCertificado>
            {
                new("Atividade", detalhe.Apr.AtividadeNome),
                new("Local", detalhe.Apr.Local),
                new("Data", detalhe.Apr.Data.ToString("dd/MM/yyyy")),
            }),
            new("Responsáveis", new List<LinhaPainelCertificado>
            {
                new("Elaboração", detalhe.Apr.ElaboradorNome ?? "—"),
                new("Supervisão", detalhe.Apr.SupervisorNome ?? "—"),
                new("Envolvidos", detalhe.Responsaveis.Count.ToString()),
            }),
        });
        var certificado = documento is null ? null : await MontadorCertificadoAssinaturas.MontarAsync(
            _db, _cripto, _qrCode, documento,
            request.SomenteTrabalhadorId is { } so ? assinaturas.Where(a => a.TrabalhadorId == so).ToList() : assinaturas, signatarios,
            s => s.Papel switch
            {
                PapelAssinatura.AprElaboracao => "APR — assinatura da elaboração",
                PapelAssinatura.AprSupervisao => "APR — assinatura da supervisão",
                _ => "APR — ciência do envolvido",
            },
            "APR", $"APR {detalhe.Apr.NumeroApr ?? "—"} · {detalhe.Apr.AtividadeNome}",
            detalhe.Apr.ObraNome ?? string.Empty, logoConteudo, rastreio, painel, compacto: true, ct);

        if (request.CertificadoAssinaturas)
            return certificado is null || _certificadoPdf is null ? null : _certificadoPdf.Gerar(certificado);

        var pdf = _pdf.Gerar(modelo);
        // Guarda a cópia exata emitida e o SHA-256 dela — é o que permite conferir, depois,
        // que o arquivo em mãos não foi adulterado (ver HashArquivoCalculador).
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

    public static AprPdfModelo MontarModelo(AprDetalheDto detalhe, byte[]? obraLogoConteudo, RastreabilidadeDocumentoResultado rastreio,
        IReadOnlyList<DocumentoSignatario>? biometricas = null,
        IReadOnlyDictionary<Guid, MontadorCertificadoAssinaturas.DadosSignatario>? signatarios = null)
    {
        // 05/11: a assinatura biométrica (quiosque) vale mais que o registro antigo por clique; depois a mais recente.
        AprAssinaturaDto? Melhor(Guid trabalhadorId, PapelAssinaturaApr papel) => detalhe.Assinaturas
            .Where(a => a.TrabalhadorId == trabalhadorId && a.Papel == papel)
            .OrderByDescending(a => a.Biometrica).ThenByDescending(a => a.DataAssinatura)
            .FirstOrDefault();
        string? Codigo(AprAssinaturaDto a) => biometricas?.FirstOrDefault(s => s.Id == a.Id) is { } s
            ? MontadorCertificadoAssinaturas.CodigoSelo(s) : null;
        string? Funcao(Guid trabalhadorId, string? padrao) =>
            signatarios is not null && signatarios.TryGetValue(trabalhadorId, out var d) ? d.Funcao ?? padrao : padrao;

        var envolvidos = detalhe.Responsaveis
            .Select(r =>
            {
                var funcao = Funcao(r.TrabalhadorId, r.TrabalhadorFuncaoNome);
                var ass = Melhor(r.TrabalhadorId, PapelAssinaturaApr.Envolvido);
                if (ass is null) return new AprPdfEnvolvido(r.TrabalhadorNome, funcao, false);
                var selo = ass.Biometrica ? new SeloAssinaturaPdf(r.TrabalhadorNome, funcao, Codigo(ass)) : null;
                return new AprPdfEnvolvido(r.TrabalhadorNome, funcao, true, ass.DataAssinatura, ass.MetodoAutenticacao, selo);
            })
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

        // Elaboração/supervisão: a pessoa definida na APR; APRs antigas sem ela usam o registro mais recente do papel.
        AprAssinaturaDto? DoPapel(Guid? pessoa, PapelAssinaturaApr papel) => pessoa is { } id
            ? Melhor(id, papel)
            : detalhe.Assinaturas.Where(a => a.Papel == papel).OrderByDescending(a => a.Biometrica).ThenByDescending(a => a.DataAssinatura).FirstOrDefault();
        var elaboracao = DoPapel(detalhe.Apr.ElaboradorTrabalhadorId, PapelAssinaturaApr.Elaboracao);
        var supervisao = DoPapel(detalhe.Apr.SupervisorTrabalhadorId, PapelAssinaturaApr.Supervisao);

        return new AprPdfModelo(
            detalhe.Apr.NumeroApr,
            detalhe.Apr.ObraNome,
            obraLogoConteudo,
            detalhe.Apr.AtividadeNome,
            detalhe.Apr.Local,
            detalhe.Apr.MaquinasEquipamentos,
            detalhe.Apr.PgrReferencia,
            detalhe.Apr.Data,
            envolvidos,
            riscos,
            new AprPdfAssinatura(elaboracao?.TrabalhadorNome ?? detalhe.Apr.ElaboradorNome, elaboracao?.TrabalhadorFuncaoNome, elaboracao?.DataAssinatura, elaboracao?.MetodoAutenticacao,
                elaboracao is { Biometrica: true } ? Codigo(elaboracao) : null),
            new AprPdfAssinatura(supervisao?.TrabalhadorNome ?? detalhe.Apr.SupervisorNome, supervisao?.TrabalhadorFuncaoNome, supervisao?.DataAssinatura, supervisao?.MetodoAutenticacao,
                supervisao is { Biometrica: true } ? Codigo(supervisao) : null),
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            // TemAssinatura vem da tabela própria AprAssinatura (Motor de Assinatura Eletrônica não é
            // usado pela APR) — rastreio.TemAssinatura é deliberadamente ignorado aqui.
            detalhe.Assinaturas.Count > 0);
    }
}
