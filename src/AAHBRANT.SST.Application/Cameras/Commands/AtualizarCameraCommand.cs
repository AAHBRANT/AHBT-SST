using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// A obra não muda na edição: as ocorrências já gravadas carregam o ObraId da câmera.
public record AtualizarCameraCommand(
    Guid Id,
    string Nome,
    Guid? AreaSstId,
    TipoCamera Tipo,
    bool Habilitada,
    int IntervaloMinimoAlertasMinutos,
    // Fonte nula = não mexe na origem da imagem (nem em equipamento, canal, stream e intervalo): quem edita só o nome ou a
    // área não precisa reenviar nada. Fonte informada = os campos da câmera IP passam a valer como enviados.
    FonteCamera? Fonte = null,
    Guid? EquipamentoCameraId = null,
    int? Canal = null,
    int? Stream = null,
    int? IntervaloEnvioSegundos = null,
    // Nulos = não mexe (a tela antiga não envia estes campos e não pode desfazer a escolha já feita).
    ParteImagemCamera? ParteImagem = null,
    bool? AvisarTelegram = null) : IRequest;

public class AtualizarCameraCommandValidator : AbstractValidator<AtualizarCameraCommand>
{
    public AtualizarCameraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.IntervaloMinimoAlertasMinutos).InclusiveBetween(0, 1440);
        RuleFor(x => x.Fonte).IsInEnum();
        When(x => x.IntervaloEnvioSegundos is not null, () => RuleFor(x => x.IntervaloEnvioSegundos!.Value).IntervaloDeEnvio());
        RuleFor(x => x.ParteImagem).ParteDaImagem();
        When(x => x.Fonte == FonteCamera.Navegador && x.ParteImagem is { } p && Enum.IsDefined(p), () =>
            RuleFor(x => x.ParteImagem).Must(p => p == ParteImagemCamera.Inteira).WithMessage(CameraIpRegras.MensagemParteSoEmCameraIp));
        When(x => x.Fonte == FonteCamera.Rtsp, () =>
        {
            RuleFor(x => x.EquipamentoCameraId).Must(id => id is { } g && g != Guid.Empty).WithMessage("Escolha o equipamento da câmera IP.");
            RuleFor(x => x.Canal).Must(c => c is 1 or 2).WithMessage("O canal (lente) precisa ser 1 ou 2.");
            RuleFor(x => x.Stream).Must(s => s is 0 or 1).WithMessage("A qualidade precisa ser principal (0) ou secundária (1).");
        });
    }
}

public class AtualizarCameraCommandHandler : IRequestHandler<AtualizarCameraCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarCameraCommand request, CancellationToken ct)
    {
        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Câmera {request.Id} não encontrada.");

        if (request.AreaSstId is { } areaId && !await _db.AreasSst.AnyAsync(a => a.Id == areaId && a.ObraId == camera.ObraId, ct))
            throw new KeyNotFoundException("Área não encontrada nesta obra.");

        var nome = request.Nome.Trim();
        if (await _db.Cameras.AnyAsync(c => c.Id != camera.Id && c.ObraId == camera.ObraId && c.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe uma câmera chamada \"{nome}\" nesta obra.");

        // Parte da imagem final: a enviada ou, sem envio, a já gravada. Só câmera IP pode ter parte diferente de "inteira".
        var fonteFinal = request.Fonte ?? camera.Fonte;
        var parteFinal = request.ParteImagem ?? camera.ParteImagem;
        if (fonteFinal != FonteCamera.Rtsp)
        {
            if (request.ParteImagem is { } pedida && pedida != ParteImagemCamera.Inteira)
                throw new InvalidOperationException(CameraIpRegras.MensagemParteSoEmCameraIp);
            parteFinal = ParteImagemCamera.Inteira;
        }

        if (request.Fonte is { } fonte)
        {
            var ip = fonte == FonteCamera.Rtsp;
            var mudouDeOrigem = camera.Fonte != fonte || camera.EquipamentoCameraId != (ip ? request.EquipamentoCameraId : null)
                || camera.Canal != (ip ? request.Canal : null) || camera.ParteImagem != parteFinal;
            if (ip) await CameraIpRegras.ExigirLenteLivreAsync(_db, request.EquipamentoCameraId!.Value, request.Canal!.Value, parteFinal, camera.Id, ct);

            camera.Fonte = fonte;
            camera.EquipamentoCameraId = ip ? request.EquipamentoCameraId : null;
            camera.Canal = ip ? request.Canal : null;
            camera.Stream = ip ? request.Stream : null;
            camera.ParteImagem = parteFinal;
            if (mudouDeOrigem)
            {
                camera.SituacaoConexao = SituacaoConexaoCamera.Desconhecida;
                camera.MensagemConexao = null;
                camera.UltimoQuadroRecebidoEmUtc = null;
            }
        }
        else if (camera.ParteImagem != parteFinal)
        {
            // Só a parte da imagem mudou (Fonte omitida): a lente continua a mesma, mas a parte não pode colidir com outra câmera.
            if (camera.Fonte == FonteCamera.Rtsp && camera.EquipamentoCameraId is { } eqId && camera.Canal is { } canalAtual)
                await CameraIpRegras.ExigirLenteLivreAsync(_db, eqId, canalAtual, parteFinal, camera.Id, ct);
            camera.ParteImagem = parteFinal;
            camera.SituacaoConexao = SituacaoConexaoCamera.Desconhecida;
            camera.MensagemConexao = null;
            camera.UltimoQuadroRecebidoEmUtc = null;
        }
        if (request.AvisarTelegram is { } avisar) camera.AvisarTelegram = avisar;
        if (request.IntervaloEnvioSegundos is { } intervaloEnvio) camera.IntervaloEnvioSegundos = intervaloEnvio;

        // Vale também para quem só ligou a câmera (Habilitada) sem mexer na origem: câmera IP ligada exige equipamento com senha.
        if (camera.Fonte == FonteCamera.Rtsp)
        {
            if (camera.EquipamentoCameraId is not { } equipamentoId)
                throw new InvalidOperationException("Escolha o equipamento da câmera IP.");
            await CameraIpRegras.ExigirEquipamentoUsavelAsync(_db, equipamentoId, camera.ObraId, request.Habilitada, ct);
        }

        camera.Nome = nome;
        camera.AreaSstId = request.AreaSstId;
        camera.Tipo = request.Tipo;
        camera.Habilitada = request.Habilitada;
        camera.IntervaloMinimoAlertasMinutos = request.IntervaloMinimoAlertasMinutos;
        await _db.SaveChangesAsync(ct);
    }
}
