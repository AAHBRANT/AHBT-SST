using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.GeracaoPgr;

// PDF do PGR gerado pela EAP (23/10/2026): só depois da aprovação do Engenheiro de Segurança.
public class GerarPdfGeracaoPgrEapTests
{
    private sealed class PdfFalso : IPgrPdfService
    {
        public DadosPgrPdf? Dados { get; private set; }

        public byte[] Gerar(DadosPgrPdf dados, byte[]? logoObra)
        {
            Dados = dados;
            return new byte[] { 1, 2, 3 };
        }
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, GeracaoPgrEap Geracao)> CriarAsync(StatusGeracaoPgrEap status, string estruturaJson)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste", Cnpj = "00.000.000/0001-00" };
        var geracao = new GeracaoPgrEap
        {
            Obra = obra,
            Nome = "PGR Obra Teste",
            DataElaboracao = new DateTime(2026, 10, 21),
            NomeArquivoEap = "eap.xlsx",
            Status = status,
            EstruturaJson = estruturaJson,
            AprovadoPorNome = "Eng. Carlos",
            AprovadoEmUtc = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
        };
        db.AddRange(obra, geracao);
        await db.SaveChangesAsync();
        return (db, geracao);
    }

    [Fact]
    public async Task GeraPdf_ComCboDoCadastro_EExpostosSoDosTrabalhadoresAtivosDaObra()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.Aprovado, "{}");
        var armador = new Funcao { Nome = "ARMADOR", CboCodigo = "715210" };
        db.Funcoes.Add(armador);
        await db.SaveChangesAsync();
        db.Trabalhadores.AddRange(
            new Trabalhador { Nome = "A", Cpf = "11111111111", ObraId = geracao.ObraId, FuncaoId = armador.Id, DataAdmissao = DateTime.UtcNow.AddYears(-1) },
            new Trabalhador { Nome = "B", Cpf = "22222222222", ObraId = geracao.ObraId, FuncaoId = armador.Id, DataAdmissao = DateTime.UtcNow.AddYears(-1) },
            new Trabalhador { Nome = "C", Cpf = "33333333333", ObraId = geracao.ObraId, FuncaoId = armador.Id, DataAdmissao = DateTime.UtcNow.AddYears(-1), DataDemissao = DateTime.UtcNow.AddDays(-5) });
        geracao.EstruturaJson = $$"""
            {"Ghes":[{"Numero":1,"Nome":"Estrutura","Funcoes":[{"FuncaoId":"{{armador.Id}}","Nome":"ARMADOR","Descricao":"x"},{"Nome":"Soldador","Descricao":"y"}],"Atividades":[]}]}
            """;
        await db.SaveChangesAsync();
        var pdf = new PdfFalso();

        await new GerarPdfGeracaoPgrEapQueryHandler(db, pdf).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default);

        var funcoes = pdf.Dados!.Funcoes!;
        var linhaArmador = Assert.Single(funcoes, f => f.Nome == "ARMADOR");
        Assert.Equal("715210", linhaArmador.Cbo);
        Assert.Equal(2, linhaArmador.Expostos); // o demitido não conta
        var linhaSoldador = Assert.Single(funcoes, f => f.Nome == "Soldador"); // função nova, ainda fora do cadastro
        Assert.Null(linhaSoldador.Cbo);
        Assert.Equal(0, linhaSoldador.Expostos);
        var revisao = Assert.Single(pdf.Dados.Revisoes!);
        Assert.Equal(0, revisao.Numero); // sem revisões registradas: emissão inicial
    }

    [Fact]
    public async Task GeraPdf_LevaOCaDoEpiLigadoNaMedida()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.Aprovado, "{}");
        var epi = new CatalogoEpi { Nome = "Protetor auricular", CertificadoAprovacaoNumero = "12345", CertificadoAprovacaoValidade = new DateTime(2028, 5, 1) };
        db.CatalogoEpis.Add(epi);
        await db.SaveChangesAsync();
        geracao.EstruturaJson = $$"""
            {"ghes":[{"numero":1,"nome":"Estrutura","funcoes":[],"atividades":[{"codigoEap":"1.1","nome":"Laje","riscosGerados":true,
            "riscos":[{"tipo":"Físico","perigo":"Ruído","probabilidade":2,"severidade":2,"medidas":[
            {"fase":2,"texto":"Protetor auricular","categoria":"EPI","catalogoEpiId":"{{epi.Id}}","decisao":"Aceita"}]}]}]}]}
            """;
        await db.SaveChangesAsync();
        var pdf = new PdfFalso();

        await new GerarPdfGeracaoPgrEapQueryHandler(db, pdf).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default);

        var info = Assert.Single(pdf.Dados!.Epis!).Value;
        Assert.Equal("Protetor auricular", info.Nome);
        Assert.Equal("12345", info.Ca);
        Assert.Equal(new DateTime(2028, 5, 1), info.CaValidade);
    }

    [Fact]
    public async Task NaoGera_PdfEnquantoNaoAprovado()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.AguardandoAprovacao, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GerarPdfGeracaoPgrEapQueryHandler(db, new PdfFalso()).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default));
    }

    [Fact]
    public async Task GeraPdf_ComDadosDaEstruturaAprovada_EMesmoNomeDoArquivo()
    {
        const string estrutura = """
            {"Ghes":[{"Numero":1,"Nome":"Estrutura","Funcoes":[{"Nome":"ARMADOR","Descricao":"Monta armaduras"}],
            "Atividades":[{"CodigoEap":"1.1","Nome":"Concretagem de laje","RiscosGerados":true,
            "Riscos":[{"Tipo":"Acidente","Perigo":"Queda de altura","Probabilidade":4,"Severidade":5,"Revisado":true,
            "Medidas":[{"Fase":1,"Texto":"Instale guarda-corpo","Decisao":"Aceita"}]}]}]}],"PlanoAcao":["Instalar linha de vida"]}
            """;
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.Aprovado, estrutura);
        var pdf = new PdfFalso();

        var arquivo = await new GerarPdfGeracaoPgrEapQueryHandler(db, pdf).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default);

        Assert.Equal(new byte[] { 1, 2, 3 }, arquivo.Conteudo);
        Assert.Equal("PGR - Obra Teste - 20261021.pdf", arquivo.NomeArquivo);
        Assert.Equal("Obra Teste", pdf.Dados!.ObraNome);
        Assert.Equal("00.000.000/0001-00", pdf.Dados.ObraCnpj);
        Assert.Equal("Eng. Carlos", pdf.Dados.AprovadoPorNome);
        Assert.Equal("Concretagem de laje", pdf.Dados.Estrutura.Ghes.Single().Atividades.Single().Nome);
        // Plano no formato antigo (uma ação por linha) é lido como ação sem responsável nem prazo.
        Assert.Equal("Instalar linha de vida", pdf.Dados.Estrutura.AcoesPlano.Single().Descricao);
    }

    [Fact]
    public async Task GeraPdf_LevaOsCursosObrigatoriosDaFuncaoNaObra_SemORecusadoNemDeOutraObra()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.Aprovado, "{}");
        var outraObra = new Obra { Codigo = "OB-02", Nome = "Outra obra" };
        var armador = new Funcao { Nome = "ARMADOR" };
        var altura = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        var basico = new CursoTreinamento { Nome = "NR-18 Condições e Meio Ambiente", NormaReferencia = "NR-18", CargaHorariaMinima = 6, ValidadeEmMeses = 12 };
        var epi = new CursoTreinamento { Nome = "NR-06 Uso de EPI", NormaReferencia = "NR-06", CargaHorariaMinima = 4, ValidadeEmMeses = 12 };
        db.AddRange(outraObra, armador, altura, basico, epi);
        await db.SaveChangesAsync();
        db.CursosObrigatoriosFuncaoObra.AddRange(
            new CursoObrigatorioFuncaoObra { ObraId = geracao.ObraId, FuncaoId = armador.Id, CursoTreinamentoId = altura.Id, Status = StatusCursoObrigatorio.Aprovado, Motivo = "Perigo \"Queda de altura\" na atividade 3.2" },
            new CursoObrigatorioFuncaoObra { ObraId = geracao.ObraId, FuncaoId = armador.Id, CursoTreinamentoId = basico.Id, Status = StatusCursoObrigatorio.Sugerido },
            new CursoObrigatorioFuncaoObra { ObraId = geracao.ObraId, FuncaoId = armador.Id, CursoTreinamentoId = epi.Id, Status = StatusCursoObrigatorio.Recusado },
            new CursoObrigatorioFuncaoObra { ObraId = outraObra.Id, FuncaoId = armador.Id, CursoTreinamentoId = epi.Id, Status = StatusCursoObrigatorio.Aprovado });
        geracao.EstruturaJson = $$"""
            {"Ghes":[{"Numero":1,"Nome":"Estrutura","Funcoes":[{"FuncaoId":"{{armador.Id}}","Nome":"ARMADOR","Descricao":"x"}],"Atividades":[]}]}
            """;
        await db.SaveChangesAsync();
        var pdf = new PdfFalso();

        await new GerarPdfGeracaoPgrEapQueryHandler(db, pdf).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default);

        var linhas = pdf.Dados!.Treinamentos!;
        Assert.Equal(new[] { "NR-18", "NR-35" }, linhas.Select(l => l.Norma).ToArray());
        var nr35 = linhas.Single(l => l.Norma == "NR-35");
        Assert.True(nr35.Aprovado);
        Assert.Equal(8, nr35.CargaHorariaMinima);
        Assert.Equal(24, nr35.ValidadeEmMeses);
        Assert.Equal("ARMADOR", nr35.Funcao);
        Assert.False(linhas.Single(l => l.Norma == "NR-18").Aprovado);
    }

    [Fact]
    public async Task GeraPdf_SemCursosObrigatorios_TreinamentosVemVazio()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.Aprovado, "{}");
        var pdf = new PdfFalso();

        await new GerarPdfGeracaoPgrEapQueryHandler(db, pdf).Handle(new GerarPdfGeracaoPgrEapQuery(geracao.Id), default);

        Assert.Empty(pdf.Dados!.Treinamentos!);
    }

    [Fact]
    public async Task CronogramaTreinamentos_SoDepoisDaAprovacao_ELevaGhesELinhas()
    {
        var (db, geracao) = await CriarAsync(StatusGeracaoPgrEap.AguardandoAprovacao, "{}");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ObterCronogramaTreinamentosGeracaoPgrQueryHandler(db).Handle(new ObterCronogramaTreinamentosGeracaoPgrQuery(geracao.Id), default));

        var armador = new Funcao { Nome = "ARMADOR" };
        var altura = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        db.AddRange(armador, altura);
        await db.SaveChangesAsync();
        db.CursosObrigatoriosFuncaoObra.Add(new CursoObrigatorioFuncaoObra { ObraId = geracao.ObraId, FuncaoId = armador.Id, CursoTreinamentoId = altura.Id, Status = StatusCursoObrigatorio.Sugerido });
        geracao.Status = StatusGeracaoPgrEap.Aprovado;
        geracao.EstruturaJson = """
            {"Ghes":[{"Numero":1,"Nome":"Estrutura","Funcoes":[{"Nome":"armador","Descricao":"x"}],"Atividades":[]}]}
            """;
        await db.SaveChangesAsync();

        var r = await new ObterCronogramaTreinamentosGeracaoPgrQueryHandler(db).Handle(new ObterCronogramaTreinamentosGeracaoPgrQuery(geracao.Id), default);

        Assert.Equal("Estrutura", Assert.Single(r.Ghes).Nome);
        var linha = Assert.Single(r.Linhas);
        Assert.Equal("NR-35", linha.Norma);
        Assert.False(linha.Aprovado); // função achada pelo nome, curso ainda só sugerido
    }
}
