using AAHBRANT.SST.Application.Cameras;
using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Cameras.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Cameras;

// Fotos de ocorrência são dado pessoal: o usuário restrito a uma obra não pode ver câmeras nem
// ocorrências de outra, e registros excluídos logicamente somem das consultas.
public class CamerasEscopoTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0x10, 0x20 };

    private sealed class AnalisadorQueNaoDeveSerChamado : IAnalisadorEpiImagem
    {
        public Task<ResultadoAnaliseEpi> AnalisarAsync(byte[] imagemJpeg, CancellationToken ct)
            => throw new InvalidOperationException("A IA não deveria ser chamada.");
    }

    private sealed class TelegramInerte : ITelegramResumoService
    {
        public Task EnviarAsync(string mensagem, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EnviarImagemAsync(byte[] imagemPng, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarDocumentoAsync(byte[] arquivo, string nomeArquivo, string legenda, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> EnviarFotoAsync(byte[] imagemJpeg, string legenda, CancellationToken ct = default) => Task.FromResult(true);
    }

    // Mesma base InMemory, dois contextos: um global (para semear) e um restrito à obra A.
    private static async Task<(SstDbContext restrito, Camera cameraB, OcorrenciaEpiCamera ocorrenciaB, SstDbContext global)> Preparar(string nomeBanco)
    {
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        var global = new SstDbContext(opcoes, new CurrentUserService());

        var obraA = new Obra { Codigo = "OA", Nome = "Obra A" };
        var obraB = new Obra { Codigo = "OB", Nome = "Obra B" };
        var cameraB = new Camera { ObraId = obraB.Id, Nome = "Portaria B" };
        var ocorrenciaB = new OcorrenciaEpiCamera
        {
            CameraId = cameraB.Id, ObraId = obraB.Id, Foto = new byte[] { 0xFF, 0xD8, 0x01 },
            RecebidaEmUtc = DateTime.UtcNow, CapturadaEmUtc = DateTime.UtcNow, PessoasNaCena = 1, SemCapacete = 1,
        };
        global.AddRange(obraA, obraB, cameraB, ocorrenciaB);
        await global.SaveChangesAsync();

        var usuario = new CurrentUserService();
        usuario.DefinirEscopo(false, new[] { obraA.Id });
        return (new SstDbContext(opcoes, usuario), cameraB, ocorrenciaB, global);
    }

    [Fact]
    public async Task ListarCameras_UsuarioDeOutraObra_NaoVeCameras()
    {
        var (restrito, _, _, global) = await Preparar(nameof(ListarCameras_UsuarioDeOutraObra_NaoVeCameras));

        Assert.Single(await new ListarCamerasQueryHandler(global).Handle(new ListarCamerasQuery(null), default));
        Assert.Empty(await new ListarCamerasQueryHandler(restrito).Handle(new ListarCamerasQuery(null), default));
    }

    [Fact]
    public async Task ListarOcorrencias_UsuarioDeOutraObra_NaoVeOcorrencias()
    {
        var (restrito, _, _, global) = await Preparar(nameof(ListarOcorrencias_UsuarioDeOutraObra_NaoVeOcorrencias));

        Assert.Single(await new ListarOcorrenciasEpiQueryHandler(global)
            .Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null), default));
        Assert.Empty(await new ListarOcorrenciasEpiQueryHandler(restrito)
            .Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null), default));
    }

    [Fact]
    public async Task ObterFoto_OcorrenciaDeOutraObra_LancaKeyNotFound()
    {
        var (restrito, _, ocorrenciaB, global) = await Preparar(nameof(ObterFoto_OcorrenciaDeOutraObra_LancaKeyNotFound));

        Assert.NotEmpty(await new ObterFotoOcorrenciaEpiQueryHandler(global).Handle(new ObterFotoOcorrenciaEpiQuery(ocorrenciaB.Id), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ObterFotoOcorrenciaEpiQueryHandler(restrito).Handle(new ObterFotoOcorrenciaEpiQuery(ocorrenciaB.Id), default));
    }

    [Fact]
    public async Task AnalisarQuadro_CameraDeOutraObra_LancaKeyNotFound()
    {
        var (restrito, cameraB, _, _) = await Preparar(nameof(AnalisarQuadro_CameraDeOutraObra_LancaKeyNotFound));
        var handler = new AnalisarQuadroCameraCommandHandler(restrito, new AnalisadorQueNaoDeveSerChamado(), new TelegramInerte(),
            NullLogger<AnalisarQuadroCameraCommandHandler>.Instance);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AnalisarQuadroCameraCommand(cameraB.Id, Jpeg, DateTime.UtcNow), default));
    }

    [Fact]
    public async Task CameraEOcorrenciaExcluidas_NaoAparecemNasConsultas()
    {
        var (_, cameraB, ocorrenciaB, global) = await Preparar(nameof(CameraEOcorrenciaExcluidas_NaoAparecemNasConsultas));
        (await global.Cameras.SingleAsync(c => c.Id == cameraB.Id)).Ativo = false;
        (await global.OcorrenciasEpiCamera.SingleAsync(o => o.Id == ocorrenciaB.Id)).Ativo = false;
        await global.SaveChangesAsync();

        Assert.Empty(await new ListarCamerasQueryHandler(global).Handle(new ListarCamerasQuery(null), default));
        Assert.Empty(await new ListarOcorrenciasEpiQueryHandler(global)
            .Handle(new ListarOcorrenciasEpiQuery(null, null, null, null, null), default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ObterFotoOcorrenciaEpiQueryHandler(global).Handle(new ObterFotoOcorrenciaEpiQuery(ocorrenciaB.Id), default));
    }
}
