using AAHBRANT.SST.Application.EscopoPcmso;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.EscopoPcmso;

public class ObterEscopoPcmsoQueryTests
{
    [Fact]
    public async Task Handle_MontaFuncoesRiscosEAtividadesCriticasDoPgrVigente()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Nome = "Obra A", Codigo = "A", Cnpj = "00.000.000/0001-00" };
        var armador = new Funcao { Nome = "Armador" };
        var nr35 = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        db.Obras.Add(obra);
        db.Funcoes.Add(armador);
        db.CursosTreinamento.Add(nr35);
        db.Pgrs.Add(new Pgr { ObraId = obra.Id, Nome = "PGR Obra A — Rev. 01", Status = StatusPgr.Vigente, DataElaboracao = new DateTime(2026, 1, 10) });
        var ghe = new Ghe { ObraId = obra.Id, Numero = 2, Setor = "Estrutura" };
        ghe.Funcoes.Add(new GheFuncao { FuncaoId = armador.Id, DescricaoAtividades = "Corte e dobra" });
        db.Ghes.Add(ghe);
        var a1 = new Atividade { ObraId = obra.Id, GheId = ghe.Id, Nome = "Montagem de pilares" };
        var a2 = new Atividade { ObraId = obra.Id, GheId = ghe.Id, Nome = "Montagem de vigas" };
        db.Atividades.AddRange(a1, a2);
        var queda = new Perigo { Nome = "Queda de altura", Agente = "Acidente" };
        db.Perigos.Add(queda);
        db.Riscos.Add(new Risco { AtividadeId = a1.Id, PerigoId = queda.Id, NivelRisco = NivelRisco.Moderado });
        db.Riscos.Add(new Risco { AtividadeId = a2.Id, PerigoId = queda.Id, NivelRisco = NivelRisco.Critico });
        db.Trabalhadores.Add(new Trabalhador { Nome = "Zé", Cpf = "12345678901", ObraId = obra.Id, FuncaoId = armador.Id, DataAdmissao = DateTime.UtcNow.AddYears(-1) });
        db.CursosObrigatoriosFuncaoObra.Add(new CursoObrigatorioFuncaoObra
        {
            ObraId = obra.Id, FuncaoId = armador.Id, CursoTreinamentoId = nr35.Id, Status = StatusCursoObrigatorio.Aprovado,
        });
        await db.SaveChangesAsync();

        var escopo = await new ObterEscopoPcmsoQueryHandler(db).Handle(new ObterEscopoPcmsoQuery(obra.Id), default);

        Assert.NotNull(escopo);
        Assert.Equal("PGR Obra A — Rev. 01", escopo!.PgrNome);
        var funcao = Assert.Single(escopo.Funcoes);
        Assert.Equal(("Armador", 1, 2), (funcao.Funcao, funcao.Trabalhadores, funcao.GheNumero));
        // Mesmo perigo em duas atividades do GHE vira uma linha, com o maior nível.
        var risco = Assert.Single(escopo.Riscos);
        Assert.Equal(NivelRisco.Critico, risco.Nivel);
        Assert.Equal("Acidente", risco.Tipo);
        var critica = Assert.Single(escopo.AtividadesCriticas);
        Assert.Contains("NR-35", critica.Descricao);
        Assert.Equal(["Armador"], critica.Funcoes);
        Assert.Contains(escopo.Pendencias, p => p.Contains("Endereço"));
    }
}
