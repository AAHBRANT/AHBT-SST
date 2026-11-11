using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Dados de uma pessoa para decidir em quais etapas de curso ela entra.
public sealed record PessoaEtapasIntegracao(
    Guid TrabalhadorId,
    // Técnico de Segurança: isento dos cursos (como na liberação e na agenda de turmas). Continua na etapa da OS.
    bool Isento,
    // Normas ("NR-06"...) com treinamento dentro da validade.
    IReadOnlySet<string> NormasValidas,
    // Normas exigidas pela função no PGR da obra (vale para NR-12 e NR-35).
    IReadOnlySet<string> NormasExigidasPeloPgr);

public sealed record CursoEtapaIntegracao(Guid CursoId, int CargaPadraoHoras);

public sealed record EtapaCalculada(
    int Ordem, TipoEtapaIntegracao Tipo, Guid? CursoId, int CargaMinimaHoras, IReadOnlyList<Guid> Participantes, bool Pulada);

// Regra única das etapas da integração (decisão do usuário, 11/11/2026): ordem fixa NR-06, NR-12, NR-18, NR-35 e OS.
//  - NR-06 e NR-18: todos, menos quem já tem a NR válida.
//  - NR-12 e NR-35: quem tem a NR exigida pelo PGR para a função, menos quem já tem a NR válida.
//  - OS: todos (nunca é pulada).
// Etapa de curso sem ninguém é Pulada. Carga mínima: a maior entre o padrão do curso/NR e o piso decidido pelo
// usuário (NR-18 4 h, NR-12 8 h, NR-35 8 h; NR-06 sem piso: o QSMS ajusta no catálogo, 0 = sem trava).
public static class CalculadoraEtapasIntegracao
{
    public static readonly IReadOnlyList<(int Ordem, TipoEtapaIntegracao Tipo, string? Norma, int PisoHoras, bool SoSeExigidaPeloPgr)> Definicao =
    [
        (1, TipoEtapaIntegracao.Nr06, "NR-06", 0, false),
        (2, TipoEtapaIntegracao.Nr12, "NR-12", 8, true),
        (3, TipoEtapaIntegracao.Nr18, "NR-18", 4, false),
        (4, TipoEtapaIntegracao.Nr35, "NR-35", 8, true),
        (5, TipoEtapaIntegracao.OrdemServico, null, 0, false),
    ];

    public static string Titulo(TipoEtapaIntegracao tipo) => tipo switch
    {
        TipoEtapaIntegracao.Nr06 => "NR-06 — Uso de EPI",
        TipoEtapaIntegracao.Nr12 => "NR-12 — Máquinas e equipamentos",
        TipoEtapaIntegracao.Nr18 => "NR-18 — Construção civil",
        TipoEtapaIntegracao.Nr35 => "NR-35 — Trabalho em altura",
        _ => "Ordem de Serviço (NR-01)",
    };

    public static string? NormaDoTipo(TipoEtapaIntegracao tipo) => Definicao.First(d => d.Tipo == tipo).Norma;

    public static IReadOnlyList<EtapaCalculada> Calcular(
        IReadOnlyList<PessoaEtapasIntegracao> pessoas,
        IReadOnlyDictionary<TipoEtapaIntegracao, CursoEtapaIntegracao> cursos)
    {
        var etapas = new List<EtapaCalculada>();
        foreach (var d in Definicao)
        {
            if (d.Norma is null)
            {
                etapas.Add(new EtapaCalculada(d.Ordem, d.Tipo, null, 0, pessoas.Select(p => p.TrabalhadorId).ToList(), false));
                continue;
            }

            var participantes = pessoas
                .Where(p => !p.Isento
                    && !p.NormasValidas.Contains(d.Norma)
                    && (!d.SoSeExigidaPeloPgr || p.NormasExigidasPeloPgr.Contains(d.Norma)))
                .Select(p => p.TrabalhadorId)
                .ToList();
            cursos.TryGetValue(d.Tipo, out var curso);
            var carga = Math.Max(curso?.CargaPadraoHoras ?? 0, d.PisoHoras);
            etapas.Add(new EtapaCalculada(d.Ordem, d.Tipo, curso?.CursoId, carga, participantes, participantes.Count == 0));
        }
        return etapas;
    }
}
