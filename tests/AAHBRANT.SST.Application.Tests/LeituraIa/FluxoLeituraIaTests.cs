using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Ghes.Commands;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Tests.LeituraIa;

// Fluxo inteiro sem IA de verdade: iniciar → processar (leitor falso) → cadastrar → nova leitura
// (nova revisão) substitui a estrutura anterior sem apagá-la.
public class FluxoLeituraIaTests
{
    private sealed class FilaFalsa : IFilaLeituraIa
    {
        public List<Guid> Enfileiradas { get; } = new();
        public ValueTask EnfileirarAsync(Guid leituraId, CancellationToken ct = default) { Enfileiradas.Add(leituraId); return ValueTask.CompletedTask; }
    }

    private sealed class ExtratorFalso : IExtratorTextoPdf
    {
        public IReadOnlyList<string> ExtrairPaginas(byte[] pdf) => new[] { new string('x', 600) };
    }

    private sealed class LeitorFalso : ILeitorDocumentoSstIa
    {
        public LeituraPgrIa Pgr { get; set; } = null!;
        public Exception? Falha { get; set; }

        public async Task<LeituraPgrIa> LerPgrAsync(IReadOnlyList<string> paginas, Func<string, int, int, Task> progresso, CancellationToken ct)
        {
            await progresso("lendo", 1, 2);
            if (Falha is not null) throw Falha;
            return Pgr;
        }

        public Task<LeituraPcmsoIa> LerPcmsoAsync(IReadOnlyList<string> paginas, Func<string, int, int, Task> progresso, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class NotificacaoFalsa : INotificacaoTeamsService
    {
        public Task<bool> EnviarAsync(Guid usuarioId, string titulo, string? descricao, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class GeradorNumeroFalso : IGeradorNumeroDocumentoService
    {
        public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-1");
    }

    // Só o comando de importação passa pelo "mediator" no cadastro.
    private sealed class EnviadorFalso : ISender
    {
        private readonly IAppDbContext _db;
        public EnviadorFalso(IAppDbContext db) => _db = db;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => (TResponse)(object)await new ImportarEstruturaSstObraCommandHandler(_db, new GeradorNumeroFalso())
                .Handle((ImportarEstruturaSstObraCommand)(object)request, ct);

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static SstDbContext CriarDb(string nome) =>
        new(new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nome).Options, new CurrentUserService());

    private static LeituraPgrIa Pgr(params (int Numero, string Funcao, string Agente, int P, int S)[] ghes) => new(
        new CabecalhoPgrIa("Eng.", null, "02", null, new DateTime(2027, 10, 10), new DateTime(2028, 10, 10)),
        ghes.Select(g => new ImportarEstruturaGhe(g.Numero, "Canteiro", null, null, null, null, null,
            new List<ImportarEstruturaFuncao> { new(g.Funcao, null, null, 1, null) },
            new List<ImportarEstruturaRisco> { new("Acidentes", g.Agente, null, null, null, null, g.P, g.S, $"{g.P * g.S} - Risco", null) })).ToList(),
        new List<string> { "Divulgação do PGR" });

    private static async Task<(SstDbContext Db, Pgr Pgr, Funcao Pedreiro)> SemearAsync(string nome)
    {
        var db = CriarDb(nome);
        var obra = new Obra { Codigo = "R", Nome = "Roger" };
        var pgr = new Pgr { Obra = obra, Nome = "PGR", DataElaboracao = new DateTime(2024, 10, 10), DocumentoConteudo = new byte[] { 1 }, DocumentoContentType = "application/pdf" };
        var pedreiro = new Funcao { Nome = "PEDREIRO" };
        var matriz = new MatrizRiscoConfig { Nome = "5x5", NumNiveisProbabilidade = 5, NumNiveisSeveridade = 5 };
        for (var p = 1; p <= 5; p++)
            for (var s = 1; s <= 5; s++)
                matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = p, Severidade = s, NivelRisco = p * s <= 4 ? NivelRisco.Baixo : NivelRisco.Moderado });
        db.AddRange(pgr, pedreiro, matriz);
        await db.SaveChangesAsync();
        return (db, pgr, pedreiro);
    }

    private static async Task<Guid> LerAsync(SstDbContext db, Guid pgrId, LeitorFalso leitor)
    {
        var fila = new FilaFalsa();
        var id = await new IniciarLeituraIaCommandHandler(db, fila).Handle(new IniciarLeituraIaCommand(DocumentoLeituraIa.Pgr, pgrId), CancellationToken.None);
        Assert.Equal(new[] { id }, fila.Enfileiradas);
        await new ProcessadorLeituraIa(db, new ExtratorFalso(), leitor, new NotificacaoFalsa(), NullLogger<ProcessadorLeituraIa>.Instance)
            .ProcessarAsync(id, CancellationToken.None);
        return id;
    }

    [Fact]
    public async Task Ler_revisar_e_cadastrar_e_depois_nova_revisao_substitui_sem_apagar()
    {
        var (db, pgr, pedreiro) = await SemearAsync(nameof(Ler_revisar_e_cadastrar_e_depois_nova_revisao_substitui_sem_apagar));

        // 1ª leitura: primeira estrutura da obra.
        var leitor = new LeitorFalso { Pgr = Pgr((1, "Pedreiro", "Trabalho em altura", 3, 4), (12, "Vigia", "Violência", 1, 3)) };
        var id = await LerAsync(db, pgr.Id, leitor);
        var leitura = (await new ObterLeituraIaQueryHandler(db).Handle(new ObterLeituraIaQuery(id, DocumentoLeituraIa.Pgr), CancellationToken.None))!;
        Assert.Equal(StatusLeituraIa.Concluida, leitura.Status);
        Assert.False(leitura.Resultado!.HaEstruturaAtual);
        Assert.Equal(("existe", pedreiro.Id), leitura.Resultado.Funcoes.Single(f => f.NomeDocumento == "Pedreiro") is var p ? (p.Situacao, p.FuncaoIdSugerida!.Value) : default);

        await new CadastrarLeituraIaCommandHandler(db, new EnviadorFalso(db)).Handle(new CadastrarLeituraIaCommand(id, DocumentoLeituraIa.Pgr,
            new List<MapeamentoFuncaoLeituraIa> { new("Pedreiro", pedreiro.Id), new("Vigia", null) }), CancellationToken.None);
        Assert.Equal(2, await db.Ghes.CountAsync());
        Assert.Equal(new DateTime(2028, 10, 10), (await db.Pgrs.SingleAsync()).DataTermino);
        Assert.Equal(StatusLeituraIa.Cadastrada, (await db.LeiturasDocumentoIa.SingleAsync()).Status);

        // 2ª leitura (nova revisão): GHE 12 sai, risco do GHE 01 muda de nível.
        leitor.Pgr = Pgr((1, "Pedreiro", "Trabalho em altura", 1, 2));
        var id2 = await LerAsync(db, pgr.Id, leitor);
        var segunda = (await new ObterLeituraIaQueryHandler(db).Handle(new ObterLeituraIaQuery(id2, DocumentoLeituraIa.Pgr), CancellationToken.None))!;
        Assert.True(segunda.Resultado!.HaEstruturaAtual);
        Assert.Contains(segunda.Resultado.Diferencas, d => d.Tipo == "removido" && d.Oque.StartsWith("GHE 12"));
        Assert.Contains(segunda.Resultado.Diferencas, d => d.Tipo == "alterado" && d.Antes == "Moderado" && d.Depois == "Baixo");

        await new CadastrarLeituraIaCommandHandler(db, new EnviadorFalso(db)).Handle(new CadastrarLeituraIaCommand(id2, DocumentoLeituraIa.Pgr,
            new List<MapeamentoFuncaoLeituraIa> { new("Pedreiro", pedreiro.Id) }), CancellationToken.None);

        Assert.Single(await db.Ghes.ToListAsync());
        Assert.Equal(3, await db.Ghes.IgnoreQueryFilters().CountAsync());
        // Atividades antigas continuam no banco, mas saem da lista de atividades em uso.
        Assert.Equal(3, await db.Atividades.CountAsync());
        Assert.Single(await AAHBRANT.SST.Application.Common.AtividadesEmUso.EmUso(db.Atividades, db).ToListAsync());
    }

    [Fact]
    public async Task Falha_da_IA_vira_status_Falhou_com_mensagem_e_pode_ler_de_novo()
    {
        var (db, pgr, _) = await SemearAsync(nameof(Falha_da_IA_vira_status_Falhou_com_mensagem_e_pode_ler_de_novo));
        var leitor = new LeitorFalso { Falha = new InvalidOperationException("timeout") };

        var id = await LerAsync(db, pgr.Id, leitor);

        var l = await db.LeiturasDocumentoIa.SingleAsync(x => x.Id == id);
        Assert.Equal(StatusLeituraIa.Falhou, l.Status);
        Assert.Contains("Tente de novo", l.Erro);
        Assert.Empty(await db.Ghes.ToListAsync());

        leitor.Falha = null;
        leitor.Pgr = Pgr((1, "Pedreiro", "Ruído", 1, 3));
        var id2 = await LerAsync(db, pgr.Id, leitor);
        Assert.NotEqual(id, id2);
        Assert.Equal(StatusLeituraIa.Concluida, (await db.LeiturasDocumentoIa.SingleAsync(x => x.Id == id2)).Status);
    }
}
