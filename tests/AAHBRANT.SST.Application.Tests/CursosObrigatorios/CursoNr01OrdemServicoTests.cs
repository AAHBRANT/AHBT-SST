using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.CursosObrigatorios;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.CursosObrigatorios;

// NR-01 = a própria OS assinada (11/11/2026): o curso "NR-01 - ORDEM DE SERVIÇO" exigido pelo PGR não bloqueia a emissão da
// OS e, na liberação, conta como cumprido quando o trabalhador assinou a OS vigente da obra.
public class CursoNr01OrdemServicoTests
{
    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    [Theory]
    [InlineData("NR-01 - ORDEM DE SERVIÇO", null, true)]
    [InlineData("Ordem de Servico", null, true)]
    [InlineData("Curso de ordem de serviço", "", true)]
    [InlineData("Integração", "NR-01", true)]
    [InlineData("Integração", "NR 01", true)]
    [InlineData("Integração", "NR-1", true)]
    [InlineData("Integração", "NR1", true)]
    [InlineData("Integração", "nr-01", true)]
    [InlineData("NR 01 Disposições gerais", null, true)]
    [InlineData("NR-1 Disposições", null, true)]
    [InlineData("NR-10 Segurança em Eletricidade", "NR-10", false)]
    [InlineData("NR-11 Transporte de Materiais", "NR-11", false)]
    [InlineData("NR-12 Máquinas e Equipamentos", "NR-12", false)]
    [InlineData("NR-18 Construção", "NR-18", false)]
    [InlineData("NR-35 Trabalho em Altura", "NR 35", false)]
    [InlineData("NR-100", "NR-100", false)]
    [InlineData("NR-06 EPI", null, false)]
    [InlineData(null, null, false)]
    public void Eh_ReconheceVariacoesENaoPegaOutrasNrs(string? nome, string? norma, bool esperado)
        => Assert.Equal(esperado, CursoNr01OrdemServico.Eh(nome, norma));

    private sealed record Cenario(SstDbContext Db, Obra Obra, Funcao Funcao, Trabalhador Pessoa, CursoTreinamento Nr01, CursoTreinamento Nr10, CursoTreinamento Nr12);

    private static async Task<Cenario> PrepararAsync(bool aprovarNoPgr = true)
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Parque Roger", Codigo = "PR" };
        var funcao = new Funcao { Nome = "Eletricista" };
        var nr01 = new CursoTreinamento { Nome = "NR-01 - ORDEM DE SERVIÇO", NormaReferencia = "NR-01", ValidadeEmMeses = 12 };
        var nr10 = new CursoTreinamento { Nome = "NR-10 Segurança em Eletricidade", NormaReferencia = "NR-10", ValidadeEmMeses = 24 };
        var nr12 = new CursoTreinamento { Nome = "NR-12 Máquinas e Equipamentos", NormaReferencia = "NR-12", ValidadeEmMeses = 24 };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CursosTreinamento.AddRange(nr01, nr10, nr12);
        await db.SaveChangesAsync();
        var pessoa = new Trabalhador
        {
            Nome = "Maria", Cpf = Guid.NewGuid().ToString("N")[..11], ObraId = obra.Id, FuncaoId = funcao.Id, DataAdmissao = Hoje.AddYears(-1),
        };
        db.Trabalhadores.Add(pessoa);
        if (aprovarNoPgr)
            foreach (var curso in new[] { nr01, nr10, nr12 })
                db.CursosObrigatoriosFuncaoObra.Add(new CursoObrigatorioFuncaoObra
                {
                    ObraId = obra.Id, FuncaoId = funcao.Id, CursoTreinamentoId = curso.Id, Status = StatusCursoObrigatorio.Aprovado,
                });
        db.Asos.Add(new Aso
        {
            TrabalhadorId = pessoa.Id, Tipo = TipoExameAso.Periodico, ResultadoStatus = ResultadoAso.Apto,
            DataExame = Hoje.AddDays(-10), DataValidade = Hoje.AddDays(355),
        });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, funcao, pessoa, nr01, nr10, nr12);
    }

    private static Treinamento Feito(Cenario c, CursoTreinamento curso) => new()
    {
        TrabalhadorId = c.Pessoa.Id, CursoTreinamentoId = curso.Id, DataRealizacao = Hoje.AddDays(-30), DataValidade = Hoje.AddDays(300),
    };

    private static async Task AssinarOsAsync(Cenario c, int revisao = 0, bool funcionarioAssina = true)
    {
        var os = new OrdemServico
        {
            TrabalhadorId = c.Pessoa.Id, ObraId = c.Obra.Id, Numero = $"OS-{revisao}", Revisao = revisao, EmitidaEm = DateTime.UtcNow,
            ConteudoJson = "{}",
        };
        c.Db.OrdensServico.Add(os);
        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = OrdemServico.EntidadeTipoAssinatura, EntidadeId = os.Id, ObraId = c.Obra.Id,
            Status = StatusDocumentoAssinatura.EmAndamento,
        };
        c.Db.DocumentosAssinatura.Add(documento);
        if (funcionarioAssina)
            c.Db.DocumentoSignatarios.Add(new DocumentoSignatario
            {
                DocumentoAssinaturaId = documento.Id, TrabalhadorId = c.Pessoa.Id,
                MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow,
            });
        await c.Db.SaveChangesAsync();
    }

    private static Task<MinutaOrdemServicoDto?> Minuta(Cenario c)
        => new ObterMinutaOrdemServicoQueryHandler(c.Db).Handle(new ObterMinutaOrdemServicoQuery(c.Pessoa.Id), default);

    private static Task<LiberacaoTrabalhoDto> Liberacao(Cenario c)
        => new ObterLiberacaoParaTrabalhoQueryHandler(c.Db).Handle(new ObterLiberacaoParaTrabalhoQuery(null), default);

    [Fact]
    public async Task Minuta_CursoNr01ExigidoNaoEntraNemGeraPendencia_MasNr10ENr12Continuam()
    {
        var c = await PrepararAsync();

        var minuta = (await Minuta(c))!;

        Assert.DoesNotContain(minuta.Treinamentos, t => t.Curso.Contains("NR-01"));
        Assert.DoesNotContain(minuta.Pendencias, p => p.Tipo == "TreinamentoPendente" && p.Titulo.Contains("NR-01"));
        Assert.Contains(minuta.Treinamentos, t => t.Curso.StartsWith("NR-10") && t.Situacao == "NaoRealizado");
        Assert.Contains(minuta.Treinamentos, t => t.Curso.StartsWith("NR-12") && t.Situacao == "NaoRealizado");
        Assert.Contains(minuta.Pendencias, p => p.Tipo == "TreinamentoPendente" && p.Titulo.Contains("NR-10"));
        Assert.Contains(minuta.Pendencias, p => p.Tipo == "TreinamentoPendente" && p.Titulo.Contains("NR-12"));
    }

    [Fact]
    public async Task Liberacao_OsVigenteAssinadaPeloFuncionario_CumpreANr01()
    {
        var c = await PrepararAsync();
        c.Db.Treinamentos.AddRange(Feito(c, c.Nr10), Feito(c, c.Nr12));
        await c.Db.SaveChangesAsync();
        await AssinarOsAsync(c);

        var r = await Liberacao(c);

        Assert.Equal(1, r.Liberados);
        Assert.Empty(r.PessoasBloqueadas);
    }

    [Fact]
    public async Task Liberacao_SemOsAssinada_Nr01FicaPendenteComTextoNovo_ENaoDizNuncaFeito()
    {
        var c = await PrepararAsync();
        c.Db.Treinamentos.AddRange(Feito(c, c.Nr10), Feito(c, c.Nr12));
        await c.Db.SaveChangesAsync();

        var r = await Liberacao(c);

        var pessoa = Assert.Single(r.PessoasBloqueadas);
        var motivo = Assert.Single(pessoa.Motivos);
        Assert.Equal("Treinamento", motivo.Tipo);
        Assert.Equal("Ordem de Serviço (NR-01) não assinada", motivo.Texto);
    }

    [Fact]
    public async Task Liberacao_OsEmitidaMasSoComAssinaturaDoTecnico_NaoCumpreANr01()
    {
        var c = await PrepararAsync();
        c.Db.Treinamentos.AddRange(Feito(c, c.Nr10), Feito(c, c.Nr12));
        await c.Db.SaveChangesAsync();
        await AssinarOsAsync(c, funcionarioAssina: false);

        var r = await Liberacao(c);

        Assert.Equal("Ordem de Serviço (NR-01) não assinada", Assert.Single(Assert.Single(r.PessoasBloqueadas).Motivos).Texto);
    }

    [Fact]
    public async Task Liberacao_SoARevisaoVigenteConta_RevisaoNovaSemAssinaturaVoltaAPendente()
    {
        var c = await PrepararAsync();
        c.Db.Treinamentos.AddRange(Feito(c, c.Nr10), Feito(c, c.Nr12));
        await c.Db.SaveChangesAsync();
        await AssinarOsAsync(c, revisao: 0);
        await AssinarOsAsync(c, revisao: 1, funcionarioAssina: false);

        var r = await Liberacao(c);

        Assert.Equal("Ordem de Serviço (NR-01) não assinada", Assert.Single(Assert.Single(r.PessoasBloqueadas).Motivos).Texto);
    }

    [Fact]
    public async Task Liberacao_OutrosCursosContinuamExigidosMesmoComOsAssinada()
    {
        var c = await PrepararAsync();
        await AssinarOsAsync(c);

        var r = await Liberacao(c);

        var pessoa = Assert.Single(r.PessoasBloqueadas);
        Assert.Contains(pessoa.Motivos, m => m.Texto.StartsWith("NR-10"));
        Assert.Contains(pessoa.Motivos, m => m.Texto.StartsWith("NR-12"));
        Assert.DoesNotContain(pessoa.Motivos, m => m.Texto.Contains("NR-01"));
    }

    // ---- CPF completo na OS (11/11/2026) ----

    private sealed class PdfFalso : IOrdemServicoPdfService
    {
        public OrdemServicoPdfModelo? Modelo { get; private set; }
        public byte[] Gerar(OrdemServicoPdfModelo modelo) { Modelo = modelo; return new byte[] { 1 }; }
    }

    private sealed class RastreabilidadeFalsa : IRegistradorRastreabilidadeService
    {
        public Task<RastreabilidadeDocumentoResultado> GarantirAsync(string entidadeTipo, Guid entidadeId, CancellationToken ct)
            => Task.FromResult(new RastreabilidadeDocumentoResultado(Guid.NewGuid(), "hash", "https://exemplo/validar", Array.Empty<byte>(), false));
        public Task RegistrarArquivoAsync(Guid documentoId, byte[] pdf, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Minuta_TrazOCpfCompletoFormatado()
    {
        var c = await PrepararAsync();
        c.Pessoa.Cpf = "52998224725";
        await c.Db.SaveChangesAsync();

        var minuta = (await Minuta(c))!;

        Assert.Equal("529.982.247-25", minuta.Trabalhador.Cpf);
    }

    [Fact]
    public async Task PdfDaOs_UsaOCpfAtualCompleto_MesmoQuandoOConteudoCongeladoTemSoOMascarado()
    {
        var c = await PrepararAsync();
        c.Pessoa.Cpf = "52998224725";
        var congelado = new MinutaOrdemServicoDto
        {
            Trabalhador = new TrabalhadorOsDto { Nome = "Maria", CpfMascarado = "***.***.***-25", Funcao = "Eletricista" },
        };
        var os = new OrdemServico
        {
            TrabalhadorId = c.Pessoa.Id, ObraId = c.Obra.Id, Numero = "OS-9", Revisao = 0, EmitidaEm = DateTime.UtcNow,
            ConteudoJson = System.Text.Json.JsonSerializer.Serialize(congelado),
        };
        c.Db.OrdensServico.Add(os);
        await c.Db.SaveChangesAsync();
        var pdf = new PdfFalso();

        await new ExportarOrdemServicoPdfQueryHandler(c.Db, pdf, new RastreabilidadeFalsa())
            .Handle(new ExportarOrdemServicoPdfQuery(os.Id), default);

        Assert.Equal("529.982.247-25", pdf.Modelo!.Conteudo.Trabalhador.Cpf);
        // O conteúdo congelado no banco não é alterado.
        Assert.DoesNotContain("529.982.247-25", (await c.Db.OrdensServico.FindAsync(os.Id))!.ConteudoJson);
    }
}
