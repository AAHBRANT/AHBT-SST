using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

public class SugerirPlanoAcaoOcorrenciaTests
{
    private static readonly RiscoPgrResumo Queda = new("Queda do mesmo nível", 3, "Traumatismos", "Caminho seguro", "DDS");

    [Fact]
    public void Conferir_PerigoDoPgrExistente_AceitaComPerigoEControle()
    {
        var r = FundamentacaoAcaoPlano.Conferir(
            Acao("PGR", perigo: "queda do mesmo nível ", controle: "Caminho seguro"),
            "Armação de ferragens", new[] { Queda }, Array.Empty<RequisitoLegalResumo>());

        Assert.True(r.Confirmada);
        Assert.Equal("PGR · Armação de ferragens · Perigo: Queda do mesmo nível · Controle: Caminho seguro", r.Texto);
    }

    [Fact]
    public void Conferir_AtividadeCitadaComoPerigo_ViraSemBase()
    {
        // Erro real visto no teste com o PGR da Ponte Rio Cuiá: a IA citou a atividade como perigo.
        var r = FundamentacaoAcaoPlano.Conferir(
            Acao("PGR", perigo: "Armação de ferragens (Armador)"),
            "Armação de ferragens (Armador)", new[] { Queda }, Array.Empty<RequisitoLegalResumo>());

        Assert.False(r.Confirmada);
        Assert.StartsWith("Sem base cadastrada, validar", r.Texto);
    }

    [Fact]
    public void Conferir_RequisitoLegalSoComIdentificadorExato()
    {
        var nr35 = new RequisitoLegalResumo("NR-35", "35.4.1", "Capacitação", "Treinamento");

        Assert.True(FundamentacaoAcaoPlano.Conferir(Acao("RequisitoLegal", referencia: "NR-35 35.4.1"), null, Array.Empty<RiscoPgrResumo>(), new[] { nr35 }).Confirmada);
        Assert.False(FundamentacaoAcaoPlano.Conferir(Acao("RequisitoLegal", referencia: "NR-35 35.5"), null, Array.Empty<RiscoPgrResumo>(), new[] { nr35 }).Confirmada);
    }

    [Fact]
    public async Task Handle_ResponsavelPorPapelNaObraEFallbackParaQuemRegistra()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var outraObra = new Obra { Codigo = "OB2", Nome = "Outra" };
        var encarregado = new Usuario { Email = "enc@x.com", Nome = "Marcos Encarregado" };
        var encarregadoOutraObra = new Usuario { Email = "enc2@x.com", Nome = "Aaron Outra Obra" };
        var qsms = new Usuario { Email = "qsms@x.com", Nome = "Patrícia QSMS" };
        var tecnico = new Usuario { Email = "tec@x.com", Nome = "Técnico Logado" };
        var perfilEncarregado = new PerfilAcesso { Tipo = TipoPerfilAcesso.Encarregado, Nome = "Encarregado" };
        var perfilQsms = new PerfilAcesso { Tipo = TipoPerfilAcesso.GestorQsms, Nome = "Gestor QSMS" };
        db.AddRange(obra, outraObra, encarregado, encarregadoOutraObra, qsms, tecnico, perfilEncarregado, perfilQsms,
            new UsuarioPerfilObra { UsuarioId = encarregado.Id, PerfilAcessoId = perfilEncarregado.Id, ObraId = obra.Id },
            new UsuarioPerfilObra { UsuarioId = encarregadoOutraObra.Id, PerfilAcessoId = perfilEncarregado.Id, ObraId = outraObra.Id },
            new UsuarioPerfilObra { UsuarioId = qsms.Id, PerfilAcessoId = perfilQsms.Id, ObraId = null });
        await db.SaveChangesAsync();

        var analista = new AnalistaFalso(new AnalisePlanoIa("CincoPorques", "causas", new[]
        {
            new AcaoSugeridaIa("Corretiva", "Proteger pontas", "Critica", "Encarregado", "Metodo", null, null, "Controle de engenharia"),
            new AcaoSugeridaIa("Melhoria", "Padronizar", "Media", "GestorQsms", "SemBase", null, null, "cadastrar perigo"),
            new AcaoSugeridaIa("Preventiva", "Revisar procedimento", "Alta", "EngenheiroSeguranca", "Metodo", null, null, "Administrativo"),
            new AcaoSugeridaIa("Preventiva", "Inspecionar frente", "Alta", "TecnicoSeguranca", "Metodo", null, null, "Administrativo"),
        }));

        var r = await new SugerirPlanoAcaoOcorrenciaCommandHandler(db, analista, NullLogger<SugerirPlanoAcaoOcorrenciaCommandHandler>.Instance)
            .Handle(new SugerirPlanoAcaoOcorrenciaCommand(obra.Id, null, TipoOcorrencia.Acidente, GravidadeAcidente.SemAfastamento,
                "desc", null, null, null, tecnico.Id), default);

        Assert.Equal(MetodologiaInvestigacao.CincoPorques, r.Metodologia);
        Assert.Equal(encarregado.Id, r.Acoes[0].ResponsavelUsuarioId); // só o encarregado DESTA obra
        Assert.Equal(qsms.Id, r.Acoes[1].ResponsavelUsuarioId);        // QSMS de escopo global
        Assert.False(r.Acoes[1].BaseConfirmada);
        Assert.Equal(tecnico.Id, r.Acoes[2].ResponsavelUsuarioId);     // sem engenheiro na obra → quem registra
        Assert.NotNull(r.Acoes[2].AvisoResponsavel);
        Assert.Equal(tecnico.Id, r.Acoes[3].ResponsavelUsuarioId);     // técnico = quem registra
        Assert.Null(r.Acoes[3].AvisoResponsavel);
        Assert.True(r.Acoes[0].Prazo > DateTime.UtcNow);
    }

    private static AcaoSugeridaIa Acao(string origem, string? perigo = null, string? controle = null, string? referencia = null)
        => new("Corretiva", "d", "Alta", "Encarregado", origem, perigo, controle, referencia);

    private sealed class AnalistaFalso : IAnalistaPlanoOcorrencia
    {
        private readonly AnalisePlanoIa _resposta;
        public AnalistaFalso(AnalisePlanoIa resposta) => _resposta = resposta;
        public Task<AnalisePlanoIa> AnalisarAsync(ContextoAnaliseOcorrencia contexto, CancellationToken ct) => Task.FromResult(_resposta);
    }
}
