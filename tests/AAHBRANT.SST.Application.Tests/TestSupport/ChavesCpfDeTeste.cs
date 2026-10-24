using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Application.Tests.TestSupport;

// Chave única de CPF para TODA a suíte — e a razão é uma fonte real de teste instável.
//
// CpfCriptografiaContexto é estático global: vale para o processo inteiro, não por classe de teste.
// Sete classes chamavam Configurar() com RandomNumberGenerator.GetBytes(32), cada uma com chaves
// diferentes, e o xUnit executa classes em paralelo. Quem gravava um Trabalhador com a chave da sua
// classe e lia o CPF depois de outra classe ter trocado a chave recebia hash divergente — falha que
// mudava de teste a cada execução conforme a ordem, e sempre passava quando o teste rodava sozinho.
// Aconteceu duas vezes em 23-24/09 (SincronizarAsoGrhCommandHandlerTests e
// ListarFuncoesSemTrabalhadorQueryHandlerTests) antes de a causa ficar clara.
//
// Valores fixos e óbvios de teste: nunca usar fora deste projeto.
public static class ChavesCpfDeTeste
{
    private static readonly byte[] ChaveCriptografia = Enumerable.Repeat((byte)1, 32).ToArray();
    private static readonly byte[] ChaveHash = Enumerable.Repeat((byte)2, 32).ToArray();

    public static void Configurar() => CpfCriptografiaContexto.Configurar(ChaveCriptografia, ChaveHash);
}
