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
        var acao = Acao(c.Acidente, TipoResolucaoAcaoPlano.Inspecao, tipoInspecao: TipoInspecao.Andaimes, responsavel: c.Tecnico.Id);
        c.Db.Add(acao);
        var tec = c.Tecnico.Id;
        c.Db.AddRange(
            Inspecao(c.Obra.Id, TipoInspecao.Escadas, StatusInspecao.Concluida, tec),          // outro tipo
            Inspecao(Guid.NewGuid(), TipoInspecao.Andaimes, StatusInspecao.Concluida, tec),    // outra obra
            Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.EmAndamento, tec),       // não finalizada
            Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.Concluida),              // finalizada por OUTRA pessoa
            Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.Concluida, tec, DateTime.UtcNow.AddDays(-30))); // antes de a ação existir
        await c.Db.SaveChangesAsync();
        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));

        c.Db.Add(Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.Concluida, tec));
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
        var acao = Acao(c.Acidente, null, responsavel: c.Tecnico.Id);
        c.Db.Add(acao);
        c.Db.Add(Inspecao(c.Obra.Id, TipoInspecao.Andaimes, StatusInspecao.Concluida, c.Tecnico.Id, DateTime.UtcNow.AddMinutes(5)));
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
        var acao = Acao(c.Acidente, null, responsavel: c.Tecnico.Id);
        c.Db.Add(acao);
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirResolucaoAcaoPlanoCommandHandler(c.Db).Handle(
            new DefinirResolucaoAcaoPlanoCommand(acao.Id, TipoResolucaoAcaoPlano.Treinamento, null, null, null), default));
    }

    // 24/10/2026: acabou o "Manual" — toda ação da ocorrência se resolve por treinamento, inspeção ou EPI.
    [Fact]
    public async Task DefinirResolucao_SemVinculo_Recusa()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, null, responsavel: c.Tecnico.Id);
        c.Db.Add(acao);
        await c.Db.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirResolucaoAcaoPlanoCommandHandler(c.Db).Handle(
            new DefinirResolucaoAcaoPlanoCommand(acao.Id, null, null, null, null), default));
        Assert.Contains("não existe mais", erro.Message);
    }

    [Fact]
    public async Task DefinirResolucao_SemResponsavel_Recusa()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, null);
        c.Db.Add(acao);
        await c.Db.SaveChangesAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirResolucaoAcaoPlanoCommandHandler(c.Db).Handle(
            new DefinirResolucaoAcaoPlanoCommand(acao.Id, TipoResolucaoAcaoPlano.Inspecao, null, TipoInspecao.Andaimes, null), default));
        Assert.Contains("responsável", erro.Message);
    }

    [Fact]
    public async Task DefinirResolucao_TemaSemTema_Recusa()
    {
        var c = await CenarioAsync();
        var acao = Acao(c.Acidente, null, responsavel: c.Tecnico.Id);
        c.Db.Add(acao);
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirResolucaoAcaoPlanoCommandHandler(c.Db).Handle(
            new DefinirResolucaoAcaoPlanoCommand(acao.Id, TipoResolucaoAcaoPlano.Inspecao, null, TipoInspecao.Tema, null, null), default));
    }

    [Fact]
    public async Task CriarAcao_Ocorrencia_ExigeResponsavelEVinculo()
    {
        var c = await CenarioAsync();
        var criar = new CriarAcaoPlanoCommandHandler(c.Db);
        CriarAcaoPlanoCommand Comando(Guid? responsavel, TipoResolucaoAcaoPlano? tipo) => new(
            nameof(Acidente), c.Acidente.Id, TipoAcaoPlano.Preventiva, "Inspecionar andaime", responsavel, PrioridadeAcao.Alta, null,
            tipo, null, TipoInspecao.Andaimes);

        await Assert.ThrowsAsync<InvalidOperationException>(() => criar.Handle(Comando(null, TipoResolucaoAcaoPlano.Inspecao), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => criar.Handle(Comando(c.Tecnico.Id, null), default));

        var id = await criar.Handle(Comando(c.Tecnico.Id, TipoResolucaoAcaoPlano.Inspecao), default);
        var salva = await c.Db.AcoesPlano.SingleAsync(a => a.Id == id);
        Assert.Equal(c.Tecnico.Id, salva.ResponsavelUsuarioId);
        Assert.Equal(StatusControleRisco.Pendente, salva.Status);
    }

    // O tema do usuário (ex.: Andaime): só vale a inspeção de um item DAQUELE tema, finalizada pelo responsável.
    [Fact]
    public async Task InspecaoDeTema_SoConcluiComItemDoTemaFinalizadoPeloResponsavel()
    {
        var c = await CenarioAsync();
        var andaime = new TemaInspecao { Nome = "Andaime" };
        var escada = new TemaInspecao { Nome = "Escada" };
        var itemAndaime = new ItemInspecionavel { TemaInspecaoId = andaime.Id, ObraId = c.Obra.Id, Identificacao = "A-03" };
        var itemEscada = new ItemInspecionavel { TemaInspecaoId = escada.Id, ObraId = c.Obra.Id, Identificacao = "E-01" };
        c.Db.AddRange(andaime, escada, itemAndaime, itemEscada);
        var acao = Acao(c.Acidente, TipoResolucaoAcaoPlano.Inspecao, tipoInspecao: TipoInspecao.Tema, responsavel: c.Tecnico.Id, tema: andaime.Id);
        c.Db.Add(acao);
        var daEscada = Inspecao(c.Obra.Id, TipoInspecao.Tema, StatusInspecao.Concluida, c.Tecnico.Id);
        daEscada.ItemInspecionavelId = itemEscada.Id;
        var deOutroTecnico = Inspecao(c.Obra.Id, TipoInspecao.Tema, StatusInspecao.Concluida);
        deOutroTecnico.ItemInspecionavelId = itemAndaime.Id;
        c.Db.AddRange(daEscada, deOutroTecnico);
        await c.Db.SaveChangesAsync();

        Assert.Equal(0, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
        var aguardando = (await ResolucaoVinculadaAcaoPlano.AvaliarAsync(c.Db, new[] { c.Acidente.Id }, default))[acao.Id];
        Assert.Contains("Gilton", aguardando.Aguardando);
        Assert.Contains("Andaime", aguardando.Aguardando);

        var certa = Inspecao(c.Obra.Id, TipoInspecao.Tema, StatusInspecao.Concluida, c.Tecnico.Id);
        certa.ItemInspecionavelId = itemAndaime.Id;
        c.Db.Add(certa);
        await c.Db.SaveChangesAsync();

        Assert.Equal(1, await ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(c.Db, new[] { c.Acidente.Id }, default));
    }

    private sealed record Cenario(SstDbContext Db, Obra Obra, Acidente Acidente, Guid[] Envolvidos, Usuario Tecnico);

    private static async Task<Cenario> CenarioAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var a = new Trabalhador { Nome = "Ana", ObraId = obra.Id };
        var b = new Trabalhador { Nome = "Bruno", ObraId = obra.Id };
        var acidente = new Acidente { Tipo = TipoOcorrencia.QuaseAcidente, ObraId = obra.Id, Local = "Bloco B", Data = DataOcorrencia, Descricao = "Peça caiu" };
        var tecnico = new Usuario { Nome = "Gilton", Email = "gilton@x.com" };
        db.AddRange(obra, a, b, acidente, tecnico,
            new AcidenteEnvolvido { AcidenteId = acidente.Id, TrabalhadorId = a.Id },
            new AcidenteEnvolvido { AcidenteId = acidente.Id, TrabalhadorId = b.Id });
        await db.SaveChangesAsync();
        return new Cenario(db, obra, acidente, new[] { a.Id, b.Id }, tecnico);
    }

    private static AcaoPlano Acao(Acidente acidente, TipoResolucaoAcaoPlano? tipo, Guid? curso = null, TipoInspecao? tipoInspecao = null, Guid? epi = null, Guid? responsavel = null, Guid? tema = null) => new()
    {
        OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Tipo = TipoAcaoPlano.Corretiva, Descricao = "Ação", Prioridade = PrioridadeAcao.Alta,
        TipoResolucao = tipo, ResolucaoCursoTreinamentoId = curso, ResolucaoTipoInspecao = tipoInspecao, ResolucaoCatalogoEpiId = epi,
        ResponsavelUsuarioId = responsavel, ResolucaoTemaInspecaoId = tema,
    };

    private static void Turma(IAppDbContext db, Guid cursoId, Guid obraId, DateTime data, IEnumerable<Guid> presentes)
    {
        var sessao = new SessaoTreinamento { CursoTreinamentoId = cursoId, ObraId = obraId, DataRealizacao = data, Status = StatusSessaoTreinamento.Concluida };
        db.SessoesTreinamento.Add(sessao);
        foreach (var t in presentes)
            db.ParticipantesSessaoTreinamento.Add(new ParticipanteSessaoTreinamento { SessaoTreinamentoId = sessao.Id, TrabalhadorId = t, PresencaConfirmadaEm = data });
    }

    // Data padrão: daqui a pouco — depois de a ação do plano existir, que é quando a inspeção passa a valer.
    private static Inspecao Inspecao(Guid obraId, TipoInspecao tipo, StatusInspecao status, Guid? responsavel = null, DateTime? data = null) => new()
    {
        ObraId = obraId, TipoInspecao = tipo, Status = status, Data = data ?? DateTime.UtcNow.AddMinutes(5),
        ChecklistModeloId = Guid.NewGuid(), ResponsavelUsuarioId = responsavel ?? Guid.NewGuid(),
    };

    private static void Assinar(IAppDbContext db, Guid entregaId, Guid trabalhadorId)
    {
        var doc = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entregaId };
        doc.Signatarios.Add(new DocumentoSignatario { DocumentoAssinaturaId = doc.Id, TrabalhadorId = trabalhadorId, AssinadoEm = DataOcorrencia.AddDays(1) });
        db.DocumentosAssinatura.Add(doc);
    }
}
