namespace AAHBRANT.SST.Application.OrdensServico;

// Regra única da emissão: qualquer pendência bloqueia (pendências > 0 ⇒ não emite). Decisões do
// usuário (20/10/2026): o que veio do PGR conta como validado; sugestão da IA pendente bloqueia;
// treinamento exigido vencido ou não feito e ASO sem aptidão válida também bloqueiam.
public static class AvaliadorPendenciasOs
{
    public record RiscoAvaliado(Guid Id, string Perigo, bool Avaliado, int MedidasValidadas, int MedidasPendentes);

    public record AtividadeAvaliada(Guid Id, string Nome, List<RiscoAvaliado> Riscos, int ItensPendentes, int Procedimentos);

    public record Entrada(
        Guid? PgrVigenteId,
        bool FuncaoTemGhe,
        bool TemDescricaoAtividades,
        List<AtividadeAvaliada> Atividades,
        List<TreinamentoOsDto> Treinamentos,
        AsoOsDto? Aso,
        // Atividades por função (Entrega 4, 10/11/2026). Padrão = função "definida", que mantém o
        // comportamento anterior para quem não informa estes campos.
        // Definidas: toda atividade em uso dos GHEs da função tem decisão (Validada ou Descartada).
        bool AtividadesDaFuncaoDefinidas = true,
        // Nomes das atividades em uso ainda sem decisão do engenheiro (Pendente também conta como sem decisão).
        List<string>? AtividadesSemDecisao = null,
        // Transição: há atividades em uso e NENHUMA decisão ainda; a minuta traz todas as atividades (prévia).
        bool SemNenhumaDecisao = false,
        // Todas as atividades em uso foram decididas e nenhuma ficou confirmada para a função.
        bool TodasDescartadas = false,
        string? FuncaoNome = null,
        List<int>? GhesNumeros = null);

    public static List<PendenciaOsDto> Avaliar(Entrada e)
    {
        var p = new List<PendenciaOsDto>();
        var pgr = e.PgrVigenteId;

        if (pgr is null)
            p.Add(new("PgrNaoVigente", "A obra não tem PGR vigente",
                "As atividades e os riscos da OS vêm do PGR. Só um PGR vigente pode servir de base para orientar o trabalhador.",
                "SST", "pgr", null));

        if (!e.FuncaoTemGhe)
            p.Add(new("FuncaoSemGhe", "A função do trabalhador não está em nenhum GHE do PGR desta obra",
                "Sem o GHE não há atividades nem riscos para informar na OS.", "SST", "pgr", pgr));
        else if (!e.TemDescricaoAtividades)
            p.Add(new("SemDescricaoAtividades", "O PGR não descreve as atividades desta função",
                "A OS precisa trazer, na íntegra, as atividades da função conforme o PGR.", "SST", "pgr", pgr));

        var funcao = string.IsNullOrWhiteSpace(e.FuncaoNome) ? "do trabalhador" : e.FuncaoNome!.Trim();
        var semDecisao = e.AtividadesSemDecisao ?? new List<string>();

        if (e.FuncaoTemGhe && e.SemNenhumaDecisao)
        {
            // Transição: uma única pendência no lugar das pendências por atividade (que seriam de etapas da obra inteira).
            var ghes = e.GhesNumeros is { Count: > 0 } n
                ? (n.Count == 1 ? $"no GHE {n[0]:00}" : $"nos GHEs {string.Join(", ", n.Select(x => x.ToString("00")))}")
                : "no GHE";
            p.Add(new("AtividadesFuncaoNaoConfirmadas",
                $"As atividades da função {funcao} {ghes} ainda não foram confirmadas pelo engenheiro",
                "A OS só lista as atividades que a função executa, confirmadas pelo Engenheiro de Segurança. Sem essa confirmação a OS traria etapas da obra inteira.",
                "SST", "pgr", pgr));
        }
        // Com atividades ainda sem decisão e nenhuma confirmada, quem explica é AtividadesFuncaoSemDecisao: o
        // inventário não está vazio.
        else if (e.FuncaoTemGhe && e.Atividades.Count == 0 && (e.AtividadesDaFuncaoDefinidas || semDecisao.Count == 0))
        {
            if (e.TodasDescartadas)
                p.Add(new("AtividadesFuncaoNenhumaConfirmada", $"Nenhuma atividade confirmada para a função {funcao}",
                    "Todas as atividades do GHE foram descartadas para esta função; confirme ao menos uma atividade que ela executa.", "SST", "pgr", pgr));
            else
                p.Add(new("SemAtividades", "O GHE da função não tem atividades com riscos no inventário",
                    "Sem o inventário não há riscos nem medidas para informar na OS.", "SST", "pgr", pgr));
        }

        if (e.FuncaoTemGhe && !e.SemNenhumaDecisao && !e.AtividadesDaFuncaoDefinidas && semDecisao.Count > 0)
            p.Add(new("AtividadesFuncaoSemDecisao",
                $"{semDecisao.Count} atividade(s) sem decisão do engenheiro para a função {funcao}: {string.Join(", ", semDecisao)}",
                "Cada atividade do GHE precisa ser confirmada ou descartada para a função, para a OS listar só o que ela executa.",
                "SST", "pgr", pgr));

        // Na transição não há pendência por atividade: a atividade da obra inteira ainda não é da função.
        // Agrupado por TIPO (17/11/2026): uma linha por atividade repetia a mesma pendência 5, 10 vezes e
        // afogava o pop-up. Cada grupo leva os ids das atividades para o front resolver em lote.
        var atividades = e.SemNenhumaDecisao ? new List<AtividadeAvaliada>() : e.Atividades;

        var semAvaliacao = atividades.Where(a => a.Riscos.Any(r => !r.Avaliado)).ToList();
        if (semAvaliacao.Count > 0)
            p.Add(new("RiscoSemAvaliacao",
                $"{semAvaliacao.Sum(a => a.Riscos.Count(r => !r.Avaliado))} risco(s) sem avaliação em {Atividades(semAvaliacao)}",
                "Sem avaliação não dá para informar o nível do risco nem as medidas obrigatórias.", "SST", "pgr", pgr, Ids(semAvaliacao)));

        bool NaoSeparada(RiscoAvaliado r) => r.MedidasValidadas == 0 && r.MedidasPendentes == 0;
        var naoSeparadas = atividades.Where(a => a.Riscos.Any(NaoSeparada)).ToList();
        if (naoSeparadas.Count > 0)
            p.Add(new("MedidasNaoSeparadas",
                $"{naoSeparadas.Sum(a => a.Riscos.Count(NaoSeparada))} risco(s) com medidas não separadas por fase em {Atividades(naoSeparadas)}",
                "A OS informa as medidas antes, durante e após o trabalho para cada risco. Use \"Separar medidas com IA\" no Inventário de riscos.",
                "SST", "pgr", pgr, Ids(naoSeparadas)));

        int Pendentes(AtividadeAvaliada a) => a.Riscos.Sum(r => r.MedidasPendentes) + a.ItensPendentes;
        var comSugestao = atividades.Where(a => Pendentes(a) > 0).ToList();
        if (comSugestao.Count > 0)
            p.Add(new("SugestoesPendentes",
                $"{comSugestao.Sum(Pendentes)} sugestão(ões) da IA aguardando validação em {Atividades(comSugestao)}",
                "Sugestão não é controle implantado. Só entra na OS depois que a SST aceitar, editar ou descartar.", "SST", "pgr", pgr, Ids(comSugestao)));

        var semProcedimento = atividades.Where(a => a.Procedimentos == 0).ToList();
        if (semProcedimento.Count > 0)
            p.Add(new("SemProcedimento",
                $"{semProcedimento.Count} atividade(s) sem procedimento vinculado: {Nomes(semProcedimento)}",
                "A OS precisa indicar o procedimento a seguir em cada atividade.", "SST", "pgr", pgr, Ids(semProcedimento)));

        foreach (var t in e.Treinamentos.Where(t => t.Situacao != "Valido"))
            p.Add(new("TreinamentoPendente",
                t.Situacao == "Vencido" ? $"Treinamento vencido: {t.Curso}" : $"Treinamento não realizado: {t.Curso}",
                "Treinamento exigido pela função precisa estar válido para a OS ser emitida.", "RH", "treinamentos", null));

        if (e.Aso is null)
            p.Add(new("SemAso", "Trabalhador sem ASO", "A OS só é emitida para trabalhador apto no ASO dentro da validade.", "RH", "aso", null));
        else if (!e.Aso.Valido)
            p.Add(new("AsoInvalido", "ASO vencido ou sem aptidão",
                "A OS só é emitida para trabalhador apto no ASO dentro da validade.", "RH", "aso", null));

        return p;
    }

    private const int NomesNoTitulo = 4;

    private static List<Guid> Ids(List<AtividadeAvaliada> l) => l.Select(a => a.Id).ToList();

    // "Corte, Armação, Concretagem, Desforma e mais 3": o título continua legível com muitas atividades.
    private static string Nomes(List<AtividadeAvaliada> l)
    {
        var nomes = string.Join(", ", l.Take(NomesNoTitulo).Select(a => a.Nome));
        return l.Count > NomesNoTitulo ? $"{nomes} e mais {l.Count - NomesNoTitulo}" : nomes;
    }

    private static string Atividades(List<AtividadeAvaliada> l) =>
        l.Count == 1 ? l[0].Nome : $"{l.Count} atividades: {Nomes(l)}";
}
