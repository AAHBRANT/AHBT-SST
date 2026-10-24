using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SuporteIa.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.SuporteIa;

// Etapas 3 (Aprovação e execução) e 4 (Validação do solicitante) do Suporte IA — antes destes
// comandos existirem, a esteira era só um desenho visual sem transição real (ver handoff do
// usuário de 20/09). Cobre o ciclo feliz e as guardas de estado/identidade de cada comando.
public class FluxoAprovacaoSuporteIaTests
{
    private sealed class FilaCalendarioNula : IFilaCalendarioTeams
    {
        public Task EnfileirarAsync(CalendarioTeamsMensagem mensagem, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static IAppDbContext CriarDb(string nomeBanco) =>
        new SstDbContext(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options, new CurrentUserService());

    private static async Task<Guid> CriarSolicitacaoAsync(
        IAppDbContext db,
        StatusSolicitacaoSuporteIa status,
        Guid? solicitanteUsuarioId = null,
        string? solicitanteEmail = "solicitante@aahbrant.com")
    {
        var solicitacao = new SuporteIaSolicitacao
        {
            Tipo = TipoSolicitacaoSuporteIa.Erro,
            SeveridadeInformada = SeveridadeSolicitacaoSuporteIa.Media,
            Status = status,
            Titulo = "Teste",
            Descricao = "Descrição de teste",
            RequerAlteracaoCodigo = true,
            ResultadoTriagem = ResultadoTriagemSuporteIa.DemandaTecnica,
            SolicitanteUsuarioId = solicitanteUsuarioId,
            SolicitanteEmail = solicitanteEmail,
            TriadoEmUtc = DateTime.UtcNow,
        };
        db.SuporteIaSolicitacoes.Add(solicitacao);
        await db.SaveChangesAsync();
        return solicitacao.Id;
    }

    [Fact]
    public async Task Aprovar_ChamadoEncaminhado_MoveParaEmAnaliseTecnica()
    {
        var db = CriarDb(nameof(Aprovar_ChamadoEncaminhado_MoveParaEmAnaliseTecnica));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.Encaminhada);
        var responsavelId = Guid.NewGuid();

        var resultado = await new AprovarSolicitacaoSuporteIaCommandHandler(db)
            .Handle(new AprovarSolicitacaoSuporteIaCommand(id, responsavelId, "Responsável Teste"), CancellationToken.None);

        Assert.Equal(StatusSolicitacaoSuporteIa.EmAnaliseTecnica, resultado.Status);
        Assert.Equal(responsavelId, resultado.ResponsavelUsuarioId);
        Assert.NotNull(resultado.AprovadoEmUtc);
    }

    [Fact]
    public async Task Aprovar_ChamadoJaResolvido_Falha()
    {
        var db = CriarDb(nameof(Aprovar_ChamadoJaResolvido_Falha));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.Resolvida);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AprovarSolicitacaoSuporteIaCommandHandler(db)
                .Handle(new AprovarSolicitacaoSuporteIaCommand(id, Guid.NewGuid(), "Responsável"), CancellationToken.None));
    }

    [Fact]
    public async Task Concluir_ChamadoEmAnaliseTecnica_MoveParaAguardandoValidacao()
    {
        var db = CriarDb(nameof(Concluir_ChamadoEmAnaliseTecnica_MoveParaAguardandoValidacao));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.EmAnaliseTecnica);

        var resultado = await new ConcluirExecucaoSuporteIaCommandHandler(db)
            .Handle(new ConcluirExecucaoSuporteIaCommand(id, "Corrigido em produção"), CancellationToken.None);

        Assert.Equal(StatusSolicitacaoSuporteIa.AguardandoValidacao, resultado.Status);
        Assert.Equal("Corrigido em produção", resultado.NotaFechamento);
    }

    [Fact]
    public async Task Concluir_ChamadoAindaEncaminhado_Falha()
    {
        var db = CriarDb(nameof(Concluir_ChamadoAindaEncaminhado_Falha));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.Encaminhada);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConcluirExecucaoSuporteIaCommandHandler(db)
                .Handle(new ConcluirExecucaoSuporteIaCommand(id, null), CancellationToken.None));
    }

    [Theory]
    [InlineData(StatusSolicitacaoSuporteIa.Encaminhada)]
    [InlineData(StatusSolicitacaoSuporteIa.EmAnaliseTecnica)]
    [InlineData(StatusSolicitacaoSuporteIa.Reaberta)]
    public async Task Recusar_EstadosPermitidos_MoveParaCancelada(StatusSolicitacaoSuporteIa statusInicial)
    {
        var db = CriarDb(nameof(Recusar_EstadosPermitidos_MoveParaCancelada) + statusInicial);
        var id = await CriarSolicitacaoAsync(db, statusInicial);

        var resultado = await new RecusarSolicitacaoSuporteIaCommandHandler(db, new FilaCalendarioNula())
            .Handle(new RecusarSolicitacaoSuporteIaCommand(id, "Duplicado de outro chamado"), CancellationToken.None);

        Assert.Equal(StatusSolicitacaoSuporteIa.Cancelada, resultado.Status);
        Assert.Equal("Duplicado de outro chamado", resultado.NotaFechamento);
    }

    [Fact]
    public async Task Recusar_ChamadoResolvido_Falha()
    {
        var db = CriarDb(nameof(Recusar_ChamadoResolvido_Falha));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.Resolvida);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RecusarSolicitacaoSuporteIaCommandHandler(db, new FilaCalendarioNula())
                .Handle(new RecusarSolicitacaoSuporteIaCommand(id, "Motivo"), CancellationToken.None));
    }

    [Fact]
    public async Task Validar_ConfirmadoPeloSolicitante_MoveParaResolvida()
    {
        var db = CriarDb(nameof(Validar_ConfirmadoPeloSolicitante_MoveParaResolvida));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.AguardandoValidacao, solicitanteEmail: "quem-abriu@aahbrant.com");

        var resultado = await new ValidarSolicitacaoSuporteIaCommandHandler(db, new FilaCalendarioNula())
            .Handle(new ValidarSolicitacaoSuporteIaCommand(id, true, null, null, "quem-abriu@aahbrant.com"), CancellationToken.None);

        Assert.Equal(StatusSolicitacaoSuporteIa.Resolvida, resultado.Status);
        Assert.True(resultado.ValidacaoConfirmada);
    }

    [Fact]
    public async Task Validar_NaoConfirmado_ReabreChamado()
    {
        var db = CriarDb(nameof(Validar_NaoConfirmado_ReabreChamado));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.Respondida, solicitanteEmail: "quem-abriu@aahbrant.com");

        var resultado = await new ValidarSolicitacaoSuporteIaCommandHandler(db, new FilaCalendarioNula())
            .Handle(new ValidarSolicitacaoSuporteIaCommand(id, false, "Ainda não resolveu", null, "quem-abriu@aahbrant.com"), CancellationToken.None);

        Assert.Equal(StatusSolicitacaoSuporteIa.Reaberta, resultado.Status);
        Assert.False(resultado.ValidacaoConfirmada);
    }

    [Fact]
    public async Task Validar_PorPessoaDiferenteDoSolicitante_Falha()
    {
        var db = CriarDb(nameof(Validar_PorPessoaDiferenteDoSolicitante_Falha));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.AguardandoValidacao, solicitanteEmail: "quem-abriu@aahbrant.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ValidarSolicitacaoSuporteIaCommandHandler(db, new FilaCalendarioNula())
                .Handle(new ValidarSolicitacaoSuporteIaCommand(id, true, null, null, "outra-pessoa@aahbrant.com"), CancellationToken.None));
    }

    [Fact]
    public async Task Validar_ChamadoAindaEncaminhado_Falha()
    {
        var db = CriarDb(nameof(Validar_ChamadoAindaEncaminhado_Falha));
        var id = await CriarSolicitacaoAsync(db, StatusSolicitacaoSuporteIa.Encaminhada, solicitanteEmail: "quem-abriu@aahbrant.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ValidarSolicitacaoSuporteIaCommandHandler(db, new FilaCalendarioNula())
                .Handle(new ValidarSolicitacaoSuporteIaCommand(id, true, null, null, "quem-abriu@aahbrant.com"), CancellationToken.None));
    }

    // O handler não recebe validação do FluentValidation por conta própria — quem chama garante
    // isso via pipeline do MediatR (ou o controller). Testado à parte para não depender do pipeline.
    [Fact]
    public void Validador_NaoConfirmadoSemComentario_ExigeComentario()
    {
        var validador = new ValidarSolicitacaoSuporteIaCommandValidator();

        var resultado = validador.Validate(new ValidarSolicitacaoSuporteIaCommand(Guid.NewGuid(), false, null, null, "a@b.com"));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Validador_ConfirmadoSemComentario_NaoExigeComentario()
    {
        var validador = new ValidarSolicitacaoSuporteIaCommandValidator();

        var resultado = validador.Validate(new ValidarSolicitacaoSuporteIaCommand(Guid.NewGuid(), true, null, null, "a@b.com"));

        Assert.True(resultado.IsValid);
    }
}
