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
    // Fila 1:N: o segundo colocado ficou perto demais do primeiro para confirmar com segurança.
    RostoAmbiguo,
}

public record ResultadoIdentificacaoFacial(bool Aceito, ResultadoAutenticacaoAssinatura? Resultado, MotivoRejeicaoFacial? Motivo, double? Confianca,
    // Quem o Azure apontou, quando a confiança permite atribuir a falha a alguém (cadastro fraco).
    Guid? TrabalhadorIdProvavel = null);

public interface IAutenticacaoFacialService
{
    // Cadastra (ou atualiza) a face do trabalhador no Azure — cria o PersonGroup da obra se ainda não
    // existir, cria o Person se ainda não existir, adiciona a foto e dispara o treino, aguardando a
    // conclusão (síncrono — ação administrativa pontual, não precisa ser assíncrona).
    Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct);

    // Apaga a pessoa (e todas as faces dela) do PersonGroup da obra no Azure e retreina o grupo. Usado
    // ao refazer o cadastro facial. Idempotente: se a pessoa já não existe no Azure, não falha — assim
    // dá para repetir a operação depois de uma falha parcial. Não mexe no banco; quem chama limpa
    // AzureFacePersonId e as fotos de cadastro.
    Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct);

    // Identifica quem está na foto dentro do PersonGroup da obra informada. Não recebe TrabalhadorId
    // — ao contrário do Futronic (que já resolveu o match localmente), aqui é o Azure quem descobre
    // quem é, a partir da foto.
    //
    // exigirMargemSobreSegundoColocado: usar na fila (ninguém escolhe a pessoa). Recusa quando o
    // segundo candidato está perto demais do primeiro (ver AssinaturaOptions.MargemMinima...).
    // Toda falha (confiança baixa, não reconhecido, ambíguo) é registrada para a lista de cadastros fracos.
    Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = false);
}
