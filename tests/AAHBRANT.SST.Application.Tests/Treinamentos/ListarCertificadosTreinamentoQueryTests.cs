using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Persistencia;

namespace AAHBRANT.SST.Application.Tests.Treinamentos;

// Esta query alimenta a sub-aba Certificados E as travas da entrega de EPI (NR-06 e Integração de
// Segurança). Ela projeta campos do curso e do trabalhador, e "excluir" neste sistema é soft delete
// (SstDbContext marca Ativo=false): sem cuidado, a navegação obrigatória vira INNER JOIN e o filtro
// global do curso apaga o certificado inteiro da lista. Foi o que aconteceu em produção em 23/09 —
// o perfil do trabalhador mostrava o certificado de NR-06 e a entrega de EPI dizia que ele não
// tinha treinamento nenhum.
public class ListarCertificadosTreinamentoQueryTests
{
    private static async Task<(Trabalhador trabalhador, CursoTreinamento curso)> SemearAsync(
        SstDbContext db, bool cursoAtendeNr6 = true)
    {
        var obra = new Obra { Nome = "Edifício Aurora", Codigo = "AUR" };
        db.Obras.Add(obra);
        var trabalhador = new Trabalhador
        {
            ObraId = obra.Id,
            FuncaoId = Guid.NewGuid(),
            Nome = "Gabriel Gonçalves de Lima",
            Cpf = "00000000000",
        };
        var curso = new CursoTreinamento
        {
            Nome = "NR-06 Equipamento de Proteção Individual",
            NormaReferencia = "NR-06",
            CargaHorariaMinima = 4,
            ValidadeEmMeses = 24,
            AtendeNr6 = cursoAtendeNr6,
        };
        db.Trabalhadores.Add(trabalhador);
        db.CursosTreinamento.Add(curso);
        await db.SaveChangesAsync();

        db.Treinamentos.Add(new Treinamento
        {
            TrabalhadorId = trabalhador.Id,
            CursoTreinamentoId = curso.Id,
            DataRealizacao = new DateTime(2025, 5, 16),
            DataValidade = new DateTime(2027, 5, 16),
            CargaHorariaRealizada = 4,
        });
        await db.SaveChangesAsync();
        return (trabalhador, curso);
    }

    [Fact]
    public async Task Handle_TrabalhadorComCertificado_Retorna()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, _) = await SemearAsync(db);
        var handler = new ListarCertificadosTreinamentoQueryHandler(db);

        var lista = await handler.Handle(new ListarCertificadosTreinamentoQuery(TrabalhadorId: trabalhador.Id), default);

        var certificado = Assert.Single(lista);
        Assert.True(certificado.AtendeNr6);
        Assert.Equal("NR-06", certificado.NormaReferencia);
    }

    // O caso que quebrou a entrega de EPI em produção: o curso saiu do catálogo (soft delete), mas o
    // certificado do trabalhador continua valendo — e é ele que libera o EPI.
    [Fact]
    public async Task Handle_CursoExcluidoDoCatalogo_AindaRetornaOCertificado()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, curso) = await SemearAsync(db);
        // Estado que o banco fica depois de excluir o curso pelo catálogo: SstDbContext converte
        // Remove em Ativo=false. Aqui é feito direto porque, com o treinamento já rastreado nesta
        // mesma instância, o Remove dispara o cascade do EF antes de a conversão acontecer.
        curso.Ativo = false;
        await db.SaveChangesAsync();
        var handler = new ListarCertificadosTreinamentoQueryHandler(db);

        var lista = await handler.Handle(new ListarCertificadosTreinamentoQuery(TrabalhadorId: trabalhador.Id), default);

        var certificado = Assert.Single(lista);
        Assert.True(certificado.AtendeNr6);
        Assert.Equal("NR-06", certificado.NormaReferencia);
        Assert.Contains("NR-06", certificado.CursoNome);
    }

    // Treinamento excluído é outra história: esse sumiu por vontade de quem apagou, e continua fora.
    [Fact]
    public async Task Handle_TreinamentoExcluido_NaoRetorna()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, _) = await SemearAsync(db);
        var treinamento = db.Treinamentos.Single();
        db.Treinamentos.Remove(treinamento);
        await db.SaveChangesAsync();
        var handler = new ListarCertificadosTreinamentoQueryHandler(db);

        var lista = await handler.Handle(new ListarCertificadosTreinamentoQuery(TrabalhadorId: trabalhador.Id), default);

        Assert.Empty(lista);
    }

    [Fact]
    public async Task Handle_TrabalhadorExcluido_NaoRetorna()
    {
        var db = DbContextFactory.Criar();
        var (trabalhador, _) = await SemearAsync(db);
        trabalhador.Ativo = false; // mesmo efeito de excluir o trabalhador — ver teste do curso
        await db.SaveChangesAsync();
        var handler = new ListarCertificadosTreinamentoQueryHandler(db);

        var lista = await handler.Handle(new ListarCertificadosTreinamentoQuery(TrabalhadorId: trabalhador.Id), default);

        Assert.Empty(lista);
    }
}
