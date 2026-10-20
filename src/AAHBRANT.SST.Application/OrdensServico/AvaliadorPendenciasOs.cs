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
        AsoOsDto? Aso);

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

        if (e.FuncaoTemGhe && e.Atividades.Count == 0)
            p.Add(new("SemAtividades", "O GHE da função não tem atividades com riscos no inventário",
                "Sem o inventário não há riscos nem medidas para informar na OS.", "SST", "pgr", pgr));

        foreach (var a in e.Atividades)
        {
            // Agrupado por atividade: um PGR tem dezenas de riscos, e uma linha por risco afogava o pop-up.
            var semAvaliacao = a.Riscos.Where(r => !r.Avaliado).Select(r => r.Perigo).ToList();
            if (semAvaliacao.Count > 0)
                p.Add(new("RiscoSemAvaliacao", $"{semAvaliacao.Count} risco(s) sem avaliação em {a.Nome}: {string.Join(", ", semAvaliacao)}",
                    "Sem avaliação não dá para informar o nível do risco nem as medidas obrigatórias.", "SST", "pgr", pgr));

            var naoSeparadas = a.Riscos.Where(r => r.MedidasValidadas == 0 && r.MedidasPendentes == 0).Select(r => r.Perigo).ToList();
            if (naoSeparadas.Count > 0)
                p.Add(new("MedidasNaoSeparadas", $"{naoSeparadas.Count} risco(s) com medidas não separadas por fase em {a.Nome}: {string.Join(", ", naoSeparadas)}",
                    "A OS informa as medidas antes, durante e após o trabalho para cada risco. Use \"Separar medidas com IA\" no Inventário de riscos.", "SST", "pgr", pgr));

            var pendentes = a.Riscos.Sum(r => r.MedidasPendentes) + a.ItensPendentes;
            if (pendentes > 0)
                p.Add(new("SugestoesPendentes", $"{pendentes} sugestão(ões) da IA aguardando validação ({a.Nome})",
                    "Sugestão não é controle implantado. Só entra na OS depois que a SST aceitar, editar ou descartar.", "SST", "pgr", pgr));

            if (a.Procedimentos == 0)
                p.Add(new("SemProcedimento", $"Atividade sem procedimento vinculado: {a.Nome}",
                    "A OS precisa indicar o procedimento a seguir em cada atividade.", "SST", "pgr", pgr));
        }

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
}
