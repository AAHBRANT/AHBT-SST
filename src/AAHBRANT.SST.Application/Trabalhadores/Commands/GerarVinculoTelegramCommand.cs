using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record GerarVinculoTelegramCommand(Guid TrabalhadorId) : IRequest<GerarVinculoTelegramResultado>;

public record GerarVinculoTelegramResultado(string Codigo, string LinkTelegram);

public class GerarVinculoTelegramCommandValidator : AbstractValidator<GerarVinculoTelegramCommand>
{
    public GerarVinculoTelegramCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
    }
}

public class GerarVinculoTelegramCommandHandler : IRequestHandler<GerarVinculoTelegramCommand, GerarVinculoTelegramResultado>
{
    private readonly IAppDbContext _db;
    private readonly ITelegramService _telegram;

    public GerarVinculoTelegramCommandHandler(IAppDbContext db, ITelegramService telegram)
    {
        _db = db;
        _telegram = telegram;
    }

    public async Task<GerarVinculoTelegramResultado> Handle(GerarVinculoTelegramCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.TrabalhadorId} não encontrado.");

        // Código curto exibido no perfil — o trabalhador manda "/start <codigo>" para o bot
        // (bots não podem iniciar a conversa) e o TelegramUpdatesPollingService captura o
        // vínculo, preenchendo TelegramChatId e limpando este código.
        var codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        trabalhador.TelegramCodigoVinculo = codigo;
        await _db.SaveChangesAsync(ct);

        var link = $"https://t.me/{_telegram.ObterNomeUsuarioBot()}?start={codigo}";
        return new GerarVinculoTelegramResultado(codigo, link);
    }
}
