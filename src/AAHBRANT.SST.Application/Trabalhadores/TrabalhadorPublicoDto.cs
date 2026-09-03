namespace AAHBRANT.SST.Application.Trabalhadores;

// "Crachá digital" público — mesmo espírito de AreaPublicaDto (NTAG.md §3.B.4), mas com um recorte
// deliberadamente mais restrito: só o que um fiscal precisa ver ao escanear o QR/NTAG do capacete em
// campo (nome, função, aptidão, EPIs e treinamentos). NUNCA inclui CPF, RG, admissão, ocorrências,
// assinaturas ou qualquer outro dado do perfil completo (ObterPerfilCompletoTrabalhadorQuery) — essa
// tela não tem login, então tudo que está aqui deve poder ficar exposto sem quebrar LGPD.
public class TrabalhadorPublicoDto
{
    // Discriminador pro frontend distinguir os dois tipos de recurso que a mesma rota pública
    // (/sst/p/{codigoOuUid}) pode resolver — ver AreaPublicaDto.TipoRecurso.
    public string TipoRecurso => "trabalhador";
    public string Nome { get; set; } = string.Empty;
    public string Matricula { get; set; } = string.Empty;
    public string FuncaoNome { get; set; } = string.Empty;
    public string ObraNome { get; set; } = string.Empty;
    public bool TemFoto { get; set; }
    public string StatusAptidao { get; set; } = string.Empty;
    public List<EpiAtivoPublicoDto> EpisAtivos { get; set; } = new();
    public List<TreinamentoPublicoDto> Treinamentos { get; set; } = new();
}

public record EpiAtivoPublicoDto(string CatalogoEpiNome, DateTime? DataValidade);

public record TreinamentoPublicoDto(string CursoNome, DateTime DataValidade);
