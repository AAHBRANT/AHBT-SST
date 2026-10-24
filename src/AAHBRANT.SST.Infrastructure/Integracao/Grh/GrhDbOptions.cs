namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Leitura direta do banco do G-RH (Integração G-RH, 2026-09-16) — alternativa ao padrão de endpoint
// HTTP (GrhOptions/ColaboradorGrhClient) usada enquanto o time do G-RH não tinha capacidade de
// construir os endpoints de Alojamento/ASO. ConnectionString fica vazia até ser provisionada
// (usuário só-leitura recomendado, nunca o de administração do banco); com ela vazia,
// AlojamentoGrhDbClient/AsoGrhDbClient lançam exceção graciosamente, e GrhDbPollingService não é
// registrado (ver DependencyInjection.cs).
public class GrhDbOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int IntervaloPollingMinutos { get; set; } = 5;
}
