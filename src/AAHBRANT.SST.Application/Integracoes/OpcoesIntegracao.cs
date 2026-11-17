namespace AAHBRANT.SST.Application.Integracoes;

// Seção "Integracao" do appsettings.
public class OpcoesIntegracao
{
    public const string Secao = "Integracao";

    // Decisão final do QSMS: na troca de função ou transferência, quem já tem NR-06, NR-18 e NR-35 válidas
    // (e nenhuma ficha aberta, e não é terceirizado) não ganha ficha de entrada nova; o kit de EPI e os cursos
    // fora do pacote da função nova continuam sendo tratados. Admissão sempre abre ficha.
    public bool DispensarFichaComPacoteValido { get; set; } = true;

    // Liga quando o fluxo "Realizar integração" (lote) estiver em produção: a ficha deixa de agendar as turmas
    // do pacote (NR-06, NR-18, NR-35, NR-12 e Integração de Segurança). Desligada (padrão), a ficha agenda
    // tudo como sempre agendou.
    public bool PacotePeloFluxoRealizarIntegracao { get; set; } = false;

    // Liga o atendimento "Realizar integração" em etapas (NR-06, NR-12, NR-18, NR-35 e OS): a ficha de entrada deixa de
    // agendar o combo (NR-06, NR-12, NR-18, NR-35 e Integração de Segurança), o pop-up troca "agendado" por "Iniciar/Continuar
    // integração" e a API api/integracoes-atendimento passa a aceitar atendimentos. Desligada (padrão), tudo como antes.
    public bool AtendimentoHabilitado { get; set; } = false;

    // Fotos de evidência automáticas da etapa (16/11/2026): a tela tira a 1ª foto aos 60 s da abertura, a 2ª 5 min depois e a
    // 3ª mais 5 min depois. Configurável (appsettings Integracao:FotosAutomaticas) sem mexer no código.
    public FotosAutomaticasIntegracaoOpcoes FotosAutomaticas { get; set; } = new();
}

public class FotosAutomaticasIntegracaoOpcoes
{
    public int PrimeiraAposSegundos { get; set; } = 60;
    public int SegundaAposSegundos { get; set; } = 300;
    public int TerceiraAposSegundos { get; set; } = 300;
}
