using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Tests.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.SessoesTreinamento;

// Evidência da presença na turma (05/11): a presença guarda a imagem capturada, IP, aparelho e localização,
// e o encerramento leva isso (e o método real) para a assinatura do certificado.
public class PresencaTreinamentoEvidenciaTests
{
    private sealed class ClienteFalso(string ip, string userAgent) : IClienteIpProvider
    {
        public string? ObterIp() => ip;
        public string? ObterUserAgent() => userAgent;
    }

    private sealed class Numeros : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-0001");
    }

    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(
            string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static readonly byte[] Imagem = { 1, 2, 3, 4, 5 };

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, SessaoTreinamento Sessao, Trabalhador Trabalhador)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Alan", Cpf = "12345678901", Funcao = new Funcao { Nome = "Ajudante Geral" } };
        var curso = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        var sessao = new SessaoTreinamento { CursoTreinamento = curso, DataRealizacao = new DateTime(2026, 11, 5), CargaHorariaRealizada = 8 };
        sessao.Participantes.Add(new ParticipanteSessaoTreinamento { TrabalhadorId = trabalhador.Id });
        for (var i = 1; i <= 3; i++)
            sessao.FotosEvidencia.Add(new FotoEvidenciaSessaoTreinamento { Ordem = i, FotoConteudo = new byte[] { 9 }, FotoContentType = "image/jpeg" });
        db.Trabalhadores.Add(trabalhador);
        db.SessoesTreinamento.Add(sessao);
        await db.SaveChangesAsync();
        return (db, sessao, trabalhador);
    }

    [Fact]
    public async Task PresencaPorDigital_GuardaImagemIpAparelhoELocalizacao()
    {
        var (db, sessao, trabalhador) = await PrepararAsync();
        var handler = new RegistrarPresencaSessaoTreinamentoCommandHandler(db, new AutenticacaoBiometriaLocalFalsa(), new ClienteFalso("10.0.0.7", "Chrome Android"));

        await handler.Handle(new RegistrarPresencaSessaoTreinamentoCommand(sessao.Id, trabalhador.Id, Guid.NewGuid(), "segredo", 90,
            ImagemDigital: Imagem, Localizacao: new LocalizacaoAssinatura(StatusLocalizacaoAssinatura.Capturada, -7.1, -34.8, 15)), CancellationToken.None);

        var p = await db.ParticipantesSessaoTreinamento.SingleAsync();
        Assert.NotNull(p.PresencaConfirmadaEm);
        Assert.Equal(MetodoAutenticacaoAssinatura.Biometria, p.MetodoPresenca);
        Assert.Equal(Imagem, p.EvidenciaConteudo);
        Assert.Equal(64, p.EvidenciaHash!.Length);
        Assert.Equal("10.0.0.7", p.IpAddress);
        Assert.Equal("Chrome Android", p.UserAgent);
        Assert.Equal(StatusLocalizacaoAssinatura.Capturada, p.LocalizacaoStatus);
        Assert.Equal(-7.1, p.Latitude);
    }

    [Fact]
    public async Task Encerramento_AssinaComMetodoEEvidenciaDaPresenca_EApagaAImagemDoParticipante()
    {
        var (db, sessao, trabalhador) = await PrepararAsync();
        var participante = await db.ParticipantesSessaoTreinamento.SingleAsync();
        participante.PresencaConfirmadaEm = DateTime.UtcNow.AddMinutes(-30);
        participante.MetodoPresenca = MetodoAutenticacaoAssinatura.ReconhecimentoFacial;
        participante.EvidenciaConteudo = Imagem;
        participante.EvidenciaContentType = "image/jpeg";
        participante.EvidenciaHash = "ABC";
        participante.IpAddress = "10.0.0.7";
        participante.UserAgent = "Chrome Android (tablet)";
        participante.LocalizacaoStatus = StatusLocalizacaoAssinatura.Capturada;
        participante.Latitude = -7.1;
        participante.Longitude = -34.8;
        participante.ValidacaoRequisicaoId = "req-azure-1";
        await db.SaveChangesAsync();

        var mediator = new MediatorSimulado(req =>
        {
            if (req is CriarDocumentoAssinaturaCommand c)
            {
                var doc = new DocumentoAssinatura { EntidadeTipo = c.EntidadeTipo, EntidadeId = c.EntidadeId, Status = StatusDocumentoAssinatura.EmAndamento };
                db.DocumentosAssinatura.Add(doc);
                db.SaveChanges();
                return doc.Id;
            }
            return null;
        });
        // A requisição do encerramento vem do computador do técnico: não pode ir para a assinatura do participante.
        var registrador = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa(), new ClienteFalso("192.168.1.50", "Edge Windows"));
        var handler = new EncerrarSessaoTreinamentoCommandHandler(db, mediator, registrador,
            NullLogger<EncerrarSessaoTreinamentoCommandHandler>.Instance, new Numeros());

        await handler.Handle(new EncerrarSessaoTreinamentoCommand(sessao.Id), CancellationToken.None);

        var assinatura = await db.DocumentoSignatarios.SingleAsync(s => s.TrabalhadorId == trabalhador.Id);
        Assert.Equal(MetodoAutenticacaoAssinatura.ReconhecimentoFacial, assinatura.MetodoAutenticacao);
        Assert.Equal(Imagem, assinatura.FotoEvidenciaConteudo);
        Assert.Equal("10.0.0.7", assinatura.IpAddress);
        Assert.Equal("Chrome Android (tablet)", assinatura.UserAgent);
        Assert.Equal(StatusLocalizacaoAssinatura.Capturada, assinatura.LocalizacaoStatus);
        Assert.Equal("req-azure-1", assinatura.ValidacaoRequisicaoId);

        var depois = await db.ParticipantesSessaoTreinamento.AsNoTracking().SingleAsync();
        Assert.Null(depois.EvidenciaConteudo);
        Assert.Equal("ABC", depois.EvidenciaHash);
    }
}
