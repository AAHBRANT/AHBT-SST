using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.LeituraIa;

public class LerContratoTerceirizadoQueryTests
{
    private sealed class ExtratorFalso(params string[] paginas) : IExtratorTextoPdf
    {
        public IReadOnlyList<string> ExtrairPaginas(byte[] pdf) => paginas;
    }

    private sealed class LeitorFalso(ContratoTerceirizadoLidoIa resposta) : ILeitorContratoTerceirizadoIa
    {
        public int PaginasRecebidas { get; private set; }
        public IReadOnlyList<string> Paginas { get; private set; } = Array.Empty<string>();

        public Task<ContratoTerceirizadoLidoIa> LerContratoAsync(IReadOnlyList<string> paginas, CancellationToken ct)
        {
            PaginasRecebidas = paginas.Count;
            Paginas = paginas;
            return Task.FromResult(resposta);
        }
    }

    // OCR falso: troca cada página sem texto por "ocr <n>" (até o limite pedido) e guarda o limite.
    private sealed class OcrFalso(bool leAlgo = true) : IOcrPaginasPdf
    {
        public int? LimitePedido { get; private set; }

        public Task<IReadOnlyList<string>> CompletarAsync(byte[] pdf, IReadOnlyList<string> paginas, int maxPaginasOcr, CancellationToken ct)
        {
            LimitePedido = maxPaginasOcr;
            var feitas = 0;
            IReadOnlyList<string> resultado = paginas
                .Select((p, i) => leAlgo && PaginasDigitalizadas.SemTexto(p) && feitas++ < maxPaginasOcr ? $"ocr {i + 1} {new string('x', 50)}" : p)
                .ToList();
            return Task.FromResult(resultado);
        }
    }

    private static SstDbContext CriarDb(string nome) =>
        new(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, new CurrentUserService());

    private static ContratoTerceirizadoLidoIa Lido(string? cnpj = "12.345.678/0001-99") => new(
        new EmpresaLidaIa("Empreiteira XPTO Ltda", null, cnpj, "Alvenaria", null, null, null),
        "CT-001", "Obra Centro", new DateTime(2026, 11, 1), new DateTime(2027, 4, 30),
        new List<VagaLidaIa> { new("Pedreiro", 5) },
        new List<PessoaLidaIa> { new("João da Silva", "529.982.247-25", "Pedreiro", null) });

    [Fact]
    public async Task Ler_DevolveDadosComCnpjECpfSoNumerosEAchaEmpresaJaCadastrada()
    {
        var db = CriarDb(nameof(Ler_DevolveDadosComCnpjECpfSoNumerosEAchaEmpresaJaCadastrada));
        var existente = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        db.Empresas.Add(existente);
        await db.SaveChangesAsync();

        var resultado = await new LerContratoTerceirizadoQueryHandler(new ExtratorFalso("pagina 1"), new LeitorFalso(Lido()), db)
            .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default);

        Assert.Equal("12345678000199", resultado.Empresa.Cnpj);
        Assert.Equal("52998224725", resultado.Pessoas.Single().Cpf);
        Assert.Equal(existente.Id, resultado.EmpresaExistenteId);
        Assert.Equal("XPTO", resultado.EmpresaExistenteRazaoSocial);
    }

    [Fact]
    public async Task Ler_EmpresaNova_NaoApontaEmpresaExistente()
    {
        var db = CriarDb(nameof(Ler_EmpresaNova_NaoApontaEmpresaExistente));

        var resultado = await new LerContratoTerceirizadoQueryHandler(new ExtratorFalso("pagina 1"), new LeitorFalso(Lido(cnpj: null)), db)
            .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default);

        Assert.Null(resultado.EmpresaExistenteId);
        Assert.Null(resultado.Empresa.Cnpj);
    }

    [Fact]
    public async Task Ler_PdfSemTexto_PedePreenchimentoManual()
    {
        var db = CriarDb(nameof(Ler_PdfSemTexto_PedePreenchimentoManual));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new LerContratoTerceirizadoQueryHandler(new ExtratorFalso("", "  "), new LeitorFalso(Lido()), db)
                .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default));
        Assert.Contains("Preencha os campos manualmente", erro.Message);
    }

    [Fact]
    public async Task Ler_ContratoLongo_EnviaSoAsPrimeirasQuarentaPaginas()
    {
        var db = CriarDb(nameof(Ler_ContratoLongo_EnviaSoAsPrimeirasQuarentaPaginas));
        var leitor = new LeitorFalso(Lido());

        await new LerContratoTerceirizadoQueryHandler(new ExtratorFalso(Enumerable.Repeat("texto", 120).ToArray()), leitor, db)
            .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default);

        Assert.Equal(40, leitor.PaginasRecebidas);
    }

    [Fact]
    public async Task Ler_PdfDigitalizado_UsaOTextoDoOcr()
    {
        var db = CriarDb(nameof(Ler_PdfDigitalizado_UsaOTextoDoOcr));
        var leitor = new LeitorFalso(Lido());
        var ocr = new OcrFalso();

        await new LerContratoTerceirizadoQueryHandler(new ExtratorFalso("", "  "), leitor, db, ocr)
            .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default);

        Assert.Equal(15, ocr.LimitePedido);
        Assert.StartsWith("ocr 1", leitor.Paginas[0]);
        Assert.StartsWith("ocr 2", leitor.Paginas[1]);
    }

    [Fact]
    public async Task Ler_PdfComTexto_NaoChamaOcr()
    {
        var db = CriarDb(nameof(Ler_PdfComTexto_NaoChamaOcr));
        var ocr = new OcrFalso();

        await new LerContratoTerceirizadoQueryHandler(new ExtratorFalso(new string('t', 200)), new LeitorFalso(Lido()), db, ocr)
            .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default);

        Assert.Null(ocr.LimitePedido);
    }

    [Fact]
    public async Task Ler_OcrNaoLeuNada_PedePreenchimentoManual()
    {
        var db = CriarDb(nameof(Ler_OcrNaoLeuNada_PedePreenchimentoManual));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new LerContratoTerceirizadoQueryHandler(new ExtratorFalso("", ""), new LeitorFalso(Lido()), db, new OcrFalso(leAlgo: false))
                .Handle(new LerContratoTerceirizadoQuery(new byte[] { 1 }), default));
        Assert.Contains("nem pelas imagens das páginas", erro.Message);
    }
}
