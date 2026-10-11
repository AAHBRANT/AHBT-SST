namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class AssinaturaOptions
{
    public string UrlBaseValidacaoPublica { get; set; } = "";
    public double LimiarConfiancaBiometriaLocal { get; set; } = 50;

    // Desligado durante a transição: leitores cujo agente ainda não foi atualizado continuam pelo caminho
    // antigo (segredo + nota). Ligar (true) depois que todos os agentes das obras estiverem atualizados —
    // aí qualquer leitura sem assinatura do agente é recusada.
    public bool ExigirLeituraAssinadaBiometria { get; set; }

    // Azure Face API (docs/superpowers/specs/2026-09-04-assinatura-facial-azure-design.md) — tier F0
    // (gratuito): 20 chamadas/minuto, até 30.000 rostos. Migrar para S0 é só trocar a chave.
    public string AzureFaceApiEndpoint { get; set; } = "";
    public string AzureFaceApiKey { get; set; } = "";
    public double LimiarConfiancaFacial { get; set; } = 0.85;
    public double LimiarConfiancaFacialMinimo { get; set; } = 0.60;

    // Fila facial (identificação 1:N sem escolher a pessoa): o melhor candidato precisa estar pelo menos
    // esta diferença (0 a 1) à frente do segundo, senão a leitura é recusada como ambígua e o
    // operador usa a digital. Evita confirmar o colega parecido.
    public double MargemMinimaIdentificacaoFacial { get; set; } = 0.10;

    // Migração dos grupos para o recognition_04 (MigracaoModeloFacialBackgroundService). Roda só nesta
    // janela de horário (Brasília), quando ninguém está assinando, porque usa a mesma cota do Azure.
    public bool MigrarModeloFacialAutomaticamente { get; set; } = true;
    public int MigracaoFacialHoraInicio { get; set; } = 22;
    public int MigracaoFacialHoraFim { get; set; } = 5;
    // Pausa entre um trabalhador e outro (2 chamadas cada). 6,5 s mantém a migração abaixo de 20/min (F0).
    public int IntervaloMigracaoFacialMs { get; set; } = 6500;
}
