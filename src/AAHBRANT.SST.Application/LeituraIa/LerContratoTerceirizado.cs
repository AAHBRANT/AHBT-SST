using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.LeituraIa;

// Cadastro de empresa terceirizada pelo PDF do contrato (18/10/2026) — mesmo modelo do preenchimento do
// PGR/PCMSO (LerCabecalhoDocumento): nada é gravado aqui. A IA lê empresa, contrato, vagas e as pessoas
// listadas, a tela preenche o cadastro e só vira registro quando o técnico confere e confirma.

public interface ILeitorContratoTerceirizadoIa
{
    Task<ContratoTerceirizadoLidoIa> LerContratoAsync(IReadOnlyList<string> paginas, CancellationToken ct);
}

public record EmpresaLidaIa(string? RazaoSocial, string? NomeFantasia, string? Cnpj, string? TipoServicoPrestado,
    string? ContatoNome, string? ContatoTelefone, string? ContatoEmail);

public record VagaLidaIa(string Funcao, int? Quantidade);

public record PessoaLidaIa(string Nome, string? Cpf, string? Funcao, string? Matricula);

public record ContratoTerceirizadoLidoIa(
    EmpresaLidaIa Empresa,
    string? NumeroContrato,
    string? Obra,
    DateTime? DataInicioVigencia,
    DateTime? DataFimVigencia,
    List<VagaLidaIa> Vagas,
    List<PessoaLidaIa> Pessoas,
    // Preenchidos aqui (não pela IA): empresa com o mesmo CNPJ já cadastrada no SST.
    Guid? EmpresaExistenteId = null,
    string? EmpresaExistenteRazaoSocial = null);

public record LerContratoTerceirizadoQuery(byte[] Pdf) : IRequest<ContratoTerceirizadoLidoIa>;

public class LerContratoTerceirizadoQueryHandler : IRequestHandler<LerContratoTerceirizadoQuery, ContratoTerceirizadoLidoIa>
{
    // Contrato com anexos (relação de pessoas, planilha de serviços) passa fácil de 30 páginas; o que o
    // cadastro precisa está no preâmbulo, nas cláusulas de objeto/vigência e na relação de pessoas.
    private const int PaginasLidas = 40;

    // Contrato digitalizado: OCR só das primeiras páginas sem texto. A leitura do contrato é síncrona (o
    // técnico espera na tela), e 15 páginas em 4 chamadas paralelas cabem em menos de um minuto.
    private const int PaginasOcr = 15;

    private readonly IExtratorTextoPdf _extrator;
    private readonly ILeitorContratoTerceirizadoIa _leitor;
    private readonly IAppDbContext _db;
    private readonly IOcrPaginasPdf? _ocr;

    public LerContratoTerceirizadoQueryHandler(IExtratorTextoPdf extrator, ILeitorContratoTerceirizadoIa leitor, IAppDbContext db,
        IOcrPaginasPdf? ocr = null)
    {
        _extrator = extrator;
        _leitor = leitor;
        _db = db;
        _ocr = ocr;
    }

    public async Task<ContratoTerceirizadoLidoIa> Handle(LerContratoTerceirizadoQuery request, CancellationToken ct)
    {
        IReadOnlyList<string> paginas;
        try
        {
            paginas = _extrator.ExtrairPaginas(request.Pdf);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Não foi possível abrir o PDF. Confira se o arquivo é um PDF válido.");
        }
        IReadOnlyList<string> lidas = paginas.Take(PaginasLidas).ToList();
        if (_ocr is not null && lidas.Any(PaginasDigitalizadas.SemTexto))
            lidas = await _ocr.CompletarAsync(request.Pdf, lidas, PaginasOcr, ct);
        if (lidas.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Não foi possível ler o texto do PDF, nem pelas imagens das páginas. Preencha os campos manualmente.");

        var lido = await _leitor.LerContratoAsync(lidas, ct);

        var cnpj = SoDigitos(lido.Empresa.Cnpj);
        var empresa = cnpj is { Length: 14 }
            ? await _db.Empresas.AsNoTracking().Where(e => e.Cnpj == cnpj)
                .Select(e => new { e.Id, e.RazaoSocial }).FirstOrDefaultAsync(ct)
            : null;

        return lido with
        {
            Empresa = lido.Empresa with { Cnpj = cnpj },
            Pessoas = lido.Pessoas.Select(p => p with { Cpf = SoDigitos(p.Cpf) }).ToList(),
            EmpresaExistenteId = empresa?.Id,
            EmpresaExistenteRazaoSocial = empresa?.RazaoSocial,
        };
    }

    private static string? SoDigitos(string? s)
    {
        var d = new string((s ?? string.Empty).Where(char.IsDigit).ToArray());
        return d.Length == 0 ? null : d;
    }
}
