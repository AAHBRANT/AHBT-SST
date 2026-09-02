using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Queries;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

public record EnviarDdsTelegramCommand(Guid Id) : IRequest<EnviarDdsTelegramResultado>;

public record EnviarDdsTelegramResultado(int TotalParticipantes, int Enviados, int SemVinculo);

public class EnviarDdsTelegramCommandValidator : AbstractValidator<EnviarDdsTelegramCommand>
{
    public EnviarDdsTelegramCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EnviarDdsTelegramCommandHandler : IRequestHandler<EnviarDdsTelegramCommand, EnviarDdsTelegramResultado>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IDdsPdfService _pdf;
    private readonly ITelegramService _telegram;

    public EnviarDdsTelegramCommandHandler(IAppDbContext db, IMediator mediator, IDdsPdfService pdf, ITelegramService telegram)
    {
        _db = db;
        _mediator = mediator;
        _pdf = pdf;
        _telegram = telegram;
    }

    public async Task<EnviarDdsTelegramResultado> Handle(EnviarDdsTelegramCommand request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterDdsDetalheQuery(request.Id), ct)
            ?? throw new KeyNotFoundException($"DDS {request.Id} não encontrado.");

        var trabalhadorIds = detalhe.Participantes.Select(p => p.TrabalhadorId).ToList();
        var vinculados = await _db.Trabalhadores
            .Where(t => trabalhadorIds.Contains(t.Id) && t.TelegramChatId != null)
            .Select(t => new { t.Id, ChatId = t.TelegramChatId!.Value })
            .ToListAsync(ct);

        var logoConteudo = await _db.Obras.Where(o => o.Id == detalhe.Dds.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var pdfBytes = _pdf.Gerar(ExportarDdsPdfQueryHandler.MontarModelo(detalhe, logoConteudo));
        var nomeArquivo = $"DDS_{detalhe.Dds.Data:yyyy-MM-dd}.pdf";
        var nomesAtividades = string.Join(", ", detalhe.Dds.AtividadesNomes);
        var legenda = $"DDS — {nomesAtividades} ({detalhe.Dds.ObraNome}, {detalhe.Dds.Data:dd/MM/yyyy})";

        foreach (var vinculo in vinculados)
        {
            var envio = new Domain.Entidades.DdsTelegramEnvio
            {
                DdsId = request.Id,
                TrabalhadorId = vinculo.Id,
                ChatId = vinculo.ChatId,
                EnviadoEm = DateTime.UtcNow,
            };
            var messageId = await _telegram.EnviarDocumentoAsync(
                vinculo.ChatId, nomeArquivo, pdfBytes, legenda, callbackData: $"confirmar:{envio.Id}", ct);
            envio.MessageId = messageId;
            _db.DdsTelegramEnvios.Add(envio);
        }

        await _db.SaveChangesAsync(ct);

        return new EnviarDdsTelegramResultado(
            TotalParticipantes: trabalhadorIds.Count,
            Enviados: vinculados.Count,
            SemVinculo: trabalhadorIds.Count - vinculados.Count);
    }
}
