using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Cameras.Queries;

public enum SituacaoQuadroAoVivo
{
    // Há quadro mais novo que o pedido (Jpeg, Sequencia, CapturadoEmUtc).
    Quadro,
    // Nada novo desde "apos".
    SemNovidade,
    // O agente ainda não enviou o primeiro quadro.
    Aguardando,
    // Sessão acabou (Motivo).
    Encerrada,
}

public record QuadroAoVivoDto(SituacaoQuadroAoVivo Situacao, byte[]? Jpeg, long Sequencia, DateTime? CapturadoEmUtc, string? Motivo)
{
    // Imagem de pessoa possível: nunca em log, nem por ToString.
    public override string ToString() => $"{nameof(QuadroAoVivoDto)} {{ Situacao = {Situacao}, Sequencia = {Sequencia}, Bytes = {Jpeg?.Length ?? 0} }}";
}

// Tela: busca o último quadro da sessão com o token do usuário (escopo por obra pelo filtro global). Sessão fora do escopo ou
// inexistente = KeyNotFoundException (404). Cada chamada marca espectador (UltimaVisualizacaoEmUtc, regravada no máximo a
// cada 5 s; quando "encerrar" da tela antecipou a inatividade, a regravação devolve a sessão a "viva"). Tem efeito colateral deliberado: encerra a sessão se ela já passou do tempo máximo, da inatividade, do prazo
// para o agente começar ou do tempo sem receber quadro (agente parou), e nesse caso apaga o quadro.
public record ObterQuadroAoVivoQuery(Guid SessaoId, long? Apos) : IRequest<QuadroAoVivoDto>;

public class ObterQuadroAoVivoQueryHandler : IRequestHandler<ObterQuadroAoVivoQuery, QuadroAoVivoDto>
{
    private readonly IAppDbContext _db;
    private readonly AoVivoCameraOpcoes _opcoes;

    public ObterQuadroAoVivoQueryHandler(IAppDbContext db, IOptions<AoVivoCameraOpcoes>? opcoes = null)
    {
        _db = db;
        _opcoes = opcoes?.Value ?? new AoVivoCameraOpcoes();
    }

    public async Task<QuadroAoVivoDto> Handle(ObterQuadroAoVivoQuery request, CancellationToken ct)
    {
        // Metadados sem o quadro (até 300 KB): o JPEG só é lido quando há quadro novo para entregar.
        var s = await _db.SessoesAoVivoCamera.AsNoTracking().Where(x => x.Id == request.SessaoId)
            .Select(x => new
            {
                x.Id, x.EncerradaEmUtc, x.MotivoEncerramento, x.CodigoFalha, x.LimiteEmUtc, x.UltimaVisualizacaoEmUtc,
                x.SolicitadaEmUtc, x.IniciadaEmUtc, x.UltimoQuadroSequencia, x.UltimoQuadroRecebidoEmUtc,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Sessão ao vivo {request.SessaoId} não encontrada.");

        if (s.EncerradaEmUtc is not null) return Encerrada(s.MotivoEncerramento, s.CodigoFalha);

        var agora = DateTime.UtcNow;
        IQueryable<Domain.Entidades.SessaoAoVivoCamera> Esta() => _db.SessoesAoVivoCamera.IgnoreQueryFilters().Where(x => x.Id == s.Id);

        var parada = SessaoAoVivoCameraRegras.MotivoDeParada(s.LimiteEmUtc, s.UltimaVisualizacaoEmUtc, s.SolicitadaEmUtc, s.IniciadaEmUtc, s.UltimoQuadroRecebidoEmUtc, agora, _opcoes);
        if (parada is not null)
        {
            await SessaoAoVivoCameraRegras.EncerrarAsync(Esta(), parada, agora, ct);
            return Encerrada(parada, null);
        }

        // Espectador presente. Regrava só de tempos em tempos (as telas buscam 2 a 5 vezes por segundo).
        if (s.UltimaVisualizacaoEmUtc <= agora.AddSeconds(-_opcoes.GravarVisualizacaoACadaSegundos))
            await Esta().Where(x => x.EncerradaEmUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.UltimaVisualizacaoEmUtc, agora), ct);

        if (s.IniciadaEmUtc is null) return new QuadroAoVivoDto(SituacaoQuadroAoVivo.Aguardando, null, 0, null, null);

        if (request.Apos is { } apos && s.UltimoQuadroSequencia <= apos)
            return new QuadroAoVivoDto(SituacaoQuadroAoVivo.SemNovidade, null, s.UltimoQuadroSequencia, null, null);

        var quadro = await _db.SessoesAoVivoCamera.AsNoTracking().Where(x => x.Id == s.Id)
            .Select(x => new { x.UltimoQuadro, x.UltimoQuadroSequencia, x.UltimoQuadroCapturadoEmUtc, x.EncerradaEmUtc, x.MotivoEncerramento, x.CodigoFalha })
            .FirstOrDefaultAsync(ct);
        if (quadro is null || quadro.EncerradaEmUtc is not null) return Encerrada(quadro?.MotivoEncerramento, quadro?.CodigoFalha);
        if (quadro.UltimoQuadro is not { Length: > 0 } jpeg)
            return new QuadroAoVivoDto(SituacaoQuadroAoVivo.SemNovidade, null, quadro.UltimoQuadroSequencia, null, null);

        return new QuadroAoVivoDto(
            SituacaoQuadroAoVivo.Quadro, jpeg, quadro.UltimoQuadroSequencia,
            quadro.UltimoQuadroCapturadoEmUtc is { } c ? DateTime.SpecifyKind(c, DateTimeKind.Utc) : null, null);
    }

    private static QuadroAoVivoDto Encerrada(string? motivo, string? codigoFalha)
        => new(SituacaoQuadroAoVivo.Encerrada, null, 0, null, SessaoAoVivoCameraRegras.MotivoParaTela(motivo, codigoFalha));
}
