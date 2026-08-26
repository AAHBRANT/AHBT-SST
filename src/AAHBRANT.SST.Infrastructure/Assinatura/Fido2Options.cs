namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Configuração do relying party WebAuthn/FIDO2 (etapa 13 do Motor de Assinatura Eletrônica) — mesmo
// padrão "vazio até o recurso existir" de GraphOptions/TelegramOptions: ficam vazias até o domínio de
// produção (e, no caso do leitor de obra, o hardware) serem confirmados; Fido2AutenticacaoStrategy
// lança exceção graciosamente ao ser efetivamente usada com config vazia, sem quebrar o DI/build.
public class Fido2Options
{
    // Domínio (sem esquema/porta) onde o app roda — vira o "rpId" do WebAuthn. Precisa bater com o
    // domínio do navegador em tempo de cerimônia (ex.: "sst.aahbrant.com"), senão o navegador recusa.
    public string ServerDomain { get; set; } = string.Empty;
    public string ServerName { get; set; } = "AAHBRANT SST";
    public string[] Origins { get; set; } = Array.Empty<string>();
}
