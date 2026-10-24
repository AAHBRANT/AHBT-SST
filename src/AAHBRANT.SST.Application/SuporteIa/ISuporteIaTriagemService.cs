namespace AAHBRANT.SST.Application.SuporteIa;

public interface ISuporteIaTriagemService
{
    Task<TriagemSuporteIaResultado> TriarAsync(SuporteIaEntradaTriagem entrada, CancellationToken ct);
}

public record SuporteIaEntradaTriagem(
    string Tipo,
    string Severidade,
    string Titulo,
    string Descricao,
    string? Modulo,
    string? UrlContexto);
