using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.LeituraCertificado;

// Certificado externo lido pela IA (07/11/2026, mockup certificado-ia-perfil_2026-11-07_v1): o técnico envia foto ou
// PDF, a IA (Claude) diz quanto girar para ficar em pé e lê os dados. Nada é gravado na leitura: o técnico confere e
// salva. O arquivo é guardado já na posição certa; o original não é mantido (decisão do usuário).

/// <param name="Rotacao">Graus no sentido horário para deixar o documento em pé: 0, 90, 180 ou 270.</param>
public record CertificadoLidoIa(
    int Rotacao,
    string? Curso,
    string? Norma,
    DateTime? DataRealizacao,
    int? CargaHoraria,
    string? Instituicao,
    string? Instrutor,
    string? RegistroInstrutor,
    string? NumeroCertificado,
    string? NomeParticipante);

public interface ILeitorCertificadoIa
{
    // imagemJpeg: a página do certificado como imagem (foto ou página de PDF digitalizado). textoPdf: texto do PDF
    // digital, quando há. cursos: nomes do catálogo, para a IA devolver o nome igual.
    Task<CertificadoLidoIa> LerAsync(byte[]? imagemJpeg, string? textoPdf, IReadOnlyList<string> cursos, CancellationToken ct);
}

// Imagem/PDF do certificado: tira a imagem para a IA ler e gira o arquivo.
public interface IProcessadorArquivoCertificado
{
    // Imagem da 1ª página (já na orientação EXIF). Nulo quando é PDF digital sem imagem.
    byte[]? ImagemParaLeitura(byte[] conteudo, string contentType);
    string? TextoPdf(byte[] conteudo, string contentType);
    // Gira no sentido horário. PDF digitalizado é refeito a partir das imagens das páginas.
    (byte[] Conteudo, string ContentType) Girar(byte[] conteudo, string contentType, int graus);
}

public record CertificadoLidoDto(
    int Rotacao,
    Guid? CursoTreinamentoId,
    string? CursoLido,
    DateTime? DataRealizacao,
    DateTime? DataValidade,
    int? CargaHoraria,
    string? Instituicao,
    string? RegistroInstrutor,
    string? NumeroCertificado,
    string? NomeParticipante,
    bool NomeConfere);

public record LerCertificadoTreinamentoQuery(Guid TrabalhadorId, byte[] Conteudo, string ContentType) : IRequest<CertificadoLidoDto>;

public class LerCertificadoTreinamentoQueryHandler : IRequestHandler<LerCertificadoTreinamentoQuery, CertificadoLidoDto>
{
    private static readonly string[] Tipos = { "application/pdf", "image/jpeg", "image/png" };
    private readonly IAppDbContext _db;
    private readonly ILeitorCertificadoIa _ia;
    private readonly IProcessadorArquivoCertificado _arquivo;

    public LerCertificadoTreinamentoQueryHandler(IAppDbContext db, ILeitorCertificadoIa ia, IProcessadorArquivoCertificado arquivo)
    {
        _db = db;
        _ia = ia;
        _arquivo = arquivo;
    }

    public async Task<CertificadoLidoDto> Handle(LerCertificadoTreinamentoQuery request, CancellationToken ct)
    {
        if (!Tipos.Contains(request.ContentType))
            throw new InvalidOperationException("O certificado deve ser um arquivo PDF, JPEG ou PNG.");
        var trabalhador = await _db.Trabalhadores.NoEscopoDoTrabalhador(_db, t => t.Id).Where(t => t.Id == request.TrabalhadorId)
            .Select(t => new { t.Nome }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Funcionário não encontrado.");

        var cursos = await _db.CursosTreinamento.Select(c => new { c.Id, c.Nome, c.NormaReferencia, c.ValidadeEmMeses }).ToListAsync(ct);
        var imagem = _arquivo.ImagemParaLeitura(request.Conteudo, request.ContentType);
        var texto = imagem is null ? _arquivo.TextoPdf(request.Conteudo, request.ContentType) : null;
        if (imagem is null && string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("Não foi possível ler o arquivo. Preencha os dados manualmente.");

        var lido = await _ia.LerAsync(imagem, texto, cursos.Select(c => c.Nome).ToList(), ct);

        // Curso: nome igual ao catálogo; senão, o primeiro do catálogo da mesma NR.
        var curso = cursos.FirstOrDefault(c => Normalizar(c.Nome) == Normalizar(lido.Curso))
            ?? cursos.Where(c => NumeroNr(c.NormaReferencia) is { } n && n == (NumeroNr(lido.Norma) ?? NumeroNr(lido.Curso)))
                .OrderBy(c => c.Nome).FirstOrDefault();
        DateTime? validade = curso is not null && lido.DataRealizacao is { } d && curso.ValidadeEmMeses > 0
            ? d.AddMonths(curso.ValidadeEmMeses) : null;

        var instrutor = string.Join(" · ", new[] { lido.Instituicao, lido.Instrutor }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var nomeConfere = string.IsNullOrWhiteSpace(lido.NomeParticipante) || NomeParecido(trabalhador.Nome, lido.NomeParticipante!);
        return new CertificadoLidoDto(
            Rotacao: lido.Rotacao is 90 or 180 or 270 ? lido.Rotacao : 0,
            CursoTreinamentoId: curso?.Id,
            CursoLido: lido.Curso,
            DataRealizacao: lido.DataRealizacao,
            DataValidade: validade,
            CargaHoraria: lido.CargaHoraria,
            Instituicao: string.IsNullOrWhiteSpace(instrutor) ? null : instrutor,
            RegistroInstrutor: lido.RegistroInstrutor,
            NumeroCertificado: lido.NumeroCertificado,
            NomeParticipante: lido.NomeParticipante,
            NomeConfere: nomeConfere);
    }

    internal static string Normalizar(string? s) => FuncaoSstClassifier.Normalizar(s ?? string.Empty);

    private static int? NumeroNr(string? s)
    {
        var m = s is null ? null : System.Text.RegularExpressions.Regex.Match(s, @"NR\s*-?\s*0*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m is { Success: true } ? int.Parse(m.Groups[1].Value) : null;
    }

    // Confere primeiro e último nome (o certificado às vezes abrevia o meio ou inclui "da", "de").
    internal static bool NomeParecido(string cadastro, string lido)
    {
        var a = Normalizar(cadastro).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var b = Normalizar(lido).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length == 0 || b.Length == 0) return true;
        return a[0] == b[0] && a[^1] == b[^1];
    }
}

// "Corrigir imagem com IA" (07/11/2026): para certificado já anexado de lado ou de cabeça para baixo.
public record CorrigirArquivoCertificadoTreinamentoCommand(Guid TreinamentoId) : IRequest<int>;

public class CorrigirArquivoCertificadoTreinamentoCommandHandler : IRequestHandler<CorrigirArquivoCertificadoTreinamentoCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly ILeitorCertificadoIa _ia;
    private readonly IProcessadorArquivoCertificado _arquivo;

    public CorrigirArquivoCertificadoTreinamentoCommandHandler(IAppDbContext db, ILeitorCertificadoIa ia, IProcessadorArquivoCertificado arquivo)
    {
        _db = db;
        _ia = ia;
        _arquivo = arquivo;
    }

    public async Task<int> Handle(CorrigirArquivoCertificadoTreinamentoCommand request, CancellationToken ct)
    {
        var existe = await _db.Treinamentos.NoEscopoDoTrabalhador(_db, t => t.TrabalhadorId).AnyAsync(t => t.Id == request.TreinamentoId, ct);
        var arquivo = existe ? await _db.ArquivosCertificadoTreinamento.FirstOrDefaultAsync(a => a.TreinamentoId == request.TreinamentoId, ct) : null;
        if (arquivo is null) throw new KeyNotFoundException("Este treinamento não tem certificado anexado.");

        var imagem = _arquivo.ImagemParaLeitura(arquivo.Conteudo, arquivo.ContentType)
            ?? throw new InvalidOperationException("O arquivo é um PDF digital (não é foto nem digitalização): não há imagem para corrigir.");
        var lido = await _ia.LerAsync(imagem, null, Array.Empty<string>(), ct);
        if (lido.Rotacao is not (90 or 180 or 270)) return 0;

        var (conteudo, tipo) = _arquivo.Girar(arquivo.Conteudo, arquivo.ContentType, lido.Rotacao);
        arquivo.Conteudo = conteudo;
        arquivo.ContentType = tipo;
        arquivo.TamanhoBytes = conteudo.LongLength;
        await _db.SaveChangesAsync(ct);
        return lido.Rotacao;
    }
}
