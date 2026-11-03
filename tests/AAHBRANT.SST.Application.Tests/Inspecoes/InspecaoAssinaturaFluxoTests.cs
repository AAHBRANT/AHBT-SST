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
