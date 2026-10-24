using AAHBRANT.SST.Application.Riscos.Commands;
using AAHBRANT.SST.Application.Riscos.Medidas;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Riscos;

// Avaliação do risco e medidas por fase para a Ordem de Serviço (20/10/2026). Regra do usuário: o que
// veio do PGR entra validado; só o que a IA acrescenta precisa da validação da SST.
public class MedidasPrevencaoTests
{
    private sealed class AnalistaFalso : IAnalistaMedidasPrevencao
    {
        private readonly IReadOnlyList<MedidaSugeridaIa> _resposta;
        public ContextoMedidasRisco? Recebido { get; private set; }
        public AnalistaFalso(params MedidaSugeridaIa[] resposta) => _resposta = resposta;

        public Task<IReadOnlyList<MedidaSugeridaIa>> SepararAsync(ContextoMedidasRisco contexto, CancellationToken ct)
        {
            Recebido = contexto;
            return Task.FromResult(_resposta);
        }
    }

    private static async Task<(Infrastructure.Persistencia.SstDbContext Db, Risco Risco)> CriarRiscoAsync(string? controles = "EPI: Cinto paraquedista com talabarte duplo")
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB-01", Nome = "Obra Teste" };
        var atividade = new Atividade { ObraId = obra.Id, Nome = "Montagem de armadura" };
        var perigo = new Perigo { Nome = "Queda de altura", Fonte = "Laje" };
        var risco = new Risco
        {
            AtividadeId = atividade.Id, PerigoId = perigo.Id, Probabilidade = 4, Severidade = 5,
            NivelRisco = NivelRisco.Critico, Consequencia = "Fraturas", ControlesExistentes = controles,
        };
        var matriz = new MatrizRiscoConfig { Nome = "5x5", NumNiveisProbabilidade = 5, NumNiveisSeveridade = 5 };
        for (var p = 1; p <= 5; p++)
            for (var s = 1; s <= 5; s++)
                matriz.Celulas.Add(new MatrizRiscoCelula { Probabilidade = p, Severidade = s, NivelRisco = p * s >= 20 ? NivelRisco.Critico : NivelRisco.Alto });
        db.AddRange(obra, atividade, perigo, risco, matriz);
        await db.SaveChangesAsync();
        return (db, risco);
    }

    [Fact]
    public void Conciliar_TrechoQueEstaNoPgr_EntraValidado_IgnorandoAcentoECaixa()
    {
        var r = ConciliacaoMedidasPgr.Conciliar("EPI: Cinto paraquedista com talabarte duplo.",
            new[] { new MedidaSugeridaIa("Durante", "Use cinto paraquedista", "PGR", "cinto PARAQUEDISTA com talabarte") });

        var m = Assert.Single(r);
        Assert.Equal(StatusMedidaPrevencao.Validada, m.Status);
        Assert.Equal(OrigemMedidaPrevencao.Pgr, m.Origem);
        Assert.Equal(FaseMedidaPrevencao.Durante, m.Fase);
    }

    [Fact]
    public void Conciliar_PgrComTrechoQueNaoEstaNoTexto_ViraSugestaoPendente()
    {
        var r = ConciliacaoMedidasPgr.Conciliar("EPI: Cinto paraquedista",
            new[] { new MedidaSugeridaIa("Antes", "Instalar linha de vida", "PGR", "linha de vida certificada") });

        var m = Assert.Single(r);
        Assert.Equal(StatusMedidaPrevencao.Pendente, m.Status);
        Assert.Equal(OrigemMedidaPrevencao.SugestaoIa, m.Origem);
    }

    [Fact]
    public void Conciliar_Sugestao_EntraPendente_EDuplicadaOuVaziaSaem()
    {
        var r = ConciliacaoMedidasPgr.Conciliar("EPI: Cinto",
            new[]
            {
                new MedidaSugeridaIa("Apos", "Guardar o cinto", "Sugestao", null),
                new MedidaSugeridaIa("Apos", "guardar o cinto.", "Sugestao", null),
                new MedidaSugeridaIa("Antes", "  ", "Sugestao", null),
            });

        var m = Assert.Single(r);
        Assert.Equal(StatusMedidaPrevencao.Pendente, m.Status);
        Assert.Equal(FaseMedidaPrevencao.Apos, m.Fase);
    }

    [Fact]
    public async Task Separar_GravaMedidasDoPgrValidadasESugestoesPendentes()
    {
        var (db, risco) = await CriarRiscoAsync();
        var analista = new AnalistaFalso(
            new MedidaSugeridaIa("Durante", "Use cinto paraquedista com talabarte duplo", "PGR", "Cinto paraquedista com talabarte duplo"),
            new MedidaSugeridaIa("Antes", "Verifique a ancoragem", "Sugestao", null));

        var total = await new SepararMedidasRiscoCommandHandler(db, analista).Handle(new SepararMedidasRiscoCommand(risco.Id), default);

        Assert.Equal(2, total);
        Assert.Equal("Queda de altura", analista.Recebido!.Perigo);
        var medidas = await db.MedidasPrevencao.OrderBy(m => m.Ordem).ToListAsync();
        Assert.Equal(FaseMedidaPrevencao.Antes, medidas[0].Fase);
        Assert.Equal(StatusMedidaPrevencao.Pendente, medidas[0].Status);
        Assert.Null(medidas[0].ValidadoEmUtc);
        Assert.Equal(StatusMedidaPrevencao.Validada, medidas[1].Status);
        Assert.Equal("Texto do PGR", medidas[1].ValidadoPorNome);
    }

    [Fact]
    public async Task Separar_RiscoQueJaTemMedidas_Recusa()
    {
        var (db, risco) = await CriarRiscoAsync();
        db.MedidasPrevencao.Add(new MedidaPrevencao { RiscoId = risco.Id, Fase = FaseMedidaPrevencao.Antes, Texto = "x", Status = StatusMedidaPrevencao.Validada });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SepararMedidasRiscoCommandHandler(db, new AnalistaFalso()).Handle(new SepararMedidasRiscoCommand(risco.Id), default));
    }

    [Fact]
    public async Task Decidir_AceitarComEdicao_ValidaComTextoNovo_EDescartarNaoApaga()
    {
        var (db, risco) = await CriarRiscoAsync();
        var sst = new Usuario { Nome = "Eng. Carlos", Email = "c@x.com" };
        var a = new MedidaPrevencao { RiscoId = risco.Id, Fase = FaseMedidaPrevencao.Antes, Texto = "original", OrigemMedida = OrigemMedidaPrevencao.SugestaoIa, Status = StatusMedidaPrevencao.Pendente };
        var b = new MedidaPrevencao { RiscoId = risco.Id, Fase = FaseMedidaPrevencao.Apos, Texto = "outra", OrigemMedida = OrigemMedidaPrevencao.SugestaoIa, Status = StatusMedidaPrevencao.Pendente };
        db.AddRange(sst, a, b);
        await db.SaveChangesAsync();
        var handler = new DecidirMedidaPrevencaoCommandHandler(db);

        await handler.Handle(new DecidirMedidaPrevencaoCommand(a.Id, true, "editada", sst.Id, null), default);
        await handler.Handle(new DecidirMedidaPrevencaoCommand(b.Id, false, null, sst.Id, null), default);

        var salvaA = await db.MedidasPrevencao.SingleAsync(m => m.Id == a.Id);
        Assert.Equal(StatusMedidaPrevencao.Validada, salvaA.Status);
        Assert.Equal("editada", salvaA.Texto);
        Assert.Equal("Eng. Carlos", salvaA.ValidadoPorNome);
        Assert.Equal(StatusMedidaPrevencao.Descartada, (await db.MedidasPrevencao.SingleAsync(m => m.Id == b.Id)).Status);
    }

    [Fact]
    public async Task Adicionar_MedidaDaSst_NasceValidada()
    {
        var (db, risco) = await CriarRiscoAsync();

        await new AdicionarMedidaPrevencaoCommandHandler(db).Handle(
            new AdicionarMedidaPrevencaoCommand(risco.Id, FaseMedidaPrevencao.Durante, " Isolar a área ", null, "Técnica Ana"), default);

        var m = await db.MedidasPrevencao.SingleAsync();
        Assert.Equal(StatusMedidaPrevencao.Validada, m.Status);
        Assert.Equal(OrigemMedidaPrevencao.Manual, m.OrigemMedida);
        Assert.Equal("Isolar a área", m.Texto);
        Assert.Equal("Técnica Ana", m.ValidadoPorNome);
    }

    [Fact]
    public async Task ConfirmarAvaliacao_RegistraQuem_EMudarSeveridadeApagaConfirmacao()
    {
        var (db, risco) = await CriarRiscoAsync();
        await new ConfirmarAvaliacaoRiscoCommandHandler(db).Handle(new ConfirmarAvaliacaoRiscoCommand(risco.Id, null, "Eng. Carlos"), default);
        Assert.Equal("Eng. Carlos", (await db.Riscos.SingleAsync()).AvaliadoPorNome);

        await new AtualizarRiscoCommandHandler(db).Handle(new AtualizarRiscoCommand(
            risco.Id, risco.AtividadeId, risco.PerigoId, null, null, "Fraturas", 4, 4, risco.ControlesExistentes, null,
            null, null, StatusControleRisco.Pendente, new List<Guid>()), default);

        var salvo = await db.Riscos.SingleAsync();
        Assert.Null(salvo.AvaliadoEmUtc);
        Assert.Null(salvo.AvaliadoPorNome);
    }
}
