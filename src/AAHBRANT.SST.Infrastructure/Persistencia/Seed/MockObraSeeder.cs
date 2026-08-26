using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder de dados mocados da Fase 1 do roadmap de SST (obra fictícia "Edifício Aurora
// Corporate", 20 pavimentos, ~200 trabalhadores) — só roda em ambiente Development (ver
// Program.cs), nunca em homologação/produção. Idempotente: se a Obra com CodigoObraMock já
// existe, não faz nada. Usa somente entidades já existentes no sistema; nenhuma migration nova.
// Ver docs/superpowers/specs/2026-08-26-fase1-dados-mock-obra-design.md.
public static partial class MockObraSeeder
{
    public const string CodigoObraMock = "OBRA-MOCK-AURORA";

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var jaExiste = await db.Obras.IgnoreQueryFilters().AnyAsync(o => o.Codigo == CodigoObraMock, ct);
        if (jaExiste) return;

        var referenciaUtc = DateTime.UtcNow;

        var (obra, areas, funcoes, setores, equipes, trabalhadores) = ConstruirEstruturaOrganizacional(referenciaUtc);

        db.Obras.Add(obra);
        db.AreasSst.AddRange(areas);
        db.Funcoes.AddRange(funcoes);
        db.Setores.AddRange(setores);
        db.Equipes.AddRange(equipes);
        db.Trabalhadores.AddRange(trabalhadores);

        await db.SaveChangesAsync(ct);
    }

    private static (Obra Obra, List<AreaSst> Areas, List<Funcao> Funcoes, List<Setor> Setores, List<Equipe> Equipes, List<Trabalhador> Trabalhadores)
        ConstruirEstruturaOrganizacional(DateTime referenciaUtc)
    {
        var obra = new Obra
        {
            Codigo = CodigoObraMock,
            Nome = "Edifício Aurora Corporate",
            Cliente = "Aurora Empreendimentos Imobiliários S.A.",
            Status = StatusObra.EmAndamento,
            DataInicio = referenciaUtc.AddMonths(-6),
            DataPrevisaoTermino = referenciaUtc.AddMonths(18),
            Endereco = "Av. das Torres, 1200",
            Cidade = "Belo Horizonte",
            Uf = "MG",
        };

        var areas = new List<AreaSst>
        {
            NovaArea(obra, "SUB", "Subsolo", TipoArea.AreaDeTrabalho),
            NovaArea(obra, "TER", "Térreo", TipoArea.AreaDeTrabalho),
        };
        for (var pavimento = 1; pavimento <= 20; pavimento++)
            areas.Add(NovaArea(obra, $"P{pavimento:D2}", $"Pavimento {pavimento}", TipoArea.AreaDeTrabalho));
        areas.Add(NovaArea(obra, "CANT", "Canteiro/Almoxarifado", TipoArea.Armazenamento));

        var funcoes = DistribuicaoFuncoes
            .Select(f => new Funcao { Nome = f.Funcao })
            .ToList();

        var setores = new List<Setor>
        {
            NovoSetor(obra, "Estrutura Térreo–P10"),
            NovoSetor(obra, "Estrutura P11–P20"),
            NovoSetor(obra, "Acabamento"),
            NovoSetor(obra, "Instalações"),
            NovoSetor(obra, "Canteiro/Apoio"),
        };

        var equipes = new List<Equipe>();
        foreach (var setor in setores)
        {
            equipes.Add(new Equipe { Setor = setor, Nome = $"{setor.Nome} — Equipe A" });
            equipes.Add(new Equipe { Setor = setor, Nome = $"{setor.Nome} — Equipe B" });
        }

        var trabalhadores = new List<Trabalhador>();
        var indiceGlobal = 0;
        var indiceEquipe = 0;

        foreach (var (nomeFuncao, quantidade, _) in DistribuicaoFuncoes)
        {
            var funcao = funcoes.Single(f => f.Nome == nomeFuncao);
            for (var i = 0; i < quantidade; i++)
            {
                var equipe = equipes[indiceEquipe % equipes.Count];
                var trabalhador = new Trabalhador
                {
                    Obra = obra,
                    Setor = equipe.Setor,
                    Equipe = equipe,
                    Funcao = funcao,
                    Nome = GerarNome(indiceGlobal),
                    Matricula = $"AUR-{indiceGlobal + 1:D4}",
                    Cpf = GeradorCpfFicticio.Gerar(indiceGlobal),
                    Vinculo = TipoVinculo.Clt,
                    DataAdmissao = referenciaUtc.AddMonths(-6).AddDays(indiceGlobal % 150),
                };
                trabalhadores.Add(trabalhador);

                if (nomeFuncao == "Encarregado" && equipe.Encarregado is null)
                    equipe.Encarregado = trabalhador;

                indiceGlobal++;
                indiceEquipe++;
            }
        }

        return (obra, areas, funcoes, setores, equipes, trabalhadores);
    }

    private static AreaSst NovaArea(Obra obra, string codigo, string nome, TipoArea tipo) => new()
    {
        Obra = obra,
        Codigo = codigo,
        Nome = nome,
        Tipo = tipo,
        Riscos = new List<string> { "Queda de altura", "Queda de material", "Atropelamento por equipamento" },
        Requisitos = new List<string> { "Uso obrigatório de capacete", "Delimitação de área com fita zebrada" },
        Status = StatusArea.Ativa,
    };

    private static Setor NovoSetor(Obra obra, string nome) => new() { Obra = obra, Nome = nome };
}
