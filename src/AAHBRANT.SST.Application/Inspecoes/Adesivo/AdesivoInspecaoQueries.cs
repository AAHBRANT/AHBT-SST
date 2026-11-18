using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

public class AdesivoInspecaoInfoDto
{
    public bool Disponivel { get; set; }
    public string? MotivoIndisponivel { get; set; }
    // "Liberado" | "Inspecionado" | "NaoLiberado"; nulo quando indisponível.
    public string? Situacao { get; set; }
    public string ObjetoIdentificacao { get; set; } = string.Empty;
    public string? ObjetoDescricao { get; set; }
    public string Tema { get; set; } = string.Empty;
    public string ObraNome { get; set; } = string.Empty;
    // Data (yyyy-MM-dd); nulo sem periodicidade.
    public string? ProximaInspecaoEm { get; set; }
    public string? InspecaoEm { get; set; }
    public string? Protocolo { get; set; }
    public string? InspetorNome { get; set; }
    public string? InspetorFuncao { get; set; }
}

// Regras de elegibilidade compartilhadas entre a consulta (GET) e a geração do PDF (POST).
public static class ElegibilidadeAdesivoInspecao
{
    public const string MotivoNaoConcluida =
        "A inspeção ainda não foi concluída e assinada. O adesivo só é gerado depois da assinatura.";
    public const string MotivoObjetoNaoEncontrado =
        "O objeto desta inspeção não foi encontrado. Verifique o cadastro antes de gerar o adesivo.";

    // Toda inspeção Concluída gera adesivo; sem objeto físico o QR aponta para a própria inspeção.
    public static string? Motivo(InspecaoParaAdesivo i)
        => i.Status != StatusInspecao.Concluida ? MotivoNaoConcluida : null;

    // Respeita o filtro global de obra (SEM IgnoreQueryFilters): quem não tem a obra no escopo recebe nulo (404).
    public static Task<InspecaoParaAdesivo?> CarregarNoEscopoAsync(IAppDbContext db, Guid id, CancellationToken ct)
        => db.Inspecoes
            .Where(i => i.Id == id)
            .Select(i => new InspecaoParaAdesivo(i.Id, i.ObraId, i.Data, i.Status, i.NumeroDocumento,
                i.ResponsavelUsuarioId, i.VeiculoId, i.AlojamentoId, i.ItemInspecionavelId, i.TipoInspecao, i.AtividadeId))
            .FirstOrDefaultAsync(ct);
}

public record ObterAdesivoInspecaoQuery(Guid Id) : IRequest<AdesivoInspecaoInfoDto?>;

public class ObterAdesivoInspecaoQueryHandler : IRequestHandler<ObterAdesivoInspecaoQuery, AdesivoInspecaoInfoDto?>
{
    private readonly IAppDbContext _db;

    public ObterAdesivoInspecaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AdesivoInspecaoInfoDto?> Handle(ObterAdesivoInspecaoQuery request, CancellationToken ct)
    {
        var inspecao = await ElegibilidadeAdesivoInspecao.CarregarNoEscopoAsync(_db, request.Id, ct);
        if (inspecao is null) return null;

        var dados = await AdesivoInspecaoCarregador.MontarAsync(_db, inspecao, ct);
        if (dados is null)
            return new AdesivoInspecaoInfoDto { Disponivel = false, MotivoIndisponivel = ElegibilidadeAdesivoInspecao.MotivoObjetoNaoEncontrado };

        var motivo = ElegibilidadeAdesivoInspecao.Motivo(inspecao);
        var disponivel = motivo is null && dados.Situacao is not null;

        return new AdesivoInspecaoInfoDto
        {
            Disponivel = disponivel,
            MotivoIndisponivel = disponivel ? null : motivo ?? ElegibilidadeAdesivoInspecao.MotivoNaoConcluida,
            Situacao = dados.Situacao is { } s ? ResultadoAdesivoInspecao.Texto(s) : null,
            ObjetoIdentificacao = dados.ObjetoIdentificacao,
            ObjetoDescricao = dados.ObjetoDescricao,
            Tema = dados.Tema,
            ObraNome = dados.ObraNome,
            ProximaInspecaoEm = disponivel ? dados.ProximaInspecaoEm?.ToString("yyyy-MM-dd") : null,
            InspecaoEm = dados.InspecaoEm.ToString("yyyy-MM-dd"),
            Protocolo = dados.Protocolo,
            InspetorNome = dados.InspetorNome,
            InspetorFuncao = dados.InspetorFuncao,
        };
    }
}
