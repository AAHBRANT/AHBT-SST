namespace AAHBRANT.SST.Application.Common.Interfaces;

// E-mail institucional do SST (16/10/2026), enviado pela caixa compartilhada configurada em
// Graph:RemetenteEmail (sst@aahbrant.com). Toda mensagem leva a logo AAHBRANT anexada inline:
// o corpo HTML a referencia com <img src="cid:logo-aahbrant">, assim ela aparece mesmo quando o
// leitor bloqueia imagens externas.
public interface IEmailService
{
    public const string ContentIdLogo = "logo-aahbrant";

    // Lança exceção se o envio falhar ou se o remetente não estiver configurado — quem chama decide
    // se registra a falha e segue para o próximo destinatário.
    Task EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default);

    // Com arquivos anexos (ex.: escopo do PCMSO em PDF para o médico, 21/10).
    Task EnviarAsync(string para, string assunto, string corpoHtml, IReadOnlyList<AnexoEmail> anexos, CancellationToken ct = default);
}

public record AnexoEmail(string NomeArquivo, string ContentType, byte[] Conteudo);
