namespace AAHBRANT.SST.Application.Assinatura;

// Motivos de rejeição distintos para mensagens específicas na UI (docs/superpowers/specs/2026-09-04-
// assinatura-facial-azure-design.md §3) — "nenhum rosto" e "confiança baixa" merecem textos
// diferentes de "múltiplos rostos".
public enum MotivoRejeicaoFacial
{
    NenhumRostoDetectado,
    MultiplosRostosDetectados,
    ConfiancaBaixa,
    RostoNaoReconhecido,
}

public record ResultadoIdentificacaoFacial(bool Aceito, ResultadoAutenticacaoAssinatura? Resultado, MotivoRejeicaoFacial? Motivo, double? Confianca);

public interface IAutenticacaoFacialService
{
    // Cadastra (ou atualiza) a face do trabalhador no Azure — cria o PersonGroup da obra se ainda não
    // existir, cria o Person se ainda não existir, adiciona a foto e dispara o treino, aguardando a
    // conclusão (síncrono — ação administrativa pontual, não precisa ser assíncrona).
    Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct);

    // Identifica quem está na foto dentro do PersonGroup da obra informada. Não recebe TrabalhadorId
    // — ao contrário do Futronic (que já resolveu o match localmente), aqui é o Azure quem descobre
    // quem é, a partir da foto.
    Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct);
}
