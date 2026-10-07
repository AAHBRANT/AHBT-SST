using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using DdsEntidade = AAHBRANT.SST.Domain.Entidades.Dds;

namespace AAHBRANT.SST.Application.Tests.Dds;

public class ResumoDdsTelegramTests
{
    private sealed class TelegramFalso : ITelegramResumoService
    {
        public List<string> Mensagens { get; } = new();
        public Task EnviarAsync(string mensagem, CancellationToken ct = default)
        {
            Mensagens.Add(mensagem);
            return Task.CompletedTask;
        }
    }

    // Só o Send<TResponse> é usado pelo handler (a consulta de cadastros fracos).
    private sealed class MediatorFalso : IMediator
    {
        public List<CadastroFacialFracoDto> Fracos { get; } = new();
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse)(object)Fracos);
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    [Fact]
    public async Task Resumo_TemQuantidades_MatriculasENaoTemNomeDeFuncionario()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra Sul", Codigo = "S" };
        var usuario = new Usuario { Nome = "Responsável", Email = "dds@example.test" };
        var dds = new DdsEntidade { Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario, ResponsavelUsuarioId = usuario.Id, Data = new DateTime(2026, 10, 7) };
        var ana = new Trabalhador { Nome = "Ana Segredo", Matricula = "31", Obra = obra, ObraId = obra.Id };
        var bruno = new Trabalhador { Nome = "Bruno", Matricula = "16", Obra = obra, ObraId = obra.Id };
        var carla = new Trabalhador { Nome = "Carla", Matricula = "20", Obra = obra, ObraId = obra.Id };
        db.AddRange(obra, usuario, dds, ana, bruno, carla);
        db.DdsParticipantes.Add(new DdsParticipante { DdsId = dds.Id, TrabalhadorId = ana.Id, FotoTipo = TipoFotoParticipante.Facial });
        db.DdsParticipantes.Add(new DdsParticipante { DdsId = dds.Id, TrabalhadorId = bruno.Id, FotoTipo = TipoFotoParticipante.Biometria });
        db.DdsFuncionariosSelecionados.Add(new DdsFuncionarioSelecionado { DdsId = dds.Id, TrabalhadorId = carla.Id });
        await db.SaveChangesAsync();
        // O CreatedAtUtc do DDS é preenchido na gravação; as falhas vêm depois dele.
        for (var i = 0; i < 4; i++)
            db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial { ObraId = obra.Id, Motivo = MotivoFalhaFacial.ConfiancaBaixa, OcorridaEm = DateTime.UtcNow.AddMinutes(1) });
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();
        var mediator = new MediatorFalso();
        mediator.Fracos.Add(new CadastroFacialFracoDto(ana.Id, "Ana Segredo", "31", obra.Id, "Obra Sul", 4, "Baixa confiança", DateTime.UtcNow, DateTime.UtcNow.AddDays(-9)));

        await new EnviarResumoDdsTelegramCommandHandler(db, mediator, telegram)
            .Handle(new EnviarResumoDdsTelegramCommand(dds.Id), CancellationToken.None);

        var msg = Assert.Single(telegram.Mensagens);
        Assert.Contains("DDS encerrado — Obra Sul", msg);
        Assert.Contains("Presenças: 2 de 3", msg);
        Assert.Contains("Facial: 1", msg);
        Assert.Contains("Digital: 1", msg);
        Assert.Contains("Falhas do facial: 4", msg);
        Assert.Contains("Mat. 31 (4)", msg);
        Assert.DoesNotContain("Ana", msg); // sem nome de funcionário no Telegram
        Assert.DoesNotContain("Bruno", msg);
    }

    [Fact]
    public async Task Resumo_SemFracos_NaoPedeRevisao()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra", Codigo = "O" };
        var usuario = new Usuario { Nome = "R", Email = "r@example.test" };
        var dds = new DdsEntidade { Obra = obra, ObraId = obra.Id, ResponsavelUsuario = usuario, ResponsavelUsuarioId = usuario.Id, Data = new DateTime(2026, 10, 7) };
        db.AddRange(obra, usuario, dds);
        await db.SaveChangesAsync();
        var telegram = new TelegramFalso();

        await new EnviarResumoDdsTelegramCommandHandler(db, new MediatorFalso(), telegram)
            .Handle(new EnviarResumoDdsTelegramCommand(dds.Id), CancellationToken.None);

        Assert.DoesNotContain("Revisar o cadastro", Assert.Single(telegram.Mensagens));
    }
}
