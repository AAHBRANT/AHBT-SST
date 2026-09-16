using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Alojamentos;

public class ObterOuCriarInspecaoAlojamentoCommandHandlerTests
{
    // "oid" (claim do Entra ID) usado nos cenários em que o usuário logado deve ser resolvido com
    // sucesso — mesmo padrão de CriarDdsSemanalCommandHandler (AzureAdObjectId, não um Guid
    // escolhido em tela).
    private const string AzureAdObjectId = "oid-tecnico-01";

    private static SstDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(SstDbContext db, Alojamento alojamento, Usuario usuario, ChecklistModelo checklist)> PrepararCenario(string nomeBanco)
    {
        var db = CriarDb(nomeBanco);
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste", AzureAdObjectId = AzureAdObjectId };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        var checklist = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        checklist.Itens.Add(new ChecklistModeloItem { Descricao = "Extintor válido", ChecklistModeloId = checklist.Id });
        db.AddRange(obra, usuario, alojamento, checklist);
        await db.SaveChangesAsync();
        return (db, alojamento, usuario, checklist);
    }

    [Fact]
    public async Task Handle_SemInspecaoEmAndamento_CriaNova()
    {
        var (db, alojamento, usuario, _) = await PrepararCenario(nameof(Handle_SemInspecaoEmAndamento_CriaNova));
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var resultado = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, AzureAdObjectId), default);

        Assert.True(resultado.FoiCriadaAgora);
        Assert.Equal(usuario.Id, resultado.ResponsavelUsuarioId);
        var inspecao = await db.Inspecoes.FirstAsync(i => i.Id == resultado.InspecaoId);
        Assert.Equal(alojamento.Id, inspecao.AlojamentoId);
        Assert.Equal(alojamento.ObraId, inspecao.ObraId);
        Assert.Equal(StatusInspecao.EmAndamento, inspecao.Status);
        Assert.Single(inspecao.Respostas);
    }

    [Fact]
    public async Task Handle_ComInspecaoEmAndamento_RetornaExistenteSemCriarOutra()
    {
        var (db, alojamento, usuario, _) = await PrepararCenario(nameof(Handle_ComInspecaoEmAndamento_RetornaExistenteSemCriarOutra));
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        var primeira = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, AzureAdObjectId), default);
        var segunda = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, AzureAdObjectId), default);

        Assert.Equal(primeira.InspecaoId, segunda.InspecaoId);
        Assert.False(segunda.FoiCriadaAgora);
        Assert.Equal(1, await db.Inspecoes.CountAsync(i => i.AlojamentoId == alojamento.Id));
    }

    [Fact]
    public async Task Handle_AlojamentoSemChecklistAlojamentoCadastrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_AlojamentoSemChecklistAlojamentoCadastrado_LancaInvalidOperationException));
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste", AzureAdObjectId = AzureAdObjectId };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        db.AddRange(obra, usuario, alojamento);
        await db.SaveChangesAsync();
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, AzureAdObjectId), default));
    }

    // Cenário adicional (não estava no brief original): a resolução do usuário logado via
    // AzureAdObjectId é lógica nova introduzida aqui (mesmo padrão de CriarDdsSemanalCommandHandler,
    // mas reaplicada por esta task) — precisa de cobertura própria, distinta do "sem checklist".
    [Fact]
    public async Task Handle_UsuarioLogadoNaoVinculadoACadastro_LancaInvalidOperationException()
    {
        var (db, alojamento, _, _) = await PrepararCenario(nameof(Handle_UsuarioLogadoNaoVinculadoACadastro_LancaInvalidOperationException));
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(db, new GeradorNumeroDocumentoService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, "oid-desconhecido"), default));
    }
}
