using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DdsEntidade = AAHBRANT.SST.Domain.Entidades.Dds;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class RegistrarParticipanteFacialFilaTests
{
    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        private readonly ResultadoIdentificacaoFacial _resultado;
        public bool? ExigiuMargem { get; private set; }
        public FacialFalso(ResultadoIdentificacaoFacial resultado) => _resultado = resultado;
        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;
        public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => Task.CompletedTask;
        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = false)
        {
            ExigiuMargem = exigirMargemSobreSegundoColocado;
            return Task.FromResult(_resultado);
        }
    }

    private static SstDbContext CriarDb() => new(
        new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new CurrentUserService());

    private static ResultadoIdentificacaoFacial Reconheceu(Guid trabalhadorId, double confianca = 0.93) => new(
        true, new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.ReconhecimentoFacial), null, confianca);

    private static RegistrarParticipanteFacialFilaCommandHandler Handler(SstDbContext db, IAutenticacaoFacialService facial) =>
        new(db, facial, null!, null!, NullLogger<RegistrarParticipanteFacialFilaCommandHandler>.Instance);

    private static async Task<(DdsEntidade Dds, Trabalhador Ana)> Semear(SstDbContext db, StatusDds status = StatusDds.EmAndamento)
    {
        var obra = new Obra { Nome = "Obra A", Codigo = "A" };
        var usuario = new Usuario { Nome = "Responsável", Email = "dds@example.test" };
        var dds = new DdsEntidade
        {
            Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario, ResponsavelUsuarioId = usuario.Id,
            Data = new DateTime(2026, 10, 7), Status = status,
        };
        var ana = new Trabalhador { Nome = "Ana", Matricula = "001", Obra = obra, ObraId = obra.Id };
        db.AddRange(dds, ana);
        await db.SaveChangesAsync();
        return (dds, ana);
    }

    private static readonly byte[] Foto = { 1, 2, 3, 4 };

    [Fact]
    public async Task RostoReconhecido_RegistraPresencaFacial_ExigindoMargemSobreSegundo()
    {
        await using var db = CriarDb();
        var (dds, ana) = await Semear(db);
        db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = dds.Id, TrabalhadorId = ana.Id });
        await db.SaveChangesAsync();
        var facial = new FacialFalso(Reconheceu(ana.Id, 0.93));

        var r = await Handler(db, facial).Handle(new RegistrarParticipanteFacialFilaCommand(dds.Id, Foto), CancellationToken.None);

        Assert.Equal(ana.Id, r.TrabalhadorId);
        Assert.Equal("Ana", r.TrabalhadorNome);
        Assert.False(r.JaConfirmado);
        Assert.True(facial.ExigiuMargem);
        var p = await db.DdsParticipantes.SingleAsync();
        Assert.Equal(TipoFotoParticipante.Facial, p.FotoTipo);
        Assert.Equal(93, p.ScoreConfianca!.Value, 3);
        Assert.Empty(await db.DdsFuncionariosSelecionados.ToListAsync()); // saiu da lista de pendentes
    }

    [Fact]
    public async Task PresencaJaConfirmada_NaoGravaDeNovoEAvisa()
    {
        await using var db = CriarDb();
        var (dds, ana) = await Semear(db);
        var facial = new FacialFalso(Reconheceu(ana.Id));
        var handler = Handler(db, facial);
        await handler.Handle(new RegistrarParticipanteFacialFilaCommand(dds.Id, Foto), CancellationToken.None);

        var r = await handler.Handle(new RegistrarParticipanteFacialFilaCommand(dds.Id, Foto), CancellationToken.None);

        Assert.True(r.JaConfirmado);
        Assert.Equal("Ana", r.TrabalhadorNome);
        Assert.Equal(1, await db.DdsParticipantes.CountAsync());
    }

    [Fact]
    public async Task RostoRecusado_LancaRejeicaoFacialESemPresenca()
    {
        await using var db = CriarDb();
        var (dds, _) = await Semear(db);
        var facial = new FacialFalso(new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoAmbiguo, 0.9));

        var erro = await Assert.ThrowsAsync<RejeicaoFacialException>(() =>
            Handler(db, facial).Handle(new RegistrarParticipanteFacialFilaCommand(dds.Id, Foto), CancellationToken.None));

        Assert.Equal(MotivoRejeicaoFacial.RostoAmbiguo, erro.Motivo);
        Assert.Contains("parecido", erro.Message);
        Assert.Empty(await db.DdsParticipantes.ToListAsync());
    }

    [Fact]
    public async Task DdsEncerrado_EhRecusadoSemChamarOAzure()
    {
        await using var db = CriarDb();
        var (dds, ana) = await Semear(db, StatusDds.Concluido);
        var facial = new FacialFalso(Reconheceu(ana.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, facial).Handle(new RegistrarParticipanteFacialFilaCommand(dds.Id, Foto), CancellationToken.None));

        Assert.Null(facial.ExigiuMargem);
    }

    [Fact]
    public async Task FuncionarioDeOutraObra_EhRecusado()
    {
        await using var db = CriarDb();
        var (dds, _) = await Semear(db);
        var outraObra = new Obra { Nome = "Obra B", Codigo = "B" };
        var intruso = new Trabalhador { Nome = "Intruso", Obra = outraObra, ObraId = outraObra.Id };
        db.AddRange(outraObra, intruso);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, new FacialFalso(Reconheceu(intruso.Id))).Handle(new RegistrarParticipanteFacialFilaCommand(dds.Id, Foto), CancellationToken.None));

        Assert.Empty(await db.DdsParticipantes.ToListAsync());
    }
}
