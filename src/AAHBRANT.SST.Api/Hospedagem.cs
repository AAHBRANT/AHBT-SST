using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using AAHBRANT.SST.Api.Controllers;

namespace AAHBRANT.SST.Api;

// A mesma imagem da API pode rodar em mais de um Container App, cada um expondo só uma parte dos
// controllers (Banco de Ideias separado do app de SST, decisão do usuário em 08/10/2026):
//   Completo (padrão) — tudo, inclusive o Banco de Ideias (comportamento anterior, sem mudar nada);
//   Sst               — tudo, MENOS o Banco de Ideias e o webhook do Telegram de ideias;
//   Ideias            — SÓ o Banco de Ideias e o webhook do Telegram de ideias.
// Configurado por Hospedagem:Modo (variável Hospedagem__Modo). No modo Ideias a API não aplica
// migrations nem seeders: isso continua sendo feito pela API de SST.
public static class Hospedagem
{
    public const string Completo = "Completo";
    public const string Sst = "Sst";
    public const string Ideias = "Ideias";

    private static readonly Type[] ControllersDeIdeias = [typeof(IdeiasController), typeof(TelegramIdeiasController)];

    public static string ObterModo(IConfiguration configuracao)
    {
        var modo = configuracao["Hospedagem:Modo"];
        if (string.IsNullOrWhiteSpace(modo)) return Completo;
        return modo.Trim() switch
        {
            var m when m.Equals(Sst, StringComparison.OrdinalIgnoreCase) => Sst,
            var m when m.Equals(Ideias, StringComparison.OrdinalIgnoreCase) => Ideias,
            var m when m.Equals(Completo, StringComparison.OrdinalIgnoreCase) => Completo,
            _ => throw new InvalidOperationException(
                $"Hospedagem:Modo inválido: '{modo}'. Use Completo, Sst ou Ideias.")
        };
    }

    public static bool AplicaMigrationsESeeders(string modo) => modo != Ideias;

    public sealed class FiltroControllersPorModo : IApplicationFeatureProvider<ControllerFeature>
    {
        private readonly string _modo;

        public FiltroControllersPorModo(string modo) => _modo = modo;

        public void PopulateFeature(IEnumerable<ApplicationPart> partes, ControllerFeature feature)
        {
            if (_modo == Completo) return;

            for (var i = feature.Controllers.Count - 1; i >= 0; i--)
            {
                var ehIdeias = ControllersDeIdeias.Contains(feature.Controllers[i].AsType());
                if ((_modo == Ideias && !ehIdeias) || (_modo == Sst && ehIdeias))
                    feature.Controllers.RemoveAt(i);
            }
        }
    }
}
