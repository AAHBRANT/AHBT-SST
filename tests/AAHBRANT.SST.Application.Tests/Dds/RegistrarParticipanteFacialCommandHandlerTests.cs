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

public class RegistrarParticipanteFacialCommandHandlerTests
{
    private sealed class FacialFalso : IAutenticacaoFacialService
    {
        private readonly ResultadoIdentificacaoFacial _resultado;
        public Guid? ObraConsultada { get; private set; }

        public FacialFalso(ResultadoIdentificacaoFacial resultado) => _resultado = resultado;

        public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => Task.CompletedTask;

        public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
        {
            ObraConsultada = obraId;
            return Task.FromResult(_resultado);
        }
    }

    private static ResultadoIdentificacaoFacial Reconheceu(Guid trabalhadorId, double confianca = 0.93) => new(
        true, new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.ReconhecimentoFacial), null, confianca);

    private static SstDbContext CriarDb() => new(
        new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new CurrentUserService());

    private static async Task<(DdsEntidade Dds, Trabalhador Ana, Trabalhador Bruno, Trabalhador OutraObra)> Semear(SstDbContext db)
    {
        var obra = new Obra { Nome = "Obra A", Codigo = "A" };
        var outraObra = new Obra { Nome = "Obra B", Codigo = "B" };
        var usuario = new Usuario { Nome = "Responsável", Email = "dds@example.test" };
        var dds = new DdsEntidade
        {
            Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario, ResponsavelUsuarioId = usuario.Id,
            Data = new DateTime(2026, 9, 29),
        };
        var ana = new Trabalhador { Nome = "Ana", Matricula = "001", Obra = obra, ObraId = obra.Id };
        var bruno = new Trabalhador { Nome = "Bruno", Matricula = "002", Obra = obra, ObraId = obra.Id };
        var outro = new Trabalhador { Nome = "Outra obra", Obra = outraObra, ObraId = outraObra.Id };
        db.AddRange(dds, ana, bruno, outro);
        await db.SaveChangesAsync();
        return (dds, ana, bruno, outro);
    }

    private static RegistrarParticipanteFacialCommandHandler Handler(SstDbContext db, IAutenticacaoFacialService facial) =>
        new(db, facial, null!, null!, NullLogger<RegistrarParticipanteFacialCommandHandler>.Instance);

    private static readonly byte[] Foto = { 1, 2, 3, 4 };

    [Fact]
    public async Task RostoDoFuncionarioDaLinha_RegistraPresencaFacialComFotoDeEvidencia()
    {
        await using var db = CriarDb();
        var (dds, ana, _, _) = await Semear(db);
        db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = dds.Id, TrabalhadorId = ana.Id });
        await db.SaveChangesAsync();
        var facial = new FacialFalso(Reconheceu(ana.Id, 0.93));

        var id = await Handler(db, facial).Handle(new RegistrarParticipanteFacialCommand(dds.Id, ana.Id, Foto), default);

        var participante = await db.DdsParticipantes.SingleAsync(p => p.Id == id);
        Assert.Equal(ana.Id, participante.TrabalhadorId);
        Assert.Equal(TipoFotoParticipante.Facial, participante.FotoTipo);
        Assert.Equal(Foto, participante.FotoConteudo);
        Assert.Equal("image/jpeg", participante.FotoContentType);
        Assert.Equal(93, participante.ScoreConfianca!.Value, precision: 6);
        // ObraId sai do DDS, nunca do cliente.
        Assert.Equal(dds.ObraId, facial.ObraConsultada);
        // Presença confirmada sai da lista de selecionados pendentes.
        Assert.Empty(await db.DdsFuncionariosSelecionados.ToListAsync());
    }

    // Sem isso a presença de A poderia ser gravada com o rosto de B, bastando clicar na linha errada.
    [Fact]
    public async Task RostoDeOutroFuncionario_RecusaENaoGravaPresenca()
    {
        await using var db = CriarDb();
        var (dds, ana, bruno, _) = await Semear(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, new FacialFalso(Reconheceu(bruno.Id))).Handle(new RegistrarParticipanteFacialCommand(dds.Id, ana.Id, Foto), default));

        Assert.Contains("não corresponde", ex.Message);
        Assert.Empty(await db.DdsParticipantes.ToListAsync());
    }

    [Theory]
    [InlineData(MotivoRejeicaoFacial.NenhumRostoDetectado, "Nenhum rosto")]
    [InlineData(MotivoRejeicaoFacial.MultiplosRostosDetectados, "Mais de uma pessoa")]
    [InlineData(MotivoRejeicaoFacial.ConfiancaBaixa, "baixa confiança")]
    [InlineData(MotivoRejeicaoFacial.RostoNaoReconhecido, "não reconhecido")]
    public async Task RejeicaoDoReconhecimento_LancaRejeicaoFacialComMensagemDoMotivo(MotivoRejeicaoFacial motivo, string trecho)
    {
        await using var db = CriarDb();
        var (dds, ana, _, _) = await Semear(db);
        var rejeitado = new ResultadoIdentificacaoFacial(false, null, motivo, null);

        var ex = await Assert.ThrowsAsync<RejeicaoFacialException>(() =>
            Handler(db, new FacialFalso(rejeitado)).Handle(new RegistrarParticipanteFacialCommand(dds.Id, ana.Id, Foto), default));

        Assert.Equal(motivo, ex.Motivo);
        Assert.Contains(trecho, ex.Message);
        Assert.Empty(await db.DdsParticipantes.ToListAsync());
    }

    [Fact]
    public async Task FuncionarioDeOutraObra_Recusa()
    {
        await using var db = CriarDb();
        var (dds, _, _, outro) = await Semear(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, new FacialFalso(Reconheceu(outro.Id))).Handle(new RegistrarParticipanteFacialCommand(dds.Id, outro.Id, Foto), default));

        Assert.Empty(await db.DdsParticipantes.ToListAsync());
    }

    [Fact]
    public async Task FuncionarioQueJaTemPresenca_RecusaSemConsultarOAzure()
    {
        await using var db = CriarDb();
        var (dds, ana, _, _) = await Semear(db);
        db.DdsParticipantes.Add(new DdsParticipante { DdsId = dds.Id, TrabalhadorId = ana.Id, FotoTipo = TipoFotoParticipante.Biometria });
        await db.SaveChangesAsync();
        var facial = new FacialFalso(Reconheceu(ana.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, facial).Handle(new RegistrarParticipanteFacialCommand(dds.Id, ana.Id, Foto), default));

        Assert.Contains("já está registrado", ex.Message);
        Assert.Null(facial.ObraConsultada);
    }

    [Fact]
    public async Task DdsEncerrado_Recusa()
    {
        await using var db = CriarDb();
        var (dds, ana, _, _) = await Semear(db);
        dds.Status = StatusDds.Concluido;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(db, new FacialFalso(Reconheceu(ana.Id))).Handle(new RegistrarParticipanteFacialCommand(dds.Id, ana.Id, Foto), default));
    }
}
