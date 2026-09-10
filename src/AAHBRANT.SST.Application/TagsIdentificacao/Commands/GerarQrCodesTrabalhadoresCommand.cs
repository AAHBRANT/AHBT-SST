using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

public record GerarQrCodesTrabalhadoresCommand(Guid? ObraId = null) : IRequest<List<QrCodeTrabalhadorDto>>;

public class GerarQrCodesTrabalhadoresCommandHandler : IRequestHandler<GerarQrCodesTrabalhadoresCommand, List<QrCodeTrabalhadorDto>>
{
    private readonly IAppDbContext _db;
    private readonly IQrCodePerfilPublicoService _qrCodePerfil;

    public GerarQrCodesTrabalhadoresCommandHandler(IAppDbContext db, IQrCodePerfilPublicoService qrCodePerfil)
    {
        _db = db;
        _qrCodePerfil = qrCodePerfil;
    }

    public async Task<List<QrCodeTrabalhadorDto>> Handle(GerarQrCodesTrabalhadoresCommand request, CancellationToken ct)
    {
        var trabalhadoresQuery = _db.Trabalhadores
            .Include(t => t.Obra)
            .Where(t => t.DataDemissao == null);

        if (request.ObraId.HasValue)
            trabalhadoresQuery = trabalhadoresQuery.Where(t => t.ObraId == request.ObraId.Value);

        var trabalhadores = await trabalhadoresQuery
            .OrderBy(t => t.Nome)
            .ToListAsync(ct);

        var trabalhadorIds = trabalhadores.Select(t => t.Id).ToList();
        var tagsExistentesLista = await _db.TagsIdentificacao
            .Where(t =>
                t.Tipo == TipoTag.QrCode &&
                t.Status != StatusTag.Desativada &&
                t.EntidadeVinculadaTipo == TipoEntidadeVinculada.Trabalhador &&
                t.EntidadeVinculadaId.HasValue &&
                trabalhadorIds.Contains(t.EntidadeVinculadaId.Value))
            .ToListAsync(ct);

        var tagsExistentes = tagsExistentesLista
            .GroupBy(t => t.EntidadeVinculadaId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(t => t.Status == StatusTag.Vinculada)
                    .ThenBy(t => t.Uid)
                    .First());

        var resultado = new List<QrCodeTrabalhadorDto>();

        foreach (var trabalhador in trabalhadores)
        {
            var jaExistia = tagsExistentes.TryGetValue(trabalhador.Id, out var tag);
            if (tag is null)
            {
                tag = new TagIdentificacao
                {
                    Uid = await GerarUidUnico(ct),
                    Tipo = TipoTag.QrCode,
                    Status = StatusTag.Vinculada,
                    EntidadeVinculadaTipo = TipoEntidadeVinculada.Trabalhador,
                    EntidadeVinculadaId = trabalhador.Id
                };
                _db.TagsIdentificacao.Add(tag);
            }

            resultado.Add(new QrCodeTrabalhadorDto
            {
                TrabalhadorId = trabalhador.Id,
                TrabalhadorNome = trabalhador.Nome,
                Matricula = trabalhador.Matricula ?? string.Empty,
                ObraId = trabalhador.ObraId,
                ObraNome = trabalhador.Obra?.Nome ?? string.Empty,
                TagId = tag.Id,
                Uid = tag.Uid,
                UrlPerfil = _qrCodePerfil.MontarUrl(tag.Uid),
                JaExistia = jaExistia
            });
        }

        await _db.SaveChangesAsync(ct);
        return resultado;
    }

    private async Task<string> GerarUidUnico(CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            var uid = $"TRB-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
            if (!await _db.TagsIdentificacao.AnyAsync(t => t.Uid == uid, ct))
                return uid;
        }

        throw new InvalidOperationException("Não foi possível gerar um UID único para o QR Code do funcionário.");
    }
}
