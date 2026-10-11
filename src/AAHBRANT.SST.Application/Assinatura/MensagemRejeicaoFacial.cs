namespace AAHBRANT.SST.Application.Assinatura;

// Texto mostrado ao operador para cada motivo de rejeição do reconhecimento facial. Centraliza as
// mensagens (antes repetidas em cada comando) para os fluxos novos; os comandos antigos continuam
// com a cópia local até serem migrados.
public static class MensagemRejeicaoFacial
{
    public static string Para(MotivoRejeicaoFacial? motivo) => motivo switch
    {
        // Cada recusa diz o que fazer a seguir, incluindo a saída pela digital (11/10): antes "Rosto não
        // reconhecido." deixava a pessoa tentando a mesma coisa várias vezes.
        MotivoRejeicaoFacial.NenhumRostoDetectado => "Nenhum rosto encontrado na foto. Olhe de frente para a câmera, sem boné ou óculos escuros, e tente de novo.",
        MotivoRejeicaoFacial.MultiplosRostosDetectados => "Mais de uma pessoa apareceu na foto. Deixe só quem vai assinar na frente da câmera.",
        MotivoRejeicaoFacial.ConfiancaBaixa => "Rosto reconhecido com baixa confiança. Tente de novo de frente para a luz, ou use a digital. Se acontecer sempre, peça para refazer o cadastro facial.",
        MotivoRejeicaoFacial.RostoAmbiguo => "Rosto parecido com o de outra pessoa. Use a digital ou tente de novo de frente para a câmera.",
        _ => "Rosto não reconhecido. Confira se a pessoa tem cadastro facial nesta obra, tente de novo de frente para a luz ou use a digital.",
    };
}
