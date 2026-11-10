using AAHBRANT.SST.Application.Assinatura;

namespace AAHBRANT.SST.Application.Cameras;

// Liga cada rosto reconhecido à pessoa que a IA viu na cena, pela posição da cabeça. Na dúvida, não liga:
// notificar o trabalhador errado é pior do que deixar a ocorrência sem nome.
public static class CruzamentoRostosPessoas
{
    // A posição da cabeça informada pela IA é aproximada. O rosto vale para a pessoa quando a cabeça cai
    // dentro do retângulo do rosto ampliado por este fator, com um mínimo absoluto para rostos pequenos.
    public const double FatorTolerancia = 1.5;
    public const double ToleranciaMinima = 0.08;

    // Segunda pessoa mais próxima do rosto precisa estar pelo menos este tanto mais longe que a primeira.
    public const double FolgaSobreSegundaPessoa = 1.5;

    // Devolve, para cada índice de pessoa em `pessoas`, o rosto reconhecido ligado a ela (ou nada).
    public static IReadOnlyDictionary<int, RostoNaCena> Cruzar(IReadOnlyList<PessoaNaCena> pessoas, IReadOnlyList<RostoNaCena> rostos)
    {
        var candidatos = new List<(int Pessoa, RostoNaCena Rosto, double Distancia)>();

        foreach (var rosto in rostos.Where(r => r.TrabalhadorId is not null))
        {
            var toleranciaX = Math.Max(rosto.Largura * FatorTolerancia, ToleranciaMinima);
            var toleranciaY = Math.Max(rosto.Altura * FatorTolerancia, ToleranciaMinima);

            var proximas = pessoas
                .Select((p, indice) => (Indice: indice, Dx: Math.Abs(p.CabecaX - rosto.CentroX), Dy: Math.Abs(p.CabecaY - rosto.CentroY)))
                .Where(p => p.Dx <= toleranciaX && p.Dy <= toleranciaY)
                .Select(p => (p.Indice, Distancia: Math.Sqrt(p.Dx * p.Dx + p.Dy * p.Dy)))
                .OrderBy(p => p.Distancia)
                .ToList();

            if (proximas.Count == 0) continue;
            // Duas pessoas quase à mesma distância do rosto (lado a lado, uma atrás da outra): não dá para saber.
            if (proximas.Count > 1 && proximas[1].Distancia < proximas[0].Distancia * FolgaSobreSegundaPessoa + 1e-9) continue;

            candidatos.Add((proximas[0].Indice, rosto, proximas[0].Distancia));
        }

        // Dois rostos apontando para a mesma pessoa: também é dúvida, ninguém é ligado a ela.
        return candidatos
            .GroupBy(c => c.Pessoa)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Rosto);
    }
}
