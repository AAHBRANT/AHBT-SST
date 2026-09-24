namespace AAHBRANT.SST.Infrastructure.Documentos;

// Horário do canteiro para carimbar em documento emitido.
//
// Nenhum Dockerfile define TZ, então o container Linux roda em UTC e `DateTime.Now` dentro da
// aplicação devolve UTC — três horas à frente do relógio de quem assina o papel. Todo documento
// oficial saía com "Emitido em" adiantado: APR, Ata de treinamento, Certificado, CIPA, DDS, DDS
// semanal, Ficha de EPI, Inspeção e PT, todos pelo RodapeDocumentoPadrao (achado na varredura de
// 23/09/2026). Em desenvolvimento no Windows o defeito não aparece, porque lá `Now` já é Brasília
// — motivo de ter passado despercebido.
//
// A conversão existia desde antes, mas privada dentro de EntregaEpiPdfService e aplicada só aos
// horários de assinatura daquela ficha; aqui ela vira o ponto único para todos os documentos.
public static class HorarioBrasilia
{
    private static readonly TimeZoneInfo Fuso = ResolverFuso();

    // Agora, no fuso de Brasília, independente do fuso do servidor.
    public static DateTime Agora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso);

    // Converte um instante gravado em UTC (padrão de todo o banco: CreatedAtUtc, AssinadoEm etc.).
    public static DateTime De(DateTime dataHoraUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dataHoraUtc, DateTimeKind.Utc), Fuso);

    // O id do fuso muda de nome entre Linux (IANA) e Windows; tenta os dois para o mesmo código
    // rodar em produção e na máquina de desenvolvimento.
    private static TimeZoneInfo ResolverFuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
