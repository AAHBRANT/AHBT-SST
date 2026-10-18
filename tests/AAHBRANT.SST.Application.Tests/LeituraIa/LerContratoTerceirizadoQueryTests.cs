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

        public Task<ContratoTerceirizadoLidoIa> LerContratoAsync(IReadOnlyList<string> paginas, CancellationToken ct)
        {
            PaginasRecebidas = paginas.Count;
            return Task.FromResult(resposta);
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
        Assert.Contains("digitalizado", erro.Message);
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
}
