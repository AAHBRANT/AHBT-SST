using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Suporte à sincronização offline (app de campo sem sinal): quando a fila local reenvia uma
// mutação (POST/PUT) que já tinha sido aplicada — porque a resposta original se perdeu por queda
// de conexão antes de chegar ao dispositivo —, este registro evita duplicar o efeito (ex.: dois
// registros de presença de DDS para o mesmo evento). A chave é gerada pelo cliente por tentativa
// de mutação e enviada no header "Idempotency-Key" (ver IdempotenciaMiddleware).
public class IdempotenciaRegistro : AuditableEntity
{
    public string Chave { get; set; } = string.Empty;
    public string Rota { get; set; } = string.Empty;
    public int StatusCodeResposta { get; set; }
    public string CorpoResposta { get; set; } = string.Empty;
}
