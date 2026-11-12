using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Resultado do "Testar conexão" devolvido pelo agente (rota anônima, autenticada pelo segredo). Devolve false quando o agente
// não se autentica (o controller responde 401). Motivo e Detalhe são textos curtos SEM senha nem URL com credencial: o
// agente é responsável por mascará-los. Miniatura (opcional) é um JPEG pequeno; fica cifrada e é entregue ao front uma vez.
public record RegistrarResultadoTesteConexaoCameraCommand(
    Guid AgenteId,
    string? Segredo,
    string? VersaoAgente,
    Guid TesteId,
    bool Sucesso,
    string? Motivo,
    string? Detalhe,
    byte[]? Miniatura,
    string? Codigo = null) : IRequest<bool>;

public class RegistrarResultadoTesteConexaoCameraCommandValidator : AbstractValidator<RegistrarResultadoTesteConexaoCameraCommand>
{
    public const int TamanhoMaximoMiniaturaBytes = 150_000;

    public RegistrarResultadoTesteConexaoCameraCommandValidator()
    {
        RuleFor(x => x.TesteId).NotEmpty();
        When(x => x.Miniatura is { Length: > 0 }, () => RuleFor(x => x.Miniatura!)
            .Must(m => m.Length <= TamanhoMaximoMiniaturaBytes).WithMessage("A miniatura excede o tamanho máximo de 150 KB.")
            .Must(m => m.Length >= 2 && m[0] == 0xFF && m[1] == 0xD8).WithMessage("A miniatura precisa estar em JPEG."));
    }
}

public class RegistrarResultadoTesteConexaoCameraCommandHandler : IRequestHandler<RegistrarResultadoTesteConexaoCameraCommand, bool>
{
    public const string CodigoTesteNaoEncontrado = "teste_nao_encontrado";

    private readonly IAppDbContext _db;
    private readonly IAgenteCameraAutenticador _autenticador;
    private readonly ICurrentUserService _usuarioAtual;
    private readonly ILogger<RegistrarResultadoTesteConexaoCameraCommandHandler> _logger;

    public RegistrarResultadoTesteConexaoCameraCommandHandler(
        IAppDbContext db,
        IAgenteCameraAutenticador autenticador,
        ICurrentUserService usuarioAtual,
        ILogger<RegistrarResultadoTesteConexaoCameraCommandHandler> logger)
    {
        _db = db;
        _autenticador = autenticador;
        _usuarioAtual = usuarioAtual;
        _logger = logger;
    }

    public async Task<bool> Handle(RegistrarResultadoTesteConexaoCameraCommand request, CancellationToken ct)
    {
        var agente = await _autenticador.ValidarAsync(request.AgenteId, request.Segredo, ct);
        if (agente is null) return false;

        _usuarioAtual.DefinirEscopo(temAcessoGlobal: false, new[] { agente.ObraId });
        await ContatoAgenteCamera.RegistrarAsync(_db, agente, request.VersaoAgente, _logger, ct);

        // Sem filtro global (rota anônima), com o predicado explícito: só teste deste agente e desta obra.
        var teste = await _db.TestesConexaoCamera.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TesteId && t.Ativo && t.ObraId == agente.ObraId && t.AgenteCameraId == agente.Id, ct)
            ?? throw new KeyNotFoundException(CodigoTesteNaoEncontrado);

        // Reenvio do mesmo resultado (o agente repete quando a resposta se perde): mantém o primeiro, sem erro.
        if (teste.ConcluidoEmUtc is not null) return true;

        var agora = DateTime.UtcNow;
        teste.ConcluidoEmUtc = agora;
        teste.Sucesso = request.Sucesso;
        teste.Codigo = CodigosTesteConexaoCamera.Normalizar(request.Sucesso, request.Codigo);
        teste.Motivo = Limitar(request.Motivo, 300);
        teste.Detalhe = Limitar(request.Detalhe, 100);
        // Resultado que chega depois de o pedido expirar: a tela já mostrou "sem resposta" e ninguém buscará a imagem,
        // então a miniatura (foto de pessoa possível) nem chega a ser gravada.
        var expirado = agora - teste.SolicitadoEmUtc > SolicitarTesteConexaoCameraCommandHandler.ValidadeDoPedido;
        teste.Miniatura = request.Sucesso && !expirado && request.Miniatura is { Length: > 0 } m ? m : null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Limitar(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = texto.Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo];
    }
}
