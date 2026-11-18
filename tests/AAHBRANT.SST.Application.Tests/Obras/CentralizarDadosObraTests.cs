using System.Text.Json;
using AAHBRANT.SST.Application.EscopoPcmso;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Application.Obras.Commands;
using AAHBRANT.SST.Application.Obras.Queries;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia.Seed;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Obras;

public class CentralizarDadosObraTests
{
    private static readonly byte[] PngMinimo =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    [Fact]
    public async Task CriarEAtualizarObra_SalvaELeCnpjCepCnaeEGrauRisco()
    {
        var db = DbContextFactory.Criar();

        var obraId = await new CriarObraCommandHandler(db).Handle(new CriarObraCommand(
            Codigo: "OB-100",
            Nome: "Obra Rodovia Norte",
            Cliente: null,
            Status: StatusObra.Planejada,
            DataInicio: new DateTime(2026, 1, 10),
            DataPrevisaoTermino: new DateTime(2027, 6, 30),
            Endereco: "BR-101, km 45",
            Cidade: "João Pessoa",
            Uf: "PB",
            Cnpj: "11.222.333/0001-44",
            LogoConteudo: PngMinimo,
            LogoContentType: "image/png",
            Cep: "58000-000",
            Cnae: "4211-1/01 — Construção de rodovias e ferrovias",
            GrauRisco: 3), CancellationToken.None);

        await new AtualizarObraCommandHandler(db).Handle(new AtualizarObraCommand(
            Id: obraId,
            Codigo: "OB-100",
            Nome: "Obra Rodovia Norte",
            Cliente: null,
            Status: StatusObra.Planejada,
            DataInicio: new DateTime(2026, 1, 10),
            DataPrevisaoTermino: new DateTime(2027, 6, 30),
            DataTerminoReal: null,
            Endereco: "BR-101, km 50",
            Cidade: "João Pessoa",
            Uf: "PB",
            Cnpj: "99.888.777/0001-66",
            Cep: "58010-100",
            Cnae: "4212-0/00 — Construção de obras de arte especiais",
            GrauRisco: 4), CancellationToken.None);

        var dto = await new ObterObraPorIdQueryHandler(db).Handle(new ObterObraPorIdQuery(obraId), CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal("99.888.777/0001-66", dto!.Cnpj);
        Assert.Equal("58010-100", dto.Cep);
        Assert.Equal("4212-0/00 — Construção de obras de arte especiais", dto.Cnae);
        Assert.Equal(4, dto.GrauRisco);

        var validador = new AtualizarObraCommandValidator();
        Assert.False(validador.Validate(new AtualizarObraCommand(
            obraId, "OB-100", "Obra", null, StatusObra.Planejada, null, null, null, null, null, null, GrauRisco: 5)).IsValid);
    }

    [Fact]
    public async Task Seeder_MigraCepCnaeEGrauRiscoDoPgrEDaCipaSemSobrescreverObraPreenchida()
    {
        var db = DbContextFactory.Criar();
        var obraDoPgr = new Obra { Codigo = "OB-PGR", Nome = "Obra PGR" };
        var obraDaCipa = new Obra { Codigo = "OB-CIPA", Nome = "Obra CIPA", Cnae = "9999-9/99 — Já preenchido" };
        db.Obras.AddRange(obraDoPgr, obraDaCipa);

        var estrutura = new EstruturaGeracaoPgr
        {
            Complementares = new DadosComplementaresPgr
            {
                Cep = "30140-071",
                Cnae = "4211-1/01 — Construção de rodovias",
                GrauRisco = 3,
            },
        };
        db.GeracoesPgrEap.Add(new GeracaoPgrEap
        {
            ObraId = obraDoPgr.Id,
            Nome = "PGR 2026",
            DataElaboracao = new DateTime(2026, 5, 1),
            NomeArquivoEap = "eap.xlsx",
            Status = StatusGeracaoPgrEap.Aprovado,
            AprovadoEmUtc = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc),
            EstruturaJson = JsonSerializer.Serialize(estrutura, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        });
        db.DimensionamentosCipa.Add(new DimensionamentoCipa
        {
            ObraId = obraDaCipa.Id,
            Cnae = "4120-4/00",
            GrauRisco = 3,
            NumeroFuncionarios = 50,
            NumeroTitulares = 2,
            NumeroSuplentes = 2,
            DataCalculo = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await ObraDadosEnquadramentoSeeder.PreencherObrasAsync(db, CancellationToken.None);

        var pgrAtualizada = await db.Obras.SingleAsync(o => o.Id == obraDoPgr.Id);
        Assert.Equal("30140-071", pgrAtualizada.Cep);
        Assert.Equal("4211-1/01 — Construção de rodovias", pgrAtualizada.Cnae);
        Assert.Equal(3, pgrAtualizada.GrauRisco);

        var cipaAtualizada = await db.Obras.SingleAsync(o => o.Id == obraDaCipa.Id);
        Assert.Equal("9999-9/99 — Já preenchido", cipaAtualizada.Cnae);
        Assert.Equal(3, cipaAtualizada.GrauRisco);
    }

    [Fact]
    public async Task PgrOsEEscopoPcmso_PuxamDadosDoCadastroDaObraESemExigirCardDuplicadoNoPgr()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra
        {
            Codigo = "OB-01",
            Nome = "Consórcio Ponte Sul",
            Cnpj = "12.345.678/0001-90",
            Endereco = "Av. Beira Rio, 500",
            Cidade = "Cabedelo",
            Uf = "PB",
            Cep = "58100-000",
            Cnae = "4212-0/00 — Construção de obras de arte especiais",
            GrauRisco = 4,
        };
        var funcao = new Funcao { Nome = "Armador", CboCodigo = "715210" };
        var trabalhador = new Trabalhador
        {
            Nome = "João Silva",
            Cpf = "12345678909",
            Obra = obra,
            Funcao = funcao,
            DataAdmissao = new DateTime(2026, 2, 1),
        };
        var pgrVigente = new Pgr
        {
            Obra = obra,
            Nome = "PGR Ponte Sul",
            Status = StatusPgr.Vigente,
            DataElaboracao = new DateTime(2026, 3, 1),
        };
        db.AddRange(obra, funcao, trabalhador, pgrVigente);
        await db.SaveChangesAsync();

        // PGR sem DadosComplementaresPgr.Cnae/GrauRisco/ResponsavelImplantacaoNome: usa Obra + QuemAssina.
        var estrutura = new EstruturaGeracaoPgr
        {
            DataTermino = new DateTime(2027, 3, 1),
            Complementares = new DadosComplementaresPgr
            {
                Tecnico = new AssinanteGeracaoPgr { UsuarioId = Guid.NewGuid(), Nome = "Téc. Maria", Registro = "CFT 111", AMao = false },
                Engenheiro = new AssinanteGeracaoPgr { UsuarioId = Guid.NewGuid(), Nome = "Eng. Pedro", Registro = "CREA-PB 222", AMao = false },
            },
            Ghes = new()
            {
                new GheGerado
                {
                    Numero = 1,
                    Nome = "Estrutura",
                    Funcoes = new() { new FuncaoGerada { FuncaoId = funcao.Id, Nome = "Armador", Descricao = "Monta armaduras" } },
                    Atividades = new()
                    {
                        new AtividadeGerada
                        {
                            CodigoEap = "1.1",
                            Nome = "Armação",
                            RiscosGerados = true,
                            Riscos = new()
                            {
                                new RiscoGerado
                                {
                                    Tipo = "Acidente",
                                    Perigo = "Corte",
                                    Probabilidade = 2,
                                    Severidade = 2,
                                    Revisado = true,
                                    Medidas = new() { new MedidaGerada { Texto = "Luva", Categoria = CategoriasMedida.Epi, Decisao = "Aceita" } },
                                },
                            },
                        },
                    },
                },
            },
        }.Normalizar();

        var pendencias = PendenciasGeracaoPgr.Calcular(
            estrutura, 5, 5, null, new DadosObraPgr(obra.Cnae, obra.GrauRisco, obra.Cep, obra.Cnpj, obra.Endereco));
        Assert.Empty(pendencias);
        Assert.Empty(PendenciasGeracaoPgr.FaltandoNaObra(new DadosObraPgr(obra.Cnae, obra.GrauRisco, obra.Cep, obra.Cnpj, obra.Endereco)));

        // Ordem de Serviço puxa CEP, CNAE e Grau de Risco da Obra.
        var minutaOs = await new ObterMinutaOrdemServicoQueryHandler(db).Handle(new ObterMinutaOrdemServicoQuery(trabalhador.Id), CancellationToken.None);
        Assert.NotNull(minutaOs);
        Assert.Equal("58100-000", minutaOs!.Obra.Cep);
        Assert.Equal("4212-0/00 — Construção de obras de arte especiais", minutaOs.Obra.Cnae);
        Assert.Equal(4, minutaOs.Obra.GrauRisco);

        // Escopo do PCMSO puxa CEP, CNAE e Grau de Risco da Obra.
        var escopo = await new ObterEscopoPcmsoQueryHandler(db).Handle(new ObterEscopoPcmsoQuery(obra.Id), CancellationToken.None);
        Assert.NotNull(escopo);
        Assert.Equal("58100-000", escopo!.Cep);
        Assert.Equal("4212-0/00 — Construção de obras de arte especiais", escopo.Cnae);
        Assert.Equal(4, escopo.GrauRisco);
        Assert.DoesNotContain(escopo.Pendencias, p => p.Contains("CNAE") || p.Contains("Grau de risco"));
    }
}
