using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Assinatura;

// Abstração da estratégia biométrica (Fido2AutenticacaoStrategy, etapa 13 — docs/Motor-Assinatura-
// Eletronica.md §3). Diferente de IAutenticacaoAssinaturaService (síncrona, um passo), WebAuthn é uma
// cerimônia de desafio/resposta em duas chamadas — por isso ganha esta interface própria, como já
// previsto no comentário de IAutenticacaoAssinaturaService. As opções/respostas trafegam como JSON
// opaco (string) para a Application não precisar referenciar os tipos da biblioteca Fido2NetLib, que
// são detalhe da Infrastructure — o mesmo JSON é o que o navegador consome/produz via
// navigator.credentials.create()/get().
public interface IAutenticacaoWebAuthnService
{
    // Cadastro (enrollment): vincula uma nova credencial (dedo no leitor da obra, ou biometria/PIN do
    // celular) a um TrabalhadorId que já existe — nunca cria um trabalhador novo.
    Task<string> IniciarCadastroAsync(Guid trabalhadorId, TipoAutenticadorWebAuthn tipo, CancellationToken ct);
    Task ConfirmarCadastroAsync(Guid trabalhadorId, TipoAutenticadorWebAuthn tipo, string opcoesJson, string respostaJson, CancellationToken ct);

    // Autenticação (assinatura): trabalhadorId nulo = leitor da obra, que não sabe de antemão quem vai
    // encostar o dedo (credencial "discoverable" resolve a identidade só na resposta); preenchido =
    // celular próprio, que já sabe de quem é o aparelho antes de abrir o desafio.
    Task<string> IniciarAutenticacaoAsync(Guid? trabalhadorId, CancellationToken ct);
    Task<ResultadoAutenticacaoAssinatura> ConfirmarAutenticacaoAsync(string opcoesJson, string respostaJson, CancellationToken ct);
}
