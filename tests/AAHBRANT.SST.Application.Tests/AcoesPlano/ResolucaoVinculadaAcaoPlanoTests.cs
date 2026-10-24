using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.AcoesPlano.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.AcoesPlano;

// Etapa 2 (23/10/2026): ação vinculada só se resolve com o fato daquele item — turma do curso
// escolhido com os envolvidos, inspeção do tipo na obra, entrega do EPI assinada.
public class ResolucaoVinculadaAcaoPlanoTests
{
    private static readonly DateTime DataOcorrencia = new(2026, 10, 21);

    [Fact]
    public async Task Treinamento_SoConcluiComTurmaDoCursoEscolhidoETodosEnvolvidosPresentes()
    {
        var c = await CenarioAsync();
        var nr35 = new CursoTreinamento { Nome = "NR-35" };
        var nr18 = new CursoTreinamento { Nome = "NR-18" };
        c.Db.AddRange(nr35, nr18);
        var acao = Acao(c.Acidente, TipoResolucaoAcaoPlano.Treinamento, curso: nr35.Id);
        c.Db.Add(acao);
        // Turma de OUTRO curso com os dois envolvidos: não conta.
        Turma(c.Db, nr18.Id, c.Obra.Id, DataOcorrencia.AddDays(1), c.Envolvidos);
        // Turma do curso certo, mas só com um envolvido.
        Turma(c.Db, nr35.Id, c.Obra.Id, DataOcorrencia.AddDays(2), new[] { c.Envolvidos[0] });
        await c.Db.SaveChangesAsync();

        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
        var avaliacao = (await ResolucaoVinculadaAcaoPlano.AvaliarAsync(c.Db, new[] { c.Acidente.Id }, default))[acao.Id];
        Assert.Equal("1 de 2 envolvido(s) treinado(s)", avaliacao.Progresso);

        Turma(c.Db, nr35.Id, c.Obra.Id, DataOcorrencia.AddDays(3), new[] { c.Envolvidos[1] });
        await c.Db.SaveChangesAsync();

        Assert.Equal(1, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
        var salva = await c.Db.AcoesPlano.SingleAsync(a => a.Id == acao.Id);
        Assert.Equal(StatusControleRisco.Concluido, salva.Status);
        Assert.StartsWith("Concluída automaticamente ao concluir uma turma de NR-35", salva.DescricaoConclusao);
    }

    [Fact]
    public async Task Treinamento_TurmaAntesDaOcorrencia_NaoConta()
    {
        var c = await CenarioAsync();
        var nr35 = new CursoTreinamento { Nome = "NR-35" };
        c.Db.Add(nr35);
        c.Db.Add(Acao(c.Acidente, TipoResolucaoAcaoPlano.Treinamento, curso: nr35.Id));
        Turma(c.Db, nr35.Id, c.Obra.Id, DataOcorrencia.AddDays(-5), c.Envolvidos);
        await c.Db.SaveChangesAsync();

        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
    }

    [Fact]
    public async Task Treinamento_SemEnvolvidos_SoTurmaDestaObra()
    {
        var c = await CenarioAsync();
        c.Db.AcidentesEnvolvidos.RemoveRange(c.Db.AcidentesEnvolvidos);
        var nr35 = new CursoTreinamento { Nome = "NR-35" };
        c.Db.Add(nr35);
        c.Db.Add(Acao(c.Acidente, TipoResolucaoAcaoPlano.Treinamento, curso: nr35.Id));
        Turma(c.Db, nr35.Id, Guid.NewGuid(), DataOcorrencia.AddDays(1), Array.Empty<Guid>()); // outra obra
        await c.Db.SaveChangesAsync();
        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));

        Turma(c.Db, nr35.Id, c.Obra.Id, DataOcorrencia.AddDays(1), Array.Empty<Guid>());
        await c.Db.SaveChangesAsync();
        Assert.Equal(1, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
    }

    [Fact]
    public async Task Inspecao_SoConcluiComTipoCertoNaObraEFinalizada()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, TipoResolucaoAcaoPlano.Inspecao, tipoInspecao: TipoInspecao.Andaimes);
        c.Db.Add(acao);
        c.Db.AddRange(
            Inspecao(c.Obra.Id, TipoInspecao.Escadas, StatusInspecao.Concluida),          // outro tipo
            Inspecao(Guid.NewGuid(), TipoInspecao.Andaimes, StatusInspecao.Concluida),    // outra obra
            Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.EmAndamento));      // não finalizada
        await c.Db.SaveChangesAsync();
        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));

        c.Db.Add(Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.Concluida));
        await c.Db.SaveChangesAsync();
        Assert.Equal(1, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
    }

    [Fact]
    public async Task EntregaEpi_SoConcluiComEntregaAssinadaParaTodosEnvolvidos()
    {
        var c = await CenarioAsync();
        var cinto = new CatalogoEpi { Nome = "Cinto paraquedista" };
        c.Db.Add(cinto);
        var acao = Acao(c.Acidente, TipoResolucaoAcaoPlano.EntregaEpi, epi: cinto.Id);
        c.Db.Add(acao);
        var e1 = new EntregaEpi { TrabalhadorId = c.Envolvidos[0], CatalogoEpiId = cinto.Id, DataEntrega = DataOcorrencia.AddDays(1) };
        var e2 = new EntregaEpi { TrabalhadorId = c.Envolvidos[1], CatalogoEpiId = cinto.Id, DataEntrega = DataOcorrencia.AddDays(1) };
        c.Db.AddRange(e1, e2);
        Assinar(c.Db, e1.Id, c.Envolvidos[0]);
        await c.Db.SaveChangesAsync();

        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));

        Assinar(c.Db, e2.Id, c.Envolvidos[1]);
        await c.Db.SaveChangesAsync();
        Assert.Equal(1, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
    }

    [Fact]
    public async Task ConcluirManual_AcaoVinculada_Recusa()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, TipoResolucaoAcaoPlano.Inspecao, tipoInspecao: TipoInspecao.Andaimes);
        c.Db.Add(acao);
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConcluirAcaoPlanoCommandHandler(c.Db).Handle(new ConcluirAcaoPlanoCommand(acao.Id, "feito", null), default));
    }

    [Fact]
    public async Task DefinirResolucao_FatoJaAconteceu_ConcluiNaHora()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, null);
        c.Db.Add(acao);
        c.Db.Add(Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.Concluida));
        await c.Db.SaveChangesAsync();

        await new DefinirResolucaoAcaoPlanoCommandHandler(c.Db).Handle(
            new DefinirResolucaoAcaoPlanoCommand(acao.Id, TipoResolucaoAcaoPlano.Inspecao, null, TipoInspecao.Andaimes, null), default);

        var salva = await c.Db.AcoesPlano.SingleAsync(a => a.Id == acao.Id);
        Assert.Equal(TipoResolucaoAcaoPlano.Inspecao, salva.TipoResolucao);
        Assert.Equal(StatusControleRisco.Concluido, salva.Status);
    }

    [Fact]
    public async Task DefinirResolucao_TreinamentoSemCurso_Recusa()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, null);
        c.Db.Add(acao);
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirResolucaoAcaoPlanoCommandHandler(c.Db).Handle(
            new DefinirResolucaoAcaoPlanoCommand(acao.Id, TipoResolucaoAcaoPlano.Treinamento, null, null, null), default));
    }

    private sealed record Cenario(SstDbContext Db, Obra Obra, Acidente Acidente, Guid[] Envolvidos);

    private static async Task<Cenario> CenarioAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var a = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        var b = new Trabalhador { Nome = "Bruno", ObraId = obra.Id };
        var acidente = new Acidente { Tipo = TipoOcorrencia.QuaseAcidente, ObraId = obra.Id, Local = "Bloco B", Data = DataOcorrencia, Descricao = "Peça caiu" };
        db.AddRange(obra, a, b, acidente,
            new AcidenteEnvolvido { AcidenteId = acidente.Id, TrabalhadorId = a.Id },
            new AcidenteEnvolvido { AcidenteId = acidente.Id, TrabalhadorId = b.Id });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, acidente, new[] { a.Id, b.Id });
    }

    private static AcaoPlano Acao(Acidente acidente, TipoResolucaoAcaoPlano? tipo, Guid? curso = null, TipoInspecao? tipoInspecao = null, Guid? epi = null) => new()
    {
        OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Tipo = TipoAcaoPlano.Corretiva, Descricao = "Ação", Prioridade = PrioridadeAcao.Alta,
        TipoResolucao = tipo, ResolucaoCursoTreinamentoId = curso, ResolucaoTipoInspecao = tipoInspecao, ResolucaoCatalogoEpiId = epi,
    };

    private static void Turma(IAppDbContext db, Guid cursoId, Guid obraId, DateTime data, IEnumerable<Guid> presentes)
    {
        var sessao = new SessaoTreinamento { CursoTreinamentoId = cursoId, ObraId = obraId, DataRealizacao = data, Status = StatusSessaoTreinamento.Concluida };
        db.SessoesTreinamento.Add(sessao);
        foreach (var t in presentes)
            db.ParticipantesSessaoTreinamento.Add(new ParticipanteSessaoTreinamento { SessaoTreinamentoId = sessao.Id, TrabalhadorId = t, PresencaConfirmadaEm = data });
    }

    private static Inspecao Inspecao(Guid obraId, TipoInspecao tipo, StatusInspecao status) => new()
    {
        ObraId = obraId, TipoInspecao = tipo, Status = status, Data = DataOcorrencia.AddDays(1),
        ChecklistModeloId = Guid.NewGuid(), ResponsavelUsuarioId = Guid.NewGuid(),
    };

    private static void Assinar(IAppDbContext db, Guid entregaId, Guid trabalhadorId)
    {
        var doc = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entregaId };
        doc.Signatarios.Add(new DocumentoSignatario { DocumentoAssinaturaId = doc.Id, TrabalhadorId = trabalhadorId, AssinadoEm = DataOcorrencia.AddDays(1) });
        db.DocumentosAssinatura.Add(doc);
    }
}
