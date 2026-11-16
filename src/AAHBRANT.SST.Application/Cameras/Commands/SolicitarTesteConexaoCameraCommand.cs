using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// "Testar conexão": o servidor não alcança a câmera (ela está na rede local da obra), então isto só cria um PEDIDO que o
// agente da obra recebe no próximo contato (junto da configuração). Informe a câmera já cadastrada OU o equipamento com
// canal e stream (para testar antes de salvar a câmera). Devolve o id do teste, que o front consulta depois.
public record SolicitarTesteConexaoCameraCommand(
    Guid? CameraId = null,
    Guid? EquipamentoCameraId = null,
    int? Canal = null,
    int? Stream = null,
    // Metade da imagem a recortar na miniatura (só ao testar pelo equipamento, antes de salvar a câmera; pela câmera vale a dela).
    ParteImagemCamera? ParteImagem = null) : IRequest<Guid>;

public class SolicitarTesteConexaoCameraCommandValidator : AbstractValidator<SolicitarTesteConexaoCameraCommand>
{
    public SolicitarTesteConexaoCameraCommandValidator()
    {
        RuleFor(x => x).Must(x => (x.CameraId is not null) != (x.EquipamentoCameraId is not null))
            .WithMessage("Informe a câmera ou o equipamento a testar (um dos dois).");
        When(x => x.Canal is not null, () => RuleFor(x => x.Canal).Must(c => c is 1 or 2).WithMessage("O canal (lente) precisa ser 1 ou 2."));
        When(x => x.Stream is not null, () => RuleFor(x => x.Stream).Must(s => s is 0 or 1).WithMessage("A qualidade precisa ser principal (0) ou secundária (1)."));
        RuleFor(x => x.ParteImagem).ParteDaImagem();
    }
}

public class SolicitarTesteConexaoCameraCommandHandler : IRequestHandler<SolicitarTesteConexaoCameraCommand, Guid>
{
    // Passado este tempo sem resposta do agente, o pedido expira e o front mostra "sem resposta".
    public static readonly TimeSpan ValidadeDoPedido = TimeSpan.FromMinutes(2);

    // A miniatura do teste existe só para o técnico ver a imagem uma vez; se ninguém a buscou, é apagada depois disto.
    public static readonly TimeSpan RetencaoDaMiniatura = TimeSpan.FromMinutes(10);

    private readonly IAppDbContext _db;
    public SolicitarTesteConexaoCameraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(SolicitarTesteConexaoCameraCommand request, CancellationToken ct)
    {
        Guid equipamentoId;
        Guid? cameraId = null;
        int canal;
        int stream;
        ParteImagemCamera parte;
        if (request.CameraId is { } idDaCamera)
        {
            var camera = await _db.Cameras.AsNoTracking().Where(c => c.Id == idDaCamera)
                .Select(c => new { c.Id, c.Fonte, c.EquipamentoCameraId, c.Canal, c.Stream, c.ParteImagem }).FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException($"Câmera {idDaCamera} não encontrada.");
            if (camera.EquipamentoCameraId is not { } eq || camera.Canal is not { } cn)
                throw new InvalidOperationException("Esta câmera não é uma câmera IP: não há conexão a testar.");
            equipamentoId = eq;
            cameraId = camera.Id;
            canal = cn;
            stream = camera.Stream ?? 0;
            parte = camera.ParteImagem;
        }
        else
        {
            equipamentoId = request.EquipamentoCameraId!.Value;
            canal = request.Canal ?? 1;
            stream = request.Stream ?? 0;
            parte = request.ParteImagem ?? ParteImagemCamera.Inteira;
        }

        var equipamento = await _db.EquipamentosCamera.AsNoTracking().Where(e => e.Id == equipamentoId)
            .Select(e => new { e.Id, e.ObraId, e.AgenteCameraId }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Equipamento não encontrado.");

        var agente = await _db.AgentesCamera.AsNoTracking().Where(a => a.Id == equipamento.AgenteCameraId)
            .Select(a => new { a.ObraId, a.Ativo }).FirstOrDefaultAsync(ct);
        if (agente is null || agente.ObraId != equipamento.ObraId)
            throw new InvalidOperationException("O agente do equipamento é de outra obra.");
        if (!agente.Ativo)
            throw new InvalidOperationException("O agente deste equipamento foi revogado. Escolha outro agente no equipamento.");

        var agora = DateTime.UtcNow;
        await MiniaturasTesteConexao.LimparAntigasAsync(_db, equipamento.AgenteCameraId, agora, ct);

        // Pedido igual ainda em aberto: reaproveita (duplo clique não empilha testes).
        var limite = agora - ValidadeDoPedido;
        var emAberto = await _db.TestesConexaoCamera.AsNoTracking()
            .Where(t => t.AgenteCameraId == equipamento.AgenteCameraId && t.EquipamentoCameraId == equipamento.Id
                && t.Canal == canal && t.Stream == stream && t.ParteImagem == parte && t.ConcluidoEmUtc == null && t.SolicitadoEmUtc >= limite)
            .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (emAberto is { } existente) return existente;

        var teste = new TesteConexaoCamera
        {
            ObraId = equipamento.ObraId,
            AgenteCameraId = equipamento.AgenteCameraId,
            EquipamentoCameraId = equipamento.Id,
            CameraId = cameraId,
            Canal = canal,
            Stream = stream,
            ParteImagem = parte,
            SolicitadoEmUtc = agora,
        };
        _db.TestesConexaoCamera.Add(teste);
        await _db.SaveChangesAsync(ct);
        return teste.Id;
    }
}
