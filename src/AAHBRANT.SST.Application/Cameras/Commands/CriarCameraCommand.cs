using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

public record CriarCameraCommand(
    Guid ObraId,
    string Nome,
    Guid? AreaSstId,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos,
    // Câmera IP lida pelo agente da obra (Fonte = Rtsp): equipamento, lente (canal 1 ou 2) e qualidade (stream 0 = principal,
    // 1 = secundário). Omitidos = câmera do navegador, como sempre foi.
    FonteCamera Fonte = FonteCamera.Navegador,
    Guid? EquipamentoCameraId = null,
    int? Canal = null,
    int? Stream = null,
    int IntervaloEnvioSegundos = CameraIpRegras.IntervaloEnvioPadraoSegundos,
    // Câmera IP de duas lentes empilhadas: metade da imagem analisada (só vale para Fonte = Rtsp). AvisarTelegram = false
    // mantém a análise e a ocorrência, mas não envia o alerta ao Telegram. Omitidos = imagem inteira e com aviso, como sempre foi.
    ParteImagemCamera ParteImagem = ParteImagemCamera.Inteira,
    bool AvisarTelegram = true) : IRequest<Guid>;

public class CriarCameraCommandValidator : AbstractValidator<CriarCameraCommand>
{
    public CriarCameraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.IntervaloMinimoAlertasMinutos).InclusiveBetween(0, 1440);
        RuleFor(x => x.Fonte).IsInEnum();
        RuleFor(x => x.IntervaloEnvioSegundos).IntervaloDeEnvio();
        RuleFor(x => x.ParteImagem).ParteDaImagem();
        When(x => x.Fonte != FonteCamera.Rtsp && Enum.IsDefined(x.ParteImagem), () =>
            RuleFor(x => x.ParteImagem).Equal(ParteImagemCamera.Inteira).WithMessage(CameraIpRegras.MensagemParteSoEmCameraIp));
        When(x => x.Fonte == FonteCamera.Rtsp, () =>
        {
            RuleFor(x => x.EquipamentoCameraId).Must(id => id is { } g && g != Guid.Empty).WithMessage("Escolha o equipamento da câmera IP.");
            RuleFor(x => x.Canal).Must(c => c is 1 or 2).WithMessage("O canal (lente) precisa ser 1 ou 2.");
            RuleFor(x => x.Stream).Must(s => s is 0 or 1).WithMessage("A qualidade precisa ser principal (0) ou secundária (1).");
        });
    }
}

public class CriarCameraCommandHandler : IRequestHandler<CriarCameraCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarCameraCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.AreaSstId is { } areaId && !await _db.AreasSst.AnyAsync(a => a.Id == areaId && a.ObraId == request.ObraId, ct))
            throw new KeyNotFoundException("Área não encontrada nesta obra.");

        var nome = request.Nome.Trim();
        if (await _db.Cameras.AnyAsync(c => c.ObraId == request.ObraId && c.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe uma câmera chamada \"{nome}\" nesta obra.");

        var ip = request.Fonte == FonteCamera.Rtsp;
        if (ip)
        {
            await CameraIpRegras.ExigirEquipamentoUsavelAsync(_db, request.EquipamentoCameraId!.Value, request.ObraId, request.Habilitada, ct);
            await CameraIpRegras.ExigirLenteLivreAsync(_db, request.EquipamentoCameraId!.Value, request.Canal!.Value, request.ParteImagem, null, ct);
        }

        var camera = new Camera
        {
            ObraId = request.ObraId,
            Nome = nome,
            AreaSstId = request.AreaSstId,
            Tipo = request.Tipo,
            Habilitada = request.Habilitada,
            IntervaloMinimoAlertasMinutos = request.IntervaloMinimoAlertasMinutos,
            Fonte = request.Fonte,
            EquipamentoCameraId = ip ? request.EquipamentoCameraId : null,
            Canal = ip ? request.Canal : null,
            Stream = ip ? request.Stream : null,
            IntervaloEnvioSegundos = request.IntervaloEnvioSegundos,
            ParteImagem = ip ? request.ParteImagem : ParteImagemCamera.Inteira,
            AvisarTelegram = request.AvisarTelegram,
        };
        _db.Cameras.Add(camera);
        await _db.SaveChangesAsync(ct);
        return camera.Id;
    }
}
