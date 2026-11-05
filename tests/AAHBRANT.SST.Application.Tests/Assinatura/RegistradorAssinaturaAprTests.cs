using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// APR por biometria (05/11): o quiosque grava o papel da pessoa na APR (elaboração, supervisão ou
// envolvido) e recusa quem não está na APR.
public class RegistradorAssinaturaAprTests
{
    private sealed class AuditoriaServiceFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(
            string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed record Cenario(SstDbContext Db, RegistradorAssinaturaService Servico, DocumentoAssinatura Documento,
        Trabalhador Elaborador, Trabalhador Supervisor, Trabalhador Envolvido, Trabalhador DeFora);

    private static Trabalhador Pessoa(string nome, string cpf) => new() { Nome = nome, Cpf = cpf, Funcao = new Funcao { Nome = "Pedreiro" } };

    private static async Task<Cenario> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var elaborador = Pessoa("Gilton", "11111111111");
        var supervisor = Pessoa("Paulo", "22222222222");
        var envolvido = Pessoa("Alan", "33333333333");
        var deFora = Pessoa("Zé de fora", "44444444444");
        db.Trabalhadores.AddRange(elaborador, supervisor, envolvido, deFora);
        var apr = new Apr { Local = "Bloco B", Data = DateTime.Today, ElaboradorTrabalhadorId = elaborador.Id, SupervisorTrabalhadorId = supervisor.Id };
        apr.Responsaveis.Add(new AprResponsavel { TrabalhadorId = envolvido.Id });
        db.Aprs.Add(apr);
        var documento = new DocumentoAssinatura { EntidadeTipo = nameof(Apr), EntidadeId = apr.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        return new Cenario(db, new RegistradorAssinaturaService(db, new AuditoriaServiceFalsa()), documento, elaborador, supervisor, envolvido, deFora);
    }

    private static Task Assinar(Cenario c, Trabalhador t, MetodoAutenticacaoAssinatura metodo = MetodoAutenticacaoAssinatura.ReconhecimentoFacial) =>
        c.Servico.RegistrarAsync(c.Documento.Id, new ResultadoAutenticacaoAssinatura(t.Id, metodo), "10.0.0.1", CancellationToken.None);

    [Fact]
    public async Task CadaPessoa_AssinaNoPapelQueTemNaApr()
    {
        var c = await PrepararAsync();

        await Assinar(c, c.Elaborador);
        await Assinar(c, c.Supervisor, MetodoAutenticacaoAssinatura.Biometria);
        await Assinar(c, c.Envolvido);

        var papeis = await c.Db.DocumentoSignatarios.ToDictionaryAsync(s => s.TrabalhadorId, s => s.Papel);
        Assert.Equal(PapelAssinatura.AprElaboracao, papeis[c.Elaborador.Id]);
        Assert.Equal(PapelAssinatura.AprSupervisao, papeis[c.Supervisor.Id]);
        Assert.Null(papeis[c.Envolvido.Id]);
    }

    [Fact]
    public async Task QuemNaoEstaNaApr_ERecusado()
    {
        var c = await PrepararAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Assinar(c, c.DeFora));
        Assert.Contains("não está entre os responsáveis ou envolvidos", ex.Message);
        Assert.False(await c.Db.DocumentoSignatarios.AnyAsync());
    }

    [Fact]
    public async Task ElaboradorQueTambemEEnvolvido_AssinaOsDoisPapeis()
    {
        var c = await PrepararAsync();
        c.Db.AprResponsaveis.Add(new AprResponsavel { AprId = c.Documento.EntidadeId, TrabalhadorId = c.Elaborador.Id });
        await c.Db.SaveChangesAsync();

        await Assinar(c, c.Elaborador);
        await Assinar(c, c.Elaborador);

        var papeis = await c.Db.DocumentoSignatarios.Where(s => s.TrabalhadorId == c.Elaborador.Id).Select(s => s.Papel).ToListAsync();
        Assert.Contains(PapelAssinatura.AprElaboracao, papeis);
        Assert.Contains(null, papeis);
    }
}
