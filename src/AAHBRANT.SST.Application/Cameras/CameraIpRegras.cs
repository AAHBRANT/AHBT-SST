using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras;

// Regras da câmera IP (fonte Rtsp) compartilhadas entre criar e editar a câmera.
public static class CameraIpRegras
{
    public const int IntervaloEnvioMinimoSegundos = 1;
    public const int IntervaloEnvioMaximoSegundos = 600;
    public const int IntervaloEnvioPadraoSegundos = 3;

    public static IRuleBuilderOptions<T, int> IntervaloDeEnvio<T>(this IRuleBuilder<T, int> regra)
        => regra.InclusiveBetween(IntervaloEnvioMinimoSegundos, IntervaloEnvioMaximoSegundos)
            .WithMessage($"O intervalo de envio precisa estar entre {IntervaloEnvioMinimoSegundos} e {IntervaloEnvioMaximoSegundos} segundos.");

    // Equipamento da câmera IP: existe (no escopo do usuário), é da mesma obra da câmera e, para a câmera ligada, o agente dele
    // é da mesma obra e está ativo e a senha já foi definida (decisão do produto: câmera IP sem senha não é ativada).
    // Só lê o indicador de senha: a senha em si nunca é carregada aqui.
    public static async Task ExigirEquipamentoUsavelAsync(
        IAppDbContext db, Guid equipamentoId, Guid obraId, bool habilitada, CancellationToken ct)
    {
        var equipamento = await db.EquipamentosCamera.AsNoTracking().Where(e => e.Id == equipamentoId)
            .Select(e => new { e.ObraId, e.AgenteCameraId, SenhaDefinida = e.SenhaCifrada != null && e.SenhaCifrada != string.Empty })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Equipamento não encontrado.");

        if (equipamento.ObraId != obraId)
            throw new InvalidOperationException("O equipamento escolhido é de outra obra.");

        var agente = await db.AgentesCamera.AsNoTracking().Where(a => a.Id == equipamento.AgenteCameraId)
            .Select(a => new { a.ObraId, a.Ativo }).FirstOrDefaultAsync(ct);
        if (agente is null || agente.ObraId != obraId)
            throw new InvalidOperationException("O agente do equipamento escolhido é de outra obra.");

        if (!habilitada) return;

        if (!equipamento.SenhaDefinida)
            throw new InvalidOperationException("Defina a senha do equipamento antes de ativar a câmera IP.");
        if (!agente.Ativo)
            throw new InvalidOperationException("O agente deste equipamento foi revogado. Escolha outro agente no equipamento antes de ativar a câmera.");
    }

    // Cada lente (equipamento + canal) tem uma única câmera em uso: duas câmeras na mesma lente enviariam o mesmo quadro duas vezes.
    public static async Task ExigirLenteLivreAsync(
        IAppDbContext db, Guid equipamentoId, int canal, Guid? cameraAtualId, CancellationToken ct)
    {
        var ocupada = await db.Cameras.AnyAsync(c =>
            c.EquipamentoCameraId == equipamentoId && c.Canal == canal && (cameraAtualId == null || c.Id != cameraAtualId), ct);
        if (ocupada)
            throw new InvalidOperationException($"Já existe uma câmera para o canal {canal} deste equipamento.");
    }
}
