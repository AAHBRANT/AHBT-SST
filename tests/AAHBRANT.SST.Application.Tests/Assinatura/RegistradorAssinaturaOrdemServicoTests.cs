using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Assinatura;

// Ordem de Serviço (03/11/2026): assinam só o funcionário (digital/facial) e, depois dele, o técnico de
// segurança com o próprio login.
public class RegistradorAssinaturaOrdemServicoTests
{
    private sealed class AuditoriaFalsa : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static async Task<(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext Db, DocumentoAssinatura Documento, Trabalhador Funcionario, Trabalhador Tecnico, Trabalhador Colega)> PrepararAsync()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Obras.Add(obra);
        Trabalhador Novo(string nome, string funcao) => new()
        {
            ObraId = obra.Id, Nome = nome, Cpf = Guid.NewGuid().ToString("N")[..11], DataAdmissao = DateTime.UtcNow, Funcao = new Funcao { Nome = funcao },
        };
        var funcionario = Novo("Pedro Pedreiro", "Pedreiro");
        var tecnico = Novo("Tânia Técnica", "Técnico de Segurança do Trabalho");
        var colega = Novo("Carlos Carpinteiro", "Carpinteiro");
        db.Trabalhadores.AddRange(funcionario, tecnico, colega);
        var os = new OrdemServico { TrabalhadorId = funcionario.Id, ObraId = obra.Id, Numero = "OS-2026-0001", EmitidaEm = DateTime.UtcNow, ConteudoJson = "{}" };
        db.OrdensServico.Add(os);
        var documento = new DocumentoAssinatura { EntidadeTipo = OrdemServico.EntidadeTipoAssinatura, EntidadeId = os.Id, ObraId = obra.Id };
        db.DocumentosAssinatura.Add(documento);
        await db.SaveChangesAsync();
        return (db, documento, funcionario, tecnico, colega);
    }

    private static RegistradorAssinaturaService Registrador(AAHBRANT.SST.Infrastructure.Persistencia.SstDbContext db) => new(db, new AuditoriaFalsa());

    [Fact]
    public async Task Tecnico_AntesDoFuncionario_ERecusado()
    {
        var (db, documento, _, tecnico, _) = await PrepararAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Registrador(db).RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(tecnico.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, default));
        Assert.Contains("funcionário assina primeiro", erro.Message);
    }

    [Fact]
    public async Task Funcionario_DepoisTecnico_RegistraAsDuas()
    {
        var (db, documento, funcionario, tecnico, _) = await PrepararAsync();
        var registrador = Registrador(db);

        await registrador.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(funcionario.Id, MetodoAutenticacaoAssinatura.Biometria), null, default);
        await registrador.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(tecnico.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, default);

        Assert.Equal(2, db.DocumentoSignatarios.Count(s => s.DocumentoAssinaturaId == documento.Id));
    }

    [Fact]
    public async Task DigitalDeOutraPessoa_ERecusada()
    {
        var (db, documento, _, _, colega) = await PrepararAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Registrador(db).RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(colega.Id, MetodoAutenticacaoAssinatura.Biometria), null, default));
    }

    [Fact]
    public async Task LoginDeQuemNaoETecnico_ERecusado()
    {
        var (db, documento, funcionario, _, colega) = await PrepararAsync();
        var registrador = Registrador(db);
        await registrador.RegistrarAsync(documento.Id, new ResultadoAutenticacaoAssinatura(funcionario.Id, MetodoAutenticacaoAssinatura.Biometria), null, default);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => registrador.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(colega.Id, MetodoAutenticacaoAssinatura.SessaoLogada), null, default));
        Assert.Contains("Técnico de Segurança", erro.Message);
    }
}
