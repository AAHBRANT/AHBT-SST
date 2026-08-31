namespace AAHBRANT.SST.Application.Cipa;

public record AtaEleicaoCipaCandidatoModelo(string Nome, string Matricula, int Votos, string StatusLabel);

public record AtaEleicaoCipaPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string? NumeroDocumento,
    DateTime DataConvocacao,
    DateTime DataVotacao,
    DateTime? DataApuracao,
    IReadOnlyList<AtaEleicaoCipaCandidatoModelo> Candidatos);

public record AtaReuniaoCipaParticipanteModelo(string Nome, bool Presente);

public record AtaReuniaoCipaPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string TipoLabel,
    DateTime DataReuniao,
    string? Pauta,
    string? Deliberacoes,
    IReadOnlyList<AtaReuniaoCipaParticipanteModelo> Participantes);

public interface ICipaPdfService
{
    byte[] GerarAtaEleicao(AtaEleicaoCipaPdfModelo modelo);
    byte[] GerarAtaReuniao(AtaReuniaoCipaPdfModelo modelo);
}
