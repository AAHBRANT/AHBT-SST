using System.Security.Cryptography;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

public class RegistradorAssinaturaServiceTests
{
    [Fact]
    public async Task RegistrarAsync_MesmoTecnicoPodeAssinarEntregaEpiComoRecebedorEResponsavel()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Técnico de Segurança" };
        var tecnico = new Trabalhador { Nome = "Carlos Técnico", Cpf = "11122233344", Funcao = funcao };
        var entrega = new EntregaEpi { Trabalhador = tecnico, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(tecnico);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(tecnico.Id, MetodoAutenticacaoAssinatura.Biometria),
            "10.0.0.1",
            CancellationToken.None);
        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(tecnico.Id, MetodoAutenticacaoAssinatura.SessaoLogada),
            "10.0.0.2",
            CancellationToken.None);

        var assinaturas = await db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .Select(s => s.MetodoAutenticacao)
            .ToListAsync();

        Assert.Contains(MetodoAutenticacaoAssinatura.Biometria, assinaturas);
        Assert.Contains(MetodoAutenticacaoAssinatura.SessaoLogada, assinaturas);
        Assert.Equal(2, assinaturas.Count);
    }

    [Fact]
    public async Task RegistrarAsync_MesmoTrabalhadorNaoPodeRepetirAssinaturaDeRecebedor()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Carlos", Cpf = "11122233344", Funcao = new Funcao { Nome = "Pedreiro" } };
        var entrega = new EntregaEpi { Trabalhador = trabalhador, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Trabalhadores.Add(trabalhador);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria),
            null,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task RegistrarAsync_TrabalhadorComumNaoPodeAssinarEntregaEpiComoRecebedorEResponsavel()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = new Trabalhador { Nome = "Carlos", Cpf = "11122233344", Funcao = new Funcao { Nome = "Pedreiro" } };
        var entrega = new EntregaEpi { Trabalhador = trabalhador, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Trabalhadores.Add(trabalhador);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria),
            null,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.SessaoLogada),
            null,
            CancellationToken.None));
    }

    // Biometria identifica a PESSOA — e o documento tem dono. Sem esta checagem, um colega
    // identificado por engano (rosto parecido acima do limiar) assinava a entrega de outro, e o
    // documento ficava com o nome errado. O risco deixou de ser teórico em 24/09, quando o Azure
    // devolveu candidato para alguém que nem tinha cadastro facial.
    [Fact]
    public async Task RegistrarAsync_RostoDeOutroTrabalhador_RecusaEExplicaDeQuemEra()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var dono = new Trabalhador { Nome = "Gabriel dos Santos Brito", Cpf = "11122233344", Funcao = funcao };
        var outro = new Trabalhador { Nome = "Gabriel Gonçalves de Lima", Cpf = "55566677788", Funcao = funcao };
        var entrega = new EntregaEpi { Trabalhador = dono, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(dono, outro);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(outro.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            null,
            CancellationToken.None));

        Assert.Contains("O rosto reconhecido é de Gabriel Gonçalves de Lima", ex.Message);
        Assert.Contains("Gabriel dos Santos Brito", ex.Message);
        Assert.Empty(db.DocumentoSignatarios);
    }

    [Fact]
    public async Task RegistrarAsync_DigitalDeOutroTrabalhador_TambemRecusa()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var dono = new Trabalhador { Nome = "Dono da Entrega", Cpf = "11122233344", Funcao = funcao };
        var outro = new Trabalhador { Nome = "Colega Qualquer", Cpf = "55566677788", Funcao = funcao };
        var entrega = new EntregaEpi { Trabalhador = dono, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(dono, outro);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(outro.Id, MetodoAutenticacaoAssinatura.Biometria),
            null,
            CancellationToken.None));

        Assert.Contains("A digital lida é de Colega Qualquer", ex.Message);
    }

    [Fact]
    public async Task RegistrarAsync_BiometriaDoProprioDono_Registra()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var dono = new Trabalhador { Nome = "Dono da Entrega", Cpf = "11122233344", Funcao = funcao };
        var entrega = new EntregaEpi { Trabalhador = dono, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(dono);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(dono.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            null,
            CancellationToken.None);

        Assert.Single(db.DocumentoSignatarios);
    }

    [Fact]
    public async Task RegistrarAsync_AssinaturaFacialGuardaFotoEHashNoCofre()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var dono = new Trabalhador { Nome = "Dono da Entrega", Cpf = "11122233344", Funcao = funcao };
        var entrega = new EntregaEpi { Trabalhador = dono, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        var foto = new byte[] { 1, 2, 3, 4, 5 };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(dono);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(dono.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial),
            "10.0.0.1",
            CancellationToken.None,
            foto,
            "image/png");

        var signatario = await db.DocumentoSignatarios.SingleAsync();
        Assert.Equal(foto, signatario.FotoEvidenciaConteudo);
        Assert.Equal("image/png", signatario.FotoEvidenciaContentType);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(foto)), signatario.FotoEvidenciaHash);
    }

    // Quem assina por sessão logada é o responsável/instrutor, que legitimamente não é o trabalhador
    // do documento — a checagem não pode alcançar esse caso, senão trava o visto do técnico.
    [Fact]
    public async Task RegistrarAsync_SessaoLogadaDeOutraPessoa_ContinuaPermitida()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var dono = new Trabalhador { Nome = "Dono da Entrega", Cpf = "11122233344", Funcao = funcao };
        var responsavel = new Trabalhador { Nome = "Técnico Responsável", Cpf = "55566677788", Funcao = funcao };
        var entrega = new EntregaEpi { Trabalhador = dono, Quantidade = 1, DataEntrega = DateTime.UtcNow };
        var documento = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entrega.Id };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.AddRange(dono, responsavel);
        db.EntregasEpi.Add(entrega);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(responsavel.Id, MetodoAutenticacaoAssinatura.SessaoLogada),
            null,
            CancellationToken.None);

        Assert.Single(db.DocumentoSignatarios);
    }

    // Documento sem dono único (cada participante da turma assina o seu) não entra na regra —
    // restringir aqui quebraria o fluxo legítimo de assinatura coletiva.
    [Fact]
    public async Task RegistrarAsync_TipoSemDonoUnico_NaoAplicaARegra()
    {
        using var db = DbContextFactory.Criar();
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var participante = new Trabalhador { Nome = "Participante da Turma", Cpf = "11122233344", Funcao = funcao };
        var documento = new DocumentoAssinatura { EntidadeTipo = "SessaoTreinamento", EntidadeId = Guid.NewGuid() };
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(participante);
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();

        var servico = new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa());

        await servico.RegistrarAsync(
            documento.Id,
            new ResultadoAutenticacaoAssinatura(participante.Id, MetodoAutenticacaoAssinatura.Biometria),
            null,
            CancellationToken.None);

        Assert.Single(db.DocumentoSignatarios);
    }

    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(
            string acao,
            string entidadeTipo,
            Guid entidadeId,
            Guid? usuarioId,
            Guid? trabalhadorId,
            object? dadosDepois,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
