namespace AAHBRANT.SST.Application.Trabalhadores;

// "Crachá digital" do trabalhador — mesmo espírito de AreaPublicaDto (NTAG.md §3.B.4), mas agora
// autenticado: o UID da tag é só um ponteiro físico, e o backend continua aplicando login/escopo.
// Ainda assim, mantém recorte enxuto e nunca inclui CPF/RG.
public class TrabalhadorPublicoDto
{
    // Discriminador pro frontend distinguir os dois tipos de recurso que a mesma rota
    // (/sst/p/{codigoOuUid}) pode resolver — ver AreaPublicaDto.TipoRecurso.
    public string TipoRecurso => "trabalhador";
    public Guid TrabalhadorId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;
    public string FuncaoNome { get; set; } = string.Empty;
    public string ObraNome { get; set; } = string.Empty;
    public bool TemFoto { get; set; }
    public string StatusAptidao { get; set; } = string.Empty;
    public List<EpiAtivoPublicoDto> EpisAtivos { get; set; } = new();
    public List<TreinamentoPublicoDto> Treinamentos { get; set; } = new();
    public List<DdsPublicoDto> HistoricoDds { get; set; } = new();
}

public record EpiAtivoPublicoDto(string CatalogoEpiNome, DateTime? DataValidade);

public record TreinamentoPublicoDto(string CursoNome, DateTime DataValidade);

// Só Data/Obra/Tema — nunca a foto ou o ScoreConfianca da biometria do participante (evidência de
// presença é dado sensível demais pra tela sem login, mesmo cuidado já tomado com CPF/assinaturas).
public record DdsPublicoDto(DateTime Data, string ObraNome, string? Tema);
