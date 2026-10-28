using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Tests.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.SessoesTreinamento;

// O instrutor da turma assina todos os certificados com uma leitura de digital. Os certificados de
// turma encerrada já saem finalizados: o comando reabre, assina e finaliza de novo.
public class AssinarTurmaComoInstrutorCommandTests
{
    private sealed record Cenario(SstDbContext Db, SessaoTreinamento Sessao, Trabalhador Instrutor, List<DocumentoAssinatura> Documentos, MediatorSimulado Mediator);

    private static Trabalhador NovoTrabalhador(string nome, string cpf) =>
        new() { Nome = nome, Cpf = cpf, Funcao = new Funcao { Nome = "Técnico de Segurança" } };

    private static async Task<Cenario> PrepararAsync(int certificados = 2, bool instrutorEscolhido = true, bool concluida = true)
    {
        var db = DbContextFactory.Criar();
        var instrutor = NovoTrabalhador("Gilton", "11122233344");
        var gabriel = NovoTrabalhador("Gabriel", "99988877766");
        db.Trabalhadores.AddRange(instrutor, gabriel);
        db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = instrutor.Id, PodeAssinarEmOutrasObras = true });

        var sessao = new SessaoTreinamento
        {
            Status = concluida ? StatusSessaoTreinamento.Concluida : StatusSessaoTreinamento.EmAndamento,
            InstrutorTrabalhadorId = instrutorEscolhido ? instrutor.Id : null,
        };
        var documentos = new List<DocumentoAssinatura>();
        for (var i = 0; i < certificados; i++)
        {
            var participante = NovoTrabalhador($"Participante {i}", $"5556667778{i}");
            var treinamento = new Treinamento { TrabalhadorId = participante.Id };
            // Certificado já finalizado, com a assinatura automática antiga de quem encerrou a turma.
            var documento = new DocumentoAssinatura
            {
                EntidadeTipo = nameof(Treinamento), EntidadeId = treinamento.Id,
                Status = StatusDocumentoAssinatura.Finalizado, ConteudoHash = "hash-antigo", TokenValidacaoPublica = "token-fixo",
            };
            documento.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = gabriel.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.SessaoLogada, AssinadoEm = DateTime.UtcNow });
            db.Trabalhadores.Add(participante);
            db.Treinamentos.Add(treinamento);
            db.DocumentosAssinatura.Add(documento);
            documentos.Add(documento);
            sessao.Participantes.Add(new ParticipanteSessaoTreinamento { TrabalhadorId = participante.Id, TreinamentoGeradoId = treinamento.Id });
        }
        db.SessoesTreinamento.Add(sessao);
        await db.SaveChangesAsync();
        return new Cenario(db, sessao, instrutor, documentos, new MediatorSimulado(_ => null));
    }

    private static AssinarTurmaComoInstrutorCommandHandler Handler(Cenario c) =>
        new(c.Db, c.Mediator, new AutenticacaoBiometriaLocalFalsa(), new RegistradorAssinaturaService(c.Db, new AuditoriaServiceFalsa()),
            new AuditoriaServiceFalsa(), NullLogger<AssinarTurmaComoInstrutorCommandHandler>.Instance);

    private static AssinarTurmaComoInstrutorCommand Comando(Cenario c, Guid? trabalhadorId = null) =>
        new(c.Sessao.Id, Guid.NewGuid(), "segredo", trabalhadorId ?? c.Instrutor.Id, 90);

    [Fact]
    public async Task InstrutorAssinaTodosOsCertificados_ReabreOsFinalizadosEPedeNovaFinalizacao()
    {
        var c = await PrepararAsync(certificados: 2);

        var resultado = await Handler(c).Handle(Comando(c), CancellationToken.None);

        Assert.Equal(2, resultado.Assinados);
        Assert.Equal(0, resultado.Falhas);
        foreach (var d in c.Documentos)
        {
            var assinaturas = await c.Db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == d.Id).ToListAsync();
            Assert.Contains(assinaturas, s => s.Papel == PapelAssinatura.Instrutor && s.TrabalhadorId == c.Instrutor.Id);
            // A assinatura antiga continua no cofre.
            Assert.Contains(assinaturas, s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada);
        }
        // O token de validação não muda (o QR já impresso continua valendo).
        Assert.All(await c.Db.DocumentosAssinatura.ToListAsync(), d => Assert.Equal("token-fixo", d.TokenValidacaoPublica));
        Assert.Equal(2, c.Mediator.Requisicoes.OfType<FinalizarDocumentoCommand>().Count());
    }

    [Fact]
    public async Task DigitalDeOutroTrabalhador_NaoAssinaNoLugarDoInstrutorDaTurma()
    {
        var c = await PrepararAsync();
        var outro = NovoTrabalhador("Outro", "12312312312");
        c.Db.Trabalhadores.Add(outro);
        c.Db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = outro.Id });
        await c.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(c).Handle(Comando(c, outro.Id), CancellationToken.None));
        Assert.Empty(await c.Db.DocumentoSignatarios.Where(s => s.Papel == PapelAssinatura.Instrutor).ToListAsync());
    }

    [Fact]
    public async Task SemInstrutorEscolhido_NaoAssina()
    {
        var c = await PrepararAsync(instrutorEscolhido: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(c).Handle(Comando(c), CancellationToken.None));
    }

    [Fact]
    public async Task TurmaNaoEncerrada_NaoAssina()
    {
        var c = await PrepararAsync(concluida: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(c).Handle(Comando(c), CancellationToken.None));
    }

    [Fact]
    public async Task SegundaLeitura_NaoRepeteAAssinatura()
    {
        var c = await PrepararAsync(certificados: 2);
        await Handler(c).Handle(Comando(c), CancellationToken.None);
        // Simula o fim da finalização (o mediator de teste não finaliza).
        foreach (var d in await c.Db.DocumentosAssinatura.ToListAsync()) d.Status = StatusDocumentoAssinatura.Finalizado;
        await c.Db.SaveChangesAsync();

        var resultado = await Handler(c).Handle(Comando(c), CancellationToken.None);

        Assert.Equal(0, resultado.Assinados);
        Assert.Equal(2, resultado.JaAssinados);
        Assert.Equal(2, await c.Db.DocumentoSignatarios.CountAsync(s => s.Papel == PapelAssinatura.Instrutor));
    }

    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }
}
