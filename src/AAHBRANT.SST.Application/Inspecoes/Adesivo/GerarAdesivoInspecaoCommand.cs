using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.TagsIdentificacao;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

public record GerarAdesivoInspecaoCommand(Guid Id, FormatoAdesivo Formato, int? Posicao) : IRequest<GerarAdesivoInspecaoResultado>;

// NaoEncontrada => 404 (inclusive inspeção de obra fora do escopo); Motivo preenchido => 409 com a mensagem em pt-BR.
public record GerarAdesivoInspecaoResultado(byte[]? Pdf, string? Motivo, bool NaoEncontrada = false);

public class GerarAdesivoInspecaoCommandHandler : IRequestHandler<GerarAdesivoInspecaoCommand, GerarAdesivoInspecaoResultado>
{
    public const int PosicoesPorFolha = 8;
    public const string MotivoPosicaoInvalida = "Posição inválida: escolha uma posição de 1 a 8 na folha.";

    private readonly IAppDbContext _db;
    private readonly IQrCodePerfilPublicoService _qrCodePerfil;
    private readonly IAdesivoInspecaoPdfService _pdf;

    public GerarAdesivoInspecaoCommandHandler(IAppDbContext db, IQrCodePerfilPublicoService qrCodePerfil, IAdesivoInspecaoPdfService pdf)
    {
        _db = db;
        _qrCodePerfil = qrCodePerfil;
        _pdf = pdf;
    }

    public async Task<GerarAdesivoInspecaoResultado> Handle(GerarAdesivoInspecaoCommand request, CancellationToken ct)
    {
        var inspecao = await ElegibilidadeAdesivoInspecao.CarregarNoEscopoAsync(_db, request.Id, ct);
        if (inspecao is null)
            return new GerarAdesivoInspecaoResultado(null, null, NaoEncontrada: true);

        var motivo = ElegibilidadeAdesivoInspecao.Motivo(inspecao);
        if (motivo is not null)
            return new GerarAdesivoInspecaoResultado(null, motivo);

        var posicao = request.Posicao ?? 1;
        if (request.Formato == FormatoAdesivo.A4 && (posicao < 1 || posicao > PosicoesPorFolha))
            return new GerarAdesivoInspecaoResultado(null, MotivoPosicaoInvalida);

        var dados = await AdesivoInspecaoCarregador.MontarAsync(_db, inspecao, ct);
        if (dados is null)
            return new GerarAdesivoInspecaoResultado(null, ElegibilidadeAdesivoInspecao.MotivoObjetoNaoEncontrado);
        if (dados.Situacao is not { } situacao)
            return new GerarAdesivoInspecaoResultado(null, ElegibilidadeAdesivoInspecao.MotivoNaoConcluida);

        var uid = await GarantirTagQrAsync(dados.ObjetoTipo, dados.ObjetoId, ct);

        var logo = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == dados.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var urlQr = _qrCodePerfil.MontarUrl(uid);
        if (!string.IsNullOrWhiteSpace(dados.Protocolo))
            urlQr += "?i=" + Uri.EscapeDataString(dados.Protocolo);

        var pdf = _pdf.Gerar(new AdesivoInspecaoPdfModelo(
            situacao,
            dados.ObjetoIdentificacao,
            dados.ObjetoDescricao,
            dados.Tema,
            dados.ObraNome,
            logo is { Length: > 0 } ? logo : null,
            dados.InspecaoEm,
            dados.ProximaInspecaoEm,
            dados.InspetorNome,
            dados.InspetorFuncao,
            dados.Protocolo ?? "—",
            urlQr,
            request.Formato,
            request.Formato == FormatoAdesivo.A4 ? posicao : 1));

        return new GerarAdesivoInspecaoResultado(pdf, null);
    }

    // Mesmo padrão de GerarQrCodesTrabalhadoresCommand: reaproveita a tag QR vinculada ao objeto (a mesma para
    // sempre, para o adesivo antigo continuar apontando para o objeto) e só cria se não existir.
    private async Task<string> GarantirTagQrAsync(TipoEntidadeVinculada tipo, Guid objetoId, CancellationToken ct)
    {
        var existentes = await _db.TagsIdentificacao
            .Where(t => t.Tipo == TipoTag.QrCode
                && t.Status != StatusTag.Desativada
                && t.EntidadeVinculadaTipo == tipo
                && t.EntidadeVinculadaId == objetoId)
            .ToListAsync(ct);

        var tag = existentes
            .OrderByDescending(t => t.Status == StatusTag.Vinculada)
            .ThenBy(t => t.Uid, StringComparer.Ordinal)
            .FirstOrDefault();
        if (tag is not null) return tag.Uid;

        tag = new TagIdentificacao
        {
            Uid = await GerarUidUnicoAsync(PrefixoUid(tipo), ct),
            Tipo = TipoTag.QrCode,
            Status = StatusTag.Vinculada,
            EntidadeVinculadaTipo = tipo,
            EntidadeVinculadaId = objetoId,
        };
        _db.TagsIdentificacao.Add(tag);
        await _db.SaveChangesAsync(ct);
        return tag.Uid;
    }

    private static string PrefixoUid(TipoEntidadeVinculada tipo) => tipo switch
    {
        TipoEntidadeVinculada.Veiculo => "VEI",
        TipoEntidadeVinculada.Alojamento => "ALO",
        TipoEntidadeVinculada.Inspecao => "INS",
        _ => "ITM",
    };

    private async Task<string> GerarUidUnicoAsync(string prefixo, CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            var uid = $"{prefixo}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
            if (!await _db.TagsIdentificacao.IgnoreQueryFilters().AnyAsync(t => t.Uid == uid, ct))
                return uid;
        }

        throw new InvalidOperationException("Não foi possível gerar um UID único para o QR Code do adesivo.");
    }
}
