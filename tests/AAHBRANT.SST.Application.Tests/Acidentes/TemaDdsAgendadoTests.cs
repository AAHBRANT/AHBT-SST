using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.Commands;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

// Ocorrência registrada por relato agenda, obrigatoriamente, um tema para o DDS do próximo dia útil
// da obra; encerrar o DDS com esse tema conclui a ação do plano (08/10/2026).
public class TemaDdsAgendadoTests
{
    [Theory]
    [InlineData("2026-10-08", "2026-10-09")] // quinta → sexta
    [InlineData("2026-10-09", "2026-10-12")] // sexta → segunda
    [InlineData("2026-10-10", "2026-10-12")] // sábado → segunda
    [InlineData("2026-10-11", "2026-10-12")] // domingo → segunda
    public void ProximoDiaUtil_PulaFimDeSemana(string dia, string esperado)
        => Assert.Equal(DateTime.Parse(esperado), CalendarioObra.ProximoDiaUtil(DateTime.Parse(dia)));

    [Fact]
    public void Validador_RelatoComPlanoSemTema_Recusa()
    {
        var comando = Comando(Guid.NewGuid()) with { AcoesPlano = new List<AcaoPlanoNovaOcorrencia>(), TemaDds = null };
        var resultado = new CriarAcidenteCommandValidator().Validate(comando);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("tema do DDS"));
    }

    [Fact]
    public void Validador_RegistroManualSemPlano_NaoExigeTema()
        => Assert.True(new CriarAcidenteCommandValidator().Validate(Comando(Guid.NewGuid())).IsValid);

    [Fact]
    public async Task Registro_AgendaTemaEConcluiAcaoAoEncerrarDds()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        var tecnico = new Usuario { Nome = "Técnico", Email = "t@x.com" };
        db.AddRange(obra, tecnico);
        await db.SaveChangesAsync();

        var comando = Comando(obra.Id) with
        {
            AcoesPlano = new List<AcaoPlanoNovaOcorrencia>(),
            TemaDds = new TemaDdsNovaOcorrencia("Uso seguro de escada de mão", "Recentemente tivemos uma queda..."),
            UsuarioAtualId = tecnico.Id,
        };
        var acidenteId = await new CriarAcidenteCommandHandler(db, new PublicadorGrhFalso()).Handle(comando, default);

        var agendamento = await db.TemasDdsAgendados.Include(t => t.CatalogoTemaDds).SingleAsync();
        var acaoDds = await db.AcoesPlano.SingleAsync(a => a.OrigemId == acidenteId);
        Assert.Equal("Uso seguro de escada de mão", agendamento.CatalogoTemaDds!.Nome);
        Assert.Equal(CalendarioObra.ProximoDiaUtil(CalendarioObra.AgoraEmBrasilia()), agendamento.Data);
        Assert.Equal(acaoDds.Id, agendamento.AcaoPlanoId);
        Assert.Equal(tecnico.Id, acaoDds.ResponsavelUsuarioId);
        Assert.Equal(agendamento.Data, acaoDds.Prazo);

        // Aparece para o DDS daquele dia na obra.
        var sugeridos = await new ListarTemasDdsAgendadosQueryHandler(db).Handle(new ListarTemasDdsAgendadosQuery(obra.Id, agendamento.Data), default);
        Assert.Equal(agendamento.CatalogoTemaDdsId, Assert.Single(sugeridos).CatalogoTemaDdsId);

        // Registro do dia criado pela tela com OUTRO tema: o servidor impõe o tema agendado (fixo).
        var outroTema = new CatalogoTemaDds { Nome = "Outubro Rosa" };
        var atividade = new Atividade { ObraId = obra.Id, Nome = "Soldagem" };
        var semanal = new DdsSemanal
        {
            ObraId = obra.Id, ResponsavelUsuarioId = tecnico.Id,
            DataInicioSemana = agendamento.Data.AddDays(-(((int)agendamento.Data.DayOfWeek + 6) % 7)),
        };
        semanal.DataFimSemana = semanal.DataInicioSemana.AddDays(4);
        db.AddRange(outroTema, atividade, semanal);
        await db.SaveChangesAsync();

        var ddsId = await new CriarDdsCommandHandler(db, new GeradorNumeroDocumentoService(db))
            .Handle(new CriarDdsCommand(semanal.Id, new List<Guid> { atividade.Id }, agendamento.Data, outroTema.Id), default);
        var dds = await db.Dds.SingleAsync(d => d.Id == ddsId);
        Assert.Equal(agendamento.CatalogoTemaDdsId, dds.CatalogoTemaDdsId);
        Assert.Equal("Uso seguro de escada de mão", dds.TemaLivreNome);
        Assert.Equal(ddsId, (await db.TemasDdsAgendados.SingleAsync()).DdsId); // vinculado na criação
        Assert.Empty(await new ListarTemasDdsAgendadosQueryHandler(db).Handle(new ListarTemasDdsAgendadosQuery(obra.Id, agendamento.Data), default));

        // DDS encerrado → ação "DDS do dia seguinte" concluída.
        for (var i = 1; i <= 3; i++)
            db.DdsFotosEvidencia.Add(new DdsFotoEvidencia { DdsId = ddsId, Ordem = i, FotoConteudo = new byte[] { 1 }, FotoContentType = "image/jpeg" });
        await db.SaveChangesAsync();
        await new EncerrarDdsCommandHandler(db).Handle(new EncerrarDdsCommand(ddsId), default);

        Assert.Equal(StatusControleRisco.Concluido, (await db.AcoesPlano.SingleAsync(a => a.Id == acaoDds.Id)).Status);
        Assert.NotNull((await db.TemasDdsAgendados.SingleAsync()).AplicadoEm);
    }

    [Fact]
    public async Task TemaDeDiaSemDds_SegueParaOProximoDds()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Ponte" };
        var tema = new CatalogoTemaDds { Nome = "Tema pendente" };
        db.AddRange(obra, tema, new TemaDdsAgendado
        {
            ObraId = obra.Id, Data = new DateTime(2026, 10, 9), CatalogoTemaDdsId = tema.Id, OrigemTipo = "Acidente",
        });
        await db.SaveChangesAsync();

        // Sexta ficou sem DDS: na segunda o tema ainda aparece (e seria o fixo do registro).
        var segunda = await new ListarTemasDdsAgendadosQueryHandler(db).Handle(new ListarTemasDdsAgendadosQuery(obra.Id, new DateTime(2026, 10, 12)), default);
        Assert.Equal("Tema pendente", Assert.Single(segunda).Nome);
        // Mas não antes do dia previsto.
        Assert.Empty(await new ListarTemasDdsAgendadosQueryHandler(db).Handle(new ListarTemasDdsAgendadosQuery(obra.Id, new DateTime(2026, 10, 8)), default));
    }

    private static CriarAcidenteCommand Comando(Guid obraId) => new(
        TipoOcorrencia.Acidente, obraId, null, null, "Bloco B", new DateTime(2026, 10, 8), null, "Queda de escada",
        null, null, null, false, null, null, null, null, GravidadeAcidente.SemAfastamento, null);

    private sealed class PublicadorGrhFalso : IPublicadorAcidenteGrh
    {
        public Task PublicarAsync(AcidenteGrhEvento evento, CancellationToken ct = default) => Task.CompletedTask;
    }
}
