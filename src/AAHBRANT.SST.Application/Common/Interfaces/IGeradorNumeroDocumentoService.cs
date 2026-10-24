namespace AAHBRANT.SST.Application.Common.Interfaces;

// Numeração automática de documentos internos (pedido do usuário, 03/09) — ver ContadorDocumento.cs
// para o porquê de nunca usar isto em números que vêm de fora do sistema (CAT, CA de EPI,
// certificado de treinamento). Formato: "{prefixo}-{ano}-0001", sequencial reiniciando a cada ano.
public interface IGeradorNumeroDocumentoService
{
    // Incrementa (ou cria) o contador do prefixo/ano corrente na mesma unidade de trabalho do
    // chamador e devolve o número já formatado — não chama SaveChangesAsync sozinho, para que o
    // incremento do contador seja persistido na MESMA transação do documento sendo criado (ver
    // CriarAprCommand.cs etc.): se a criação do documento falhar depois, o número não é "gasto".
    Task<string> GerarAsync(string prefixo, CancellationToken ct);
}
