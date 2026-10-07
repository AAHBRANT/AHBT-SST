using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Dds.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Relatorios;

// Disparo diário das 08:00: gera e publica a lista de presença de todo DDS encerrado ainda não informado.
// "A partir de agora": só considera DDS a partir do dia anterior ao primeiro disparo, para o histórico antigo
// não inundar o Telegram. Devolve quantos relatórios foram publicados.
public record GerarListasDePresencaCommand : IRequest<int>;

public class GerarListasDePresencaCommandHandler : IRequestHandler<GerarListasDePresencaCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IImagemListaPresencaService _imagem;
    private readonly IPublicadorRelatorio _publicador;
    private readonly ILogger<GerarListasDePresencaCommandHandler> _logger;

    public GerarListasDePresencaCommandHandler(
        IAppDbContext db,
        IMediator mediator,
        IImagemListaPresencaService imagem,
        IPublicadorRelatorio publicador,
        ILogger<GerarListasDePresencaCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _imagem = imagem;
        _publicador = publicador;
        _logger = logger;
    }

    public static string ChaveDoDds(Guid ddsId) => $"dds:{ddsId}";

    public async Task<int> Handle(GerarListasDePresencaCommand request, CancellationToken ct)
    {
        var primeiraExecucao = await _db.ExecucoesRelatorioAgendado
            .Where(e => e.Tipo == TipoRelatorio.ListaPresencaDds)
            .MinAsync(e => (DateTime?)e.Data, ct);
        var corte = (primeiraExecucao ?? FusoBrasilia.Hoje()).AddDays(-1);

        var candidatos = await _db.Dds
            .Where(d => d.Status == StatusDds.Concluido && !d.SemExpediente && d.Data >= corte)
            .OrderBy(d => d.Data)
            .Select(d => d.Id)
            .ToListAsync(ct);
        if (candidatos.Count == 0) return 0;

        var chaves = candidatos.Select(ChaveDoDds).ToList();
        var jaPublicados = (await _db.RelatoriosGerados.IgnoreQueryFilters()
                .Where(r => chaves.Contains(r.ChaveUnica)).Select(r => r.ChaveUnica).ToListAsync(ct))
            .ToHashSet();

        var publicados = 0;
        foreach (var ddsId in candidatos.Where(id => !jaPublicados.Contains(ChaveDoDds(id))))
        {
            try
            {
                if (await PublicarAsync(ddsId, ct)) publicados++;
            }
            catch (Exception ex)
            {
                // Um DDS com problema não impede os outros; ele é tentado de novo no próximo disparo.
                _logger.LogError(ex, "Falha ao gerar a lista de presença do DDS {DdsId}.", ddsId);
            }
        }
        return publicados;
    }

    private async Task<bool> PublicarAsync(Guid ddsId, CancellationToken ct)
    {
        var dados = await _mediator.Send(new ConstruirListaPresencaQuery(ddsId), ct);
        if (dados is null || dados.Total == 0) return false;

        var png = _imagem.Gerar(dados);
        var pdf = await _mediator.Send(new ExportarDdsPdfQuery(ddsId), ct);
        var ddsObraId = await _db.Dds.Where(d => d.Id == ddsId).Select(d => d.ObraId).FirstAsync(ct);

        var novo = new NovoRelatorio(
            TipoRelatorio.ListaPresencaDds, ddsObraId, ChaveDoDds(ddsId),
            Titulo(dados), Resumo(dados), png, pdf, NomeDoPdf(dados), ddsId, dados.DataDds, dados.DataDds);
        return await _publicador.PublicarAsync(novo, ct) is not null;
    }

    private static readonly string[] DiasDaSemana = { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };

    public static string Titulo(ListaPresencaDados d) => $"Presença do DDS · {d.Obra} · {d.DataDds:dd/MM}";

    public static string Resumo(ListaPresencaDados d)
    {
        var ausentes = d.Ausentes.Count;
        var texto = $"Presença do DDS, {d.Obra}, {DiasDaSemana[(int)d.DataDds.DayOfWeek]} {d.DataDds:dd/MM}: " +
            $"{d.QuantidadePresentes} de {d.Total} presentes, {ausentes} {(ausentes == 1 ? "ausente" : "ausentes")}.";
        if (d.DuracaoMinutos is { } minutos) texto += $" Duração do DDS: {FormatarDuracao(minutos)}.";
        return texto;
    }

    public static string FormatarDuracao(int minutos) => minutos < 60 ? $"{minutos} min" : $"{minutos / 60}h{minutos % 60:00}";

    public static string NomeDoPdf(ListaPresencaDados d)
    {
        var obra = new string(d.Obra.Where(char.IsLetterOrDigit).ToArray());
        return $"Lista-de-Presenca_{(obra.Length > 0 ? obra : "Obra")}_{d.DataDds:yyyy-MM-dd}.pdf";
    }
}
