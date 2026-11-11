using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Application.Tests.Assinatura;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// "Realizar integração" (11/11/2026): iniciar, avançar etapa a etapa, proteções da turma e escopo por obra.
public class IntegracaoAtendimentoEtapasTests
{
    [Fact]
    public async Task Iniciar_Cria5Etapas_PrimeiraEmAndamento_ETurmaSoComOsParticipantesDaEtapa()
    {
        var c = await CenarioIntegracao.CriarAsync();

        var id = await c.IniciarAsync(c.A, c.B);

        var dto = await c.ObterAsync(id);
        Assert.Equal("emAndamento", dto.Status);
        Assert.Equal(1, dto.EtapaAtualOrdem);
        Assert.Equal(2, dto.Participantes.Count);
        Assert.Equal(new[] { "nr06", "nr12", "nr18", "nr35", "ordemServico" }, dto.Etapas.Select(e => e.Tipo));
        Assert.Equal(new[] { "emAndamento", "pendente", "pendente", "pendente", "pendente" }, dto.Etapas.Select(e => e.Status));
        Assert.Equal(new[] { 0, 8, 4, 8, 0 }, dto.Etapas.Select(e => e.CargaMinimaHoras));

        // NR-06: turma da etapa com A e B, do atendimento, com o instrutor escolhido.
        var etapa1 = dto.Etapas[0];
        Assert.Equal(2, etapa1.Participantes.Count);
        var sessao = await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == etapa1.SessaoTreinamentoId);
        Assert.Equal(id, sessao.IntegracaoAtendimentoId);
        Assert.Equal(c.Instrutor.Id, sessao.InstrutorTrabalhadorId);
        Assert.Equal(c.Nr06.Id, sessao.CursoTreinamentoId);
        // Etapa ainda não aberta não tem turma.
        Assert.Null(dto.Etapas[1].SessaoTreinamentoId);
    }

    [Fact]
    public async Task Iniciar_EtapasDeCursoSemParticipante_ViramPuladas()
    {
        var c = await CenarioIntegracao.CriarAsync();

        var id = await c.IniciarAsync(c.B); // Ajudante: sem NR-12 nem NR-35 no PGR

        var dto = await c.ObterAsync(id);
        Assert.Equal(new[] { "emAndamento", "pulada", "pendente", "pulada", "pendente" }, dto.Etapas.Select(e => e.Status));
    }

    [Fact]
    public async Task Iniciar_UmSoAtendimentoEmAndamentoPorObra()
    {
        var c = await CenarioIntegracao.CriarAsync();
        await c.IniciarAsync(c.A);

        var ex = await Assert.ThrowsAsync<IntegracaoConflitoException>(() => c.IniciarAsync(c.B));

        Assert.Contains("em andamento", ex.Message);
    }

    [Fact]
    public async Task Iniciar_ComAChaveDesligada_Recusa()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Opcoes.AtendimentoHabilitado = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.IniciarAsync(c.A));
        Assert.Empty(await c.Db.IntegracoesAtendimento.ToListAsync());
    }

    [Fact]
    public async Task Avancar_RecusaSemPresencaCargaFotosEInstrutor_ComAListaDoQueFalta()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        await c.AvancarAsync(id); // NR-06 concluída; abre NR-12 (A, carga mínima 8 h)

        var ex = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.AvancarAsync(id));

        Assert.Contains(ex.Faltas, f => f.Contains("Carga horária"));
        Assert.Contains(ex.Faltas, f => f.Contains("3 foto"));
        Assert.Contains(ex.Faltas, f => f.Contains("Assinatura biométrica pendente"));
        Assert.DoesNotContain(ex.Faltas, f => f.Contains("Instrutor"));

        var etapa = await c.EtapaAsync(id, 2);
        var sessao = await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == etapa.SessaoTreinamentoId);
        sessao.InstrutorTrabalhadorId = null;
        await c.Db.SaveChangesAsync();
        var semInstrutor = await Assert.ThrowsAsync<IntegracaoBloqueadaException>(() => c.AvancarAsync(id));
        Assert.Contains(semInstrutor.Faltas, f => f.Contains("Instrutor"));
        Assert.Equal(StatusEtapaIntegracao.EmAndamento, (await c.EtapaAsync(id, 2)).Status);
    }

    [Fact]
    public async Task Avancar_GeraUmCertificadoPorPessoa_CadaUmComODocumentoDeAssinaturaProprio()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1);
        var etapa = await c.EtapaAsync(id, 1);

        await c.AvancarAsync(id);

        var certificados = await c.Db.Treinamentos.Where(t => t.SessaoTreinamentoId == etapa.SessaoTreinamentoId).ToListAsync();
        Assert.Equal(2, certificados.Count);
        Assert.Equal(2, certificados.Select(t => t.NumeroCertificado).Distinct().Count());
        Assert.All(certificados, t => Assert.Equal(c.Nr06.Id, t.CursoTreinamentoId));
        var documentos = await c.Db.DocumentosAssinatura.Where(d => d.EntidadeTipo == nameof(Treinamento)).ToListAsync();
        Assert.Equal(2, documentos.Count);
        Assert.Equal(certificados.Select(t => t.Id).OrderBy(i => i), documentos.Select(d => d.EntidadeId).OrderBy(i => i));
        // A assinatura biométrica de cada pessoa vai no certificado dela, e só nele.
        foreach (var t in certificados)
        {
            var doc = documentos.Single(d => d.EntidadeId == t.Id);
            var assinaturas = await c.Db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == doc.Id).ToListAsync();
            var assinatura = Assert.Single(assinaturas);
            Assert.Equal(t.TrabalhadorId, assinatura.TrabalhadorId);
            Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, assinatura.MetodoAutenticacao);
        }
    }

    [Fact]
    public async Task Avancar_EIdempotente_DuploCliqueNaoRepeteCertificadoNemPulaEtapa()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1);

        var primeira = await c.AvancarAsync(id, ordem: 1);
        var repetida = await c.AvancarAsync(id, ordem: 1);

        Assert.Equal(2, primeira.EtapaAtualOrdem);
        Assert.Equal(2, repetida.EtapaAtualOrdem);
        Assert.Equal(2, await c.Db.Treinamentos.CountAsync());
        Assert.Equal(StatusEtapaIntegracao.EmAndamento, (await c.EtapaAsync(id, 2)).Status);
    }

    [Fact]
    public async Task Avancar_RetomaDepoisDeInterrupcao_SemDuplicarCertificados()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A, c.B);
        await c.CompletarEtapaAtualAsync(id, 1);
        var etapa = await c.EtapaAsync(id, 1);
        // Simula a queda entre encerrar a turma e gravar a etapa: a turma já está concluída, a etapa ainda não.
        await c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(etapa.SessaoTreinamentoId!.Value, null, ViaIntegracao: true), default);
        Assert.Equal(StatusEtapaIntegracao.EmAndamento, (await c.EtapaAsync(id, 1)).Status);

        var dto = await c.AvancarAsync(id);

        Assert.Equal("concluida", dto.Etapas[0].Status);
        Assert.Equal(2, dto.EtapaAtualOrdem);
        Assert.Equal(2, await c.Db.Treinamentos.CountAsync());
    }

    [Fact]
    public async Task PresencaEFoto_ForaDaEtapaEmAndamento_SaoRecusadas_EOControleDeTurmaComumFunciona()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        var etapa1 = await c.EtapaAsync(id, 1);
        var sessao1 = etapa1.SessaoTreinamentoId!.Value;
        await c.AvancarAsync(id);

        // Etapa 1 já concluída (a turma também): nada mais entra nela.
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao1, 1, CenarioIntegracao.Bytes(999)));
        var presenca = new RegistrarPresencaSessaoTreinamentoCommandHandler(c.Db, new AutenticacaoBiometriaLocalFalsa());
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenca.Handle(
            new RegistrarPresencaSessaoTreinamentoCommand(sessao1, c.A.Id, Guid.NewGuid(), "segredo", 90), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DefinirInstrutorSessaoTreinamentoCommandHandler(c.Db)
            .Handle(new DefinirInstrutorSessaoTreinamentoCommand(sessao1, c.Instrutor.Id), default));

        // Turma comum (sem atendimento) continua aceitando a foto.
        var comum = new SessaoTreinamento { ObraId = c.Obra.Id, CursoTreinamentoId = c.Nr18.Id, DataRealizacao = DateTime.UtcNow.Date, CargaHorariaRealizada = 4 };
        c.Db.SessoesTreinamento.Add(comum);
        await c.Db.SaveChangesAsync();
        await c.FotoAsync(comum.Id, 1, CenarioIntegracao.Bytes(998));
    }

    [Fact]
    public async Task Foto_ComHashJaUsadoEmOutraEtapa_ERecusada()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        var sessao1 = (await c.EtapaAsync(id, 1)).SessaoTreinamentoId!.Value;
        var fotoDaEtapa1 = (await c.Db.FotosEvidenciaSessaoTreinamento.Where(f => f.SessaoTreinamentoId == sessao1).OrderBy(f => f.Ordem).FirstAsync());
        Assert.Equal(64, fotoDaEtapa1.FotoHash!.Length);
        await c.AvancarAsync(id);
        var sessao2 = (await c.EtapaAsync(id, 2)).SessaoTreinamentoId!.Value;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao2, 1, fotoDaEtapa1.FotoConteudo));
        Assert.Contains("outra etapa", ex.Message);

        // Foto nova, tirada agora, entra.
        await c.FotoAsync(sessao2, 1, CenarioIntegracao.Bytes(5000));
        // E a mesma foto não vale em dois quadros da mesma etapa.
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.FotoAsync(sessao2, 2, CenarioIntegracao.Bytes(5000)));
    }

    [Fact]
    public async Task TurmaDeIntegracao_NaoEncerraAlteraDataNemExcluiPelaTelaComum()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        await c.CompletarEtapaAtualAsync(id, 1);
        var sessao = (await c.EtapaAsync(id, 1)).SessaoTreinamentoId!.Value;

        var encerrar = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            c.EncerrarHandler().Handle(new EncerrarSessaoTreinamentoCommand(sessao), default));
        Assert.Contains("integração", encerrar.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AlterarDataSessaoTreinamentoCommandHandler(c.Db, new FilaCalendarioTeamsFalsa())
                .Handle(new AlterarDataSessaoTreinamentoCommand(sessao, DateTime.UtcNow.Date.AddDays(1)), default));

        var excluir = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExcluirSessaoTreinamentoCommandHandler(c.Db, new AuditoriaIntegracaoFalsa(), new CurrentUserService())
                .Handle(new ExcluirSessaoTreinamentoCommand(sessao), default));
        Assert.Contains("etapa de uma integração", excluir.Message);

        Assert.Equal(StatusSessaoTreinamento.EmAndamento, (await c.Db.SessoesTreinamento.SingleAsync(s => s.Id == sessao)).Status);
        Assert.Empty(await c.Db.Treinamentos.ToListAsync());
    }

    [Fact]
    public async Task Participantes_EntramESaemSoAntesDeQualquerAssinatura()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.B); // NR-12 e NR-35 puladas
        var incluir = new IncluirParticipantesIntegracaoCommandHandler(c.Db);

        // A (Operador) precisa de NR-12/NR-35: as etapas puladas voltam a Pendente (estão à frente da atual).
        await incluir.Handle(new IncluirParticipantesIntegracaoCommand(id, [c.A.Id]), default);

        var dto = await c.ObterAsync(id);
        Assert.Equal(2, dto.Participantes.Count);
        Assert.Equal(2, dto.Etapas[0].Participantes.Count); // entrou também na turma da NR-06 em andamento
        Assert.Equal(new[] { "emAndamento", "pendente", "pendente", "pendente", "pendente" }, dto.Etapas.Select(e => e.Status));
        Assert.NotNull(dto.Etapas[1].CursoTreinamentoId);
        Assert.Equal(8, dto.Etapas[1].CargaMinimaHoras);

        await new RetirarParticipanteIntegracaoCommandHandler(c.Db).Handle(new RetirarParticipanteIntegracaoCommand(id, c.A.Id), default);
        Assert.Single((await c.ObterAsync(id)).Participantes);

        // Depois da primeira assinatura a lista fecha.
        await incluir.Handle(new IncluirParticipantesIntegracaoCommand(id, [c.A.Id]), default);
        var turma = (await c.EtapaAsync(id, 1)).SessaoTreinamentoId!.Value;
        var inscrito = await c.Db.ParticipantesSessaoTreinamento.FirstAsync(p => p.SessaoTreinamentoId == turma && p.TrabalhadorId == c.B.Id);
        inscrito.PresencaConfirmadaEm = DateTime.UtcNow;
        await c.Db.SaveChangesAsync();
        Assert.False((await c.ObterAsync(id)).PodeEditarParticipantes);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RetirarParticipanteIntegracaoCommandHandler(c.Db).Handle(new RetirarParticipanteIntegracaoCommand(id, c.A.Id), default));
        var novo = await c.NovaPessoaAsync(c.Ajudante, "Caio Novo");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            incluir.Handle(new IncluirParticipantesIntegracaoCommand(id, [novo.Id]), default));
    }

    // ---------- Escopo por obra e autorização ----------

    [Fact]
    public async Task TecnicoDeOutraObra_RecebeNotFound_EmTodasAsConsultasEComandos()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(false, [Guid.NewGuid()]); // só enxerga uma obra que não é esta
        var dbOutro = new SstDbContext(c.OpcoesDb, usuario);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ObterIntegracaoQueryHandler(dbOutro).Handle(new ObterIntegracaoQuery(id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ObterEntradaIntegracaoQueryHandler(dbOutro, Options.Create(c.Opcoes))
            .Handle(new ObterEntradaIntegracaoQuery(c.Obra.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AvancarEtapaIntegracaoCommandHandler(dbOutro, c.Mediator, c.Numeros)
            .Handle(new AvancarEtapaIntegracaoCommand(id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new IncluirParticipantesIntegracaoCommandHandler(dbOutro)
            .Handle(new IncluirParticipantesIntegracaoCommand(id, [c.B.Id]), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ConcluirIntegracaoCommandHandler(dbOutro, c.Mediator)
            .Handle(new ConcluirIntegracaoCommand(id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new IniciarIntegracaoCommandHandler(dbOutro, c.Numeros, Options.Create(c.Opcoes))
            .Handle(new IniciarIntegracaoCommand(c.Obra.Id, [c.B.Id], c.Instrutor.Id), default));
    }

    [Fact]
    public async Task ComEntraLigado_SoTecnicoOuAdministradorComVinculoNaObraConduz()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var id = await c.IniciarAsync(c.A);
        var outraObra = new Obra { Codigo = "OUT", Nome = "Outra" };
        var tecnicoDaObra = new Usuario { AzureAdObjectId = "oid-tecnico", Nome = "Tecnico", Email = "t@x.com" };
        var tecnicoDeOutra = new Usuario { AzureAdObjectId = "oid-outra", Nome = "Outra", Email = "o@x.com" };
        var rh = new Usuario { AzureAdObjectId = "oid-rh", Nome = "RH", Email = "rh@x.com" };
        var perfilTecnico = new PerfilAcesso { Tipo = TipoPerfilAcesso.TecnicoSeguranca, Nome = "Técnico" };
        var perfilRh = new PerfilAcesso { Tipo = TipoPerfilAcesso.Rh, Nome = "RH" };
        c.Db.Obras.Add(outraObra);
        c.Db.Usuarios.AddRange(tecnicoDaObra, tecnicoDeOutra, rh);
        c.Db.PerfisAcesso.AddRange(perfilTecnico, perfilRh);
        c.Db.UsuariosPerfilObra.AddRange(
            new UsuarioPerfilObra { UsuarioId = tecnicoDaObra.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = c.Obra.Id },
            new UsuarioPerfilObra { UsuarioId = tecnicoDeOutra.Id, PerfilAcessoId = perfilTecnico.Id, ObraId = outraObra.Id },
            new UsuarioPerfilObra { UsuarioId = rh.Id, PerfilAcessoId = perfilRh.Id, ObraId = c.Obra.Id });
        await c.Db.SaveChangesAsync();
        var consulta = new ObterIntegracaoQueryHandler(c.Db);

        Assert.NotNull(await consulta.Handle(new ObterIntegracaoQuery(id, "oid-tecnico", true), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => consulta.Handle(new ObterIntegracaoQuery(id, "oid-outra", true), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => consulta.Handle(new ObterIntegracaoQuery(id, "oid-rh", true), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => consulta.Handle(new ObterIntegracaoQuery(id, null, true), default));
    }

    [Fact]
    public async Task Entrada_ListaQuemAguardaEOsBloqueios()
    {
        var c = await CenarioIntegracao.CriarAsync();
        c.Db.PlanosIntegracao.Add(new PlanoIntegracao { TrabalhadorId = c.A.Id, ObraId = c.Obra.Id, FuncaoId = c.Operador.Id });
        await c.Db.SaveChangesAsync();
        var handler = new ObterEntradaIntegracaoQueryHandler(c.Db, Options.Create(c.Opcoes));

        var entrada = await handler.Handle(new ObterEntradaIntegracaoQuery(c.Obra.Id), default);

        Assert.True(entrada.Habilitada);
        Assert.True(entrada.PodeIniciar);
        Assert.Equal(c.A.Id, Assert.Single(entrada.Candidatos).TrabalhadorId);
        Assert.Equal(c.Instrutor.Id, Assert.Single(entrada.Instrutores).TrabalhadorId);

        var id = await c.IniciarAsync(c.A);
        var depois = await handler.Handle(new ObterEntradaIntegracaoQuery(c.Obra.Id), default);
        Assert.False(depois.PodeIniciar);
        Assert.Equal(id, depois.AtendimentoEmAndamento!.Id);
        Assert.True(depois.Candidatos.Single().JaParticipa);

        c.Opcoes.AtendimentoHabilitado = false;
        Assert.Contains((await handler.Handle(new ObterEntradaIntegracaoQuery(c.Obra.Id), default)).Bloqueios, b => b.Codigo == "FluxoNaoHabilitado");
    }

    [Fact]
    public async Task Relatorio_ListaSoTurmasDoComboForaDeAtendimento()
    {
        var c = await CenarioIntegracao.CriarAsync();
        var antiga = new SessaoTreinamento { ObraId = c.Obra.Id, CursoTreinamentoId = c.Nr18.Id, DataRealizacao = DateTime.UtcNow.Date.AddDays(2), CargaHorariaRealizada = 4 };
        antiga.Participantes.Add(new ParticipanteSessaoTreinamento { TrabalhadorId = c.A.Id });
        var outroCurso = new CursoTreinamento { Nome = "NR-10", NormaReferencia = "NR-10" };
        c.Db.CursosTreinamento.Add(outroCurso);
        c.Db.SessoesTreinamento.AddRange(antiga,
            new SessaoTreinamento { ObraId = c.Obra.Id, CursoTreinamentoId = outroCurso.Id, DataRealizacao = DateTime.UtcNow.Date, CargaHorariaRealizada = 8 });
        await c.Db.SaveChangesAsync();
        await c.IniciarAsync(c.A); // a turma da etapa NR-06 não entra no relatório

        var linhas = await new RelatorioTurmasIntegracaoAgendadasQueryHandler(c.Db).Handle(new RelatorioTurmasIntegracaoAgendadasQuery(), default);

        var linha = Assert.Single(linhas);
        Assert.Equal(antiga.Id, linha.SessaoTreinamentoId);
        Assert.Equal(1, linha.Participantes);
    }
}
