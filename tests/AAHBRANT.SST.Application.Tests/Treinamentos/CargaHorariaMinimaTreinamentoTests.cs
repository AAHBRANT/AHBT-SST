using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Treinamentos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Treinamentos;

// Teste E2E 21/10: a turma recusava NR-06 abaixo de 4h, mas o registro individual aceitava e emitia
// certificado AAHBRANT com a mesma carga. Externo segue livre (é o documento de outra instituição).
public class CargaHorariaMinimaTreinamentoTests
{
    private static async Task<(Trabalhador trabalhador, CursoTreinamento curso)> SemearAsync(SstDbContext db)
    {
        var trabalhador = new Trabalhador { ObraId = Guid.NewGuid(), FuncaoId = Guid.NewGuid(), Nome = "Ana Costa", Cpf = "00000000000" };
        var curso = new CursoTreinamento { Nome = "NR-06 Equipamento de Proteção Individual", NormaReferencia = "NR-06", CargaHorariaMinima = 4, ValidadeEmMeses = 24 };
        db.Trabalhadores.Add(trabalhador);
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();
        return (trabalhador, curso);
    }

    private static CriarTreinamentoCommand Criar(Trabalhador t, CursoTreinamento c, int carga, OrigemCertificadoTreinamento origem) =>
        new(t.Id, c.Id, new DateTime(2026, 10, 21), new DateTime(2028, 10, 21), carga, "SESMT", null, null, null, origem);

    [Fact]
    public async Task Criar_AahbrantAbaixoDoMinimo_ERecusado()
    {
        var db = DbContextFactory.Criar();
        var (t, curso) = await SemearAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CriarTreinamentoCommandHandler(db).Handle(Criar(t, curso, 1, OrigemCertificadoTreinamento.Aahbrant), default));

        Assert.Contains("carga horária mínima", ex.Message);
        Assert.Empty(db.Treinamentos);
    }

    [Fact]
    public async Task Criar_AahbrantNoMinimo_EAceito()
    {
        var db = DbContextFactory.Criar();
        var (t, curso) = await SemearAsync(db);

        await new CriarTreinamentoCommandHandler(db).Handle(Criar(t, curso, 4, OrigemCertificadoTreinamento.Aahbrant), default);

        Assert.Single(db.Treinamentos);
    }

    [Fact]
    public async Task Criar_ExternoAbaixoDoMinimo_EAceito()
    {
        var db = DbContextFactory.Criar();
        var (t, curso) = await SemearAsync(db);

        await new CriarTreinamentoCommandHandler(db).Handle(Criar(t, curso, 1, OrigemCertificadoTreinamento.Externo), default);

        Assert.Single(db.Treinamentos);
    }

    [Fact]
    public async Task Atualizar_ReduzCargaAbaixoDoMinimo_ERecusado()
    {
        var db = DbContextFactory.Criar();
        var (t, curso) = await SemearAsync(db);
        var id = await new CriarTreinamentoCommandHandler(db).Handle(Criar(t, curso, 4, OrigemCertificadoTreinamento.Aahbrant), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AtualizarTreinamentoCommandHandler(db).Handle(new AtualizarTreinamentoCommand(
                id, t.Id, curso.Id, new DateTime(2026, 10, 21), new DateTime(2028, 10, 21), 2, "SESMT", null, null, null), default));
    }

    // Registro antigo abaixo do mínimo continua editável nos campos que a regra não olha.
    [Fact]
    public async Task Atualizar_RegistroAntigoAbaixoDoMinimo_EditarOutroCampo_EAceito()
    {
        var db = DbContextFactory.Criar();
        var (t, curso) = await SemearAsync(db);
        var antigo = new Treinamento
        {
            TrabalhadorId = t.Id, CursoTreinamentoId = curso.Id, DataRealizacao = new DateTime(2026, 1, 10),
            DataValidade = new DateTime(2028, 1, 10), CargaHorariaRealizada = 2, OrigemCertificado = OrigemCertificadoTreinamento.Aahbrant,
        };
        db.Treinamentos.Add(antigo);
        await db.SaveChangesAsync();

        await new AtualizarTreinamentoCommandHandler(db).Handle(new AtualizarTreinamentoCommand(
            antigo.Id, t.Id, curso.Id, antigo.DataRealizacao, antigo.DataValidade, 2, "SESMT", "CERT-123", null, null), default);

        Assert.Equal("CERT-123", db.Treinamentos.Single().NumeroCertificado);
    }
}
