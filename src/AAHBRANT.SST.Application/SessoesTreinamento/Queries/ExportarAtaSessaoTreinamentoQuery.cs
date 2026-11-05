using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Cbo;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

// SomenteTrabalhadorId (dossiê, LGPD): o certificado traz só a evidência desse funcionário.
public record ExportarAtaSessaoTreinamentoQuery(Guid SessaoTreinamentoId, Guid? SomenteTrabalhadorId = null) : IRequest<byte[]?>;

public class ExportarAtaSessaoTreinamentoQueryHandler : IRequestHandler<ExportarAtaSessaoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IAtaSessaoTreinamentoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;
    private readonly IImagemBiometricaCriptografia? _cripto;
    private readonly IQrCodeDocumentoService? _qrCode;

    public ExportarAtaSessaoTreinamentoQueryHandler(IAppDbContext db, IAtaSessaoTreinamentoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade,
        IImagemBiometricaCriptografia? cripto = null, IQrCodeDocumentoService? qrCode = null)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
        _cripto = cripto;
        _qrCode = qrCode;
    }

    public async Task<byte[]?> Handle(ExportarAtaSessaoTreinamentoQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.SessaoTreinamentoId, ct);
        if (sessao is null || sessao.Obra is null || sessao.CursoTreinamento is null) return null;

        var inscritos = await _db.ParticipantesSessaoTreinamento.AsNoTracking()
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .OrderBy(p => p.Trabalhador!.Nome)
            .Select(p => new InscritoAta(
                p.TrabalhadorId,
                p.Trabalhador!.Nome,
                p.Trabalhador.Matricula,
                p.Trabalhador.Funcao != null ? p.Trabalhador.Funcao.Nome : null,
                p.Trabalhador.Funcao != null ? p.Trabalhador.Funcao.CboCodigo : null,
                p.Trabalhador.TermoAceiteAssinaturaEletronicaEm,
                p.Trabalhador.ConsentimentoBiometriaEm,
                p.PresencaConfirmadaEm,
                p.MetodoPresenca,
                p.TreinamentoGeradoId,
                p.EvidenciaHash))
            .ToListAsync(ct);

        // 05/11: assinaturas reais dos certificados da turma (é lá que a presença vira assinatura, no
        // encerramento) — trazem a evidência guardada na presença e a do instrutor.
        var treinamentoIds = inscritos.Where(i => i.TreinamentoGeradoId != null).Select(i => i.TreinamentoGeradoId!.Value).ToList();
        var documentos = await _db.DocumentosAssinatura.AsNoTracking()
            .Include(d => d.Signatarios)
            .Where(d => d.EntidadeTipo == nameof(Treinamento) && treinamentoIds.Contains(d.EntidadeId) && d.Status != StatusDocumentoAssinatura.Cancelado)
            .ToListAsync(ct);
        var documentoPorTreinamento = documentos.GroupBy(d => d.EntidadeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Status == StatusDocumentoAssinatura.Finalizado).ThenByDescending(d => d.CreatedAtUtc).First());

        var assinaturaParticipante = new Dictionary<Guid, (DocumentoAssinatura Doc, DocumentoSignatario Sig)>();
        foreach (var i in inscritos.Where(i => i.TreinamentoGeradoId != null))
            if (documentoPorTreinamento.TryGetValue(i.TreinamentoGeradoId!.Value, out var doc)
                && doc.Signatarios.Where(x => x.TrabalhadorId == i.TrabalhadorId && x.Papel != PapelAssinatura.Instrutor)
                    .OrderBy(x => x.AssinadoEm).FirstOrDefault() is { } sig)
                assinaturaParticipante[i.TrabalhadorId] = (doc, sig);

        var participantes = inscritos.Select(i =>
        {
            var funcao = FuncaoComCbo(i.FuncaoNome, i.Cbo);
            assinaturaParticipante.TryGetValue(i.TrabalhadorId, out var a);
            var codigo = a.Sig?.FotoEvidenciaHash ?? i.EvidenciaHash ?? a.Sig?.Id.ToString("N").ToUpperInvariant();
            return new AtaSessaoTreinamentoPdfParticipanteModelo(
                i.Nome, i.Matricula, i.PresencaConfirmadaEm, a.Sig?.MetodoAutenticacao ?? i.MetodoPresenca, funcao,
                i.PresencaConfirmadaEm is null ? null : new SeloAssinaturaPdf(i.Nome, funcao, codigo));
        }).ToList();

        var fotos = await _db.FotosEvidenciaSessaoTreinamento
            .Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .Select(f => f.FotoConteudo)
            .ToListAsync(ct);

        // Chave sintética "SessaoTreinamento"/SessaoTreinamentoId: a Ata agrega N participantes, cada
        // um já individualmente assinado via seu próprio DocumentoAssinatura("Treinamento", Id) —
        // ninguém assina a Ata em si, então TemAssinatura nunca é usado aqui (RodapeDocumentoPadrao
        // recebe temAssinatura: false diretamente no PdfService).
        var rastreio = await _rastreabilidade.GarantirAsync("SessaoTreinamento", sessao.Id, ct);

        // Instrutor: quem assinou os certificados como instrutor (login ou digital), ou o cadastrado na turma.
        var assinaturaInstrutor = documentos.SelectMany(d => d.Signatarios.Select(x => (Doc: d, Sig: x)))
            .Where(x => x.Sig.Papel == PapelAssinatura.Instrutor || x.Sig.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada)
            .OrderBy(x => x.Sig.AssinadoEm).FirstOrDefault();
        var instrutorId = assinaturaInstrutor.Sig?.TrabalhadorId ?? sessao.InstrutorTrabalhadorId;
        AssinaturaInstrutorAtaPdf? instrutor = null;
        string? instrutorNome = null, instrutorCargo = null;
        if (instrutorId is { } idInstrutor)
        {
            var dadosInstrutor = await _db.Trabalhadores.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.Id == idInstrutor)
                .Select(t => new { t.Nome, Funcao = t.Funcao != null ? t.Funcao.Nome : null })
                .FirstOrDefaultAsync(ct);
            var perfil = await _db.PerfisInstrutor.AsNoTracking()
                .Where(p => p.TrabalhadorId == idInstrutor && p.NumeroRegistro != null)
                .Select(p => new { p.Conselho, p.NumeroRegistro, p.UfRegistro })
                .FirstOrDefaultAsync(ct);
            var registro = perfil is not null
                ? $"{(perfil.Conselho == ConselhoInstrutor.Crea ? "CREA" : "MTE")}-{perfil.NumeroRegistro}{(string.IsNullOrWhiteSpace(perfil.UfRegistro) ? "" : "/" + perfil.UfRegistro)}"
                : sessao.InstrutorRegistroProfissional;
            instrutorNome = dadosInstrutor?.Nome;
            instrutorCargo = string.Join(" · ", new[] { dadosInstrutor?.Funcao, registro }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(instrutorCargo)) instrutorCargo = null;
            if (instrutorNome is not null)
                instrutor = new AssinaturaInstrutorAtaPdf(instrutorNome, instrutorCargo,
                    assinaturaInstrutor.Sig?.AssinadoEm, assinaturaInstrutor.Sig?.MetodoAutenticacao, assinaturaInstrutor.Sig?.FotoEvidenciaHash);
        }

        var certificado = request.SomenteTrabalhadorId is { } so
            ? await MontarCertificadoAsync(sessao, inscritos,
                assinaturaParticipante.Where(kv => kv.Key == so).ToDictionary(kv => kv.Key, kv => kv.Value), default,
                instrutorNome, instrutorCargo, rastreio, ct)
            : await MontarCertificadoAsync(sessao, inscritos, assinaturaParticipante, assinaturaInstrutor,
            instrutorNome, instrutorCargo, rastreio, ct);

        var modelo = new AtaSessaoTreinamentoPdfModelo(
            sessao.Obra.Nome,
            sessao.Obra.LogoConteudo,
            sessao.CursoTreinamento.Nome,
            sessao.CursoTreinamento.NormaReferencia,
            sessao.DataRealizacao,
            sessao.CargaHorariaRealizada,
            sessao.InstituicaoInstrutor,
            sessao.NumeroCertificado,
            sessao.DataEncerramento,
            participantes,
            fotos,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            certificado,
            instrutor);

        var pdf = _pdf.Gerar(modelo);
        // Guarda a cópia exata emitida e o SHA-256 dela — é o que permite conferir, depois,
        // que o arquivo em mãos não foi adulterado (ver HashArquivoCalculador).
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

    private sealed record InscritoAta(
        Guid TrabalhadorId, string Nome, string? Matricula, string? FuncaoNome, string? Cbo,
        DateTime? TermoAceite, DateTime? Consentimento, DateTime? PresencaConfirmadaEm,
        MetodoAutenticacaoAssinatura? MetodoPresenca, Guid? TreinamentoGeradoId, string? EvidenciaHash);

    private static string? FuncaoComCbo(string? nome, string? cbo) => nome is null ? null
        : string.IsNullOrWhiteSpace(cbo) ? nome : $"{nome} · CBO {CodigoCbo.Formatar(cbo)}";

    // Certificado de Assinaturas Eletrônicas da lista (05/11, modelo único, compacto): uma evidência por
    // participante que assinou + a do instrutor, com a biometria cadastrada vigente ao lado da captura.
    private async Task<CertificadoAssinaturasModelo?> MontarCertificadoAsync(
        SessaoTreinamento sessao,
        List<InscritoAta> inscritos,
        Dictionary<Guid, (DocumentoAssinatura Doc, DocumentoSignatario Sig)> assinaturas,
        (DocumentoAssinatura Doc, DocumentoSignatario Sig) instrutor,
        string? instrutorNome, string? instrutorCargo,
        RastreabilidadeDocumentoResultado rastreio, CancellationToken ct)
    {
        if (assinaturas.Count == 0 && instrutor.Sig is null) return null;

        var referencias = await ReferenciaBiometricaVigente.CarregarAsync(_db, assinaturas.Keys.ToList(), _cripto, ct);
        var curso = sessao.CursoTreinamento!.Nome;
        var descricao = $"Lista de presença — {curso}";

        EvidenciaAssinaturaPdf Evidencia(DocumentoAssinatura doc, DocumentoSignatario sig)
        {
            var refs = referencias.Para(sig.TrabalhadorId, sig.MetodoAutenticacao, sig.AssinadoEm);
            byte[]? qr = null;
            if (_qrCode is not null && !string.IsNullOrWhiteSpace(doc.TokenValidacaoPublica))
            {
                try { qr = _qrCode.Gerar(doc.TokenValidacaoPublica!).Png; }
                catch { qr = null; }
            }
            return new EvidenciaAssinaturaPdf(
                sig.MetodoAutenticacao, sig.AssinadoEm, doc.CreatedAtUtc, sig.IpAddress, sig.UserAgent, sig.LocalizacaoStatus,
                sig.Latitude, sig.Longitude, sig.PrecisaoMetros, sig.ValidacaoModelo, sig.ValidacaoGrupoId, sig.ValidacaoRequisicaoId,
                sig.DispositivoAgenteId, sig.FotoEvidenciaConteudo, sig.FotoEvidenciaHash,
                refs.Foto, refs.FotoEm, refs.FotoHash, refs.Digital, refs.DigitalEm, qr);
        }

        var itens = new List<CertificadoAssinaturaItem>();
        foreach (var i in inscritos)
            if (assinaturas.TryGetValue(i.TrabalhadorId, out var a))
                itens.Add(new CertificadoAssinaturaItem(descricao, i.Nome,
                    $"{FuncaoComCbo(i.FuncaoNome, i.Cbo) ?? "—"} · Mat. {i.Matricula ?? "—"}",
                    i.TermoAceite, i.Consentimento, Evidencia(a.Doc, a.Sig)));
        if (instrutor.Sig is not null)
            itens.Add(new CertificadoAssinaturaItem("Lista de presença — assinatura do instrutor", instrutorNome ?? "Instrutor",
                instrutorCargo, null, null, Evidencia(instrutor.Doc, instrutor.Sig)));

        var norma = sessao.CursoTreinamento.NormaReferencia;
        var painel = new PainelCertificado("Treinamento", $"Turma {sessao.NumeroCertificado ?? "—"} · {curso}", new List<SecaoPainelCertificado>
        {
            new("Curso", new List<LinhaPainelCertificado>
            {
                new("Curso", string.IsNullOrWhiteSpace(norma) ? curso : $"{curso} ({norma})"),
                new("Carga horária", $"{sessao.CargaHorariaRealizada} h"),
                new("Data", sessao.DataRealizacao.ToString("dd/MM/yyyy")),
                new("Local", string.IsNullOrWhiteSpace(sessao.Local) ? "—" : sessao.Local!),
            }),
            new("Instrutor", new List<LinhaPainelCertificado>
            {
                new("Nome", instrutorNome ?? sessao.InstituicaoInstrutor ?? "—"),
                new("Registro", instrutorCargo ?? "—"),
            }),
        });

        return new CertificadoAssinaturasModelo(
            "Lista de Presença de Treinamento",
            $"Lista de Presença · Turma {sessao.NumeroCertificado ?? "—"} · {curso}",
            rastreio.DocumentoId.ToString(), DateTime.UtcNow, $"Sistema SST · {sessao.Obra!.Nome}",
            rastreio.ConteudoHash, sessao.Status != StatusSessaoTreinamento.Concluida, itens,
            sessao.Obra.Nome, sessao.Obra.LogoConteudo, rastreio.UrlValidacaoPublica, rastreio.QrCodePng,
            Compacto: true, PainelGeral: painel);
    }
}
