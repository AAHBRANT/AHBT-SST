using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Assinatura biométrica (digital ou facial) do instrutor numa turma de treinamento, colhida ANTES do encerramento
// (decisão do usuário, 16/11/2026): uma por turma, junto dos colaboradores. Guarda a mesma prova que o participante
// guarda na presença (imagem com SHA-256, IP, aparelho, localização e validação). No encerramento ela vira a assinatura
// do instrutor (papel Instrutor) em cada certificado; depois de copiada, a imagem é apagada daqui e o hash permanece.
// Tem ObraId próprio (filtro de escopo por obra) e não tem navegação obrigatória para o pai.
public class AssinaturaInstrutorSessaoTreinamento : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Guid SessaoTreinamentoId { get; set; }
    public Guid InstrutorTrabalhadorId { get; set; }
    // Biometria (digital) ou ReconhecimentoFacial; nunca SessaoLogada.
    public MetodoAutenticacaoAssinatura Metodo { get; set; }
    public DateTime AssinadoEmUtc { get; set; }
    public double? ScoreConfianca { get; set; }

    public byte[]? EvidenciaConteudo { get; set; }
    public string? EvidenciaContentType { get; set; }
    public string? EvidenciaHash { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public StatusLocalizacaoAssinatura LocalizacaoStatus { get; set; } = StatusLocalizacaoAssinatura.NaoInformada;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? PrecisaoMetros { get; set; }
    public Guid? DispositivoAgenteId { get; set; }
    public string? ValidacaoModelo { get; set; }
    public string? ValidacaoGrupoId { get; set; }
    public string? ValidacaoRequisicaoId { get; set; }
}
