using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Application.Trabalhadores.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Trabalhadores;

// Auditoria 12/11/2026: o cadastro só guardava a função atual. A troca passa a deixar uma linha de
// histórico, e a ficha de EPI e a OS perguntam "qual era a função em tal data".
public class HistoricoFuncaoTests
{
    static HistoricoFuncaoTests() => ChavesCpfDeTeste.Configurar();

    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => Task.CompletedTask;
        public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => Task.CompletedTask;
        public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => Task.FromResult<ResultadoMigracaoModeloFacial?>(null);
        public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = false)
            => throw new NotSupportedException();
    }

    private static async Task<(SstDbContext Db, Trabalhador Trabalhador, Funcao Pedreiro, Funcao Carpinteiro)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var pedreiro = new Funcao { Nome = "Pedreiro" };
        var carpinteiro = new Funcao { Nome = "Carpinteiro" };
        db.Obras.Add(obra);
        db.Funcoes.AddRange(pedreiro, carpinteiro);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id, FuncaoId = pedreiro.Id, Nome = "Fulano", Matricula = "001",
            Cpf = "52998224725", DataAdmissao = DateTime.UtcNow.AddYears(-1),
        };
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();
        return (db, trabalhador, pedreiro, carpinteiro);
    }

    private static Task AtualizarAsync(SstDbContext db, Trabalhador t, Guid funcaoId) =>
        new AtualizarTrabalhadorCommandHandler(db, new FacialFalso(), NullLogger<AtualizarTrabalhadorCommandHandler>.Instance)
            .Handle(new AtualizarTrabalhadorCommand(t.Id, t.ObraId, null, null, funcaoId, t.Nome, t.Matricula!, t.Cpf,
                TipoVinculo.Clt, t.DataAdmissao, null, null), default);

    [Fact]
    public async Task Atualizar_TrocandoFuncao_GravaUmaLinhaDeHistorico()
    {
        var (db, trabalhador, pedreiro, carpinteiro) = await PrepararAsync();
        var antes = DateTime.UtcNow;

        await AtualizarAsync(db, trabalhador, carpinteiro.Id);

        var historico = await db.HistoricosFuncaoTrabalhador.SingleAsync();
        Assert.Equal(trabalhador.Id, historico.TrabalhadorId);
        Assert.Equal(pedreiro.Id, historico.FuncaoAnteriorId);
        Assert.Equal(carpinteiro.Id, historico.FuncaoNovaId);
        Assert.InRange(historico.AlteradaEm, antes, DateTime.UtcNow);
    }

    [Fact]
    public async Task Atualizar_SemTrocarFuncao_NaoGravaHistorico()
    {
        var (db, trabalhador, pedreiro, _) = await PrepararAsync();

        await AtualizarAsync(db, trabalhador, pedreiro.Id);

        Assert.False(await db.HistoricosFuncaoTrabalhador.AnyAsync());
    }

    [Fact]
    public async Task Atualizar_TrocandoFuncao_NaoApagaTreinamentosNemEntregasDeEpi()
    {
        var (db, trabalhador, _, carpinteiro) = await PrepararAsync();
        var curso = new CursoTreinamento { Nome = "NR-35" };
        var epi = new CatalogoEpi { Nome = "Capacete" };
        db.CursosTreinamento.Add(curso);
        db.CatalogoEpis.Add(epi);
        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhador.Id, CursoTreinamentoId = curso.Id,
            DataRealizacao = DateTime.UtcNow.AddMonths(-1), DataValidade = DateTime.UtcNow.AddYears(1),
        });
        db.EntregasEpi.Add(new EntregaEpi
        {
            TrabalhadorId = trabalhador.Id, CatalogoEpiId = epi.Id, Quantidade = 1,
            DataEntrega = DateTime.UtcNow.AddMonths(-1), Confirmada = true,
        });
        await db.SaveChangesAsync();

        await AtualizarAsync(db, trabalhador, carpinteiro.Id);

        Assert.Equal(1, await db.Treinamentos.CountAsync(t => t.TrabalhadorId == trabalhador.Id));
        Assert.Equal(1, await db.EntregasEpi.CountAsync(e => e.TrabalhadorId == trabalhador.Id));
    }

    [Fact]
    public void FuncaoNaData_SemTroca_DevolveFuncaoAtual()
    {
        var trabalhador = Guid.NewGuid();
        var atual = Guid.NewGuid();

        var funcao = HistoricoFuncao.FuncaoNaData(Array.Empty<HistoricoFuncao.Troca>(), trabalhador, atual, DateTime.UtcNow);

        Assert.Equal(atual, funcao);
    }

    [Fact]
    public void FuncaoNaData_ComDuasTrocas_DevolveAFuncaoDeCadaPeriodo()
    {
        var trabalhador = Guid.NewGuid();
        var (pedreiro, carpinteiro, encarregado) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var primeiraTroca = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var segundaTroca = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var trocas = new[]
        {
            // Fora de ordem de propósito, e com a troca de outro trabalhador no meio.
            new HistoricoFuncao.Troca(trabalhador, carpinteiro, segundaTroca),
            new HistoricoFuncao.Troca(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
            new HistoricoFuncao.Troca(trabalhador, pedreiro, primeiraTroca),
        };

        Guid Em(DateTime data) => HistoricoFuncao.FuncaoNaData(trocas, trabalhador, encarregado, data);

        Assert.Equal(pedreiro, Em(primeiraTroca.AddDays(-30)));
        Assert.Equal(carpinteiro, Em(primeiraTroca.AddDays(30)));
        Assert.Equal(carpinteiro, Em(segundaTroca.AddSeconds(-1)));
        Assert.Equal(encarregado, Em(segundaTroca.AddDays(1)));
    }
}
