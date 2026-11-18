using AAHBRANT.SST.Application.Inspecoes.Adesivo;
using AAHBRANT.SST.Application.TagsIdentificacao;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes.Adesivo;

// Adesivo de inspeção (LIBERADO / INSPECIONADO / NÃO LIBERADO) e leitura pública do QR.
public class AdesivoInspecaoTests
{
    static AdesivoInspecaoTests() => ChavesCpfDeTeste.Configurar();

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    // ---------- Regra pura ----------

    [Theory]
    [InlineData(StatusInspecao.EmAndamento)]
    [InlineData(StatusInspecao.AguardandoAssinatura)]
    public void Regra_SoInspecaoConcluidaGeraSelo(StatusInspecao status)
    {
        Assert.Null(ResultadoAdesivoInspecao.Calcular(status, new StatusItemChecklist?[] { StatusItemChecklist.Conforme }, ModeloAdesivoInspecao.Liberado));
    }

    [Theory]
    [InlineData(ModeloAdesivoInspecao.Liberado)]
    [InlineData(ModeloAdesivoInspecao.Inspecionado)]
    public void Regra_QualquerNaoConformeBloqueia_SejaQualForOModelo(ModeloAdesivoInspecao modelo)
    {
        var respostas = new StatusItemChecklist?[] { StatusItemChecklist.Conforme, StatusItemChecklist.NaoConforme, StatusItemChecklist.NaoAplicavel };
        Assert.Equal(SituacaoAdesivo.NaoLiberado, ResultadoAdesivoInspecao.Calcular(StatusInspecao.Concluida, respostas, modelo));
    }

    [Fact]
    public void Regra_SemNaoConforme_SeguePeloModeloDoObjeto()
    {
        var respostas = new StatusItemChecklist?[] { StatusItemChecklist.Conforme, StatusItemChecklist.NaoAplicavel, null };
        Assert.Equal(SituacaoAdesivo.Liberado, ResultadoAdesivoInspecao.Calcular(StatusInspecao.Concluida, respostas, ModeloAdesivoInspecao.Liberado));
        Assert.Equal(SituacaoAdesivo.Inspecionado, ResultadoAdesivoInspecao.Calcular(StatusInspecao.Concluida, respostas, ModeloAdesivoInspecao.Inspecionado));
    }

    [Fact]
    public void Regra_TextosDoJson()
    {
        Assert.Equal("Liberado", ResultadoAdesivoInspecao.Texto(SituacaoAdesivo.Liberado));
        Assert.Equal("Inspecionado", ResultadoAdesivoInspecao.Texto(SituacaoAdesivo.Inspecionado));
        Assert.Equal("NaoLiberado", ResultadoAdesivoInspecao.Texto(SituacaoAdesivo.NaoLiberado));
    }

    [Fact]
    public void MapeamentoDosTemasPadrao_CobreOs14Temas()
    {
        var mapa = AAHBRANT.SST.Infrastructure.Persistencia.Seed.TemasInspecaoPadraoSeeder.ModeloAdesivoPorTema;
        Assert.Equal(14, mapa.Count);
        Assert.Equal(6, mapa.Values.Count(m => m == ModeloAdesivoInspecao.Inspecionado));
        Assert.Equal(ModeloAdesivoInspecao.Inspecionado, mapa["Extintores"]);
        Assert.Equal(ModeloAdesivoInspecao.Liberado, mapa["Andaimes"]);
    }

    // ---------- Cenário com banco em memória ----------

    private sealed class Cenario
    {
        public required DbContextOptions<SstDbContext> Opcoes { get; init; }
        public required SstDbContext Global { get; init; }
        public required SstDbContext RestritoA { get; init; }
        public required SstDbContext Anonimo { get; init; }
        public required Obra Obra { get; init; }
        public required Usuario Inspetor { get; init; }
        public required Veiculo Veiculo { get; init; }
        public required ItemInspecionavel Extintor { get; init; }
        public required Alojamento Alojamento { get; init; }
    }

    private static SstDbContext Contexto(DbContextOptions<SstDbContext> opcoes, bool global, params Guid[] obras)
    {
        var usuario = new CurrentUserService();
        if (!global) usuario.DefinirEscopo(false, obras);
        return new SstDbContext(opcoes, usuario);
    }

    private static async Task<Cenario> CriarAsync(byte[]? logo = null)
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var global = Contexto(opcoes, true);

        var obra = new Obra { Id = ObraA, Codigo = "OA", Nome = "Obra Beira Rio", LogoConteudo = logo, LogoContentType = logo is null ? null : "image/png" };
        var outraObra = new Obra { Id = ObraB, Codigo = "OB", Nome = "Obra B" };
        var funcao = new Funcao { Nome = "Técnico de Segurança do Trabalho" };
        var trabalhador = new Trabalhador { ObraId = ObraA, Funcao = funcao, Nome = "Carlos Eduardo Menezes", Cpf = "00000000001" };
        var inspetor = new Usuario { Nome = "Carlos Eduardo Menezes", Email = "carlos@example.com", TrabalhadorId = trabalhador.Id };
        var veiculo = new Veiculo { ObraId = ObraA, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "RTK-4B27", MarcaModelo = "Volvo FH", PeriodicidadeInspecaoDias = 30 };
        var tema = new TemaInspecao { Nome = "Extintores", ModeloAdesivo = ModeloAdesivoInspecao.Inspecionado };
        var extintor = new ItemInspecionavel { TemaInspecaoId = tema.Id, ObraId = ObraA, Identificacao = "EXT-PQS-0142", Descricao = "Pó químico 6 kg", PeriodicidadeInspecaoDias = 30 };
        var alojamento = new Alojamento { ObraId = ObraA, Nome = "Alojamento 03 - Bloco B", Endereco = "Rua A, 10" };

        global.Obras.AddRange(obra, outraObra);
        global.Funcoes.Add(funcao);
        global.Trabalhadores.Add(trabalhador);
        global.Usuarios.Add(inspetor);
        global.Veiculos.Add(veiculo);
        global.TemasInspecao.Add(tema);
        global.ItensInspecionaveis.Add(extintor);
        global.Alojamentos.Add(alojamento);
        global.ConfiguracoesAlojamento.Add(new ConfiguracaoAlojamento { DiasParaInspecaoAtrasada = 45 });
        await global.SaveChangesAsync();

        return new Cenario
        {
            Opcoes = opcoes,
            Global = global,
            RestritoA = Contexto(opcoes, false, ObraA),
            // Requisição anônima: sem usuário, o filtro global de obra nega tudo (Obra, Usuário, Veículo, Inspeção...).
            Anonimo = Contexto(opcoes, false),
            Obra = obra,
            Inspetor = inspetor,
            Veiculo = veiculo,
            Extintor = extintor,
            Alojamento = alojamento,
        };
    }

    private static async Task<Inspecao> InspecaoAsync(Cenario c, Action<Inspecao> objeto, StatusInspecao status, DateTime data,
        string protocolo, bool comNaoConforme = false, Guid? obraId = null)
    {
        var inspecao = new Inspecao
        {
            ObraId = obraId ?? ObraA,
            ChecklistModeloId = Guid.NewGuid(),
            Data = data,
            Status = status,
            NumeroDocumento = protocolo,
            ResponsavelUsuarioId = c.Inspetor.Id,
            CreatedAtUtc = data,
        };
        objeto(inspecao);
        c.Global.Inspecoes.Add(inspecao);
        c.Global.InspecaoItemRespostas.Add(new InspecaoItemResposta { InspecaoId = inspecao.Id, ChecklistModeloItemId = Guid.NewGuid(), StatusItem = StatusItemChecklist.Conforme });
        if (comNaoConforme)
            c.Global.InspecaoItemRespostas.Add(new InspecaoItemResposta { InspecaoId = inspecao.Id, ChecklistModeloItemId = Guid.NewGuid(), StatusItem = StatusItemChecklist.NaoConforme, Observacao = "texto sigiloso da NC" });
        await c.Global.SaveChangesAsync();
        return inspecao;
    }

    private sealed class PdfFalso : IAdesivoInspecaoPdfService
    {
        public AdesivoInspecaoPdfModelo? Ultimo { get; private set; }
        public byte[] Gerar(AdesivoInspecaoPdfModelo modelo)
        {
            Ultimo = modelo;
            return new byte[] { 0x25, 0x50, 0x44, 0x46 };
        }
    }

    private sealed class QrFalso : IQrCodePerfilPublicoService
    {
        public QrCodePerfilPublicoResultado Gerar(string uid) => new(Array.Empty<byte>(), MontarUrl(uid));
        public string MontarUrl(string uid) => $"https://app.exemplo/#/p/{uid}";
    }

    private static GerarAdesivoInspecaoCommandHandler Handler(SstDbContext db, PdfFalso pdf)
        => new(db, new QrFalso(), pdf);

    private static readonly DateTime Hoje = new(2026, 11, 17);

    // ---------- GET / POST: elegibilidade (409) ----------

    [Fact]
    public async Task Gerar_InspecaoNaoConcluida_DevolveMotivo()
    {
        var c = await CriarAsync();
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.AguardandoAssinatura, Hoje, "INSP-1");

        var resultado = await Handler(c.Global, new PdfFalso()).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.A4, 1), default);

        Assert.Null(resultado.Pdf);
        Assert.False(resultado.NaoEncontrada);
        Assert.Equal(ElegibilidadeAdesivoInspecao.MotivoNaoConcluida, resultado.Motivo);

        var info = await new ObterAdesivoInspecaoQueryHandler(c.Global).Handle(new ObterAdesivoInspecaoQuery(insp.Id), default);
        Assert.NotNull(info);
        Assert.False(info!.Disponivel);
        Assert.Equal(ElegibilidadeAdesivoInspecao.MotivoNaoConcluida, info.MotivoIndisponivel);
        Assert.Null(info.Situacao);
    }

    [Fact]
    public async Task Gerar_SemObjetoFisico_GeraAdesivoDaPropriaInspecao()
    {
        var c = await CriarAsync();
        var insp = await InspecaoAsync(c, _ => { }, StatusInspecao.Concluida, Hoje, "INSP-2");
        var pdf = new PdfFalso();

        var resultado = await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.Etiqueta, null), default);

        Assert.NotNull(resultado.Pdf);
        Assert.Null(resultado.Motivo);
        Assert.Equal(SituacaoAdesivo.Inspecionado, pdf.Ultimo!.Situacao);
        Assert.Equal("Inspeção", pdf.Ultimo.Tema);
        Assert.Null(pdf.Ultimo.ProximaInspecaoEm);
        var tag = await c.Global.TagsIdentificacao.SingleAsync(t => t.EntidadeVinculadaId == insp.Id);
        Assert.Equal(TipoEntidadeVinculada.Inspecao, tag.EntidadeVinculadaTipo);
        Assert.StartsWith("INS-", tag.Uid);

        var info = await new ObterAdesivoInspecaoQueryHandler(c.Global).Handle(new ObterAdesivoInspecaoQuery(insp.Id), default);
        Assert.True(info!.Disponivel);
        Assert.Equal("Inspecionado", info.Situacao);

        // A leitura pública do QR mostra a situação da própria inspeção.
        var publico = await new ResolverEquipamentoPublicoQueryHandler(c.Global).Handle(new ResolverEquipamentoPublicoQuery(tag.Uid), default);
        Assert.NotNull(publico);
        Assert.Equal("Inspecionado", publico!.Situacao);
        Assert.Equal("INSP-2", publico.Protocolo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-3)]
    public async Task Gerar_A4_PosicaoInvalida_DevolveMotivo(int posicao)
    {
        var c = await CriarAsync();
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-3");
        var pdf = new PdfFalso();

        var resultado = await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.A4, posicao), default);

        Assert.Null(resultado.Pdf);
        Assert.Equal(GerarAdesivoInspecaoCommandHandler.MotivoPosicaoInvalida, resultado.Motivo);
        Assert.Null(pdf.Ultimo);
    }

    [Fact]
    public async Task Gerar_A4_SemPosicao_UsaPosicao1_EEtiquetaIgnoraPosicao()
    {
        var c = await CriarAsync();
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-4");
        var pdf = new PdfFalso();

        var a4 = await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.A4, null), default);
        Assert.NotNull(a4.Pdf);
        Assert.Equal(1, pdf.Ultimo!.Posicao);

        var etiqueta = await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.Etiqueta, 99), default);
        Assert.NotNull(etiqueta.Pdf);
        Assert.Equal(FormatoAdesivo.Etiqueta, pdf.Ultimo!.Formato);
    }

    // ---------- Escopo por obra ----------

    [Fact]
    public async Task Escopo_InspecaoDeOutraObra_DevolveNaoEncontrada()
    {
        var c = await CriarAsync();
        var daObraB = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-B", obraId: ObraB);
        var daObraA = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-A");

        var handler = Handler(c.RestritoA, new PdfFalso());
        var fora = await handler.Handle(new GerarAdesivoInspecaoCommand(daObraB.Id, FormatoAdesivo.A4, 1), default);
        var dentro = await handler.Handle(new GerarAdesivoInspecaoCommand(daObraA.Id, FormatoAdesivo.A4, 1), default);

        Assert.True(fora.NaoEncontrada);
        Assert.Null(fora.Pdf);
        Assert.NotNull(dentro.Pdf);

        var consulta = new ObterAdesivoInspecaoQueryHandler(c.RestritoA);
        Assert.Null(await consulta.Handle(new ObterAdesivoInspecaoQuery(daObraB.Id), default));
        Assert.NotNull(await consulta.Handle(new ObterAdesivoInspecaoQuery(daObraA.Id), default));
    }

    // ---------- Conteúdo do adesivo e tag ----------

    [Fact]
    public async Task Gerar_Veiculo_Liberado_CriaTagUmaVezSo_ComQrDoProtocolo_E_LogoDaObra()
    {
        var logo = new byte[] { 1, 2, 3, 4 };
        var c = await CriarAsync(logo);
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-2026-001234");
        var pdf = new PdfFalso();
        var handler = Handler(c.Global, pdf);

        await handler.Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.A4, 3), default);
        var primeiro = pdf.Ultimo!;
        await handler.Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.A4, 5), default);
        var segundo = pdf.Ultimo!;

        var tags = await c.Global.TagsIdentificacao.Where(t => t.EntidadeVinculadaId == c.Veiculo.Id).ToListAsync();
        var tag = Assert.Single(tags);
        Assert.Equal(TipoEntidadeVinculada.Veiculo, tag.EntidadeVinculadaTipo);
        Assert.Equal(TipoTag.QrCode, tag.Tipo);
        Assert.StartsWith("VEI-", tag.Uid);

        Assert.Equal($"https://app.exemplo/#/p/{tag.Uid}?i=INSP-2026-001234", primeiro.UrlQr);
        Assert.Equal(primeiro.UrlQr, segundo.UrlQr);
        Assert.Equal(SituacaoAdesivo.Liberado, primeiro.Situacao);
        Assert.Equal("RTK-4B27", primeiro.ObjetoIdentificacao);
        Assert.Equal("Veículos", primeiro.Tema);
        Assert.Equal("Obra Beira Rio", primeiro.ObraNome);
        Assert.Equal(logo, primeiro.ObraLogoConteudo);
        Assert.Equal("Carlos Eduardo Menezes", primeiro.InspetorNome);
        Assert.Equal("Técnico de Segurança do Trabalho", primeiro.InspetorFuncao);
        Assert.Equal(3, primeiro.Posicao);
        Assert.Equal(Hoje.AddDays(30), primeiro.ProximaInspecaoEm);
    }

    [Fact]
    public async Task Gerar_ObraSemLogo_DeixaOEspacoEmBranco()
    {
        var c = await CriarAsync(logo: null);
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-5");
        var pdf = new PdfFalso();

        await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.Etiqueta, null), default);

        Assert.Null(pdf.Ultimo!.ObraLogoConteudo);
    }

    [Fact]
    public async Task Gerar_ItemDoTemaInspecionado_ComNaoConforme_VaiParaNaoLiberado_ESemPeriodicidadeNaoTemProxima()
    {
        var c = await CriarAsync();
        var semPeriodicidade = new ItemInspecionavel { TemaInspecaoId = c.Extintor.TemaInspecaoId, ObraId = ObraA, Identificacao = "EXT-2" };
        c.Global.ItensInspecionaveis.Add(semPeriodicidade);
        await c.Global.SaveChangesAsync();

        var limpa = await InspecaoAsync(c, i => i.ItemInspecionavelId = c.Extintor.Id, StatusInspecao.Concluida, Hoje, "INSP-6");
        var bloqueada = await InspecaoAsync(c, i => i.ItemInspecionavelId = semPeriodicidade.Id, StatusInspecao.Concluida, Hoje, "INSP-7", comNaoConforme: true);
        var pdf = new PdfFalso();
        var handler = Handler(c.Global, pdf);

        await handler.Handle(new GerarAdesivoInspecaoCommand(limpa.Id, FormatoAdesivo.Etiqueta, null), default);
        Assert.Equal(SituacaoAdesivo.Inspecionado, pdf.Ultimo!.Situacao);
        Assert.Equal("Extintores", pdf.Ultimo.Tema);
        Assert.Equal(Hoje.AddDays(30), pdf.Ultimo.ProximaInspecaoEm);
        Assert.StartsWith("ITM-", (await c.Global.TagsIdentificacao.SingleAsync(t => t.EntidadeVinculadaId == c.Extintor.Id)).Uid);

        await handler.Handle(new GerarAdesivoInspecaoCommand(bloqueada.Id, FormatoAdesivo.Etiqueta, null), default);
        Assert.Equal(SituacaoAdesivo.NaoLiberado, pdf.Ultimo!.Situacao);
        Assert.Null(pdf.Ultimo.ProximaInspecaoEm);

        var info = await new ObterAdesivoInspecaoQueryHandler(c.Global).Handle(new ObterAdesivoInspecaoQuery(bloqueada.Id), default);
        Assert.True(info!.Disponivel);
        Assert.Equal("NaoLiberado", info.Situacao);
        Assert.Null(info.ProximaInspecaoEm);
        Assert.Equal("EXT-2", info.ObjetoIdentificacao);
    }

    [Fact]
    public async Task Gerar_Alojamento_RecebeInspecionado_ComProximaPelaConfiguracaoGlobal()
    {
        var c = await CriarAsync();
        var insp = await InspecaoAsync(c, i => i.AlojamentoId = c.Alojamento.Id, StatusInspecao.Concluida, Hoje, "INSP-8");
        var pdf = new PdfFalso();

        await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.Etiqueta, null), default);

        Assert.Equal(SituacaoAdesivo.Inspecionado, pdf.Ultimo!.Situacao);
        Assert.Equal("Alojamento", pdf.Ultimo.Tema);
        Assert.Equal(Hoje.AddDays(45), pdf.Ultimo.ProximaInspecaoEm);
        Assert.StartsWith("ALO-", (await c.Global.TagsIdentificacao.SingleAsync(t => t.EntidadeVinculadaId == c.Alojamento.Id)).Uid);
    }

    [Fact]
    public async Task Gerar_VeiculoSemPeriodicidade_NaoTemProxima()
    {
        var c = await CriarAsync();
        c.Veiculo.PeriodicidadeInspecaoDias = null;
        await c.Global.SaveChangesAsync();
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-9");
        var pdf = new PdfFalso();

        await Handler(c.Global, pdf).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.Etiqueta, null), default);

        Assert.Null(pdf.Ultimo!.ProximaInspecaoEm);
    }

    // ---------- Rota pública (anônima) ----------

    private static async Task<TagIdentificacao> TagAsync(Cenario c, string uid, TipoEntidadeVinculada tipo, Guid id)
    {
        var tag = new TagIdentificacao { Uid = uid, Tipo = TipoTag.QrCode, Status = StatusTag.Vinculada, EntidadeVinculadaTipo = tipo, EntidadeVinculadaId = id };
        c.Global.TagsIdentificacao.Add(tag);
        await c.Global.SaveChangesAsync();
        return tag;
    }

    [Fact]
    public async Task Publico_Anonimo_ResolveComObraEUsuarioFiltrados_ESoDevolveCamposPublicos()
    {
        var c = await CriarAsync();
        await TagAsync(c, "VEI-teste", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-2026-001234", comNaoConforme: false);

        // Prova de que o cenário é "produção": no contexto anônimo a obra e a inspeção somem pelo filtro global de obra
        // (o usuário só some se estiver inativo — coberto no último teste desta seção).
        Assert.Empty(await c.Anonimo.Obras.ToListAsync());
        Assert.Empty(await c.Anonimo.Inspecoes.ToListAsync());

        var dto = await new ResolverEquipamentoPublicoQueryHandler(c.Anonimo)
            .Handle(new ResolverEquipamentoPublicoQuery("VEI-teste"), default);

        Assert.NotNull(dto);
        Assert.Equal("Equipamento", dto!.Tipo);
        Assert.Equal("Liberado", dto.Situacao);
        Assert.Equal("RTK-4B27", dto.ObjetoIdentificacao);
        Assert.Equal("Veículos", dto.Tema);
        Assert.Equal("Obra Beira Rio", dto.ObraNome);
        Assert.Equal("Carlos Eduardo Menezes", dto.InspetorNome);
        Assert.Equal("INSP-2026-001234", dto.ProtocoloAtual);
        Assert.Equal("INSP-2026-001234", dto.Protocolo);
        Assert.Equal("2026-11-17", dto.UltimaInspecaoEm);
        Assert.Equal("2026-12-17", dto.ProximaInspecaoEm);

        // Nada de Id, foto, texto das NCs nem CPF: a lista de campos é fechada.
        // Só o que vai no JSON (InspecaoDocumentoId é [JsonIgnore] e nunca sai da API).
        var campos = typeof(EquipamentoPublicoDto).GetProperties()
            .Where(p => !p.IsDefined(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true))
            .Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[]
        {
            "InspetorNome", "ObjetoIdentificacao", "ObraNome", "Protocolo", "ProtocoloAtual", "ProximaInspecaoEm",
            "Situacao", "Tema", "TemDocumento", "Tipo", "TipoRecurso", "UltimaInspecaoEm",
        }, campos);
    }

    [Fact]
    public async Task Publico_SituacaoEDaInspecaoConcluidaMaisRecente_NaoDaDoAdesivoNemDaEmAndamento()
    {
        var c = await CriarAsync();
        await TagAsync(c, "ITM-teste", TipoEntidadeVinculada.ItemInspecionavel, c.Extintor.Id);
        await InspecaoAsync(c, i => i.ItemInspecionavelId = c.Extintor.Id, StatusInspecao.Concluida, Hoje.AddDays(-10), "INSP-ANTIGA", comNaoConforme: true);
        await InspecaoAsync(c, i => i.ItemInspecionavelId = c.Extintor.Id, StatusInspecao.Concluida, Hoje.AddDays(-1), "INSP-ATUAL");
        // Mais nova ainda, mas sem concluir: não conta.
        await InspecaoAsync(c, i => i.ItemInspecionavelId = c.Extintor.Id, StatusInspecao.EmAndamento, Hoje, "INSP-ABERTA", comNaoConforme: true);
        var handler = new ResolverEquipamentoPublicoQueryHandler(c.Anonimo);

        // Adesivo antigo (era NÃO LIBERADO): a leitura mostra a situação ATUAL e avisa qual protocolo é o do adesivo.
        var antigo = await handler.Handle(new ResolverEquipamentoPublicoQuery("ITM-teste", "INSP-ANTIGA"), default);
        Assert.Equal("Inspecionado", antigo!.Situacao);
        Assert.Equal("INSP-ATUAL", antigo.ProtocoloAtual);
        Assert.Equal("INSP-ANTIGA", antigo.Protocolo);
        Assert.Equal("Extintores", antigo.Tema);

        // Protocolo desconhecido, de EmAndamento ou lixo não é repetido na resposta.
        foreach (var pedido in new[] { "INSP-ABERTA", "INSP-NAO-EXISTE", "<script>", new string('x', 200) })
        {
            var dto = await handler.Handle(new ResolverEquipamentoPublicoQuery("ITM-teste", pedido), default);
            Assert.Equal("INSP-ATUAL", dto!.Protocolo);
        }
    }

    [Fact]
    public async Task Publico_NaoRepeteProtocoloDeOutroObjeto()
    {
        var c = await CriarAsync();
        await TagAsync(c, "VEI-teste", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-VEICULO");
        await InspecaoAsync(c, i => i.ItemInspecionavelId = c.Extintor.Id, StatusInspecao.Concluida, Hoje, "INSP-OUTRO-OBJETO");

        var dto = await new ResolverEquipamentoPublicoQueryHandler(c.Anonimo)
            .Handle(new ResolverEquipamentoPublicoQuery("VEI-teste", "INSP-OUTRO-OBJETO"), default);

        Assert.Equal("INSP-VEICULO", dto!.Protocolo);
    }

    [Fact]
    public async Task Publico_NaoResolve_TagInexistente_TagDeTrabalhador_ObjetoExcluido_OuSemInspecaoConcluida()
    {
        var c = await CriarAsync();
        var handler = new ResolverEquipamentoPublicoQueryHandler(c.Anonimo);

        Assert.Null(await handler.Handle(new ResolverEquipamentoPublicoQuery("NAO-EXISTE"), default));

        await TagAsync(c, "TRB-x", TipoEntidadeVinculada.Trabalhador, Guid.NewGuid());
        Assert.Null(await handler.Handle(new ResolverEquipamentoPublicoQuery("TRB-x"), default));

        await TagAsync(c, "VEI-teste", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        Assert.Null(await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste"), default)); // sem inspeção concluída

        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-1");
        Assert.NotNull(await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste"), default));

        c.Veiculo.Ativo = false;
        await c.Global.SaveChangesAsync();
        Assert.Null(await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste"), default)); // excluído (soft delete)
    }

    [Fact]
    public async Task Publico_ObraComLogoDesativadaOuInspetorDesativado_ContinuaResolvendo()
    {
        var c = await CriarAsync();
        await TagAsync(c, "ALO-teste", TipoEntidadeVinculada.Alojamento, c.Alojamento.Id);
        await InspecaoAsync(c, i => i.AlojamentoId = c.Alojamento.Id, StatusInspecao.Concluida, Hoje, "INSP-ALO");
        c.Obra.Ativo = false;
        c.Inspetor.Ativo = false;
        await c.Global.SaveChangesAsync();

        var dto = await new ResolverEquipamentoPublicoQueryHandler(c.Anonimo)
            .Handle(new ResolverEquipamentoPublicoQuery("ALO-teste"), default);

        Assert.NotNull(dto);
        Assert.Equal("Obra Beira Rio", dto!.ObraNome);
        Assert.Equal("Carlos Eduardo Menezes", dto.InspetorNome);
        Assert.Equal("Inspecionado", dto.Situacao);
    }

    // ---------- ?i= falso/real, inspeção excluída, tag Desativada/Perdida ----------

    [Fact]
    public async Task Publico_ProtocoloFalsoDaUrl_NuncaVoltaNaResposta_EProtocoloRealAntigoVolta()
    {
        var c = await CriarAsync();
        await TagAsync(c, "VEI-teste", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje.AddDays(-30), "INSP-ANTIGA");
        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-ATUAL");
        var handler = new ResolverEquipamentoPublicoQueryHandler(c.Anonimo);

        // O front decide o aviso por Protocolo != ProtocoloAtual e só exibe Protocolo: com ?i= falso (inclusive HTML) os
        // dois ficam iguais (sem aviso) e o texto injetado não aparece na resposta.
        var falso = await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste", "FALSO<img src=x onerror=alert(1)>"), default);
        Assert.Equal(falso!.ProtocoloAtual, falso.Protocolo);
        Assert.DoesNotContain("FALSO", falso.Protocolo);

        var real = await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste", "INSP-ANTIGA"), default);
        Assert.Equal("INSP-ANTIGA", real!.Protocolo);
        Assert.Equal("INSP-ATUAL", real.ProtocoloAtual);
        Assert.NotEqual(real.ProtocoloAtual, real.Protocolo);
    }

    [Fact]
    public async Task Publico_InspecaoConcluidaExcluida_NaoContaNemConfirmaProtocolo()
    {
        var c = await CriarAsync();
        await TagAsync(c, "VEI-teste", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje.AddDays(-30), "INSP-ANTIGA");
        var maisNova = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-EXCLUIDA", comNaoConforme: true);
        var handler = new ResolverEquipamentoPublicoQueryHandler(c.Anonimo);

        maisNova.Ativo = false; // soft delete
        await c.Global.SaveChangesAsync();

        var dto = await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste", "INSP-EXCLUIDA"), default);
        Assert.Equal("INSP-ANTIGA", dto!.ProtocoloAtual);
        Assert.Equal("INSP-ANTIGA", dto.Protocolo);
        Assert.Equal("Liberado", dto.Situacao); // a NC da inspeção excluída não bloqueia mais
    }

    [Theory]
    [InlineData(StatusTag.Desativada)]
    [InlineData(StatusTag.Perdida)]
    public async Task Publico_TagDesativadaOuPerdida_NaoResolveOAdesivo(StatusTag status)
    {
        var c = await CriarAsync();
        var tag = await TagAsync(c, "VEI-teste", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-1");
        var handler = new ResolverEquipamentoPublicoQueryHandler(c.Anonimo);
        Assert.NotNull(await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste"), default));

        tag.Status = status;
        await c.Global.SaveChangesAsync();

        Assert.Null(await handler.Handle(new ResolverEquipamentoPublicoQuery("VEI-teste"), default));
    }

    // ---------- Nome do objeto nas tags 4/5/6 (listagem, por Id e resolução por Uid) ----------

    [Fact]
    public async Task Tags_Veiculo_Item_Alojamento_ResolvemONomeNaListagemENaResolucao_ComEscopoPorObra()
    {
        var c = await CriarAsync();
        var veiculoB = new Veiculo { ObraId = ObraB, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "OUT-0001", MarcaModelo = "X" };
        c.Global.Veiculos.Add(veiculoB);
        await c.Global.SaveChangesAsync();
        await TagAsync(c, "VEI-A", TipoEntidadeVinculada.Veiculo, c.Veiculo.Id);
        await TagAsync(c, "ITM-A", TipoEntidadeVinculada.ItemInspecionavel, c.Extintor.Id);
        await TagAsync(c, "ALO-A", TipoEntidadeVinculada.Alojamento, c.Alojamento.Id);
        await TagAsync(c, "VEI-B", TipoEntidadeVinculada.Veiculo, veiculoB.Id);

        Assert.True(NomeEntidadeVinculada.EhObjetoDeInspecao(TipoEntidadeVinculada.Veiculo));
        Assert.False(NomeEntidadeVinculada.EhObjetoDeInspecao(TipoEntidadeVinculada.Area));

        // Usuário restrito à obra A: vê as 3 tags de A com nome, e a tag do veículo da obra B nem aparece.
        var lista = await new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ListarTagsIdentificacaoQueryHandler(c.RestritoA)
            .Handle(new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ListarTagsIdentificacaoQuery(), default);
        var porUid = lista.ToDictionary(t => t.Uid);
        Assert.False(porUid.ContainsKey("VEI-B"));
        Assert.Equal("RTK-4B27", porUid["VEI-A"].EntidadeVinculadaNome);
        Assert.Equal("EXT-PQS-0142", porUid["ITM-A"].EntidadeVinculadaNome);
        Assert.Equal("Alojamento 03 - Bloco B", porUid["ALO-A"].EntidadeVinculadaNome);

        var porId = await new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ObterTagIdentificacaoPorIdQueryHandler(c.RestritoA)
            .Handle(new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ObterTagIdentificacaoPorIdQuery(porUid["ALO-A"].Id), default);
        Assert.Equal("Alojamento 03 - Bloco B", porId!.EntidadeVinculadaNome);

        var resolvida = await new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ResolverTagPorUidQueryHandler(c.RestritoA)
            .Handle(new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ResolverTagPorUidQuery("ITM-A"), default);
        Assert.Equal("EXT-PQS-0142", resolvida!.EntidadeVinculadaNome);
        Assert.Null(await new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ResolverTagPorUidQueryHandler(c.RestritoA)
            .Handle(new AAHBRANT.SST.Application.TagsIdentificacao.Queries.ResolverTagPorUidQuery("VEI-B"), default));
    }

    // ---------- Tradução para SQL Server (InMemory executa qualquer LINQ; o SQL Server não) ----------

    [Fact]
    public void ConsultasPublicas_TraduzemParaSqlServer_SemJoinEmEntidadeFiltrada()
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>()
            .UseSqlServer("Server=nao-conecta;Database=traducao;Trusted_Connection=True;").Options;
        var anonimo = new CurrentUserService();
        anonimo.DefinirEscopo(false, Array.Empty<Guid>());
        using var db = new SstDbContext(opcoes, anonimo);
        var id = Guid.NewGuid();

        var ultima = db.Inspecoes.IgnoreQueryFilters()
            .Where(i => i.Ativo && i.Status == StatusInspecao.Concluida && i.VeiculoId == id)
            .OrderByDescending(i => i.Data).ThenByDescending(i => i.CreatedAtUtc)
            .Select(i => new InspecaoParaAdesivo(i.Id, i.ObraId, i.Data, i.Status, i.NumeroDocumento, i.ResponsavelUsuarioId, i.VeiculoId, i.AlojamentoId, i.ItemInspecionavelId, i.TipoInspecao, i.AtividadeId))
            .ToQueryString();
        var respostas = db.InspecaoItemRespostas.IgnoreQueryFilters()
            .Where(r => r.InspecaoId == id && r.Ativo).Select(r => r.StatusItem).ToQueryString();
        var obra = db.Obras.IgnoreQueryFilters().Where(o => o.Id == id).Select(o => o.Nome).ToQueryString();
        var usuario = db.Usuarios.IgnoreQueryFilters().Where(u => u.Id == id).Select(u => new { u.Nome, u.TrabalhadorId }).ToQueryString();
        var tag = db.TagsIdentificacao.Where(t => t.Uid == "x").Select(t => new { t.EntidadeVinculadaTipo, t.EntidadeVinculadaId }).ToQueryString();
        var escopoTags = db.TagsIdentificacao.NoEscopoDaObra(db).ToQueryString();

        foreach (var sql in new[] { ultima, respostas, obra, usuario, tag })
            Assert.DoesNotContain("JOIN", sql);
        Assert.Contains("[Veiculos]", escopoTags);
        Assert.Contains("[ItensInspecionaveis]", escopoTags);
        Assert.Contains("[Alojamentos]", escopoTags);
    }

    // ---------- QR abre o PDF assinado (18/11/2026) ----------

    private static async Task DocumentoFinalizadoAsync(Cenario c, Inspecao insp, byte[] pdf)
    {
        c.Global.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = nameof(Inspecao),
            EntidadeId = insp.Id,
            ObraId = insp.ObraId,
            Status = StatusDocumentoAssinatura.Finalizado,
            FinalizadoEm = insp.Data,
            PdfConteudo = pdf,
        });
        await c.Global.SaveChangesAsync();
    }

    private static ObterDocumentoEquipamentoPublicoQueryHandler DocumentoAnonimo(Cenario c)
        => new(new MediatorSimulado(r => new ResolverEquipamentoPublicoQueryHandler(c.Anonimo)
            .Handle((ResolverEquipamentoPublicoQuery)r, default).GetAwaiter().GetResult()), c.Anonimo);

    [Fact]
    public async Task QrPublico_AbreSemLogin_OPdfDaInspecaoDoAdesivo()
    {
        var c = await CriarAsync();
        var antiga = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje.AddDays(-30), "INSP-ANTIGA");
        var atual = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-ATUAL");
        await DocumentoFinalizadoAsync(c, antiga, new byte[] { 1, 1, 1 });
        await DocumentoFinalizadoAsync(c, atual, new byte[] { 2, 2, 2 });
        await Handler(c.Global, new PdfFalso()).Handle(new GerarAdesivoInspecaoCommand(atual.Id, FormatoAdesivo.Etiqueta, null), default);
        var uid = (await c.Global.TagsIdentificacao.SingleAsync(t => t.EntidadeVinculadaId == c.Veiculo.Id)).Uid;

        var situacao = await new ResolverEquipamentoPublicoQueryHandler(c.Anonimo).Handle(new ResolverEquipamentoPublicoQuery(uid), default);
        Assert.True(situacao!.TemDocumento);

        // Sem ?i=: a mais recente. Com ?i= de um adesivo antigo: o PDF daquele adesivo.
        Assert.Equal(new byte[] { 2, 2, 2 }, (await DocumentoAnonimo(c).Handle(new ObterDocumentoEquipamentoPublicoQuery(uid, null), default))!.Conteudo);
        Assert.Equal(new byte[] { 1, 1, 1 }, (await DocumentoAnonimo(c).Handle(new ObterDocumentoEquipamentoPublicoQuery(uid, "INSP-ANTIGA"), default))!.Conteudo);
        // Protocolo que não é deste objeto não abre outro documento: cai na atual.
        Assert.Equal(new byte[] { 2, 2, 2 }, (await DocumentoAnonimo(c).Handle(new ObterDocumentoEquipamentoPublicoQuery(uid, "INSP-QUALQUER"), default))!.Conteudo);
        // UID inexistente não abre nada.
        Assert.Null(await DocumentoAnonimo(c).Handle(new ObterDocumentoEquipamentoPublicoQuery("VEI-naoexiste", null), default));
    }

    [Fact]
    public async Task QrPublico_SemDocumentoFinalizado_NaoTemPdf()
    {
        var c = await CriarAsync();
        var insp = await InspecaoAsync(c, i => i.VeiculoId = c.Veiculo.Id, StatusInspecao.Concluida, Hoje, "INSP-SEM-PDF");
        await Handler(c.Global, new PdfFalso()).Handle(new GerarAdesivoInspecaoCommand(insp.Id, FormatoAdesivo.Etiqueta, null), default);
        var uid = (await c.Global.TagsIdentificacao.SingleAsync(t => t.EntidadeVinculadaId == c.Veiculo.Id)).Uid;

        var situacao = await new ResolverEquipamentoPublicoQueryHandler(c.Anonimo).Handle(new ResolverEquipamentoPublicoQuery(uid), default);
        Assert.False(situacao!.TemDocumento);
        Assert.Null(await DocumentoAnonimo(c).Handle(new ObterDocumentoEquipamentoPublicoQuery(uid, null), default));
    }
}
