using System.Runtime.CompilerServices;
using AAHBRANT.SST.Application.IntegracoesAtendimento;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

// Os testes abrem etapas "no passado" (antes de 16/11/2026, quando a carga automática entrou). Para eles a regra vale desde sempre;
// os testes da data de corte passam o início da regra explicitamente a GarantirAsync.
internal static class RegraCargaAutomaticaInicializacao
{
    [ModuleInitializer]
    internal static void Init() => PeriodosAutomaticosEtapa.InicioDaRegraVigenteUtc = DateTime.MinValue;
}
