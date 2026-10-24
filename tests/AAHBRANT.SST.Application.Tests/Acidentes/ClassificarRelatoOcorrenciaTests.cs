using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Acidentes;

public class ClassificarRelatoOcorrenciaTests
{
    private static readonly CorrespondenciaNomesTrabalhadores.Candidato Joao = new(Guid.NewGuid(), "João Pedro da Silva");
    private static readonly CorrespondenciaNomesTrabalhadores.Candidato JoaoMarcos = new(Guid.NewGuid(), "João Marcos Souza");
    private static readonly CorrespondenciaNomesTrabalhadores.Candidato Maria = new(Guid.NewGuid(), "Maria de Fátima Lima");

    [Fact]
    public void Localizar_NomeParcialSemAcento_EncontraUnicoFuncionario()
    {
        var r = CorrespondenciaNomesTrabalhadores.Localizar(new[] { "joao pedro", "Maria Fatima" }, new[] { Joao, JoaoMarcos, Maria });

        Assert.Equal(new[] { Joao.Id, Maria.Id }, r.Localizados);
        Assert.Empty(r.Pendencias);
    }

    [Fact]
    public void Localizar_NomeAmbiguoOuAusente_ViraPendenciaENuncaPalpite()
    {
        var r = CorrespondenciaNomesTrabalhadores.Localizar(new[] { "João", "Carlos" }, new[] { Joao, JoaoMarcos, Maria });

        Assert.Empty(r.Localizados);
        Assert.Equal(2, r.Pendencias.Count);
        Assert.Contains("2 funcionários", r.Pendencias[0]);
        Assert.Contains("não foi encontrado", r.Pendencias[1]);
    }

    [Fact]
    public async Task Handle_MapeiaAtividadeEFuncionarioDaObraEAplicaRegrasDeSeguranca()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Residencial Aurora" };
        var outraObra = new Obra { Codigo = "OB2", Nome = "Outra" };
        var atividade = new Atividade { ObraId = obra.Id, Nome = "Trabalho em altura" };
        var joao = new Trabalhador { Nome = "João Pedro da Silva", ObraId = obra.Id };
        var joaoOutraObra = new Trabalhador { Nome = "João Pedro Santos", ObraId = outraObra.Id };
        db.AddRange(obra, outraObra, atividade, joao, joaoOutraObra);
        await db.SaveChangesAsync();

        var falso = new ClassificadorFalso(new OcorrenciaSugeridaIa(
            "Acidente", "IncapacidadePermanenteParcial", "Bloco B", "2026-10-08", "09:30", "Descrição técnica",
            "Torção", null, "UPA", HouveAfastamento: false, DiasAfastamento: 3, "trabalho em altura",
            new[] { "João Pedro" },
            new[]
            {
                new PerguntaRelatoOcorrencia("outro", "Havia condição adicional no local?"),
                new PerguntaRelatoOcorrencia("outro", "havia condição adicional no local?"),
            }));
        var handler = new ClassificarRelatoOcorrenciaCommandHandler(db, falso);

        var r = await handler.Handle(new ClassificarRelatoOcorrenciaCommand("relato", obra.Id), default);

        Assert.Equal(TipoOcorrencia.Acidente, r.Tipo);
        Assert.Equal(atividade.Id, r.AtividadeId);
        Assert.Equal(new[] { joao.Id }, r.TrabalhadoresIds); // só procura na obra escolhida
        Assert.Equal("2026-10-08", r.Data);
        Assert.Equal("09:30", r.Hora);
        Assert.Null(r.DiasAfastamento); // sem afastamento, dias não são aproveitados
        Assert.Contains(r.Avisos, a => a.Contains("Quadro III"));
        Assert.Single(r.Perguntas); // pergunta repetida pelo modelo aparece uma vez só
        Assert.Equal(new[] { "Trabalho em altura" }, falso.AtividadesRecebidas);
    }

    [Fact]
    public async Task Handle_ValoresInvalidosDoModelo_CaemEmPadraoSeguro()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Add(obra);
        await db.SaveChangesAsync();

        var falso = new ClassificadorFalso(new OcorrenciaSugeridaIa(
            "Inventado", null, new string('x', 300), "08/10/2026", "9h30", null,
            null, null, null, null, null, "Atividade que não existe",
            Array.Empty<string>(), Array.Empty<PerguntaRelatoOcorrencia>()));
        var handler = new ClassificarRelatoOcorrenciaCommandHandler(db, falso);

        var r = await handler.Handle(new ClassificarRelatoOcorrenciaCommand("o relato original", obra.Id), default);

        Assert.Equal(TipoOcorrencia.Incidente, r.Tipo);
        Assert.Equal(GravidadeAcidente.SemAfastamento, r.Gravidade);
        Assert.Equal(200, r.Local!.Length);
        Assert.Null(r.Data);
        Assert.Null(r.Hora);
        Assert.Null(r.AtividadeId);
        Assert.Equal("o relato original", r.Descricao);
    }

    [Fact]
    public async Task Handle_RepassaSoAsRespostasPreenchidas()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        db.Add(obra);
        await db.SaveChangesAsync();
        var falso = new ClassificadorFalso(new OcorrenciaSugeridaIa(
            "Acidente", "SemAfastamento", null, null, null, "d", null, null, null, null, null, null,
            Array.Empty<string>(), Array.Empty<PerguntaRelatoOcorrencia>()));

        await new ClassificarRelatoOcorrenciaCommandHandler(db, falso).Handle(
            new ClassificarRelatoOcorrenciaCommand("relato", obra.Id, new[]
            {
                new RespostaPerguntaRelato("Houve CAT?", "Não"),
                new RespostaPerguntaRelato("Qual o horário?", "  "),
            }),
            default);

        Assert.Equal(new[] { "Houve CAT?" }, falso.ComplementosRecebidos.Select(c => c.Pergunta));
    }

    private sealed class ClassificadorFalso : IClassificadorRelatoOcorrencia
    {
        private readonly OcorrenciaSugeridaIa _resposta;
        public IReadOnlyList<string> AtividadesRecebidas { get; private set; } = Array.Empty<string>();

        public ClassificadorFalso(OcorrenciaSugeridaIa resposta) => _resposta = resposta;

        public IReadOnlyList<RespostaPerguntaRelato> ComplementosRecebidos { get; private set; } = Array.Empty<RespostaPerguntaRelato>();

        public Task<OcorrenciaSugeridaIa> ClassificarAsync(
            string relato, IReadOnlyList<RespostaPerguntaRelato> complementos, DateTime agoraLocal,
            IReadOnlyList<string> atividadesDaObra, CancellationToken ct)
        {
            AtividadesRecebidas = atividadesDaObra;
            ComplementosRecebidos = complementos;
            return Task.FromResult(_resposta);
        }
    }
}
