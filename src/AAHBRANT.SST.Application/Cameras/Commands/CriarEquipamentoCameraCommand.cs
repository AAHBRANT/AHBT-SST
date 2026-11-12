using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// A senha é opcional aqui e pode ser definida (ou trocada) depois por DefinirSenhaEquipamentoCameraCommand. Câmera IP
// (fonte Rtsp) só pode ser ativada com a senha definida. A senha nunca é impressa em log (ToString sobrescrito).
public record CriarEquipamentoCameraCommand(
    Guid ObraId,
    Guid AgenteCameraId,
    string Nome,
    string Host,
    int PortaRtsp = 554,
    int PortaOnvif = 8899,
    string? EnderecoMac = null,
    string? Usuario = null,
    string? Senha = null,
    string? Fabricante = null,
    string? Modelo = null) : IRequest<Guid>
{
    public override string ToString() => $"{nameof(CriarEquipamentoCameraCommand)} {{ ObraId = {ObraId}, Nome = {Nome} }}";
}

public class CriarEquipamentoCameraCommandValidator : AbstractValidator<CriarEquipamentoCameraCommand>
{
    public CriarEquipamentoCameraCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.AgenteCameraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Host).HostDeRede();
        RuleFor(x => x.PortaRtsp).PortaDeRede("RTSP");
        RuleFor(x => x.PortaOnvif).PortaDeRede("ONVIF");
        RuleFor(x => x.EnderecoMac).MacValido();
        RuleFor(x => x.Usuario).MaximumLength(100);
        RuleFor(x => x.Senha).MaximumLength(EquipamentoCameraRegras.TamanhoMaximoSenha)
            .WithMessage($"A senha pode ter no máximo {EquipamentoCameraRegras.TamanhoMaximoSenha} caracteres.");
        RuleFor(x => x.Fabricante).MaximumLength(100);
        RuleFor(x => x.Modelo).MaximumLength(100);
    }
}

public class CriarEquipamentoCameraCommandHandler : IRequestHandler<CriarEquipamentoCameraCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICredencialCameraCriptografia _cripto;

    public CriarEquipamentoCameraCommandHandler(IAppDbContext db, ICredencialCameraCriptografia cripto)
    {
        _db = db;
        _cripto = cripto;
    }

    public async Task<Guid> Handle(CriarEquipamentoCameraCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        await EquipamentoCameraValidacoes.ExigirAgenteDaObraAsync(_db, request.AgenteCameraId, request.ObraId, ct);

        var nome = request.Nome.Trim();
        if (await _db.EquipamentosCamera.AnyAsync(e => e.ObraId == request.ObraId && e.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe um equipamento chamado \"{nome}\" nesta obra.");

        EquipamentoCameraRegras.TentarNormalizarMac(request.EnderecoMac, out var mac);
        var equipamento = new EquipamentoCamera
        {
            ObraId = request.ObraId,
            AgenteCameraId = request.AgenteCameraId,
            Nome = nome,
            Host = request.Host.Trim(),
            PortaRtsp = request.PortaRtsp,
            PortaOnvif = request.PortaOnvif,
            EnderecoMac = mac,
            Usuario = EquipamentoCameraRegras.LimparOpcional(request.Usuario),
            SenhaCifrada = string.IsNullOrEmpty(request.Senha) ? null : _cripto.Cifrar(request.Senha),
            Fabricante = EquipamentoCameraRegras.LimparOpcional(request.Fabricante),
            Modelo = EquipamentoCameraRegras.LimparOpcional(request.Modelo),
        };
        _db.EquipamentosCamera.Add(equipamento);
        await _db.SaveChangesAsync(ct);
        return equipamento.Id;
    }
}

// Verificações que equipamento, agente e câmera repetem para garantir que são da mesma obra.
internal static class EquipamentoCameraValidacoes
{
    public static async Task ExigirAgenteDaObraAsync(IAppDbContext db, Guid agenteId, Guid obraId, CancellationToken ct)
    {
        var agente = await db.AgentesCamera.AsNoTracking().Where(a => a.Id == agenteId)
            .Select(a => new { a.ObraId, a.Ativo }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Agente {agenteId} não encontrado.");

        if (!agente.Ativo)
            throw new InvalidOperationException("Este agente foi revogado. Escolha outro agente da obra.");
        if (agente.ObraId != obraId)
            throw new InvalidOperationException("O agente escolhido é de outra obra.");
    }
}
