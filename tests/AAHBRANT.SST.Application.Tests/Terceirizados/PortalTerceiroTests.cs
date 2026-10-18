using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Terceirizados;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Application.Terceirizados.Portal;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Terceirizados;

public class PortalTerceiroTests
{
    static PortalTerceiroTests()
    {
        ChavesCpfDeTeste.Configurar();
    }

    // PDF mínimo: o validador confere a assinatura "%PDF" do conteúdo contra o tipo declarado.
    private static readonly byte[] PdfFalso = "%PDF-1.4 teste"u8.ToArray();

    private sealed class EmailFalso : IEmailService
    {
        public List<(string Para, string Assunto, string Html)> Enviados { get; } = new();
        public bool Falhar { get; set; }

        public Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
        {
            if (Falhar) throw new InvalidOperationException("Graph fora do ar");
            Enviados.Add((para, assunto, corpoHtml));
            return Task.CompletedTask;
        }
    }

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public List<string> Acoes { get; } = new();

        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
        {
            Acoes.Add(acao);
            return Task.CompletedTask;
        }
    }

    private sealed class ConfiguracaoFalsa : IPortalTerceiroConfiguracao
    {
        public string UrlBase => "https://sst.teste/";
    }

    private sealed record Cenario(
        SstDbContext Db, CurrentUserService Usuario, Obra Obra, Empresa Empresa, Funcao Funcao, Contrato Contrato,
        Trabalhador Pessoa, CursoTreinamento Integracao);

    private static async Task<Cenario> SemearAsync(string nomeBanco)
    {
        var usuario = new CurrentUserService();
        var db = new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, usuario);

        var obra = new Obra { Codigo = "O1", Nome = "Obra Centro" };
        var empresa = new Empresa { RazaoSocial = "Empreiteira XPTO", Cnpj = "12345678000199", ContatoEmail = "contato@xpto.com.br" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, ValidadeEmMeses = 12, CargaHorariaMinima = 4 };
        db.AddRange(obra, empresa, funcao, integracao);
        await db.SaveChangesAsync();

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id, ObraId = obra.Id, NumeroContrato = "CT-001", GJuriContratoId = "gjuri-1",
            DataInicioVigencia = DateOnly.FromDateTime(DateTime.UtcNow),
            DataFimVigencia = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
        };
        db.Contratos.Add(contrato);
        db.ContratoVagasFuncao.Add(new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcao.Id, QuantidadeVagas = 3, QuantidadePreenchidas = 1 });
        var pessoa = new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = funcao.Id, Nome = "João Pedreiro", Matricula = "T-1", Cpf = "52998224725",
            Vinculo = TipoVinculo.Terceirizado, EmpresaId = empresa.Id, ContratoId = contrato.Id, DataAdmissao = DateTime.UtcNow,
        };
        db.Trabalhadores.Add(pessoa);
        await db.SaveChangesAsync();

        return new Cenario(db, usuario, obra, empresa, funcao, contrato, pessoa, integracao);
    }

    private static GerarAcessoPortalTerceiroCommandHandler HandlerGerar(IAppDbContext db, EmailFalso email, AuditoriaFalsa? auditoria = null) =>
        new(db, email, new ConfiguracaoFalsa(), auditoria ?? new AuditoriaFalsa(), NullLogger<GerarAcessoPortalTerceiroCommandHandler>.Instance);

    private static string TokenDoLink(string link) => link[(link.LastIndexOf('/') + 1)..];

    private static string CodigoDoEmail(string html) => Regex.Match(html, @">(\d{6})<").Groups[1].Value;

    // Simula a requisição anônima de produção: sem usuário, o filtro global de obra nega tudo.
    private static void ComoAnonimo(Cenario c) => c.Usuario.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());

    private static async Task<(string Token, string Sessao, Guid AcessoId)> EntrarAsync(Cenario c, EmailFalso email)
    {
        var gerado = await HandlerGerar(c.Db, email).Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default);
        var token = TokenDoLink(gerado.Link);
        ComoAnonimo(c);
        await new SolicitarCodigoPortalCommandHandler(c.Db, email, NullLogger<SolicitarCodigoPortalCommandHandler>.Instance)
            .Handle(new SolicitarCodigoPortalCommand(token), default);
        var codigo = CodigoDoEmail(email.Enviados[^1].Html);
        var sessao = await new VerificarCodigoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new VerificarCodigoPortalCommand(token, codigo), default);
        return (token, sessao.Sessao, gerado.AcessoId);
    }

    private static EnviarDocumentoPortalCommandHandler HandlerEnviar(IAppDbContext db, TecnicosSegurancaPorObraServiceFalso? tecnicos = null, FilaNotificacaoTeamsFalsa? fila = null) =>
        new(db, tecnicos ?? new TecnicosSegurancaPorObraServiceFalso(), fila ?? new FilaNotificacaoTeamsFalsa(), new AuditoriaFalsa());

    private static EnviarDocumentoPortalCommand ComandoAso(Guid acessoId, Guid trabalhadorId) => new(
        acessoId, trabalhadorId, TipoDocumentoTerceiro.Aso, "aso.pdf", PdfFalso, "application/pdf",
        DateTime.UtcNow.Date.AddDays(-2), DateTime.UtcNow.Date.AddYears(1), TipoExameAso.Admissional, ResultadoAso.Apto,
        null, null, null, null);

    [Fact]
    public async Task GerarAcesso_EnviaLinkPorEmailEGuardaSoOHashDoToken()
    {
        var c = await SemearAsync(nameof(GerarAcesso_EnviaLinkPorEmailEGuardaSoOHashDoToken));
        var email = new EmailFalso();

        var gerado = await HandlerGerar(c.Db, email).Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default);

        Assert.True(gerado.EmailEnviado);
        Assert.StartsWith("https://sst.teste/#/portal-terceiros/", gerado.Link);
        var envio = Assert.Single(email.Enviados);
        Assert.Equal("contato@xpto.com.br", envio.Para);
        Assert.Contains(gerado.Link, envio.Html);

        var acesso = await c.Db.AcessosPortalTerceiro.SingleAsync();
        var token = TokenDoLink(gerado.Link);
        Assert.NotEqual(token, acesso.TokenHash);
        Assert.Equal(SegredoPortal.Hash(token), acesso.TokenHash);
        Assert.True(acesso.ExpiraEmUtc > DateTime.UtcNow.AddMonths(6));
    }

    [Fact]
    public async Task GerarAcesso_DeNovo_RevogaOAnterior()
    {
        var c = await SemearAsync(nameof(GerarAcesso_DeNovo_RevogaOAnterior));
        var handler = HandlerGerar(c.Db, new EmailFalso());

        var primeiro = await handler.Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default);
        await handler.Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, "outro@xpto.com.br", null), default);

        var anterior = await c.Db.AcessosPortalTerceiro.SingleAsync(a => a.Id == primeiro.AcessoId);
        Assert.NotNull(anterior.RevogadoEmUtc);
        ComoAnonimo(c);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new SolicitarCodigoPortalCommandHandler(c.Db, new EmailFalso(), NullLogger<SolicitarCodigoPortalCommandHandler>.Instance)
                .Handle(new SolicitarCodigoPortalCommand(TokenDoLink(primeiro.Link)), default));
    }

    [Fact]
    public async Task GerarAcesso_SemEmailDeContato_PedeOEmail()
    {
        var c = await SemearAsync(nameof(GerarAcesso_SemEmailDeContato_PedeOEmail));
        c.Empresa.ContatoEmail = null;
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerGerar(c.Db, new EmailFalso()).Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default));
    }

    [Fact]
    public async Task GerarAcesso_EmailFalhou_DevolveOLinkMesmoAssim()
    {
        var c = await SemearAsync(nameof(GerarAcesso_EmailFalhou_DevolveOLinkMesmoAssim));

        var gerado = await HandlerGerar(c.Db, new EmailFalso { Falhar = true })
            .Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default);

        Assert.False(gerado.EmailEnviado);
        Assert.True(await c.Db.AcessosPortalTerceiro.AnyAsync(a => a.Id == gerado.AcessoId));
    }

    [Fact]
    public async Task Entrada_LinkMaisCodigo_GeraSessaoQueResolveParaAObraDoContrato()
    {
        var c = await SemearAsync(nameof(Entrada_LinkMaisCodigo_GeraSessaoQueResolveParaAObraDoContrato));

        var (_, sessao, acessoId) = await EntrarAsync(c, new EmailFalso());

        var resolvida = await new ResolverSessaoPortalQueryHandler(c.Db).Handle(new ResolverSessaoPortalQuery(sessao), default);
        Assert.NotNull(resolvida);
        Assert.Equal(acessoId, resolvida!.AcessoId);
        Assert.Equal(c.Obra.Id, resolvida.ObraId);
        Assert.Null(await new ResolverSessaoPortalQueryHandler(c.Db).Handle(new ResolverSessaoPortalQuery("sessao-inventada"), default));
    }

    [Fact]
    public async Task SolicitarCodigo_RepetidoLogoEmSeguida_NaoReenviaEmail()
    {
        var c = await SemearAsync(nameof(SolicitarCodigo_RepetidoLogoEmSeguida_NaoReenviaEmail));
        var email = new EmailFalso();
        var gerado = await HandlerGerar(c.Db, email).Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default);
        ComoAnonimo(c);
        var handler = new SolicitarCodigoPortalCommandHandler(c.Db, email, NullLogger<SolicitarCodigoPortalCommandHandler>.Instance);

        var resposta = await handler.Handle(new SolicitarCodigoPortalCommand(TokenDoLink(gerado.Link)), default);
        await handler.Handle(new SolicitarCodigoPortalCommand(TokenDoLink(gerado.Link)), default);

        Assert.Equal(2, email.Enviados.Count); // convite + um único código
        Assert.Equal("c******@xpto.com.br", resposta.EmailMascarado);
        Assert.Equal("Obra Centro", resposta.ObraNome);
    }

    [Fact]
    public async Task VerificarCodigo_ErradoCincoVezes_InvalidaOCodigo()
    {
        var c = await SemearAsync(nameof(VerificarCodigo_ErradoCincoVezes_InvalidaOCodigo));
        var email = new EmailFalso();
        var gerado = await HandlerGerar(c.Db, email).Handle(new GerarAcessoPortalTerceiroCommand(c.Contrato.Id, null, null), default);
        var token = TokenDoLink(gerado.Link);
        ComoAnonimo(c);
        await new SolicitarCodigoPortalCommandHandler(c.Db, email, NullLogger<SolicitarCodigoPortalCommandHandler>.Instance)
            .Handle(new SolicitarCodigoPortalCommand(token), default);
        var codigoCerto = CodigoDoEmail(email.Enviados[^1].Html);
        var codigoErrado = codigoCerto == "000000" ? "111111" : "000000";
        var verificar = new VerificarCodigoPortalCommandHandler(c.Db, new AuditoriaFalsa());

        for (var i = 0; i < PortalTerceiroRegras.MaximoTentativasCodigo; i++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => verificar.Handle(new VerificarCodigoPortalCommand(token, codigoErrado), default));

        // Nem o código certo vale mais depois de esgotar as tentativas.
        await Assert.ThrowsAsync<InvalidOperationException>(() => verificar.Handle(new VerificarCodigoPortalCommand(token, codigoCerto), default));
    }

    [Fact]
    public async Task ContratoEncerrado_RevogaOAcessoEASessaoDeixaDeValer()
    {
        var c = await SemearAsync(nameof(ContratoEncerrado_RevogaOAcessoEASessaoDeixaDeValer));
        var (_, sessao, acessoId) = await EntrarAsync(c, new EmailFalso());
        c.Usuario.DefinirEscopo(temAcessoGlobal: true, Array.Empty<Guid>());

        await new ContratoEncerradoWebhookCommandHandler(c.Db, new TecnicosSegurancaPorObraServiceFalso(), new FilaNotificacaoTeamsFalsa())
            .Handle(new ContratoEncerradoWebhookCommand("gjuri-1", DateOnly.FromDateTime(DateTime.UtcNow)), default);

        Assert.NotNull((await c.Db.AcessosPortalTerceiro.SingleAsync(a => a.Id == acessoId)).RevogadoEmUtc);
        Assert.Null(await new ResolverSessaoPortalQueryHandler(c.Db).Handle(new ResolverSessaoPortalQuery(sessao), default));
    }

    [Fact]
    public async Task EnviarDocumento_SemAceitarOTermo_ERecusado()
    {
        var c = await SemearAsync(nameof(EnviarDocumento_SemAceitarOTermo_ERecusado));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerEnviar(c.Db).Handle(ComandoAso(acessoId, c.Pessoa.Id), default));
    }

    [Fact]
    public async Task EnviarDocumento_ComTermo_FicaAguardandoValidacaoEAlertaOTecnico()
    {
        var c = await SemearAsync(nameof(EnviarDocumento_ComTermo_FicaAguardandoValidacaoEAlertaOTecnico));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var tecnicoId = Guid.NewGuid();
        var fila = new FilaNotificacaoTeamsFalsa();

        var documentoId = await HandlerEnviar(c.Db, new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = { tecnicoId } }, fila)
            .Handle(ComandoAso(acessoId, c.Pessoa.Id), default);

        var documento = await c.Db.DocumentosEnviadosTerceiro.SingleAsync(d => d.Id == documentoId);
        Assert.Equal(StatusDocumentoTerceiro.AguardandoValidacao, documento.Status);
        Assert.Equal(64, documento.HashSha256.Length);
        var alerta = await c.Db.Alertas.IgnoreQueryFilters().SingleAsync(a => a.EntidadeOrigemId == documentoId);
        Assert.Equal(TipoAlerta.DocumentoTerceiroAguardandoValidacao, alerta.Tipo);
        Assert.Equal(tecnicoId, alerta.DestinatarioUsuarioId);
        Assert.Single(fila.Mensagens);

        // Enviado ainda não libera ninguém: a pendência de ASO continua até a aprovação.
        Assert.False(await c.Db.Asos.AnyAsync());
        Assert.Contains("ASO válido pendente",
            await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(c.Db, c.Pessoa.Id, c.Funcao.Id));
    }

    [Fact]
    public async Task EnviarDocumento_DeNovo_SubstituiOEnvioAindaNaoConferido()
    {
        var c = await SemearAsync(nameof(EnviarDocumento_DeNovo_SubstituiOEnvioAindaNaoConferido));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var handler = HandlerEnviar(c.Db);

        var primeiro = await handler.Handle(ComandoAso(acessoId, c.Pessoa.Id), default);
        await handler.Handle(ComandoAso(acessoId, c.Pessoa.Id), default);

        var anterior = await c.Db.DocumentosEnviadosTerceiro.SingleAsync(d => d.Id == primeiro);
        Assert.Equal(StatusDocumentoTerceiro.Recusado, anterior.Status);
        Assert.Equal(EnviarDocumentoPortalCommandHandler.MotivoSubstituido, anterior.MotivoRecusa);
        Assert.Single(await c.Db.DocumentosEnviadosTerceiro.Where(d => d.Status == StatusDocumentoTerceiro.AguardandoValidacao).ToListAsync());
    }

    [Fact]
    public async Task EnviarDocumento_ParaPessoaDeOutroContrato_NaoEncontra()
    {
        var c = await SemearAsync(nameof(EnviarDocumento_ParaPessoaDeOutroContrato_NaoEncontra));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var deFora = new Trabalhador
        {
            ObraId = c.Obra.Id, FuncaoId = c.Funcao.Id, Nome = "Fulano CLT", Matricula = "CLT-9", Cpf = "11144477735",
            DataAdmissao = DateTime.UtcNow,
        };
        c.Db.Trabalhadores.Add(deFora);
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => HandlerEnviar(c.Db).Handle(ComandoAso(acessoId, deFora.Id), default));
    }

    [Fact]
    public async Task AprovarAso_CriaOAsoEAPendenciaSomeDaLiberacao()
    {
        var c = await SemearAsync(nameof(AprovarAso_CriaOAsoEAPendenciaSomeDaLiberacao));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var documentoId = await HandlerEnviar(c.Db, new TecnicosSegurancaPorObraServiceFalso { UsuarioIdsARetornar = { Guid.NewGuid() } })
            .Handle(ComandoAso(acessoId, c.Pessoa.Id), default);
        c.Usuario.DefinirEscopo(temAcessoGlobal: false, new[] { c.Obra.Id }); // técnico da obra
        var tecnicoId = Guid.NewGuid();

        var asoId = await new AprovarDocumentoTerceiroCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AprovarDocumentoTerceiroCommand(documentoId, tecnicoId), default);

        var aso = await c.Db.Asos.SingleAsync(a => a.Id == asoId);
        Assert.Equal(ResultadoAso.Apto, aso.ResultadoStatus);
        Assert.Equal(c.Pessoa.Id, aso.TrabalhadorId);
        var documento = await c.Db.DocumentosEnviadosTerceiro.SingleAsync(d => d.Id == documentoId);
        Assert.Equal(StatusDocumentoTerceiro.Aprovado, documento.Status);
        Assert.Equal(tecnicoId, documento.ValidadoPorUsuarioId);
        Assert.All(await c.Db.Alertas.IgnoreQueryFilters().Where(a => a.EntidadeOrigemId == documentoId).ToListAsync(),
            a => Assert.Equal(StatusAlerta.Resolvido, a.Status));
        Assert.DoesNotContain("ASO válido pendente",
            await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(c.Db, c.Pessoa.Id, c.Funcao.Id));
    }

    [Fact]
    public async Task AprovarCertificado_CriaTreinamentoExternoComOArquivoOriginal()
    {
        var c = await SemearAsync(nameof(AprovarCertificado_CriaTreinamentoExternoComOArquivoOriginal));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var documentoId = await HandlerEnviar(c.Db).Handle(new EnviarDocumentoPortalCommand(
            acessoId, c.Pessoa.Id, TipoDocumentoTerceiro.CertificadoTreinamento, "integracao.pdf", PdfFalso, "application/pdf",
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddYears(1), null, null, c.Integracao.Id, 4, "SESI", "123"), default);
        c.Usuario.DefinirEscopo(temAcessoGlobal: true, Array.Empty<Guid>());

        var treinamentoId = await new AprovarDocumentoTerceiroCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AprovarDocumentoTerceiroCommand(documentoId, Guid.NewGuid()), default);

        var treinamento = await c.Db.Treinamentos.SingleAsync(t => t.Id == treinamentoId);
        Assert.Equal(OrigemCertificadoTreinamento.Externo, treinamento.OrigemCertificado);
        Assert.Equal(4, treinamento.CargaHorariaRealizada);
        var arquivo = await c.Db.ArquivosCertificadoTreinamento.SingleAsync(a => a.TreinamentoId == treinamentoId);
        Assert.Equal(PdfFalso, arquivo.Conteudo);
        Assert.DoesNotContain("Integração de Segurança pendente",
            await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(c.Db, c.Pessoa.Id, c.Funcao.Id));
    }

    [Fact]
    public async Task Aprovar_TecnicoDeOutraObra_NaoEncontraODocumento()
    {
        var c = await SemearAsync(nameof(Aprovar_TecnicoDeOutraObra_NaoEncontraODocumento));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var documentoId = await HandlerEnviar(c.Db).Handle(ComandoAso(acessoId, c.Pessoa.Id), default);
        c.Usuario.DefinirEscopo(temAcessoGlobal: false, new[] { Guid.NewGuid() });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AprovarDocumentoTerceiroCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AprovarDocumentoTerceiroCommand(documentoId, Guid.NewGuid()), default));
        Assert.Empty(await new ListarDocumentosTerceiroQueryHandler(c.Db).Handle(new ListarDocumentosTerceiroQuery(null, null), default));
    }

    [Fact]
    public async Task Recusar_GuardaOMotivoEAvisaAEmpresa()
    {
        var c = await SemearAsync(nameof(Recusar_GuardaOMotivoEAvisaAEmpresa));
        var email = new EmailFalso();
        var (_, _, acessoId) = await EntrarAsync(c, email);
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        var documentoId = await HandlerEnviar(c.Db).Handle(ComandoAso(acessoId, c.Pessoa.Id), default);
        c.Usuario.DefinirEscopo(temAcessoGlobal: true, Array.Empty<Guid>());

        await new RecusarDocumentoTerceiroCommandHandler(c.Db, new AuditoriaFalsa(), email, NullLogger<RecusarDocumentoTerceiroCommandHandler>.Instance)
            .Handle(new RecusarDocumentoTerceiroCommand(documentoId, Guid.NewGuid(), "Foto sem leitura"), default);

        var documento = await c.Db.DocumentosEnviadosTerceiro.SingleAsync(d => d.Id == documentoId);
        Assert.Equal(StatusDocumentoTerceiro.Recusado, documento.Status);
        Assert.Equal("Foto sem leitura", documento.MotivoRecusa);
        Assert.Contains("Foto sem leitura", email.Enviados[^1].Html);
        Assert.Equal("contato@xpto.com.br", email.Enviados[^1].Para);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AprovarDocumentoTerceiroCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AprovarDocumentoTerceiroCommand(documentoId, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Painel_MostraVagasPessoasPendenciasEDocumentos()
    {
        var c = await SemearAsync(nameof(Painel_MostraVagasPessoasPendenciasEDocumentos));
        var (_, _, acessoId) = await EntrarAsync(c, new EmailFalso());
        await new AceitarTermoPortalCommandHandler(c.Db, new AuditoriaFalsa())
            .Handle(new AceitarTermoPortalCommand(acessoId, PortalTerceiroRegras.VersaoTermoAtual), default);
        await HandlerEnviar(c.Db).Handle(ComandoAso(acessoId, c.Pessoa.Id), default);

        var painel = await new ObterPainelPortalQueryHandler(c.Db).Handle(new ObterPainelPortalQuery(acessoId), default);

        Assert.True(painel.TermoAceito);
        Assert.Equal("Empreiteira XPTO", painel.EmpresaNome);
        var vaga = Assert.Single(painel.Vagas);
        Assert.Equal("Pedreiro", vaga.FuncaoNome);
        var pessoa = Assert.Single(painel.Pessoas);
        Assert.False(pessoa.Liberado);
        Assert.Contains("ASO válido pendente", pessoa.Pendencias);
        Assert.Equal(StatusDocumentoTerceiro.AguardandoValidacao, Assert.Single(pessoa.Documentos).Status);
        Assert.Contains(painel.Cursos, curso => curso.Id == c.Integracao.Id);
    }

    [Fact]
    public void ValidadorEnvio_AsoSemResultado_EInvalido()
    {
        var comando = ComandoAso(Guid.NewGuid(), Guid.NewGuid()) with { ResultadoAso = null };

        var resultado = new EnviarDocumentoPortalCommandValidator().Validate(comando);

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void ValidadorEnvio_ConteudoQueNaoEPdf_EInvalido()
    {
        var comando = ComandoAso(Guid.NewGuid(), Guid.NewGuid()) with { Conteudo = "texto qualquer"u8.ToArray() };

        Assert.False(new EnviarDocumentoPortalCommandValidator().Validate(comando).IsValid);
    }
}
