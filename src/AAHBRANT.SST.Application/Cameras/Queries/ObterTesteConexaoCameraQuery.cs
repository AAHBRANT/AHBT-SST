using AAHBRANT.SST.Application.Cameras.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Queries;

// Situacao: "Pendente" (o agente ainda não respondeu), "Concluido" (veja Sucesso/Motivo) ou "Expirado" (o agente não
// respondeu a tempo: provavelmente desligado ou sem internet).
public record TesteConexaoCameraDto(
    Guid Id,
    string Situacao,
    bool? Sucesso,
    string? Motivo,
    // Código estável do motivo (ver CodigosTesteConexaoCamera); nulo no sucesso, no pedido pendente e no expirado.
    string? Codigo,
    string? Detalhe,
    // JPEG em base64, entregue UMA vez: na primeira consulta depois da resposta do agente e apagado em seguida.
    string? MiniaturaBase64,
    DateTime SolicitadoEmUtc,
    DateTime? ConcluidoEmUtc);

public record ObterTesteConexaoCameraQuery(Guid Id) : IRequest<TesteConexaoCameraDto>;

// É uma consulta com efeito colateral deliberado: a miniatura (foto de pessoa possível) é apagada ao ser entregue.
public class ObterTesteConexaoCameraQueryHandler : IRequestHandler<ObterTesteConexaoCameraQuery, TesteConexaoCameraDto>
{
    public const string Pendente = "Pendente";
    public const string Concluido = "Concluido";
    public const string Expirado = "Expirado";

    private readonly IAppDbContext _db;
    public ObterTesteConexaoCameraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TesteConexaoCameraDto> Handle(ObterTesteConexaoCameraQuery request, CancellationToken ct)
    {
        var teste = await _db.TestesConexaoCamera.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Teste de conexão {request.Id} não encontrado.");

        var agora = DateTime.UtcNow;
        // Miniatura que ninguém buscou a tempo (ou que chegou depois de o pedido expirar) é descartada, nunca entregue.
        if (teste.Miniatura is { Length: > 0 } && agora - teste.SolicitadoEmUtc > SolicitarTesteConexaoCameraCommandHandler.RetencaoDaMiniatura)
        {
            teste.Miniatura = null;
            await _db.SaveChangesAsync(ct);
        }
        await MiniaturasTesteConexao.LimparAntigasAsync(_db, teste.AgenteCameraId, agora, ct);

        string? miniatura = null;
        if (teste.Miniatura is { Length: > 0 } bytes)
        {
            miniatura = Convert.ToBase64String(bytes);
            teste.Miniatura = null;
            await _db.SaveChangesAsync(ct);
        }

        var situacao = teste.ConcluidoEmUtc is not null ? Concluido
            : agora - teste.SolicitadoEmUtc > SolicitarTesteConexaoCameraCommandHandler.ValidadeDoPedido ? Expirado
            : Pendente;
        var motivo = situacao == Expirado
            ? "O agente da obra não respondeu a tempo. Confira se o computador do agente está ligado e com internet."
            : teste.Motivo;

        return new TesteConexaoCameraDto(
            teste.Id, situacao, situacao == Concluido ? teste.Sucesso : null, motivo, situacao == Concluido ? teste.Codigo : null, teste.Detalhe, miniatura,
            DateTime.SpecifyKind(teste.SolicitadoEmUtc, DateTimeKind.Utc),
            teste.ConcluidoEmUtc is { } c ? DateTime.SpecifyKind(c, DateTimeKind.Utc) : null);
    }
}
