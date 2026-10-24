using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Infrastructure.Documentos;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.SessoesTreinamento;

// Turma com o padrão da NR (19/10): carga horária nunca abaixo do mínimo, conteúdo gravado na turma
// e, para quem não é Técnico de Segurança, o servidor ignora o que veio e grava o padrão.
public class CriarSessaoTreinamentoPadraoNrTests
{
    private static (IAppDbContext Db, Guid ObraId, Guid CursoId) Montar(string nome, string? conteudoCurso = null)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options;
        var db = new SstDbContext(options, new CurrentUserService());
        var obra = new Obra { Nome = "Obra Parque Roger" };
        var curso = new CursoTreinamento
        {
            Nome = "NR-35 Trabalho em Altura",
            NormaReferencia = "NR-35",
            CargaHorariaMinima = 0,
            ValidadeEmMeses = 0,
            ConteudoProgramatico = conteudoCurso,
        };
        db.Obras.Add(obra);
        db.CursosTreinamento.Add(curso);
        db.SaveChanges();
        return (db, obra.Id, curso.Id);
    }

    private static CriarSessaoTreinamentoCommandHandler Handler(IAppDbContext db)
        => new(db, new GeradorNumeroDocumentoService(db));

    private static CriarSessaoTreinamentoCommand Comando(Guid obraId, Guid cursoId, int cargaHoraria, bool autenticacao = false) => new(
        obraId, cursoId, new DateTime(2026, 10, 19), cargaHoraria, "Maria Ferreira", new List<Guid> { Guid.NewGuid() },
        ConteudoProgramatico: "Tópico próprio", Local: "Galpão", InstrutorRegistroProfissional: "MTE 1", ValidadeEmMeses: 36,
        AzureAdObjectId: null, AutenticacaoHabilitada: autenticacao);

    [Fact]
    public async Task CargaHorariaAbaixoDoMinimoDaNr_Recusa()
    {
        var (db, obraId, cursoId) = Montar(nameof(CargaHorariaAbaixoDoMinimoDaNr_Recusa));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(db).Handle(Comando(obraId, cursoId, 6), CancellationToken.None));

        Assert.Contains("8h", erro.Message);
    }

    [Fact]
    public async Task Tecnico_GravaOQueInformou()
    {
        var (db, obraId, cursoId) = Montar(nameof(Tecnico_GravaOQueInformou));

        var id = await Handler(db).Handle(Comando(obraId, cursoId, 10), CancellationToken.None);

        var sessao = await db.SessoesTreinamento.SingleAsync(s => s.Id == id);
        Assert.Equal(10, sessao.CargaHorariaRealizada);
        Assert.Equal("Tópico próprio", sessao.ConteudoProgramatico);
        Assert.Equal("Galpão", sessao.Local);
        Assert.Equal(36, sessao.ValidadeEmMeses);
    }

    [Fact]
    public async Task SemPerfilDeTecnico_GravaOPadraoDaNr()
    {
        var (db, obraId, cursoId) = Montar(nameof(SemPerfilDeTecnico_GravaOPadraoDaNr));

        // Autenticação ligada e nenhum usuário com perfil: não pode editar.
        var id = await Handler(db).Handle(Comando(obraId, cursoId, 12, autenticacao: true), CancellationToken.None);

        var sessao = await db.SessoesTreinamento.SingleAsync(s => s.Id == id);
        Assert.Equal(8, sessao.CargaHorariaRealizada);
        Assert.Equal(24, sessao.ValidadeEmMeses);
        Assert.Equal("Obra Parque Roger", sessao.Local);
        Assert.Null(sessao.InstituicaoInstrutor);
        Assert.StartsWith("Normas e regulamentos aplicáveis ao trabalho em altura", sessao.ConteudoProgramatico);
    }

    [Fact]
    public async Task ConteudoProprioDoCurso_TemPrioridadeSobreONr()
    {
        var (db, obraId, cursoId) = Montar(nameof(ConteudoProprioDoCurso_TemPrioridadeSobreONr), conteudoCurso: "Conteúdo do curso");

        var id = await Handler(db).Handle(Comando(obraId, cursoId, 8, autenticacao: true), CancellationToken.None);

        Assert.Equal("Conteúdo do curso", (await db.SessoesTreinamento.SingleAsync(s => s.Id == id)).ConteudoProgramatico);
    }

    // Um número por participante no encerramento: chamadas seguidas antes do SaveChanges não podem
    // repetir número quando o contador do prefixo ainda não existe no banco.
    [Fact]
    public async Task GeradorNumero_ChamadasSeguidasSemSalvar_NaoRepetem()
    {
        var (db, _, _) = Montar(nameof(GeradorNumero_ChamadasSeguidasSemSalvar_NaoRepetem));
        var gerador = new GeradorNumeroDocumentoService(db);

        var a = await gerador.GerarAsync("AAH-NR35", CancellationToken.None);
        var b = await gerador.GerarAsync("AAH-NR35", CancellationToken.None);

        Assert.NotEqual(a, b);
        Assert.EndsWith("-0002", b);
    }
}
