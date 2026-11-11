namespace AAHBRANT.SST.Application.Integracoes;

// Seção "Integracao" do appsettings.
public class OpcoesIntegracao
{
    public const string Secao = "Integracao";

    // Decisão final do QSMS: na troca de função ou transferência, quem já tem NR-06, NR-18 e NR-35 válidas
    // não ganha ficha de entrada nova (o kit de EPI continua sendo reajustado). Admissão sempre abre ficha.
    public bool DispensarFichaComPacoteValido { get; set; } = true;
}
