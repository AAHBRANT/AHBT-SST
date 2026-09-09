namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração para calcular o hash determinístico de um CPF (usado para localizar um Trabalhador
// existente por CPF, ex.: SincronizarColaboradorGrhCommand) sem a Application depender diretamente
// de Infrastructure/CpfCriptografiaConversor (regra de dependência do Clean Architecture). A chave
// de hash em si é segredo de Infrastructure — a Application só conhece o resultado.
public interface ICpfHashService
{
    string CalcularHash(string cpfPlano);
}
