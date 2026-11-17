using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Acidentes.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

// PR 1 de "pessoa externa envolvida / testemunha / aguardando laudo / 3 fotos em todos os tipos".
public class OcorrenciaPessoaExternaTests
{
    private const string CpfValido = "52998224725";

    // ---------- Tipo OcorrenciaComPessoaExterna ----------

    [Fact]
    public async Task Criar_Tipo7_SemPessoaExterna_Recusa()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var handler = new CriarAcidenteCommandHandler(db, new PublicadorFalso());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna), default));
        Assert.Contains("pessoa externa", ex.Message);
    }

    [Fact]
    public async Task Criar_Tipo7_ComFuncionarioEnvolvido_Recusa_MasAceitaTestemunha()
    {
        var (db, obra, joao, maria) = await CenarioAsync();
        var handler = new CriarAcidenteCommandHandler(db, new PublicadorFalso());
        var pessoa = new List<PessoaExternaEntrada> { new(null, "Carlos Visitante", VinculoPessoaExterna.Visitante) };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = pessoa,
                TrabalhadoresIds = new List<Guid> { joao.Id },
            }, default));

        var id = await handler.Handle(Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
        {
            PessoasExternas = pessoa,
            Testemunhas = new List<Guid> { maria.Id },
        }, default);

        var vinculo = await db.AcidentesEnvolvidos.SingleAsync(e => e.AcidenteId == id);
        Assert.Equal(maria.Id, vinculo.TrabalhadorId);
        Assert.Equal(PapelEnvolvido.Testemunha, vinculo.Papel);
        Assert.Null((await db.Acidentes.SingleAsync(a => a.Id == id)).TrabalhadorId);
    }

    [Fact]
    public async Task Criar_Tipo7_ForcaGravidadeSemAfastamentoEZeraCat()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var handler = new CriarAcidenteCommandHandler(db, new PublicadorFalso());

        var id = await handler.Handle(Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
        {
            PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Terceiro, Resultado: ResultadoPessoaExterna.Obito) },
            Gravidade = GravidadeAcidente.Obito,
            HouveAfastamento = true,
            DiasAfastamento = 30,
            NumeroCat = "CAT-1",
        }, default);

        var a = await db.Acidentes.SingleAsync(x => x.Id == id);
        Assert.Equal(GravidadeAcidente.SemAfastamento, a.Gravidade);
        Assert.Equal(0, a.DiasDebitados);
        Assert.False(a.HouveAfastamento);
        Assert.Null(a.DiasAfastamento);
        Assert.Null(a.NumeroCat);
    }

    [Fact]
    public async Task Atualizar_Tipo7_SemEnviarPessoas_MantemAsGravadas_EListaVaziaRecusa()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Visitante) },
            }, default);
        var atualizar = new AtualizarAcidenteCommandHandler(db, new PublicadorFalso());

        await atualizar.Handle(Atualizar(id, obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna), default);
        Assert.Single(await db.AcidentesPessoasExternas.ToListAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            atualizar.Handle(Atualizar(id, obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada>(),
            }, default));
    }

    // ---------- LGPD: máscara no DTO e consulta auditada ----------

    [Fact]
    public async Task Detalhe_DevolveCpfETelefoneMascarados_NuncaEmClaro()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada>
                {
                    new(null, "Carlos", VinculoPessoaExterna.Visitante, Cpf: "529.982.247-25", Telefone: "(11) 98765-4321"),
                },
            }, default);

        var detalhe = await new ObterAcidenteDetalheQueryHandler(db).Handle(new ObterAcidenteDetalheQuery(id), default);

        var p = Assert.Single(detalhe.Acidente.PessoasExternas);
        Assert.Equal("***.***.***-25", p.CpfMascarado);
        Assert.EndsWith("4321", p.TelefoneMascarado);
        Assert.DoesNotContain("98765", p.TelefoneMascarado);
        Assert.DoesNotContain(CpfValido, System.Text.Json.JsonSerializer.Serialize(detalhe));

        // No banco o valor está gravado só com dígitos (e cifrado pelo conversor em SQL Server).
        Assert.Equal(CpfValido, (await db.AcidentesPessoasExternas.SingleAsync()).Cpf);
    }

    [Fact]
    public async Task Atualizar_ReenviandoValorMascarado_MantemOCpfGravado()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Visitante, Cpf: CpfValido) },
            }, default);
        var pessoaId = (await db.AcidentesPessoasExternas.SingleAsync()).Id;

        await new AtualizarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Atualizar(id, obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada>
                {
                    new(pessoaId, "Carlos Alberto", VinculoPessoaExterna.Visitante, Cpf: "***.***.***-25"),
                },
            }, default);

        var p = await db.AcidentesPessoasExternas.SingleAsync();
        Assert.Equal("Carlos Alberto", p.Nome);
        Assert.Equal(CpfValido, p.Cpf);
    }

    [Fact]
    public void Validador_RecusaCpfInvalido_EAceitaMascarado()
    {
        var validador = new PessoaExternaEntradaValidator();
        Assert.False(validador.Validate(new PessoaExternaEntrada(null, "X", VinculoPessoaExterna.Outro, Cpf: "11111111111")).IsValid);
        Assert.False(validador.Validate(new PessoaExternaEntrada(null, "", VinculoPessoaExterna.Outro)).IsValid);
        Assert.True(validador.Validate(new PessoaExternaEntrada(null, "X", VinculoPessoaExterna.Outro, Cpf: CpfValido)).IsValid);
        Assert.True(validador.Validate(new PessoaExternaEntrada(Guid.NewGuid(), "X", VinculoPessoaExterna.Outro, Cpf: "***.***.***-25")).IsValid);
    }

    [Fact]
    public async Task ObterDadosPessoais_DevolveEmClaro_EGravaAuditoriaSemODado()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Visitante, Cpf: CpfValido, Telefone: "11987654321") },
            }, default);
        var pessoaId = (await db.AcidentesPessoasExternas.SingleAsync()).Id;
        var usuario = Guid.NewGuid();
        var auditoria = new AuditoriaFalsa();

        var dados = await new ObterDadosPessoaisPessoaExternaQueryHandler(db, auditoria).Handle(
            new ObterDadosPessoaisPessoaExternaQuery(id, pessoaId, usuario), default);

        Assert.Equal(CpfValido, dados.Cpf);
        Assert.Equal("11987654321", dados.Telefone);
        var registro = Assert.Single(auditoria.Registros);
        Assert.Equal("VerDadosPessoaisPessoaExterna", registro.Acao);
        Assert.Equal(pessoaId, registro.EntidadeId);
        Assert.Equal(usuario, registro.UsuarioId);
        Assert.DoesNotContain(CpfValido, System.Text.Json.JsonSerializer.Serialize(registro.Dados));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ObterDadosPessoaisPessoaExternaQueryHandler(db, auditoria).Handle(
                new ObterDadosPessoaisPessoaExternaQuery(Guid.NewGuid(), pessoaId, usuario), default));
    }

    // ---------- Evento G-RH ----------

    [Fact]
    public async Task EventoGrh_SoPapelEnvolvido_TestemunhaNaoEntra()
    {
        var (db, obra, joao, maria) = await CenarioAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Criar(obra.Id, TipoOcorrencia.Acidente) with
            {
                TrabalhadoresIds = new List<Guid> { joao.Id },
                Testemunhas = new List<Guid> { maria.Id },
            }, default);

        var evento = await AcidenteGrhEventoFactory.CriarAsync(db, await db.Acidentes.SingleAsync(a => a.Id == id), default);

        Assert.NotNull(evento);
        Assert.Equal(joao.Cpf, evento!.TrabalhadorCpf);
        Assert.Equal(new[] { joao.Cpf }, evento.TrabalhadoresCpf);
    }

    [Fact]
    public async Task EventoGrh_Tipo7SemFuncionarioEnvolvido_NaoPublica()
    {
        var (db, obra, _, maria) = await CenarioAsync();
        var publicador = new PublicadorFalso();
        var id = await new CriarAcidenteCommandHandler(db, publicador).Handle(
            Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
            {
                PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Visitante) },
                Testemunhas = new List<Guid> { maria.Id },
            }, default);

        Assert.Null(await AcidenteGrhEventoFactory.CriarAsync(db, await db.Acidentes.SingleAsync(a => a.Id == id), default));
        Assert.Empty(publicador.Eventos);
    }

    // ---------- Testemunha x envolvido ----------

    [Fact]
    public async Task Sincronizar_TestemunhasNulo_MantemTestemunhas_ELinhaDoMesmoFuncionarioMudaDePapel()
    {
        var (db, obra, joao, maria) = await CenarioAsync();
        var acidente = new Acidente { ObraId = obra.Id, Local = "x", Descricao = "y" };
        db.Acidentes.Add(acidente);
        await db.SaveChangesAsync();

        await AcidenteEnvolvidosSync.SincronizarAsync(db, acidente, new List<Guid> { joao.Id }, default, new List<Guid> { maria.Id });
        await db.SaveChangesAsync();
        await AcidenteEnvolvidosSync.SincronizarAsync(db, acidente, new List<Guid> { joao.Id }, default); // não manda testemunhas
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.AcidentesEnvolvidos.CountAsync());
        Assert.Equal(joao.Id, acidente.TrabalhadorId);

        // Maria passa de testemunha a envolvida: mesma linha, papel novo, e o índice único não é violado.
        await AcidenteEnvolvidosSync.SincronizarAsync(db, acidente, new List<Guid> { joao.Id, maria.Id }, default, new List<Guid>());
        await db.SaveChangesAsync();
        Assert.All(await db.AcidentesEnvolvidos.ToListAsync(), e => Assert.Equal(PapelEnvolvido.Envolvido, e.Papel));
        Assert.Equal(2, await db.AcidentesEnvolvidos.CountAsync());
    }

    [Fact]
    public void FuncionarioNaoPodeSerEnvolvidoETestemunha()
    {
        var a = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() =>
            AcidenteEnvolvidosSync.ValidarSemSobreposicao(new List<Guid> { a }, new List<Guid> { a }));
    }

    [Fact]
    public async Task Detalhe_SeparaEnvolvidosETestemunhas()
    {
        var (db, obra, joao, maria) = await CenarioAsync();
        var id = await new CriarAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            Criar(obra.Id, TipoOcorrencia.Acidente) with
            {
                TrabalhadoresIds = new List<Guid> { joao.Id },
                Testemunhas = new List<Guid> { maria.Id },
            }, default);

        var a = (await new ObterAcidenteDetalheQueryHandler(db).Handle(new ObterAcidenteDetalheQuery(id), default)).Acidente;

        Assert.Equal(joao.Id, Assert.Single(a.Envolvidos).TrabalhadorId);
        var t = Assert.Single(a.Testemunhas);
        Assert.Equal(maria.Id, t.TrabalhadorId);
        Assert.Equal(PapelEnvolvido.Testemunha, t.Papel);
    }

    // ---------- 3 fotos com corte por data ----------

    [Fact]
    public void ExigeTresFotos_AcidenteSempre_OutrosTiposSoAposOCorte()
    {
        var corte = AvancarStatusAcidenteCommandHandler.ExigeTresFotosParaTodosOsTiposApartirDe;
        Assert.Equal(new DateTime(2026, 11, 18), corte.Date);

        Assert.True(AvancarStatusAcidenteCommandHandler.ExigeTresFotos(
            new Acidente { Tipo = TipoOcorrencia.Acidente, CreatedAtUtc = corte.AddDays(-400) }));
        Assert.False(AvancarStatusAcidenteCommandHandler.ExigeTresFotos(
            new Acidente { Tipo = TipoOcorrencia.Incidente, CreatedAtUtc = corte.AddSeconds(-1) }));
        Assert.True(AvancarStatusAcidenteCommandHandler.ExigeTresFotos(
            new Acidente { Tipo = TipoOcorrencia.Incidente, CreatedAtUtc = corte }));
        Assert.True(AvancarStatusAcidenteCommandHandler.ExigeTresFotos(
            new Acidente { Tipo = TipoOcorrencia.OcorrenciaComPessoaExterna, CreatedAtUtc = corte.AddDays(3) }));
    }

    [Fact]
    public async Task Concluir_IncidenteCriadoAposCorte_SemFotos_Recusa()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.Incidente, criadoEm: new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
                new AvancarStatusAcidenteCommand(acidente.Id, "Sem ações necessárias."), default));
        Assert.Contains("três fotos", ex.Message);
    }

    [Fact]
    public async Task Concluir_IncidenteCriadoAntesDoCorte_SemFotos_ContinuaPermitido()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.Incidente, criadoEm: new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));

        await new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            new AvancarStatusAcidenteCommand(acidente.Id, "Sem ações necessárias."), default);

        Assert.Equal(StatusAcidente.Concluido, (await db.Acidentes.SingleAsync()).Status);
    }

    [Fact]
    public async Task Concluir_AcidenteAntigo_SemFotos_ContinuaExigindoTres()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.Acidente, criadoEm: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
                new AvancarStatusAcidenteCommand(acidente.Id, "x"), default));
    }

    // ---------- Conclusão sem ação exige justificativa ----------

    [Fact]
    public async Task Concluir_SemAcaoVinculada_ExigeJustificativa()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.QuaseAcidente, criadoEm: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AvancarStatusAcidenteCommand(acidente.Id, "   "), default));
        Assert.Contains("justificativa", ex.Message);

        await handler.Handle(new AvancarStatusAcidenteCommand(acidente.Id, "  Causa tratada na hora.  "), default);
        var a = await db.Acidentes.SingleAsync();
        Assert.Equal(StatusAcidente.Concluido, a.Status);
        Assert.Equal("Causa tratada na hora.", a.ConclusaoInvestigacao);
    }

    [Fact]
    public async Task Concluir_ComAcaoConcluida_JustificativaOpcional()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.QuaseAcidente, criadoEm: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        db.AcoesPlano.Add(new AcaoPlano
        {
            OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Descricao = "Treinar", Status = StatusControleRisco.Concluido,
        });
        await db.SaveChangesAsync();

        await new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso()).Handle(
            new AvancarStatusAcidenteCommand(acidente.Id), default);

        var a = await db.Acidentes.SingleAsync();
        Assert.Equal(StatusAcidente.Concluido, a.Status);
        Assert.Null(a.ConclusaoInvestigacao);
    }

    [Fact]
    public async Task Concluir_SoComAcaoDaReuniao_AindaExigeJustificativa_ComAcaoCorretivaRealFicaOpcional()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.QuaseAcidente, criadoEm: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Single(await db.AcoesPlano.ToListAsync()); // só a ação da reunião
        var handler = new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AvancarStatusAcidenteCommand(acidente.Id), default));

        db.AcoesPlano.Add(new AcaoPlano { OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Descricao = "Instalar rodapé", Tipo = TipoAcaoPlano.Corretiva, Status = StatusControleRisco.Concluido });
        await db.SaveChangesAsync();
        await handler.Handle(new AvancarStatusAcidenteCommand(acidente.Id), default);
        Assert.Equal(StatusAcidente.Concluido, (await db.Acidentes.SingleAsync()).Status);
    }

    // ---------- G-RH nunca recebe AguardandoLaudo ----------

    [Fact]
    public async Task EventoGrh_AguardandoLaudo_VaiComoEmInvestigacao()
    {
        var (db, obra, joao, _) = await CenarioAsync();
        var publicador = new PublicadorFalso();
        var id = await new CriarAcidenteCommandHandler(db, publicador).Handle(
            Criar(obra.Id, TipoOcorrencia.Acidente) with { TrabalhadoresIds = new List<Guid> { joao.Id } }, default);
        var a = await db.Acidentes.SingleAsync(x => x.Id == id);
        a.Status = StatusAcidente.AguardandoLaudo;
        await db.SaveChangesAsync();

        await new AtualizarAcidenteCommandHandler(db, publicador).Handle(
            Atualizar(id, obra.Id, TipoOcorrencia.Acidente) with { TrabalhadoresIds = new List<Guid> { joao.Id } }, default);

        Assert.Equal("EmInvestigacao", publicador.Eventos.Last().Status);
        Assert.Equal(StatusAcidente.AguardandoLaudo, (await db.Acidentes.SingleAsync()).Status);
    }

    // ---------- Tipo 7 só na criação ----------

    [Fact]
    public async Task Atualizar_TrocarParaOuDoTipo7_Recusa()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var criar = new CriarAcidenteCommandHandler(db, new PublicadorFalso());
        var comum = await criar.Handle(Criar(obra.Id, TipoOcorrencia.Incidente), default);
        var externo = await criar.Handle(Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
        {
            PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Visitante) },
        }, default);
        var atualizar = new AtualizarAcidenteCommandHandler(db, new PublicadorFalso());

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            atualizar.Handle(Atualizar(comum, obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna), default));
        Assert.Contains("só pode ser escolhido ao registrar", ex1.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            atualizar.Handle(Atualizar(externo, obra.Id, TipoOcorrencia.Incidente), default));
        Assert.Equal(TipoOcorrencia.Incidente, (await db.Acidentes.SingleAsync(a => a.Id == comum)).Tipo);
        Assert.Equal(TipoOcorrencia.OcorrenciaComPessoaExterna, (await db.Acidentes.SingleAsync(a => a.Id == externo)).Tipo);
    }

    // ---------- Providências só com óbito e antes de concluir ----------

    [Fact]
    public async Task Providencias_ExigemObito_ERecusamOcorrenciaConcluida()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var criar = new CriarAcidenteCommandHandler(db, new PublicadorFalso());
        var semObito = await criar.Handle(Criar(obra.Id, TipoOcorrencia.Incidente), default);
        var comObito = await criar.Handle(Criar(obra.Id, TipoOcorrencia.OcorrenciaComPessoaExterna) with
        {
            PessoasExternas = new List<PessoaExternaEntrada> { new(null, "Carlos", VinculoPessoaExterna.Terceiro, Resultado: ResultadoPessoaExterna.Obito) },
        }, default);
        var handler = new AtualizarProvidenciasObitoCommandHandler(db);
        var itens = new List<ProvidenciaObitoEntrada> { new("familia", true, null) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AtualizarProvidenciasObitoCommand(semObito, itens), default));
        await handler.Handle(new AtualizarProvidenciasObitoCommand(comObito, itens), default);

        (await db.Acidentes.SingleAsync(a => a.Id == comObito)).Status = StatusAcidente.Concluido;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AtualizarProvidenciasObitoCommand(comObito, itens), default));
    }

    // ---------- Aguardando laudo ----------

    [Fact]
    public async Task AguardarLaudo_ERetomar_FazemOCicloEBloqueiamConclusao()
    {
        var (db, acidente) = await EmInvestigacaoAsync(TipoOcorrencia.Acidente, criadoEm: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        await new AguardarLaudoCommandHandler(db).Handle(new AguardarLaudoCommand(acidente.Id, " Laudo do IML "), default);
        var a = await db.Acidentes.SingleAsync();
        Assert.Equal(StatusAcidente.AguardandoLaudo, a.Status);
        Assert.Equal("Laudo do IML", a.LaudoObservacao);
        Assert.NotNull(a.AguardandoLaudoDesde);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AvancarStatusAcidenteCommandHandler(db, new PublicadorFalso()).Handle(new AvancarStatusAcidenteCommand(acidente.Id, "x"), default));
        Assert.Contains("Retome", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AguardarLaudoCommandHandler(db).Handle(new AguardarLaudoCommand(acidente.Id, null), default));

        await new RetomarInvestigacaoCommandHandler(db).Handle(new RetomarInvestigacaoCommand(acidente.Id), default);
        Assert.Equal(StatusAcidente.EmInvestigacao, (await db.Acidentes.SingleAsync()).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RetomarInvestigacaoCommandHandler(db).Handle(new RetomarInvestigacaoCommand(acidente.Id), default));
    }

    [Fact]
    public async Task AguardarLaudo_SoAPartirDeEmInvestigacao()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var acidente = new Acidente { ObraId = obra.Id, Local = "x", Descricao = "y", Status = StatusAcidente.Registrado };
        db.Acidentes.Add(acidente);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AguardarLaudoCommandHandler(db).Handle(new AguardarLaudoCommand(acidente.Id, null), default));
    }

    // ---------- Providências do óbito ----------

    [Fact]
    public async Task Providencias_MarcaEDesmarca_PreservandoDataDaPrimeiraMarcacao()
    {
        var (db, obra, _, _) = await CenarioAsync();
        var acidente = new Acidente { ObraId = obra.Id, Local = "x", Descricao = "y", Gravidade = GravidadeAcidente.Obito };
        db.Acidentes.Add(acidente);
        await db.SaveChangesAsync();
        var handler = new AtualizarProvidenciasObitoCommandHandler(db);

        await handler.Handle(new AtualizarProvidenciasObitoCommand(acidente.Id, new List<ProvidenciaObitoEntrada>
        {
            new("familia", true, "Contato feito pela gerente"),
            new("inexistente", true, null),
        }), default);

        var itens = ProvidenciasObitoAcidente.Ler((await db.Acidentes.SingleAsync()).ProvidenciasObitoJson);
        Assert.Equal(ProvidenciasObitoAcidente.CodigosValidos.Count, itens.Count);
        var familia = itens.Single(i => i.Codigo == "familia");
        Assert.True(familia.Concluida);
        Assert.NotNull(familia.ConcluidaEm);
        Assert.Equal("Contato feito pela gerente", familia.Observacao);
        Assert.DoesNotContain(itens, i => i.Codigo == "inexistente");
        // Nada de prazo legal no texto; o que depende de norma manda confirmar com o jurídico.
        Assert.Contains("confirmar com o jurídico", itens.Single(i => i.Codigo == "autoridades").Descricao);

        await handler.Handle(new AtualizarProvidenciasObitoCommand(acidente.Id, new List<ProvidenciaObitoEntrada> { new("familia", false, null) }), default);
        var depois = ProvidenciasObitoAcidente.Ler((await db.Acidentes.SingleAsync()).ProvidenciasObitoJson).Single(i => i.Codigo == "familia");
        Assert.False(depois.Concluida);
        Assert.Null(depois.ConcluidaEm);
    }

    [Fact]
    public void Providencias_JsonCorrompido_VoltaAoChecklistEmBranco()
    {
        var itens = ProvidenciasObitoAcidente.Ler("{não é json");
        Assert.Equal(ProvidenciasObitoAcidente.CodigosValidos.Count, itens.Count);
        Assert.All(itens, i => Assert.False(i.Concluida));
    }

    [Fact]
    public void TelefoneMascarador_MostraSoOsQuatroUltimos()
    {
        Assert.Equal("*******4321", TelefoneMascarador.Mascarar("(11) 98765-4321"));
        Assert.Null(TelefoneMascarador.Mascarar(null));
        Assert.Null(TelefoneMascarador.Mascarar(""));
    }

    // ---------- helpers ----------

    private static async Task<(SstDbContext Db, Obra Obra, Trabalhador Joao, Trabalhador Maria)> CenarioAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "CAC" };
        var joao = new Trabalhador { Nome = "João", ObraId = obra.Id, Cpf = "11122233396" };
        var maria = new Trabalhador { Nome = "Maria", ObraId = obra.Id, Cpf = "22233344405" };
        db.AddRange(obra, joao, maria);
        await db.SaveChangesAsync();
        return (db, obra, joao, maria);
    }

    // Ocorrência já em investigação com a reunião de análise realizada (as demais travas ficam livres).
    private static async Task<(SstDbContext Db, Acidente Acidente)> EmInvestigacaoAsync(TipoOcorrencia tipo, DateTime criadoEm)
    {
        var (db, obra, _, _) = await CenarioAsync();
        var acidente = new Acidente { Tipo = tipo, ObraId = obra.Id, Local = "Bloco B", Descricao = "Peça caiu", Data = new DateTime(2026, 11, 1), Status = StatusAcidente.EmInvestigacao };
        db.Acidentes.Add(acidente);
        // Fluxo real: a reunião de análise cria sempre a sua própria AcaoPlano (já concluída ao registrar a reunião).
        var acaoReuniao = new AcaoPlano { OrigemTipo = nameof(Acidente), OrigemId = acidente.Id, Descricao = "Reunião de análise", Status = StatusControleRisco.Concluido };
        db.AcoesPlano.Add(acaoReuniao);
        db.ReunioesAnaliseOcorrencia.Add(new ReuniaoAnaliseOcorrencia
        {
            AcaoPlanoId = acaoReuniao.Id,
            AcidenteId = acidente.Id, Inicio = new DateTime(2026, 11, 2, 9, 0, 0), Fim = new DateTime(2026, 11, 2, 10, 0, 0),
            RealizadaEm = new DateTime(2026, 11, 2, 9, 30, 0),
        });
        await db.SaveChangesAsync();
        // CreatedAtUtc é carimbado no Add; o teste recua/avança para exercitar o corte por data.
        acidente.CreatedAtUtc = criadoEm;
        await db.SaveChangesAsync();
        return (db, acidente);
    }

    private static CriarAcidenteCommand Criar(Guid obraId, TipoOcorrencia tipo) => new(
        tipo, obraId, null, null, "Portaria", new DateTime(2026, 11, 17), null, "Descrição", null, null, null,
        false, null, null, null, null, GravidadeAcidente.SemAfastamento, null);

    private static AtualizarAcidenteCommand Atualizar(Guid id, Guid obraId, TipoOcorrencia tipo) => new(
        id, tipo, obraId, null, null, "Portaria", new DateTime(2026, 11, 17), null, "Descrição", null, null, null,
        false, null, null, null, null, GravidadeAcidente.SemAfastamento, null);

    private sealed class PublicadorFalso : IPublicadorAcidenteGrh
    {
        public List<AcidenteGrhEvento> Eventos { get; } = new();
        public Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }
    }

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public List<(string Acao, Guid EntidadeId, Guid? UsuarioId, object? Dados)> Registros { get; } = new();
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
        {
            Registros.Add((acao, entidadeId, usuarioId, dadosDepois));
            return Task.CompletedTask;
        }
    }
}
