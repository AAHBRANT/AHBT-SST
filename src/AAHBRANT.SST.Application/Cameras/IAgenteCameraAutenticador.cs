using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras;

// Autentica o agente de câmeras (rota anônima) pelo segredo enviado no cabeçalho. Devolve nulo quando o
// segredo falta, está errado, o agente não existe, foi revogado ou a obra dele está inativa: quem chama responde
// 401 igual em todos os casos, sem revelar qual deles (obra inativa não gera análise nem custo de IA). Usa IgnoreQueryFilters de propósito: nesta rota o escopo por obra ainda
// está zerado (o usuário é anônimo) e o filtro global esconderia o próprio agente; Ativo é conferido aqui.
public interface IAgenteCameraAutenticador
{
    Task<AgenteCamera?> ValidarAsync(Guid agenteId, string? segredo, CancellationToken ct);
}

public class AgenteCameraAutenticador : IAgenteCameraAutenticador
{
    // Hash válido (base64 de 32 bytes) de um segredo qualquer, só para gastar o mesmo tempo quando o agente não existe.
    private const string HashDeEquilibrio = "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=";

    private readonly IAppDbContext _db;
    private readonly ISegredoDispositivoHasher _hasher;

    public AgenteCameraAutenticador(IAppDbContext db, ISegredoDispositivoHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // O controller autentica ANTES de ler o corpo e o handler autentica de novo: como a instância é por requisição,
    // o último resultado positivo (mesmo agente e mesmo segredo) é reaproveitado e a consulta não se repete.
    private AgenteCamera? _validado;
    private string? _segredoValidado;

    public async Task<AgenteCamera?> ValidarAsync(Guid agenteId, string? segredo, CancellationToken ct)
    {
        if (agenteId == Guid.Empty || string.IsNullOrWhiteSpace(segredo)) return null;
        if (_validado is not null && _validado.Id == agenteId && _segredoValidado == segredo) return _validado;

        var agente = await _db.AgentesCamera.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == agenteId, ct);
        var segredoConfere = _hasher.Verificar(segredo, agente?.SegredoHash ?? HashDeEquilibrio);
        if (agente is not { Ativo: true } || !segredoConfere) return null;

        // Só depois do segredo: quem não o tem não aprende nada sobre a obra. Sem o filtro global (o escopo ainda está
        // zerado nesta rota): o que importa é a obra estar ativa. Obra inativada = agente não autentica.
        var obraAtiva = await _db.Obras.IgnoreQueryFilters().AnyAsync(o => o.Id == agente.ObraId && o.Ativo, ct);
        if (!obraAtiva) return null;

        _validado = agente;
        _segredoValidado = segredo;
        return agente;
    }
}
