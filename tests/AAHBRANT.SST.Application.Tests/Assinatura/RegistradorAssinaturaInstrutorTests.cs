using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Instrutor da turma (28/10): assina o certificado de outro trabalhador com o papel Instrutor, desde que
// tenha perfil de instrutor. A digital dele não precisa ser a do dono do certificado.
public class RegistradorAssinaturaInstrutorTests
{
    private static async Task<(SstDbContext Db, RegistradorAssinaturaService Servico)> Montar(Trabalhador instrutor, bool comPerfil)
    {
        var db = DbContextFactory.Criar();
        db.Trabalhadores.Add(instrutor);
        if (comPerfil)
            db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = instrutor.Id });
        await db.SaveChangesAsync();
        return (db, new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa()));
    }

    [Fact]
    public async Task InstrutorComPerfil_AssinaCertificadoDeOutroTrabalhador_ComDigitalDele()
    {
        var instrutor = new Trabalhador { Nome = "Gilton", Cpf = "11122233344", Funcao = new Funcao { Nome = "Técnico de Segurança" } };
        var participante = new Trabalhador { Nome = "Alan", Cpf = "55566677788", Funcao = new Funcao { Nome = "Técnico de Segurança" } };
        var (db, servico) = await Montar(instrutor, comPerfil: true);
        var treinamento = new Treinamento { TrabalhadorId = participante.Id };
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = treinamento.Id };
        db.Trabalhadores.Add(participante);
        db.Treinamentos.Add(treinamento);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        // Digital (método Biometria) de quem NÃO é o dono do certificado: com papel Instrutor é aceita.
        var resultado = await servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(instrutor.Id, MetodoAutenticacaoAssinatura.Biometria),
            "10.0.0.1", CancellationToken.None, papel: PapelAssinatura.Instrutor);

        Assert.Equal(instrutor.Id, resultado.TrabalhadorId);
        var assinatura = await db.DocumentoSignatarios.SingleAsync(s => s.DocumentoAssinaturaId == documento.Id);
        Assert.Equal(PapelAssinatura.Instrutor, assinatura.Papel);
    }

    [Fact]
    public async Task SemPerfilDeInstrutor_NaoPodeAssinarComoInstrutor()
    {
        var naoInstrutor = new Trabalhador { Nome = "Beltrano", Cpf = "11122233344", Funcao = new Funcao { Nome = "Técnico de Segurança" } };
        var (db, servico) = await Montar(naoInstrutor, comPerfil: false);
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = Guid.NewGuid() };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(naoInstrutor.Id, MetodoAutenticacaoAssinatura.Biometria),
            "10.0.0.1", CancellationToken.None, papel: PapelAssinatura.Instrutor));
    }

    [Fact]
    public async Task InstrutorNaoAssinaDuasVezesOMesmoCertificado()
    {
        var instrutor = new Trabalhador { Nome = "Gilton", Cpf = "11122233344", Funcao = new Funcao { Nome = "Técnico de Segurança" } };
        var (db, servico) = await Montar(instrutor, comPerfil: true);
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = Guid.NewGuid() };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        var resultado = new ResultadoAutenticacaoAssinatura(instrutor.Id, MetodoAutenticacaoAssinatura.Biometria);

        await servico.RegistrarAsync(documento.Id, resultado, "10.0.0.1", CancellationToken.None, papel: PapelAssinatura.Instrutor);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarAsync(documento.Id, resultado, "10.0.0.1", CancellationToken.None, papel: PapelAssinatura.Instrutor));
    }

    [Fact]
    public async Task SemPapelInstrutor_DigitalDeOutroTrabalhadorContinuaRecusadaNoCertificado()
    {
        // A regra de sempre: a digital identificada precisa ser a do dono do certificado.
        var instrutor = new Trabalhador { Nome = "Gilton", Cpf = "11122233344", Funcao = new Funcao { Nome = "Técnico de Segurança" } };
        var participante = new Trabalhador { Nome = "Alan", Cpf = "55566677788", Funcao = new Funcao { Nome = "Técnico de Segurança" } };
        var (db, servico) = await Montar(instrutor, comPerfil: true);
        var treinamento = new Treinamento { TrabalhadorId = participante.Id };
        var documento = new DocumentoAssinatura { EntidadeTipo = "Treinamento", EntidadeId = treinamento.Id };
        db.Trabalhadores.Add(participante);
        db.Treinamentos.Add(treinamento);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(instrutor.Id, MetodoAutenticacaoAssinatura.Biometria),
            "10.0.0.1", CancellationToken.None));
    }

    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }
}
