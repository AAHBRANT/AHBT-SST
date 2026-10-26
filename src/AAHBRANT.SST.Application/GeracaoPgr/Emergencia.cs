using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Plano de emergência do PGR com sugestão por mapa e IA (24/10/2026). O mapa (OpenStreetMap) devolve os
// locais reais e as rotas a partir do endereço da obra; a IA só escolhe o mais adequado aos riscos e explica
// o caminho. Nome, endereço e telefone vêm sempre do mapa, nunca da IA, e nada vira ficha sem o técnico aplicar.

public record PontoGeografico(double Latitude, double Longitude);

public record LocalGeocodificado(PontoGeografico Ponto, string EnderecoEncontrado, bool Aproximado);

public enum TipoLocalEmergencia { Hospital, Bombeiros, Policia }

public record LocalEmergenciaMapa(
    string Id,
    TipoLocalEmergencia Tipo,
    string Nome,
    string? Endereco,
    string? Telefone,
    bool AtendeUrgencia,
    PontoGeografico Ponto,
    double DistanciaRetaKm);

public record RotaMapa(double DistanciaKm, int TempoMinutos, IReadOnlyList<string> Passos);

public interface IProvedorMapaEmergencia
{
    string Fonte { get; }

    Task<LocalGeocodificado?> LocalizarAsync(string consulta, CancellationToken ct);

    Task<List<LocalEmergenciaMapa>> BuscarProximosAsync(PontoGeografico centro, int raioMetros, CancellationToken ct);

    Task<RotaMapa?> RotaAsync(PontoGeografico origem, PontoGeografico destino, CancellationToken ct);
}

// Candidato enviado à IA: só o que ela precisa para escolher e explicar. Nada de telefone nem coordenadas.
public record CandidatoEmergenciaIa(
    string Id,
    string Tipo,
    string Nome,
    string? Endereco,
    bool AtendeUrgencia,
    double DistanciaKm,
    int TempoMinutos,
    IReadOnlyList<string> Passos);

public record ContextoEmergenciaIa(
    string Obra,
    string Localizacao,
    IReadOnlyList<string> Riscos,
    IReadOnlyList<CandidatoEmergenciaIa> Candidatos,
    IReadOnlyList<string> CenariosPossiveis);

public record RecomendacaoEmergenciaIa(string Id, string? Justificativa);

public record SugestaoEmergenciaIa(IReadOnlyList<RecomendacaoEmergenciaIa> Recomendados, IReadOnlyList<string> Cenarios);

public class OpcaoEmergenciaDto
{
    public string Id { get; set; } = string.Empty;
    // "hospital", "bombeiros" ou "pm": a mesma chave das linhas da ficha.
    public string Chave { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public string? Telefone { get; set; }
    public bool AtendeUrgencia { get; set; }
    public double DistanciaKm { get; set; }
    public int TempoMinutos { get; set; }
    public string Rota { get; set; } = string.Empty;
    public bool Recomendado { get; set; }
    public string? Justificativa { get; set; }
    public string LinkMapa { get; set; } = string.Empty;
}

public class SugestaoEmergenciaDto
{
    public string Fonte { get; set; } = string.Empty;
    public DateTime CalculadoEmUtc { get; set; }
    public string? EnderecoEncontrado { get; set; }
    public bool LocalizacaoAproximada { get; set; }
    public bool UsouIa { get; set; }
    public List<OpcaoEmergenciaDto> Opcoes { get; set; } = new();
    public List<string> CenariosSugeridos { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

public record SugerirEmergenciaGeracaoPgrCommand(Guid Id) : IRequest<SugestaoEmergenciaDto>;

public class SugerirEmergenciaGeracaoPgrCommandHandler : IRequestHandler<SugerirEmergenciaGeracaoPgrCommand, SugestaoEmergenciaDto>
{
    private const int RaioBuscaMetros = 15_000;
    private const int RaioAmpliadoMetros = 35_000;
    private const int MaxHospitais = 4;
    private const int MaxOutros = 2;

    private readonly IAppDbContext _db;
    private readonly IProvedorMapaEmergencia _mapa;
    private readonly IGeradorPgrIa _ia;

    public SugerirEmergenciaGeracaoPgrCommandHandler(IAppDbContext db, IProvedorMapaEmergencia mapa, IGeradorPgrIa ia)
    {
        _db = db;
        _mapa = mapa;
        _ia = ia;
    }

    public async Task<SugestaoEmergenciaDto> Handle(SugerirEmergenciaGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var obra = await _db.Obras.Where(o => o.Id == g.ObraId).Select(o => new { o.Nome, o.Endereco, o.Cidade, o.Uf }).FirstAsync(ct);
        if (string.IsNullOrWhiteSpace(obra.Endereco) && string.IsNullOrWhiteSpace(obra.Cidade))
            throw new InvalidOperationException("Cadastre o endereço ou a cidade da obra para sugerir as rotas de emergência.");

        var resultado = new SugestaoEmergenciaDto { Fonte = _mapa.Fonte, CalculadoEmUtc = DateTime.UtcNow };

        // Endereço completo primeiro; se o mapa não achar a rua, cai para a cidade e avisa que é aproximado.
        var cidadeUf = string.Join(" - ", new[] { obra.Cidade, obra.Uf }.Where(s => !string.IsNullOrWhiteSpace(s)));
        LocalGeocodificado? origem = null;
        if (!string.IsNullOrWhiteSpace(obra.Endereco))
            origem = await _mapa.LocalizarAsync($"{obra.Endereco}, {cidadeUf}, Brasil".Trim(' ', ','), ct);
        if (origem is null && cidadeUf.Length > 0)
        {
            origem = await _mapa.LocalizarAsync($"{cidadeUf}, Brasil", ct);
            if (origem is not null) origem = origem with { Aproximado = true };
        }
        if (origem is null)
            throw new InvalidOperationException("O mapa não encontrou o endereço da obra. Confira o endereço no cadastro da obra.");
        resultado.EnderecoEncontrado = origem.EnderecoEncontrado;
        resultado.LocalizacaoAproximada = origem.Aproximado;
        if (origem.Aproximado)
            resultado.Avisos.Add("O mapa não localizou a rua da obra; as distâncias partem do centro da cidade. Corrija o endereço da obra para um resultado preciso.");

        var locais = await _mapa.BuscarProximosAsync(origem.Ponto, RaioBuscaMetros, ct);
        if (!locais.Any(l => l.Tipo == TipoLocalEmergencia.Hospital))
            locais = await _mapa.BuscarProximosAsync(origem.Ponto, RaioAmpliadoMetros, ct);
        if (locais.Count == 0)
        {
            resultado.Avisos.Add("O mapa não retornou hospitais, bombeiros nem delegacias perto da obra.");
            return resultado;
        }

        // Só os mais próximos em linha reta ganham rota (cada rota é uma consulta ao mapa).
        var uteis = locais.Where(l => !NaoAtendeEmergencia(l)).ToList();
        var escolhidos = uteis.Where(l => l.Tipo == TipoLocalEmergencia.Hospital).OrderBy(l => l.DistanciaRetaKm).Take(MaxHospitais)
            .Concat(uteis.Where(l => l.Tipo == TipoLocalEmergencia.Bombeiros).OrderBy(l => l.DistanciaRetaKm).Take(MaxOutros))
            .Concat(uteis.Where(l => l.Tipo == TipoLocalEmergencia.Policia).OrderBy(l => l.DistanciaRetaKm).Take(MaxOutros))
            .ToList();

        var comRota = new List<(LocalEmergenciaMapa Local, RotaMapa Rota)>();
        foreach (var local in escolhidos)
        {
            var rota = await _mapa.RotaAsync(origem.Ponto, local.Ponto, ct);
            if (rota is not null) comRota.Add((local, rota));
        }
        if (comRota.Count < escolhidos.Count)
            resultado.Avisos.Add("Algumas rotas não puderam ser calculadas agora e ficaram de fora.");

        var estrutura = SerializacaoGeracao.Ler(g.EstruturaJson);
        var riscos = estrutura.Ghes.SelectMany(x => x.Atividades).SelectMany(a => a.Riscos)
            .Where(r => !r.Excluido).Select(r => $"{r.Tipo}: {r.Perigo}").Distinct().Take(30).ToList();

        SugestaoEmergenciaIa? sugestao = null;
        try
        {
            sugestao = await _ia.SugerirEmergenciaAsync(new ContextoEmergenciaIa(
                obra.Nome,
                origem.EnderecoEncontrado,
                riscos,
                comRota.Select(c => new CandidatoEmergenciaIa(c.Local.Id, NomeTipo(c.Local.Tipo), c.Local.Nome, c.Local.Endereco,
                    c.Local.AtendeUrgencia, c.Rota.DistanciaKm, c.Rota.TempoMinutos, c.Rota.Passos)).ToList(),
                CenariosEmergenciaPgr.Todos), ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            resultado.Avisos.Add("A IA não respondeu agora; a recomendação seguiu só o tempo de rota e o atendimento de urgência informado no mapa.");
        }
        resultado.UsouIa = sugestao is not null;

        var recomendacoes = (sugestao?.Recomendados ?? Array.Empty<RecomendacaoEmergenciaIa>())
            .Where(r => comRota.Any(c => c.Local.Id == r.Id)).GroupBy(r => r.Id).Select(x => x.First()).ToDictionary(r => r.Id);

        // Um recomendado por tipo: o que a IA escolheu. Sem IA, o de menor tempo; para hospital, o que atende
        // urgência se chegar em até 10 minutos a mais que o mais rápido (mesmo critério pedido à IA).
        var recomendadoPorTipo = new Dictionary<TipoLocalEmergencia, string>();
        foreach (var grupo in comRota.GroupBy(c => c.Local.Tipo))
        {
            var porIa = grupo.FirstOrDefault(c => recomendacoes.ContainsKey(c.Local.Id));
            var maisRapido = grupo.Min(c => c.Rota.TempoMinutos);
            var comUrgencia = grupo
                .Where(c => grupo.Key != TipoLocalEmergencia.Hospital || c.Local.AtendeUrgencia)
                .Where(c => c.Rota.TempoMinutos <= maisRapido + 10)
                .OrderBy(c => c.Rota.TempoMinutos).FirstOrDefault();
            var escolhido = porIa.Local ?? comUrgencia.Local ?? grupo.OrderBy(c => c.Rota.TempoMinutos).First().Local;
            recomendadoPorTipo[grupo.Key] = escolhido.Id;
        }

        foreach (var (local, rota) in comRota.OrderBy(c => c.Local.Tipo).ThenBy(c => c.Rota.TempoMinutos))
        {
            recomendacoes.TryGetValue(local.Id, out var rec);
            // O caminho vem sempre dos passos calculados pelo mapa; a IA só justifica a escolha.
            var comoChegar = string.Join("\n", rota.Passos);
            resultado.Opcoes.Add(new OpcaoEmergenciaDto
            {
                Id = local.Id,
                Chave = ChaveFicha(local.Tipo),
                Nome = local.Nome,
                Endereco = local.Endereco,
                Telefone = local.Telefone,
                AtendeUrgencia = local.AtendeUrgencia,
                DistanciaKm = Math.Round(rota.DistanciaKm, 1),
                TempoMinutos = rota.TempoMinutos,
                Rota = comoChegar,
                Recomendado = recomendadoPorTipo.GetValueOrDefault(local.Tipo) == local.Id,
                Justificativa = rec?.Justificativa,
                LinkMapa = LinkRota(origem.Ponto, local.Ponto),
            });
        }

        if (resultado.Opcoes.Any(o => string.IsNullOrWhiteSpace(o.Telefone)))
            resultado.Avisos.Add("O mapa não tem telefone de alguns locais: informe-os na ficha antes de aprovar.");
        if (!resultado.Opcoes.Any(o => o.Chave == "hospital"))
            resultado.Avisos.Add("Nenhum hospital com rota calculada. Informe o hospital de referência manualmente.");

        var possiveis = CenariosEmergenciaPgr.Todos.ToHashSet();
        resultado.CenariosSugeridos = (sugestao?.Cenarios ?? Array.Empty<string>()).Where(possiveis.Contains).Distinct().ToList();
        return resultado;
    }

    // O mapa marca como hospital, bombeiro ou polícia coisas que não atendem a emergência de uma obra: clínicas
    // de especialidade, órgãos administrativos e a Polícia Federal (que não atende pelo 190). Ficam fora.
    private static readonly string[] EspecialidadesSemUrgencia = { "olhos", "oftalm", "odont", "veterin", "psiquiat", "dermat", "estétic", "estetic" };
    private static readonly string[] OrgaosAdministrativos = { "diretoria", "academia", "escola", "museu", "apoio logístico", "apoio logistico", "comando geral" };

    private static bool NaoAtendeEmergencia(LocalEmergenciaMapa l)
    {
        bool Contem(string[] termos) => termos.Any(t => l.Nome.Contains(t, StringComparison.OrdinalIgnoreCase));
        return l.Tipo switch
        {
            TipoLocalEmergencia.Hospital => Contem(EspecialidadesSemUrgencia),
            TipoLocalEmergencia.Bombeiros => Contem(OrgaosAdministrativos),
            _ => Contem(OrgaosAdministrativos) || l.Nome.Contains("federal", StringComparison.OrdinalIgnoreCase)
                 || l.Nome.Split(' ', '/', '-').Contains("PF", StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string NomeTipo(TipoLocalEmergencia t) => t switch
    {
        TipoLocalEmergencia.Hospital => "Hospital",
        TipoLocalEmergencia.Bombeiros => "Corpo de Bombeiros",
        _ => "Delegacia ou posto de polícia",
    };

    private static string ChaveFicha(TipoLocalEmergencia t) => t switch
    {
        TipoLocalEmergencia.Hospital => "hospital",
        TipoLocalEmergencia.Bombeiros => "bombeiros",
        _ => "pm",
    };

    private static string LinkRota(PontoGeografico a, PontoGeografico b) =>
        FormattableString.Invariant($"https://www.openstreetmap.org/directions?engine=fossgis_osrm_car&route={a.Latitude:F5}%2C{a.Longitude:F5}%3B{b.Latitude:F5}%2C{b.Longitude:F5}");
}
