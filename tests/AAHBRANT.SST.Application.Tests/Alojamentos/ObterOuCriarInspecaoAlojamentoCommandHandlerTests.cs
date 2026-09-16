using AAHBRANT.SST.Application.Alojamentos.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
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

    // Corrida real (o motivo desta task existir): dois técnicos clicando ao mesmo tempo no mesmo
    // alojamento. O InMemory provider do EF Core (usado nos testes acima) NÃO aplica índices únicos
    // — para provar que o índice do banco realmente dispara e que o handler recupera graciosamente
    // (em vez de deixar a DbUpdateException virar um 500 cru, já que não há middleware global de
    // tratamento de exceção), este teste usa Sqlite in-memory de verdade, mesmo provider relacional
    // já usado em AlojamentoEntidadeTests para testar o índice de AlojamentoMorador.
    //
    // Sem framework de mock no projeto e sem um jeito limpo de pausar o Handle() no meio para
    // interlear duas requisições de verdade, a corrida é simulada com uma subclasse de
    // SstDbContext (SstDbContextComCorridaSimulada, abaixo) que, na primeira chamada a
    // SaveChangesAsync, insere e COMMITA de verdade — via um segundo DbContext na mesma conexão —
    // a inspeção "concorrente" que ganha a corrida, exatamente no intervalo entre a consulta
    // inicial de "existente" do handler (que não viu nada) e o SaveChangesAsync dele (que já
    // encontra o índice único ocupado). A partir daí é o índice único real do Sqlite que rejeita a
    // inserção do handler — a DbUpdateException não é fabricada manualmente.
    [Fact]
    public async Task Handle_CorridaConcorrenteNoBanco_RecuperaERetornaInspecaoVencedoraSemPropagarExcecao()
    {
        using var conexao = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conexao.Open();
        var options = new DbContextOptionsBuilder<SstDbContext>().UseSqlite(conexao).Options;

        using var dbSetup = new SstDbContext(options, new CurrentUserService());
        dbSetup.Database.EnsureCreated();

        var obra = new Obra { Codigo = "OB1", Nome = "Obra Teste" };
        var usuario = new Usuario { Email = "tecnico@aahbrant.com", Nome = "Técnico Teste", AzureAdObjectId = AzureAdObjectId };
        var alojamento = new Alojamento { Nome = "Alojamento 01", ObraId = obra.Id };
        var checklist = new ChecklistModelo { Nome = "Checklist de Alojamento", TipoInspecao = TipoInspecao.Alojamento, Versao = 1 };
        checklist.Itens.Add(new ChecklistModeloItem { Descricao = "Extintor válido", ChecklistModeloId = checklist.Id });
        dbSetup.AddRange(obra, usuario, alojamento, checklist);
        await dbSetup.SaveChangesAsync();

        using var dbHandler = new SstDbContextComCorridaSimulada(options, new CurrentUserService())
        {
            AlojamentoIdParaCorrida = alojamento.Id,
            ObraIdParaCorrida = alojamento.ObraId,
            ChecklistModeloIdParaCorrida = checklist.Id,
            ResponsavelUsuarioIdParaCorrida = usuario.Id,
        };
        var handler = new ObterOuCriarInspecaoAlojamentoCommandHandler(dbHandler, new GeradorNumeroDocumentoService(dbHandler));

        var resultado = await handler.Handle(new ObterOuCriarInspecaoAlojamentoCommand(alojamento.Id, AzureAdObjectId), default);

        Assert.False(resultado.FoiCriadaAgora);
        var inspecoesDoAlojamento = await dbSetup.Inspecoes.Where(i => i.AlojamentoId == alojamento.Id).ToListAsync();
        Assert.Single(inspecoesDoAlojamento);
        Assert.Equal(inspecoesDoAlojamento[0].Id, resultado.InspecaoId);
        Assert.Equal("INSP-CONCORRENTE", inspecoesDoAlojamento[0].NumeroDocumento);
    }

    // Só para o teste acima: simula "a outra requisição já ganhou a corrida" na primeira chamada a
    // SaveChangesAsync, usando um DbContext próprio (mesma conexão Sqlite) para commitar de verdade
    // uma segunda inspeção em andamento do mesmo alojamento antes de delegar para o SaveChangesAsync
    // real desta instância — é aí que o índice único do Sqlite rejeita de verdade.
    private class SstDbContextComCorridaSimulada : SstDbContext
    {
        private readonly DbContextOptions<SstDbContext> _options;
        private readonly ICurrentUserService _usuarioAtual;
        private bool _corridaJaSimulada;

        public SstDbContextComCorridaSimulada(DbContextOptions<SstDbContext> options, ICurrentUserService usuarioAtual)
            : base(options, usuarioAtual)
        {
            _options = options;
            _usuarioAtual = usuarioAtual;
        }

        public Guid AlojamentoIdParaCorrida { get; set; }
        public Guid ObraIdParaCorrida { get; set; }
        public Guid ChecklistModeloIdParaCorrida { get; set; }
        public Guid ResponsavelUsuarioIdParaCorrida { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_corridaJaSimulada)
            {
                _corridaJaSimulada = true;
                using var dbConcorrente = new SstDbContext(_options, _usuarioAtual);
                dbConcorrente.Inspecoes.Add(new Inspecao
                {
                    TipoInspecao = TipoInspecao.Alojamento,
                    ObraId = ObraIdParaCorrida,
                    AlojamentoId = AlojamentoIdParaCorrida,
                    ChecklistModeloId = ChecklistModeloIdParaCorrida,
                    Data = DateTime.UtcNow,
                    ResponsavelUsuarioId = ResponsavelUsuarioIdParaCorrida,
                    NumeroDocumento = "INSP-CONCORRENTE",
                });
                await dbConcorrente.SaveChangesAsync(cancellationToken);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
