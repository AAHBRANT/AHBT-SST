namespace AAHBRANT.SST.Application.Assinatura;

// Texto mostrado ao operador para cada motivo de rejeição do reconhecimento facial. Centraliza as
// mensagens (antes repetidas em cada comando) para os fluxos novos; os comandos antigos continuam
// com a cópia local até serem migrados.
public static class MensagemRejeicaoFacial
{
    public static string Para(MotivoRejeicaoFacial? motivo) => motivo switch
    {
        MotivoRejeicaoFacial.NenhumRostoDetectado => "Nenhum rosto detectado na foto.",
        MotivoRejeicaoFacial.MultiplosRostosDetectados => "Mais de uma pessoa detectada na câmera — aproxime-se sozinho.",
        MotivoRejeicaoFacial.ConfiancaBaixa => "Rosto reconhecido com baixa confiança — tente novamente com melhor iluminação.",
        MotivoRejeicaoFacial.RostoAmbiguo => "Rosto parecido com o de outra pessoa. Use a digital ou tente de novo de frente para a câmera.",
        _ => "Rosto não reconhecido.",
    };
}
