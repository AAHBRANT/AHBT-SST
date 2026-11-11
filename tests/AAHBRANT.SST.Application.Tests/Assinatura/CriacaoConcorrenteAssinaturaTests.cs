using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Vários tablets no mesmo DDS: duas requisições passam juntas pela checagem de "já existe" e só o
// índice único segura a segunda. SQLite em memória porque o InMemory do EF não aplica índice único.
// Chaves estrangeiras desligadas: o que se testa é a unicidade, não o cadastro em volta.
public class CriacaoConcorrenteAssinaturaTests : IDisposable
{
    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<SstDbContext> _options;

    public CriacaoConcorrenteAssinaturaTests()
    {
        _conexao = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _conexao.Open();
        _conexao.CreateCollation("Latin1_General_100_BIN2", (a, b) => string.Compare(a, b, StringComparison.Ordinal));
        _options = new DbContextOptionsBuilder<SstDbContext>().UseSqlite(_conexao).Options;
        using var db = new SstDbContext(_options, new CurrentUserService());
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    [Fact]
    public async Task Documento_criado_por_outra_requisicao_no_mesmo_instante_e_reaproveitado()
    {
        var entidadeId = Guid.NewGuid();
        Guid idConcorrente = Guid.Empty;
        using var db = new ContextoComCorrida(_options, async () =>
        {
            using var outro = new SstDbContext(_options, new CurrentUserService());
            var doc = new DocumentoAssinatura { EntidadeTipo = "Dds", EntidadeId = entidadeId };
            outro.DocumentosAssinatura.Add(doc);
            await outro.SaveChangesAsync();
            idConcorrente = doc.Id;
        });

        var documento = await DocumentoAssinaturaUnico.ObterOuCriarAsync(db, "Dds", entidadeId, default);

        Assert.Equal(idConcorrente, documento.Id);
        // O mesmo contexto segue gravando (a assinatura vem logo depois) sem tentar inserir o duplicado de novo.
        await db.SaveChangesAsync();
        using var conferencia = new SstDbContext(_options, new CurrentUserService());
        Assert.Equal(1, await conferencia.DocumentosAssinatura.CountAsync(d => d.EntidadeId == entidadeId));
    }

    [Fact]
    public async Task Documento_inexistente_e_criado_uma_vez()
    {
        var entidadeId = Guid.NewGuid();
        using var db = new SstDbContext(_options, new CurrentUserService());

        var primeiro = await DocumentoAssinaturaUnico.ObterOuCriarAsync(db, "Treinamento", entidadeId, default);
        var segundo = await DocumentoAssinaturaUnico.ObterOuCriarAsync(db, "Treinamento", entidadeId, default);

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Equal(1, await db.DocumentosAssinatura.CountAsync(d => d.EntidadeId == entidadeId));
    }

    [Fact]
    public async Task Documento_desativado_nao_impede_um_novo()
    {
        var entidadeId = Guid.NewGuid();
        using (var antigo = new SstDbContext(_options, new CurrentUserService()))
        {
            antigo.DocumentosAssinatura.Add(new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entidadeId, Ativo = false });
            await antigo.SaveChangesAsync();
        }
        using var db = new SstDbContext(_options, new CurrentUserService());

        var documento = await DocumentoAssinaturaUnico.ObterOuCriarAsync(db, "EntregaEpi", entidadeId, default);

        Assert.True(documento.Ativo);
    }

    [Fact]
    public async Task Presenca_confirmada_em_outro_tablet_no_mesmo_instante_nao_duplica()
    {
        var ddsId = Guid.NewGuid();
        var trabalhadorId = Guid.NewGuid();
        using var db = new ContextoComCorrida(_options, async () =>
        {
            using var outro = new SstDbContext(_options, new CurrentUserService());
            outro.DdsParticipantes.Add(new DdsParticipante { DdsId = ddsId, TrabalhadorId = trabalhadorId });
            await outro.SaveChangesAsync();
        });
        var participante = new DdsParticipante { DdsId = ddsId, TrabalhadorId = trabalhadorId };
        db.DdsParticipantes.Add(participante);

        var gravou = await PresencaDdsUnica.GravarAsync(db, participante, Array.Empty<DdsFuncionarioSelecionado>(), default);

        Assert.False(gravou);
        await db.SaveChangesAsync();
        using var conferencia = new SstDbContext(_options, new CurrentUserService());
        Assert.Equal(1, await conferencia.DdsParticipantes.CountAsync(p => p.DdsId == ddsId && p.TrabalhadorId == trabalhadorId));
    }

    [Fact]
    public async Task Presenca_sem_concorrencia_e_gravada()
    {
        using var db = new SstDbContext(_options, new CurrentUserService());
        var participante = new DdsParticipante { DdsId = Guid.NewGuid(), TrabalhadorId = Guid.NewGuid() };
        db.DdsParticipantes.Add(participante);

        Assert.True(await PresencaDdsUnica.GravarAsync(db, participante, Array.Empty<DdsFuncionarioSelecionado>(), default));
    }

    // Roda a "outra requisição" logo antes da primeira gravação deste contexto.
    private sealed class ContextoComCorrida : SstDbContext
    {
        private readonly Func<Task> _corrida;
        private bool _jaCorreu;

        public ContextoComCorrida(DbContextOptions<SstDbContext> options, Func<Task> corrida)
            : base(options, new CurrentUserService()) => _corrida = corrida;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_jaCorreu)
            {
                _jaCorreu = true;
                await _corrida();
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
