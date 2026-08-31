using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.NaoConformidades.Commands;
using AAHBRANT.SST.Application.Tests.Alertas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.NaoConformidades;

// Cobre o fluxo de 8 status do Procedimento de Inspeção Técnica de Campo:
// Aberta --Enviar--> Enviada --Responder--> EmAndamento --RegistrarConclusao--> AguardandoValidacao
// --Encerrar--> Encerrada, com o desvio --Devolver--> Devolvida --Responder--> EmAndamento.
public class NaoConformidadeFluxoCommandsTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Usuario Responsavel, NaoConformidade Nc)> SemearAsync(IAppDbContext db)
    {
        var responsavel = new Usuario { Email = "responsavel@aahbrant.com", Nome = "Responsável" };
        db.Usuarios.Add(responsavel);

        var nc = new NaoConformidade
        {
            Descricao = "Guarda-corpo ausente na plataforma nível 3",
            OrigemDeteccao = OrigemNaoConformidade.Inspecao,
            ResponsavelUsuarioId = responsavel.Id,
        };
        db.NaoConformidades.Add(nc);

        await db.SaveChangesAsync();
        return (responsavel, nc);
    }

    [Fact]
    public async Task Enviar_OcorrenciaAbertaComResponsavel_MoveParaEnviadaECriaAlertaENotificacao()
    {
        var db = CriarDb(nameof(Enviar_OcorrenciaAbertaComResponsavel_MoveParaEnviadaECriaAlertaENotificacao));
        var (responsavel, nc) = await SemearAsync(db);
        var fila = new FilaNotificacaoTeamsFalsa();
        var handler = new EnviarNaoConformidadeCommandHandler(db, fila);

        await handler.Handle(new EnviarNaoConformidadeCommand(nc.Id), default);

        var atualizada = await db.NaoConformidades.SingleAsync(n => n.Id == nc.Id);
        Assert.Equal(StatusNaoConformidade.Enviada, atualizada.Status);

        var alerta = await db.Alertas.SingleAsync(a => a.EntidadeOrigemId == nc.Id);
        Assert.Equal(responsavel.Id, alerta.DestinatarioUsuarioId);

        var mensagem = Assert.Single(fila.Mensagens);
        Assert.Equal(alerta.Id, mensagem.AlertaId);
        Assert.Equal(responsavel.Id, mensagem.DestinatarioUsuarioId);
    }

    [Fact]
    public async Task Enviar_SemResponsavelDefinido_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Enviar_SemResponsavelDefinido_LancaInvalidOperationException));
        var nc = new NaoConformidade { Descricao = "x", OrigemDeteccao = OrigemNaoConformidade.Inspecao };
        db.NaoConformidades.Add(nc);
        await db.SaveChangesAsync();
        var handler = new EnviarNaoConformidadeCommandHandler(db, new FilaNotificacaoTeamsFalsa());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new EnviarNaoConformidadeCommand(nc.Id), default));
    }

    [Fact]
    public async Task Enviar_OcorrenciaJaEnviada_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Enviar_OcorrenciaJaEnviada_LancaInvalidOperationException));
        var (_, nc) = await SemearAsync(db);
        var handler = new EnviarNaoConformidadeCommandHandler(db, new FilaNotificacaoTeamsFalsa());
        await handler.Handle(new EnviarNaoConformidadeCommand(nc.Id), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new EnviarNaoConformidadeCommand(nc.Id), default));
    }

    [Fact]
    public async Task Responder_OcorrenciaEnviada_CriaAcaoPlanoEMoveParaEmAndamento()
    {
        var db = CriarDb(nameof(Responder_OcorrenciaEnviada_CriaAcaoPlanoEMoveParaEmAndamento));
        var (responsavel, nc) = await SemearAsync(db);
        await new EnviarNaoConformidadeCommandHandler(db, new FilaNotificacaoTeamsFalsa())
            .Handle(new EnviarNaoConformidadeCommand(nc.Id), default);
        var handler = new ResponderNaoConformidadeCommandHandler(db);

        var acaoId = await handler.Handle(
            new ResponderNaoConformidadeCommand(nc.Id, "Instalar guarda-corpo", null, PrioridadeAcao.Alta, null, null),
            default);

        var atualizada = await db.NaoConformidades.SingleAsync(n => n.Id == nc.Id);
        Assert.Equal(StatusNaoConformidade.EmAndamento, atualizada.Status);
        Assert.NotNull(atualizada.Prazo);

        var acao = await db.AcoesPlano.SingleAsync(a => a.Id == acaoId);
        Assert.Equal(nameof(Domain.Entidades.NaoConformidade), acao.OrigemTipo);
        Assert.Equal(nc.Id, acao.OrigemId);
        Assert.Equal(responsavel.Id, acao.ResponsavelUsuarioId);
        Assert.Equal(StatusControleRisco.Pendente, acao.Status);
    }

    [Fact]
    public async Task Responder_OcorrenciaAberta_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Responder_OcorrenciaAberta_LancaInvalidOperationException));
        var (_, nc) = await SemearAsync(db);
        var handler = new ResponderNaoConformidadeCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new ResponderNaoConformidadeCommand(nc.Id, "x", null, PrioridadeAcao.Baixa, null, null), default));
    }

    private static async Task<NaoConformidade> LevarAteEmAndamentoAsync(IAppDbContext db, Guid ncId)
    {
        await new EnviarNaoConformidadeCommandHandler(db, new FilaNotificacaoTeamsFalsa())
            .Handle(new EnviarNaoConformidadeCommand(ncId), default);
        await new ResponderNaoConformidadeCommandHandler(db).Handle(
            new ResponderNaoConformidadeCommand(ncId, "Instalar guarda-corpo", null, PrioridadeAcao.Alta, null, null),
            default);
        return await db.NaoConformidades.SingleAsync(n => n.Id == ncId);
    }

    [Fact]
    public async Task RegistrarConclusao_ComAcaoPendente_ConcluiAcaoEMoveParaAguardandoValidacao()
    {
        var db = CriarDb(nameof(RegistrarConclusao_ComAcaoPendente_ConcluiAcaoEMoveParaAguardandoValidacao));
        var (_, nc) = await SemearAsync(db);
        await LevarAteEmAndamentoAsync(db, nc.Id);
        var handler = new RegistrarConclusaoNaoConformidadeCommandHandler(db);

        await handler.Handle(new RegistrarConclusaoNaoConformidadeCommand(nc.Id, "Guarda-corpo instalado"), default);

        var atualizada = await db.NaoConformidades.SingleAsync(n => n.Id == nc.Id);
        Assert.Equal(StatusNaoConformidade.AguardandoValidacao, atualizada.Status);

        var acao = await db.AcoesPlano.SingleAsync(a => a.OrigemId == nc.Id);
        Assert.Equal(StatusControleRisco.Concluido, acao.Status);
        Assert.NotNull(acao.DataConclusao);
    }

    [Fact]
    public async Task RegistrarConclusao_SemAcaoPendente_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(RegistrarConclusao_SemAcaoPendente_LancaInvalidOperationException));
        var (_, nc) = await SemearAsync(db);
        await new EnviarNaoConformidadeCommandHandler(db, new FilaNotificacaoTeamsFalsa())
            .Handle(new EnviarNaoConformidadeCommand(nc.Id), default);
        // Nunca respondida -> nenhuma AcaoPlano vinculada existe.
        var handler = new RegistrarConclusaoNaoConformidadeCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RegistrarConclusaoNaoConformidadeCommand(nc.Id, null), default));
    }

    [Fact]
    public async Task Devolver_OcorrenciaAguardandoValidacao_ReabreAcaoEMoveParaDevolvida()
    {
        var db = CriarDb(nameof(Devolver_OcorrenciaAguardandoValidacao_ReabreAcaoEMoveParaDevolvida));
        var (_, nc) = await SemearAsync(db);
        await LevarAteEmAndamentoAsync(db, nc.Id);
        await new RegistrarConclusaoNaoConformidadeCommandHandler(db)
            .Handle(new RegistrarConclusaoNaoConformidadeCommand(nc.Id, null), default);
        var fila = new FilaNotificacaoTeamsFalsa();
        var handler = new DevolverNaoConformidadeCommandHandler(db, fila);

        await handler.Handle(new DevolverNaoConformidadeCommand(nc.Id, "Execução não atende ao especificado"), default);

        var atualizada = await db.NaoConformidades.SingleAsync(n => n.Id == nc.Id);
        Assert.Equal(StatusNaoConformidade.Devolvida, atualizada.Status);
        Assert.Equal("Execução não atende ao especificado", atualizada.MotivoDevolucao);

        var acao = await db.AcoesPlano.SingleAsync(a => a.OrigemId == nc.Id);
        Assert.Equal(StatusControleRisco.EmAndamento, acao.Status);
        Assert.Null(acao.DataConclusao);
        Assert.Single(fila.Mensagens);
    }

    [Fact]
    public async Task Devolver_OcorrenciaEmAndamento_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Devolver_OcorrenciaEmAndamento_LancaInvalidOperationException));
        var (_, nc) = await SemearAsync(db);
        await LevarAteEmAndamentoAsync(db, nc.Id);
        var handler = new DevolverNaoConformidadeCommandHandler(db, new FilaNotificacaoTeamsFalsa());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DevolverNaoConformidadeCommand(nc.Id, "motivo"), default));
    }

    [Fact]
    public async Task Encerrar_ComAcoesConcluidas_ValidaAcoesEMoveParaEncerrada()
    {
        var db = CriarDb(nameof(Encerrar_ComAcoesConcluidas_ValidaAcoesEMoveParaEncerrada));
        var (validador, nc) = await SemearAsync(db);
        await LevarAteEmAndamentoAsync(db, nc.Id);
        await new RegistrarConclusaoNaoConformidadeCommandHandler(db)
            .Handle(new RegistrarConclusaoNaoConformidadeCommand(nc.Id, null), default);
        var handler = new EncerrarNaoConformidadeCommandHandler(db);

        await handler.Handle(
            new EncerrarNaoConformidadeCommand(nc.Id, validador.Id, "Conferido em campo"), default);

        var atualizada = await db.NaoConformidades.SingleAsync(n => n.Id == nc.Id);
        Assert.Equal(StatusNaoConformidade.Encerrada, atualizada.Status);
        Assert.NotNull(atualizada.DataConclusao);
        Assert.Equal("Conferido em campo", atualizada.ObservacoesEncerramento);

        var acao = await db.AcoesPlano.SingleAsync(a => a.OrigemId == nc.Id);
        Assert.Equal(validador.Id, acao.ValidadoPorUsuarioId);
        Assert.NotNull(acao.DataValidacao);

        var documento = await db.DocumentosAssinatura.SingleAsync(
            d => d.EntidadeTipo == nameof(Domain.Entidades.NaoConformidade) && d.EntidadeId == nc.Id);
        Assert.Equal(StatusDocumentoAssinatura.EmAndamento, documento.Status);
    }

    [Fact]
    public async Task Encerrar_ComAcaoAindaPendente_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Encerrar_ComAcaoAindaPendente_LancaInvalidOperationException));
        var (validador, nc) = await SemearAsync(db);
        await LevarAteEmAndamentoAsync(db, nc.Id);
        // Força a NC para AguardandoValidacao sem concluir a AcaoPlano, simulando um estado
        // inconsistente que o bloqueio preventivo deve rejeitar mesmo assim.
        var ncEntidade = await db.NaoConformidades.SingleAsync(n => n.Id == nc.Id);
        ncEntidade.Status = StatusNaoConformidade.AguardandoValidacao;
        await db.SaveChangesAsync();
        var handler = new EncerrarNaoConformidadeCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new EncerrarNaoConformidadeCommand(nc.Id, validador.Id, null), default));
    }
}
