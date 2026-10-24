using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.LeituraIa;

// Importação da pasta da terceirizada (23/10/2026): a pasta chega misturada (contrato, documentos da
// empresa, ASO, certificados e fichas de cada funcionário). A tela manda um arquivo por vez para esta
// rota, que só diz o que o arquivo é e de quem é — nada é gravado aqui. A conferência e o cadastro
// acontecem na tela, pelas mesmas rotas do cadastro manual.

public interface IClassificadorDocumentoTerceirizadoIa
{
    // paginas: texto do PDF (já com OCR). imagem: arquivo de imagem solto (JPG/PNG), quando não é PDF.
    // documentosEmpresa e cursos: nomes cadastrados no SST, para a IA escolher um deles.
    Task<DocumentoTerceirizadoLidoIa> ClassificarAsync(string nomeArquivo, IReadOnlyList<string> paginas, byte[]? imagem,
        IReadOnlyList<string> documentosEmpresa, IReadOnlyList<string> cursos, CancellationToken ct);
}

// Tipo: "contrato", "documento_empresa", "aso", "certificado", "ficha_epi" ou "outro".
// AsoTipo: "admissional", "periodico", "retorno", "mudanca_funcao" ou "demissional".
// AsoResultado: "apto", "apto_restricao" ou "inapto".
public record DocumentoTerceirizadoLidoIa(
    string Tipo,
    string? DocumentoEmpresa,
    string? Curso,
    string? PessoaNome,
    string? PessoaCpf,
    string? PessoaFuncao,
    string? EmpresaRazaoSocial,
    string? EmpresaCnpj,
    DateTime? DataDocumento,
    DateTime? DataValidade,
    string? AsoTipo,
    string? AsoResultado,
    string? MedicoNome,
    string? MedicoCrm,
    int? CargaHoraria,
    string? Instituicao,
    string? Resumo);

public record DocumentoTerceirizadoClassificado(
    string NomeArquivo,
    string Tipo,
    Guid? DocumentoExigidoId,
    string? DocumentoExigidoNome,
    Guid? CursoTreinamentoId,
    string? CursoNome,
    string? PessoaNome,
    string? PessoaCpf,
    string? PessoaFuncao,
    string? EmpresaRazaoSocial,
    string? EmpresaCnpj,
    DateTime? DataDocumento,
    DateTime? DataValidade,
    TipoExameAso? AsoTipo,
    ResultadoAso? AsoResultado,
    string? MedicoNome,
    string? MedicoCrm,
    int? CargaHoraria,
    string? Instituicao,
    string? Resumo);

public record ClassificarDocumentoTerceirizadoQuery(string NomeArquivo, string ContentType, byte[] Conteudo)
    : IRequest<DocumentoTerceirizadoClassificado>;

public class ClassificarDocumentoTerceirizadoQueryHandler
    : IRequestHandler<ClassificarDocumentoTerceirizadoQuery, DocumentoTerceirizadoClassificado>
{
    // Para dizer o que o documento é, as primeiras páginas bastam (ASO e certificado têm 1 ou 2).
    private const int PaginasLidas = 6;
    private const int PaginasOcr = 3;

    private readonly IExtratorTextoPdf _extrator;
    private readonly IClassificadorDocumentoTerceirizadoIa _classificador;
    private readonly IAppDbContext _db;
    private readonly IOcrPaginasPdf? _ocr;

    public ClassificarDocumentoTerceirizadoQueryHandler(IExtratorTextoPdf extrator, IClassificadorDocumentoTerceirizadoIa classificador,
        IAppDbContext db, IOcrPaginasPdf? ocr = null)
    {
        _extrator = extrator;
        _classificador = classificador;
        _db = db;
        _ocr = ocr;
    }

    public async Task<DocumentoTerceirizadoClassificado> Handle(ClassificarDocumentoTerceirizadoQuery request, CancellationToken ct)
    {
        var ehPdf = request.ContentType == "application/pdf" || request.NomeArquivo.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<string> paginas = Array.Empty<string>();
        byte[]? imagem = null;
        if (ehPdf)
        {
            try
            {
                paginas = _extrator.ExtrairPaginas(request.Conteudo).Take(PaginasLidas).ToList();
            }
            catch (Exception)
            {
                return NaoIdentificado(request.NomeArquivo, "Não foi possível abrir o PDF.");
            }
            if (_ocr is not null && paginas.Any(PaginasDigitalizadas.SemTexto))
                paginas = await _ocr.CompletarAsync(request.Conteudo, paginas, PaginasOcr, ct);
            if (paginas.All(string.IsNullOrWhiteSpace))
                return NaoIdentificado(request.NomeArquivo, "Não foi possível ler o texto do PDF.");
        }
        else if (request.ContentType is "image/jpeg" or "image/png")
        {
            imagem = request.Conteudo;
        }
        else
        {
            return NaoIdentificado(request.NomeArquivo, "Tipo de arquivo não suportado (use PDF, JPG ou PNG).");
        }

        var exigidos = await _db.DocumentosExigidosEmpresa.AsNoTracking()
            .OrderBy(d => d.Ordem).Select(d => new { d.Id, d.Nome }).ToListAsync(ct);
        var cursos = await _db.CursosTreinamento.AsNoTracking()
            .OrderBy(c => c.Nome).Select(c => new { c.Id, c.Nome }).ToListAsync(ct);

        var lido = await _classificador.ClassificarAsync(request.NomeArquivo, paginas, imagem,
            exigidos.Select(d => d.Nome).ToList(), cursos.Select(c => c.Nome).ToList(), ct);

        var tipo = TiposValidos.Contains(lido.Tipo) ? lido.Tipo : "outro";
        var exigido = tipo == "documento_empresa" ? exigidos.FirstOrDefault(d => Igual(d.Nome, lido.DocumentoEmpresa)) : null;
        var curso = tipo == "certificado" ? cursos.FirstOrDefault(c => Igual(c.Nome, lido.Curso)) : null;
        var cpf = SoDigitos(lido.PessoaCpf);
        var cnpj = SoDigitos(lido.EmpresaCnpj);

        return new DocumentoTerceirizadoClassificado(
            request.NomeArquivo, tipo,
            exigido?.Id, exigido?.Nome ?? (tipo == "documento_empresa" ? lido.DocumentoEmpresa : null),
            curso?.Id, curso?.Nome ?? (tipo == "certificado" ? lido.Curso : null),
            Limpo(lido.PessoaNome), cpf is { Length: 11 } ? cpf : null, Limpo(lido.PessoaFuncao),
            Limpo(lido.EmpresaRazaoSocial), cnpj is { Length: 14 } ? cnpj : null,
            lido.DataDocumento, lido.DataValidade,
            tipo == "aso" ? TipoAso(lido.AsoTipo) : null,
            tipo == "aso" ? ResultadoDoAso(lido.AsoResultado) : null,
            Limpo(lido.MedicoNome), Limpo(lido.MedicoCrm),
            lido.CargaHoraria is > 0 ? lido.CargaHoraria : null, Limpo(lido.Instituicao), Limpo(lido.Resumo));
    }

    private static readonly HashSet<string> TiposValidos =
        new() { "contrato", "documento_empresa", "aso", "certificado", "ficha_epi", "outro" };

    private static DocumentoTerceirizadoClassificado NaoIdentificado(string nomeArquivo, string motivo) =>
        new(nomeArquivo, "outro", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, motivo);

    private static bool Igual(string cadastrado, string? lido) =>
        !string.IsNullOrWhiteSpace(lido) && string.Equals(cadastrado.Trim(), lido.Trim(), StringComparison.OrdinalIgnoreCase);

    private static TipoExameAso? TipoAso(string? tipo) => tipo switch
    {
        "admissional" => TipoExameAso.Admissional,
        "periodico" => TipoExameAso.Periodico,
        "retorno" => TipoExameAso.RetornoAoTrabalho,
        "mudanca_funcao" => TipoExameAso.MudancaDeFuncao,
        "demissional" => TipoExameAso.Demissional,
        _ => null,
    };

    private static ResultadoAso? ResultadoDoAso(string? resultado) => resultado switch
    {
        "apto" => ResultadoAso.Apto,
        "apto_restricao" => ResultadoAso.AptoComRestricao,
        "inapto" => ResultadoAso.Inapto,
        _ => null,
    };

    private static string? Limpo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? SoDigitos(string? s)
    {
        var d = new string((s ?? string.Empty).Where(char.IsDigit).ToArray());
        return d.Length == 0 ? null : d;
    }
}
