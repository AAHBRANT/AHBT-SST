using System.Text;
using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados.Conformidade;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using AAHBRANT.SST.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class ConformidadeEmpresaTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    static ConformidadeEmpresaTests()
    {
        ChavesCpfDeTeste.Configurar();
    }

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private sealed record Cenario(Obra Obra, Empresa Empresa, Contrato Contrato, Funcao Funcao, CursoTreinamento Integracao,
        CursoTreinamento Nr35, DocumentoExigidoEmpresa Pgr, DocumentoExigidoEmpresa ContratoSocial);

    // Empresa com um contrato vigente (1 vaga de Pedreiro, que exige NR-35), dois documentos exigidos
    // (PGR com validade e contrato social sem validade) e nenhuma pessoa ainda.
    private static async Task<Cenario> SemearAsync(IAppDbContext db)
    {
        var obra = new Obra { Codigo = "O1", Nome = "Obra 1" };
        var empresa = new Empresa { RazaoSocial = "XPTO", Cnpj = "12345678000199" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", CargaHorariaMinima = 4, ValidadeEmMeses = 12, EhIntegracaoSeguranca = true };
        var nr35 = new CursoTreinamento { Nome = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        var pgr = new DocumentoExigidoEmpresa { Nome = "PGR", ExigeValidade = true, Ordem = 1 };
        var contratoSocial = new DocumentoExigidoEmpresa { Nome = "Contrato social", ExigeValidade = false, Ordem = 2 };
        db.Obras.Add(obra);
        db.Empresas.Add(empresa);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.AddRange(integracao, nr35);
        db.DocumentosExigidosEmpresa.AddRange(pgr, contratoSocial);
        await db.SaveChangesAsync();
        db.MatrizTreinamentoFuncoes.Add(new MatrizTreinamentoFuncao { FuncaoId = funcao.Id, CursoTreinamentoId = nr35.Id });
        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001",
            DataInicioVigencia = Hoje, DataFimVigencia = Hoje.AddMonths(6),
        };
        db.Contratos.Add(contrato);
        await db.SaveChangesAsync();
        db.ContratoVagasFuncao.Add(new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcao.Id, QuantidadeVagas = 2 });
        await db.SaveChangesAsync();
        return new Cenario(obra, empresa, contrato, funcao, integracao, nr35, pgr, contratoSocial);
    }

    private static async Task<Trabalhador> AdicionarPessoaAsync(IAppDbContext db, Cenario c, string nome, string cpf, bool liberada)
    {
        var pessoa = new Trabalhador
        {
            ObraId = c.Obra.Id, FuncaoId = c.Funcao.Id, Nome = nome, Matricula = $"M-{nome}", Cpf = cpf,
            Vinculo = TipoVinculo.Terceirizado, DataAdmissao = DateTime.UtcNow,
            EmpresaId = c.Empresa.Id, ContratoId = c.Contrato.Id,
        };
        db.Trabalhadores.Add(pessoa);
        await db.SaveChangesAsync();
        if (!liberada) return pessoa;
        db.Asos.Add(new Aso
        {
            TrabalhadorId = pessoa.Id, Tipo = TipoExameAso.Admissional,
            DataExame = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11), ResultadoStatus = ResultadoAso.Apto,
        });
        foreach (var curso in new[] { c.Integracao, c.Nr35 })
            db.Treinamentos.Add(new Treinamento
            {
                TrabalhadorId = pessoa.Id, CursoTreinamentoId = curso.Id,
                DataRealizacao = DateTime.UtcNow, DataValidade = DateTime.UtcNow.AddMonths(11), CargaHorariaRealizada = 8,
            });
        db.FotosCadastroFacial.Add(new FotoCadastroFacial { TrabalhadorId = pessoa.Id, CapturadaEm = DateTime.UtcNow });
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = pessoa.Id, TemplateCriptografado = "x", CapturadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return pessoa;
    }

    private static Task EnviarAsync(IAppDbContext db, Cenario c, DocumentoExigidoEmpresa exigido, DateOnly? validade) =>
        new EnviarDocumentoEmpresaCommandHandler(db).Handle(
            new EnviarDocumentoEmpresaCommand(c.Empresa.Id, exigido.Id, validade, $"{exigido.Nome}.pdf", "application/pdf", Pdf), default);

    [Fact]
    public async Task Calcular_EmpresaSemNada_TudoPendenteZeroPorCento()
    {
        var db = CriarDb(nameof(Calcular_EmpresaSemNada_TudoPendenteZeroPorCento));
        var c = await SemearAsync(db);

        var r = (await CalculadoraConformidadeEmpresa.CalcularAsync(db, c.Empresa.Id))!;

        // 2 documentos + 1 requisito de função (Pedreiro · NR-35) sem pessoas.
        Assert.Equal(3, r.Exigidos);
        Assert.Equal(0, r.Atendidos);
        Assert.Equal(0, r.Percentual);
        Assert.Equal(FaixaConformidade.ParcialmenteApta, r.Faixa);
        var funcao = Assert.Single(r.Itens, i => i.Nivel == CalculadoraConformidadeEmpresa.NivelFuncoes);
        Assert.Equal("Nenhuma pessoa cadastrada nesta função ainda.", funcao.Detalhe);
    }

    [Fact]
    public async Task Calcular_TudoEmDia_CemPorCentoApta()
    {
        var db = CriarDb(nameof(Calcular_TudoEmDia_CemPorCentoApta));
        var c = await SemearAsync(db);
        await EnviarAsync(db, c, c.Pgr, Hoje.AddYears(1));
        await EnviarAsync(db, c, c.ContratoSocial, null);
        await AdicionarPessoaAsync(db, c, "Ana", "52998224725", liberada: true);

        var r = (await CalculadoraConformidadeEmpresa.CalcularAsync(db, c.Empresa.Id))!;

        Assert.Equal(4, r.Exigidos);
        Assert.Equal(100, r.Percentual);
        Assert.Equal(FaixaConformidade.Apta, r.Faixa);
        Assert.All(r.Niveis, n => Assert.Equal(100, n.Percentual));
    }

    [Fact]
    public async Task Calcular_PessoaSemCursoNemBiometria_ReabreFuncaoEPessoa()
    {
        var db = CriarDb(nameof(Calcular_PessoaSemCursoNemBiometria_ReabreFuncaoEPessoa));
        var c = await SemearAsync(db);
        await EnviarAsync(db, c, c.Pgr, Hoje.AddYears(1));
        await EnviarAsync(db, c, c.ContratoSocial, null);
        await AdicionarPessoaAsync(db, c, "Ana", "52998224725", liberada: true);
        await AdicionarPessoaAsync(db, c, "Bruno", "11144477735", liberada: false);

        var r = (await CalculadoraConformidadeEmpresa.CalcularAsync(db, c.Empresa.Id))!;

        // 2 docs ok + função (1 de 2 com NR-35) pendente + Ana ok + Bruno pendente = 3 de 5.
        Assert.Equal(5, r.Exigidos);
        Assert.Equal(3, r.Atendidos);
        Assert.Equal(60, r.Percentual);
        var funcao = Assert.Single(r.Itens, i => i.Nivel == CalculadoraConformidadeEmpresa.NivelFuncoes);
        Assert.Equal("1 de 2 pessoa(s) com o curso válido.", funcao.Detalhe);
        var bruno = Assert.Single(r.Itens, i => i.Titulo.StartsWith("Bruno"));
        Assert.False(bruno.Atendido);
        Assert.Contains("Biometria pendente", bruno.Detalhe);
    }

    [Fact]
    public async Task Calcular_DocumentoVencido_NaoAtendeEVenceEmBreveAinda_Atende()
    {
        var db = CriarDb(nameof(Calcular_DocumentoVencido_NaoAtendeEVenceEmBreveAinda_Atende));
        var c = await SemearAsync(db);
        await EnviarAsync(db, c, c.Pgr, Hoje.AddDays(-1));

        var r = (await CalculadoraConformidadeEmpresa.CalcularAsync(db, c.Empresa.Id))!;
        var pgr = Assert.Single(r.Itens, i => i.Titulo == "PGR");
        Assert.Equal(SituacaoItemConformidade.Vencido, pgr.Situacao);
        Assert.False(pgr.Atendido);

        await EnviarAsync(db, c, c.Pgr, Hoje.AddDays(10));
        r = (await CalculadoraConformidadeEmpresa.CalcularAsync(db, c.Empresa.Id))!;
        pgr = Assert.Single(r.Itens, i => i.Titulo == "PGR");
        Assert.Equal(SituacaoItemConformidade.VenceEmBreve, pgr.Situacao);
        Assert.True(pgr.Atendido);
        Assert.Equal(1, await db.DocumentosEmpresaTerceirizada.CountAsync()); // reenvio substitui.
    }

    [Fact]
    public async Task Calcular_DocumentoExigidoDesativado_SaiDoChecklist()
    {
        var db = CriarDb(nameof(Calcular_DocumentoExigidoDesativado_SaiDoChecklist));
        var c = await SemearAsync(db);
        await new SalvarDocumentoExigidoEmpresaCommandHandler(db).Handle(
            new SalvarDocumentoExigidoEmpresaCommand(c.ContratoSocial.Id, "Contrato social", null, false, Ativo: false), default);

        var r = (await CalculadoraConformidadeEmpresa.CalcularAsync(db, c.Empresa.Id))!;

        Assert.DoesNotContain(r.Itens, i => i.Titulo == "Contrato social");
        Assert.Equal(2, r.Exigidos);
    }

    [Theory]
    [InlineData(10, 10, 100, "apta")]
    [InlineData(9, 10, 90, "quase")]
    [InlineData(8, 10, 80, "quase")]
    [InlineData(199, 200, 99, "quase")]
    [InlineData(7, 10, 70, "parcial")]
    public void Faixas_SeguemAEspecificacao(int atendidos, int exigidos, int percentual, string faixa)
    {
        var p = CalculadoraConformidadeEmpresa.Percentual(atendidos, exigidos);
        Assert.Equal(percentual, p);
        Assert.Equal(faixa, CalculadoraConformidadeEmpresa.Faixa(p));
    }

    [Fact]
    public void Faixa_SemItens()
    {
        Assert.Null(CalculadoraConformidadeEmpresa.Percentual(0, 0));
        Assert.Equal(FaixaConformidade.SemItens, CalculadoraConformidadeEmpresa.Faixa(null));
    }

    [Fact]
    public async Task Listar_OrdenaDaMenosAdequadaParaAMais()
    {
        var db = CriarDb(nameof(Listar_OrdenaDaMenosAdequadaParaAMais));
        var c = await SemearAsync(db);
        var outra = new Empresa { RazaoSocial = "Alfa", Cnpj = "99999999000199" };
        db.Empresas.Add(outra);
        await db.SaveChangesAsync();
        await new EnviarDocumentoEmpresaCommandHandler(db).Handle(
            new EnviarDocumentoEmpresaCommand(outra.Id, c.Pgr.Id, Hoje.AddYears(1), "pgr.pdf", "application/pdf", Pdf), default);

        var lista = await new ListarConformidadeEmpresasQueryHandler(db).Handle(new ListarConformidadeEmpresasQuery(), default);

        Assert.Equal(new[] { "XPTO", "Alfa" }, lista.Select(e => e.RazaoSocial));
        Assert.Equal(0, lista[0].Percentual);
        Assert.Equal(50, lista[1].Percentual);
    }

    [Fact]
    public async Task Enviar_DocumentoComValidadeSemData_Recusa()
    {
        var db = CriarDb(nameof(Enviar_DocumentoComValidadeSemData_Recusa));
        var c = await SemearAsync(db);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => EnviarAsync(db, c, c.Pgr, null));
        Assert.Equal("Informe a data de validade do PGR.", erro.Message);
    }

    [Fact]
    public void Validador_AceitaPdfJpgPngERecusaOutros()
    {
        var v = new EnviarDocumentoEmpresaCommandValidator();
        EnviarDocumentoEmpresaCommand Cmd(byte[] b) => new(Guid.NewGuid(), Guid.NewGuid(), null, "a", "x", b);
        Assert.True(v.Validate(Cmd(Pdf)).IsValid);
        Assert.True(v.Validate(Cmd(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })).IsValid);
        Assert.True(v.Validate(Cmd(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A })).IsValid);
        Assert.False(v.Validate(Cmd(Encoding.ASCII.GetBytes("GIF89a"))).IsValid);
    }

    [Fact]
    public async Task ArquivoERemocao_FuncionamPelaEmpresaCerta()
    {
        var db = CriarDb(nameof(ArquivoERemocao_FuncionamPelaEmpresaCerta));
        var c = await SemearAsync(db);
        await EnviarAsync(db, c, c.Pgr, Hoje.AddYears(1));
        var doc = await db.DocumentosEmpresaTerceirizada.SingleAsync();

        var arquivo = await new ObterArquivoDocumentoEmpresaQueryHandler(db).Handle(new ObterArquivoDocumentoEmpresaQuery(c.Empresa.Id, doc.Id), default);
        Assert.Equal(Pdf, arquivo!.Conteudo);
        Assert.Equal("application/pdf", arquivo.ContentType);
        Assert.Null(await new ObterArquivoDocumentoEmpresaQueryHandler(db).Handle(new ObterArquivoDocumentoEmpresaQuery(Guid.NewGuid(), doc.Id), default));

        await new RemoverDocumentoEmpresaCommandHandler(db).Handle(new RemoverDocumentoEmpresaCommand(c.Empresa.Id, doc.Id), default);
        Assert.Equal(0, await db.DocumentosEmpresaTerceirizada.CountAsync());
    }

    [Fact]
    public async Task SalvarDocumentoExigido_NomeRepetido_Recusa()
    {
        var db = CriarDb(nameof(SalvarDocumentoExigido_NomeRepetido_Recusa));
        await SemearAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new SalvarDocumentoExigidoEmpresaCommandHandler(db).Handle(
            new SalvarDocumentoExigidoEmpresaCommand(null, "PGR", null, true, true), default));
        var novoId = await new SalvarDocumentoExigidoEmpresaCommandHandler(db).Handle(
            new SalvarDocumentoExigidoEmpresaCommand(null, "  Alvará  ", null, true, true), default);
        var novo = await db.DocumentosExigidosEmpresa.SingleAsync(d => d.Id == novoId);
        Assert.Equal("Alvará", novo.Nome);
        Assert.Equal(3, novo.Ordem);
    }

    [Fact]
    public async Task AlertaProvider_UmItemPorObraDosContratosVigentes()
    {
        var db = CriarDb(nameof(AlertaProvider_UmItemPorObraDosContratosVigentes));
        var c = await SemearAsync(db);
        var obra2 = new Obra { Codigo = "O2", Nome = "Obra 2" };
        db.Obras.Add(obra2);
        await db.SaveChangesAsync();
        db.Contratos.Add(new Contrato
        {
            EmpresaId = c.Empresa.Id, ObraId = obra2.Id, NumeroContrato = "CT-002",
            DataInicioVigencia = Hoje, DataFimVigencia = Hoje.AddMonths(6),
        });
        await db.SaveChangesAsync();
        await EnviarAsync(db, c, c.Pgr, Hoje.AddDays(5));
        await EnviarAsync(db, c, c.ContratoSocial, null); // sem validade: não gera alerta.

        var itens = await new DocumentoEmpresaTerceirizadaAlertaProvider(db).ObterItensAsync();

        Assert.Equal(2, itens.Count);
        Assert.Equal(new[] { c.Obra.Id, obra2.Id }.OrderBy(x => x), itens.Select(i => i.ObraId!.Value).OrderBy(x => x));
        Assert.Equal(2, itens.Select(i => i.EntidadeOrigemId).Distinct().Count());
        Assert.All(itens, i => Assert.Equal(TipoAlerta.DocumentoTerceirizadoVencendo, i.TipoAlertaVencendo));
        Assert.All(itens, i => Assert.StartsWith("PGR da XPTO", i.Titulo));
    }
}
