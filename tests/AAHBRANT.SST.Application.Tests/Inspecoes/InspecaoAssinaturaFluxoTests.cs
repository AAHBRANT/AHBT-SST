using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Inspecoes.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Regra de 03/11/2026: a inspeção só fica Concluída depois de assinada. "Finalizar" leva para
// Aguardando assinatura; "Reabrir" só é possível enquanto ninguém assinou.
public class InspecaoAssinaturaFluxoTests
{
    private const string AzureAdObjectId = "oid-tecnico-fluxo";

    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    // Alojamento com uma inspeção já respondida (Conforme + fotos), pronta para finalizar.
    private static async Task<(SstDbContext db, Alojamento alojamento, Inspecao inspecao)> PrepararInspecaoPronta(string nomeBanco)
    {
        var db = CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico", AzureAdObjectId = AzureAdObjectId };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        var checklist = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        var item = new ChecklistModeloItem { Descricao = "Extintor válido", ChecklistModeloId = checklist.Id };
        checklist.Itens.Add(item);
        db.AddRange(obra, usuario, alojamento, checklist);
        await db.SaveChangesAsync();

        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento,
            ObraId = obra.Id,
            AlojamentoId = alojamento.Id,
            ChecklistModeloId = checklist.Id,
            Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuario.Id,
        };
        inspecao.Respostas.Add(new InspecaoItemResposta
        {
            ChecklistModeloItemId = item.Id,
            StatusItem = StatusItemChecklist.Conforme,
            FotoConteudo = new byte[] { 1, 2, 3 },
            FotoContentType = "image/jpeg",
        });
        db.Inspecoes.Add(inspecao);
        await db.SaveChangesAsync();
        return (db, alojamento, inspecao);
    }

    [Fact]
    public async Task Finalizar_VaiParaAguardandoAssinatura_ECriaDocumentoDeAssinatura()
    {
        var (db, _, inspecao) = await PrepararInspecaoPronta(nameof(Finalizar_VaiParaAguardandoAssinatura_ECriaDocumentoDeAssinatura));

        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        Assert.Equal(StatusInspecao.AguardandoAssinatura, (await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id)).Status);
        Assert.Equal(1, await db.DocumentosAssinatura.CountAsync(d => d.EntidadeTipo == nameof(Inspecao) && d.EntidadeId == inspecao.Id));
    }

    [Fact]
    public async Task Reabrir_AguardandoSemAssinatura_VoltaParaEmAndamento()
    {
        var (db, _, inspecao) = await PrepararInspecaoPronta(nameof(Reabrir_AguardandoSemAssinatura_VoltaParaEmAndamento));
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        await new ReabrirInspecaoCommandHandler(db).Handle(new ReabrirInspecaoCommand(inspecao.Id), default);

        Assert.Equal(StatusInspecao.EmAndamento, (await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id)).Status);
    }

    [Fact]
    public async Task Reabrir_ComAssinaturaRegistrada_LancaInvalidOperationException()
    {
        var (db, _, inspecao) = await PrepararInspecaoPronta(nameof(Reabrir_ComAssinaturaRegistrada_LancaInvalidOperationException));
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);
        var documento = await db.DocumentosAssinatura.FirstAsync(d => d.EntidadeId == inspecao.Id);
        db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = Guid.NewGuid(),
            AssinadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ReabrirInspecaoCommandHandler(db).Handle(new ReabrirInspecaoCommand(inspecao.Id), default));

        Assert.Contains("já tem assinatura", ex.Message);
        Assert.Equal(StatusInspecao.AguardandoAssinatura, (await db.Inspecoes.FirstAsync(i => i.Id == inspecao.Id)).Status);
    }

    [Fact]
    public async Task Reabrir_InspecaoEmAndamento_LancaInvalidOperationException()
    {
        var (db, _, inspecao) = await PrepararInspecaoPronta(nameof(Reabrir_InspecaoEmAndamento_LancaInvalidOperationException));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ReabrirInspecaoCommandHandler(db).Handle(new ReabrirInspecaoCommand(inspecao.Id), default));
    }

    [Fact]
    public async Task ObterOuCriar_ComInspecaoAguardandoAssinatura_RetornaEssaEmVezDeCriarOutra()
    {
        var (db, alojamento, inspecao) = await PrepararInspecaoPronta(nameof(ObterOuCriar_ComInspecaoAguardandoAssinatura_RetornaEssaEmVezDeCriarOutra));
        await new EncerrarInspecaoCommandHandler(db).Handle(new EncerrarInspecaoCommand(inspecao.Id), default);

        var resultado = await new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, AzureAdObjectId), default);

        Assert.Equal(inspecao.Id, resultado.InspecaoId);
        Assert.False(resultado.FoiCriadaAgora);
        Assert.Equal(1, await db.Inspecoes.CountAsync(i => i.AlojamentoId == alojamento.Id));
    }
}

// "Finalizar" assina na hora quem realizou a inspeção (Técnico/Engenheiro de Segurança); nos demais casos
// a inspeção fica aguardando a assinatura de um técnico.
public class AssinarInspecaoAoFinalizarTests
{
    private const string Oid = "oid-tecnico-assina";

    private sealed class AuditoriaFalsa : AAHBRANT.SST.Application.Common.Interfaces.IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static async Task<(SstDbContext db, Inspecao inspecao, AssinarInspecaoAoFinalizarCommandHandler handler)> Montar(
        string banco, string funcao, bool usuarioEhResponsavel = true, bool comVinculo = true)
    {
        var db = AAHBRANT.SST.Application.Tests.TestSupport.DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var trabalhador = new Trabalhador { Nome = "Gilton", Cpf = "11122233344", ObraId = obra.Id, Funcao = new Funcao { Nome = funcao } };
        var logado = new Usuario { Email = "a@aahbrant.com", Nome = "Logado", AzureAdObjectId = Oid, TrabalhadorId = comVinculo ? trabalhador.Id : null };
        var outro = new Usuario { Email = "b@aahbrant.com", Nome = "Outro" };
        var checklist = new ChecklistModelo { Nome = "Checklist", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        db.AddRange(obra, trabalhador, logado, outro, checklist);
        await db.SaveChangesAsync();
        var inspecao = new Inspecao
        {
            TipoInspecao = TipoInspecao.Alojamento, ObraId = obra.Id, ChecklistModeloId = checklist.Id, Data = DateTime.UtcNow,
            ResponsavelUsuarioId = usuarioEhResponsavel ? logado.Id : outro.Id, Status = StatusInspecao.AguardandoAssinatura,
        };
        db.Inspecoes.Add(inspecao);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura { EntidadeTipo = nameof(Inspecao), EntidadeId = inspecao.Id });
        await db.SaveChangesAsync();
        return (db, inspecao, new AssinarInspecaoAoFinalizarCommandHandler(db, new AAHBRANT.SST.Application.Assinatura.RegistradorAssinaturaService(db, new AuditoriaFalsa())));
    }

    [Fact]
    public async Task ResponsavelTecnicoDeSeguranca_AssinaNaHora()
    {
        var (db, inspecao, handler) = await Montar(nameof(ResponsavelTecnicoDeSeguranca_AssinaNaHora), "Técnico de Segurança do Trabalho");

        var r = await handler.Handle(new AssinarInspecaoAoFinalizarCommand(inspecao.Id, Oid), default);

        Assert.True(r.Assinada);
        Assert.Equal("Gilton", r.AssinadaPor);
        var signatario = await db.DocumentoSignatarios.SingleAsync();
        Assert.Equal(MetodoAutenticacaoAssinatura.SessaoLogada, signatario.MetodoAutenticacao);
    }

    [Theory]
    [InlineData("Pedreiro", true, true)]            // função que não pode assinar inspeção
    [InlineData("Técnico de Segurança", false, true)] // outro usuário realizou a inspeção
    [InlineData("Técnico de Segurança", true, false)] // usuário sem vínculo com trabalhador
    public async Task NaoAssina_FicaAguardando(string funcao, bool responsavel, bool vinculo)
    {
        var (db, inspecao, handler) = await Montar("na" + funcao + responsavel + vinculo, funcao, responsavel, vinculo);

        var r = await handler.Handle(new AssinarInspecaoAoFinalizarCommand(inspecao.Id, Oid), default);

        Assert.False(r.Assinada);
        Assert.False(string.IsNullOrWhiteSpace(r.Motivo));
        Assert.Equal(0, await db.DocumentoSignatarios.CountAsync());
        Assert.Equal(StatusInspecao.AguardandoAssinatura, (await db.Inspecoes.FirstAsync()).Status);
    }

    [Fact]
    public async Task SemUsuarioIdentificado_NaoAssina()
    {
        var (_, inspecao, handler) = await Montar(nameof(SemUsuarioIdentificado_NaoAssina), "Técnico de Segurança");

        var r = await handler.Handle(new AssinarInspecaoAoFinalizarCommand(inspecao.Id, null), default);

        Assert.False(r.Assinada);
    }
}
