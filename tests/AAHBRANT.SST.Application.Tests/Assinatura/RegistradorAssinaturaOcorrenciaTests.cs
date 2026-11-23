using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Ocorrência assinada pelo técnico e pelo engenheiro de segurança (23/11/2026): uma assinatura de cada,
// e só técnico ou engenheiro de segurança assinam.
public class RegistradorAssinaturaOcorrenciaTests
{
    [Fact]
    public async Task TecnicoEEngenheiro_AssinamUmaVezCada_EDocumentoFicaCompleto()
    {
        using var db = DbContextFactory.Criar();
        var tecnico = Trabalhador("Técnico Exemplo", "Técnico de Segurança do Trabalho", "11122233344");
        var outroTecnico = Trabalhador("Outro Técnico", "Tec. Segurança Do Trabalho", "22233344455");
        var engenheiro = Trabalhador("Engenheiro Exemplo", "Engenheiro de Segurança do Trabalho", "33344455566");
        var documento = new DocumentoAssinatura { EntidadeTipo = "Acidente", EntidadeId = Guid.NewGuid() };
        db.Trabalhadores.AddRange(tecnico, outroTecnico, engenheiro);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var servico = new RegistradorAssinaturaService(db, new AuditoriaFalsa());

        await servico.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(tecnico.Id, MetodoAutenticacaoAssinatura.Biometria), null, CancellationToken.None);
        Assert.False((await AssinaturasOcorrencia.ObterAsync(db, documento.Id, CancellationToken.None)).Completa);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(outroTecnico.Id, MetodoAutenticacaoAssinatura.Biometria), null, CancellationToken.None));
        Assert.Contains("técnico de segurança", erro.Message);

        await servico.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(engenheiro.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, CancellationToken.None);
        var situacao = await AssinaturasOcorrencia.ObterAsync(db, documento.Id, CancellationToken.None);
        Assert.True(situacao.TecnicoAssinou);
        Assert.True(situacao.EngenheiroAssinou);
        Assert.True(situacao.Completa);
    }

    [Fact]
    public async Task FuncaoQueNaoESst_NaoAssinaOcorrencia()
    {
        using var db = DbContextFactory.Criar();
        var pedreiro = Trabalhador("Pedreiro Exemplo", "Pedreiro", "11122233344");
        var documento = new DocumentoAssinatura { EntidadeTipo = "Acidente", EntidadeId = Guid.NewGuid() };
        db.Trabalhadores.Add(pedreiro);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new RegistradorAssinaturaService(db, new AuditoriaFalsa()).RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(pedreiro.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, CancellationToken.None));
    }

    [Theory]
    [InlineData("Engenheiro de Segurança do Trabalho", AssinaturasOcorrencia.Papel.Engenheiro)]
    [InlineData("Eng. Segurança do Trabalho", AssinaturasOcorrencia.Papel.Engenheiro)]
    [InlineData("Técnico de Segurança do Trabalho", AssinaturasOcorrencia.Papel.Tecnico)]
    [InlineData("Engenheiro Civil", null)]
    [InlineData("Auxiliar Técnico de Engenharia", null)]
    public void Classificar_SoTecnicoOuEngenheiroDeSeguranca(string funcao, AssinaturasOcorrencia.Papel? esperado) =>
        Assert.Equal(esperado, AssinaturasOcorrencia.Classificar(funcao));

    private static Trabalhador Trabalhador(string nome, string funcao, string cpf) =>
        new() { Nome = nome, Cpf = cpf, Funcao = new Funcao { Nome = funcao } };

    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }
}
