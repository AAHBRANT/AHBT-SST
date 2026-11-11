using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// O que falta para uma etapa de curso poder avançar (decisão de 11/11/2026): todos com presença biométrica confirmada
// (a presença é a assinatura do certificado), carga lançada >= mínimo (0 não trava), 3 fotos próprias e instrutor.
public static class AvaliadorEtapaIntegracao
{
    public const int FotosNecessarias = 3;

    public static List<string> Faltas(
        IntegracaoEtapa etapa,
        SessaoTreinamento sessao,
        IReadOnlyCollection<ParticipanteSessaoTreinamento> participantes,
        IReadOnlyDictionary<Guid, string> nomes,
        int minutosLancados,
        int fotos)
    {
        var faltas = new List<string>();
        if (participantes.Count == 0)
            faltas.Add("A etapa não tem participantes.");

        if (sessao.InstrutorTrabalhadorId is null)
            faltas.Add("Instrutor da turma não definido.");

        if (minutosLancados <= 0)
            faltas.Add("Carga horária: lance ao menos um período (o certificado leva a carga real da etapa).");
        else if (etapa.CargaMinimaHoras > 0 && minutosLancados < etapa.CargaMinimaHoras * 60)
            faltas.Add($"Carga horária lançada ({Formatar(minutosLancados)}) abaixo do mínimo de {etapa.CargaMinimaHoras} h.");

        if (fotos < FotosNecessarias)
            faltas.Add($"Faltam {FotosNecessarias - fotos} foto(s) de evidência (são {FotosNecessarias}, tiradas pela câmera nesta etapa).");

        var semPresenca = participantes.Where(p => p.PresencaConfirmadaEm is null)
            .Select(p => nomes.GetValueOrDefault(p.TrabalhadorId, "Participante")).OrderBy(n => n).ToList();
        if (semPresenca.Count > 0)
        {
            var lista = semPresenca.Count <= 5 ? string.Join(", ", semPresenca) : $"{string.Join(", ", semPresenca.Take(5))} e mais {semPresenca.Count - 5}";
            faltas.Add($"Assinatura biométrica pendente ({semPresenca.Count}): {lista}.");
        }
        return faltas;
    }

    public static string Formatar(int minutos) =>
        minutos <= 0 ? "0 h" : minutos % 60 == 0 ? $"{minutos / 60} h" : $"{minutos / 60} h {minutos % 60} min";
}
