using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Pendência estruturada de uma etapa: código estável (a tela torna a linha clicável) e o texto exibido.
public sealed record PendenciaEtapa(string Codigo, string Texto);

// O instrutor da etapa: já assinou (biometria guardada) e que biometria tem cadastrada.
public sealed record SituacaoInstrutorEtapa(bool Assinou, bool TemDigital, bool TemFacial)
{
    public bool TemBiometria => TemDigital || TemFacial;
}

// O que falta para uma etapa de curso poder avançar (decisões de 11/11 e 16/11/2026): todos com presença biométrica
// confirmada (a presença é a assinatura do certificado), instrutor com assinatura biométrica guardada, carga lançada >=
// mínimo (0 não trava) e, quando o período é automático, o horário de fim já ter chegado; 3 fotos próprias e instrutor.
public static class AvaliadorEtapaIntegracao
{
    public const int FotosNecessarias = 3;

    public static class Codigos
    {
        public const string InstrutorNaoDefinido = "instrutor_nao_definido";
        public const string SemParticipantes = "sem_participantes";
        public const string Carga = "carga";
        public const string CargaAguardandoFim = "carga_aguardando_fim";
        public const string Fotos = "fotos";
        public const string AssinaturasColaboradores = "assinaturas_colaboradores";
        public const string InstrutorAssinatura = "instrutor_assinatura";
        public const string InstrutorSemBiometria = "instrutor_sem_biometria";
    }

    public const string TextoInstrutorPendente = "Assinatura biométrica do instrutor pendente.";
    public const string TextoInstrutorSemBiometria = "Instrutor sem digital nem facial cadastrada: cadastre no perfil dele.";

    // agoraBrasilia só é informado em teste (horário de Brasília); nulo = agora.
    public static List<PendenciaEtapa> Pendencias(
        IntegracaoEtapa etapa,
        SessaoTreinamento sessao,
        IReadOnlyCollection<ParticipanteSessaoTreinamento> participantes,
        IReadOnlyDictionary<Guid, string> nomes,
        IReadOnlyCollection<PeriodoEtapaIntegracao> periodos,
        int fotos,
        SituacaoInstrutorEtapa instrutor,
        DateTime? agoraBrasilia = null)
    {
        var pendencias = new List<PendenciaEtapa>();
        if (participantes.Count == 0)
            pendencias.Add(new(Codigos.SemParticipantes, "A etapa não tem participantes."));

        if (sessao.InstrutorTrabalhadorId is null)
            pendencias.Add(new(Codigos.InstrutorNaoDefinido, "Instrutor da turma não definido."));

        var minutosLancados = periodos.Sum(p => p.Minutos);
        if (minutosLancados <= 0)
            pendencias.Add(new(Codigos.Carga, "Carga horária: lance ao menos um período (o certificado leva a carga real da etapa)."));
        else if (etapa.CargaMinimaHoras > 0 && minutosLancados < etapa.CargaMinimaHoras * 60)
            pendencias.Add(new(Codigos.Carga, $"Carga horária lançada ({Formatar(minutosLancados)}) abaixo do mínimo de {etapa.CargaMinimaHoras} h."));

        var aguardando = AguardandoFim(periodos, agoraBrasilia ?? AAHBRANT.SST.Application.PlanosIntegracao.AgendaTreinamentosIntegracao.AgoraBrasilia());
        if (aguardando is not null)
            pendencias.Add(new(Codigos.CargaAguardandoFim, aguardando));

        if (fotos < FotosNecessarias)
            pendencias.Add(new(Codigos.Fotos, $"Faltam {FotosNecessarias - fotos} foto(s) de evidência (são {FotosNecessarias}, tiradas pela câmera nesta etapa)."));

        var semPresenca = participantes.Where(p => p.PresencaConfirmadaEm is null)
            .Select(p => nomes.GetValueOrDefault(p.TrabalhadorId, "Participante")).OrderBy(n => n).ToList();
        if (semPresenca.Count > 0)
        {
            var lista = semPresenca.Count <= 5 ? string.Join(", ", semPresenca) : $"{string.Join(", ", semPresenca.Take(5))} e mais {semPresenca.Count - 5}";
            pendencias.Add(new(Codigos.AssinaturasColaboradores, $"Assinatura biométrica pendente ({semPresenca.Count}): {lista}."));
        }

        if (sessao.InstrutorTrabalhadorId is not null && !instrutor.Assinou)
        {
            pendencias.Add(new(Codigos.InstrutorAssinatura, TextoInstrutorPendente));
            if (!instrutor.TemBiometria)
                pendencias.Add(new(Codigos.InstrutorSemBiometria, TextoInstrutorSemBiometria));
        }
        return pendencias;
    }

    public static List<string> Faltas(
        IntegracaoEtapa etapa, SessaoTreinamento sessao, IReadOnlyCollection<ParticipanteSessaoTreinamento> participantes,
        IReadOnlyDictionary<Guid, string> nomes, IReadOnlyCollection<PeriodoEtapaIntegracao> periodos, int fotos,
        SituacaoInstrutorEtapa instrutor, DateTime? agoraBrasilia = null) =>
        Pendencias(etapa, sessao, participantes, nomes, periodos, fotos, instrutor, agoraBrasilia).Select(p => p.Texto).ToList();

    // Período automático ainda em curso: "Carga horária fecha às HH:mm — faltam X h YY min" (horário de Brasília).
    // Vale o fim do ÚLTIMO período automático; depois dele, vale a regra normal do mínimo.
    public static string? AguardandoFim(IReadOnlyCollection<PeriodoEtapaIntegracao> periodos, DateTime agoraBrasilia)
    {
        var automaticos = periodos.Where(p => p.Automatico).ToList();
        if (automaticos.Count == 0) return null;
        var fim = automaticos.Max(p => PeriodosAutomaticosEtapa.FimReal(p.Data, p.Inicio, p.Minutos));
        if (fim <= agoraBrasilia) return null;

        var faltam = (int)Math.Ceiling((fim - agoraBrasilia).TotalMinutes);
        var hora = fim.ToString("HH:mm");
        var quando = fim.Date == agoraBrasilia.Date ? $"às {hora}" : $"às {hora} de {fim:dd/MM}";
        return $"Carga horária fecha {quando} — faltam {Restante(faltam)}.";
    }

    private static string Restante(int minutos) =>
        minutos >= 60 ? $"{minutos / 60} h {minutos % 60:00} min" : $"{minutos} min";

    public static string Formatar(int minutos) =>
        minutos <= 0 ? "0 h" : minutos % 60 == 0 ? $"{minutos / 60} h" : $"{minutos / 60} h {minutos % 60} min";
}
