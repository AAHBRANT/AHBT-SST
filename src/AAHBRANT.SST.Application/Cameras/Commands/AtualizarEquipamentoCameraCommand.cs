using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// A obra não muda na edição. A senha costuma ser trocada por DefinirSenhaEquipamentoCameraCommand; aqui ela é opcional e só
// vira obrigatória quando o endereço (Host) muda: apontar a mesma credencial guardada para outro endereço a enviaria a um
// aparelho que o técnico não conferiu. Senha vazia = não informada.
public record AtualizarEquipamentoCameraCommand(
    Guid Id,
    Guid AgenteCameraId,
    string Nome,
    string Host,
    int PortaRtsp = 554,
    int PortaOnvif = 8899,
    string? EnderecoMac = null,
    string? Usuario = null,
    string? Fabricante = null,
    string? Modelo = null,
    string? Senha = null) : IRequest
{
    public override string ToString() => $"{nameof(AtualizarEquipamentoCameraCommand)} {{ Id = {Id}, Host = {Host} }}";
}

public class AtualizarEquipamentoCameraCommandValidator : AbstractValidator<AtualizarEquipamentoCameraCommand>
{
    public AtualizarEquipamentoCameraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AgenteCameraId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Host).HostDeRede();
        RuleFor(x => x.PortaRtsp).PortaDeRede("RTSP");
        RuleFor(x => x.PortaOnvif).PortaDeRede("ONVIF");
        RuleFor(x => x.EnderecoMac).MacValido();
        RuleFor(x => x.Usuario).MaximumLength(100);
        RuleFor(x => x.Fabricante).MaximumLength(100);
        RuleFor(x => x.Modelo).MaximumLength(100);
        RuleFor(x => x.Senha).MaximumLength(EquipamentoCameraRegras.TamanhoMaximoSenha)
            .WithMessage($"A senha pode ter no máximo {EquipamentoCameraRegras.TamanhoMaximoSenha} caracteres.");
    }
}

public class AtualizarEquipamentoCameraCommandHandler : IRequestHandler<AtualizarEquipamentoCameraCommand>
{
    public const string MensagemSenhaAoTrocarEndereco = "Informe a senha novamente ao trocar o endereço da câmera.";

    private readonly IAppDbContext _db;
    private readonly ICredencialCameraCriptografia _cripto;

    public AtualizarEquipamentoCameraCommandHandler(IAppDbContext db, ICredencialCameraCriptografia cripto)
    {
        _db = db;
        _cripto = cripto;
    }

    public async Task Handle(AtualizarEquipamentoCameraCommand request, CancellationToken ct)
    {
        var equipamento = await _db.EquipamentosCamera.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Equipamento {request.Id} não encontrado.");

        await EquipamentoCameraValidacoes.ExigirAgenteDaObraAsync(_db, request.AgenteCameraId, equipamento.ObraId, ct);

        var nome = request.Nome.Trim();
        if (await _db.EquipamentosCamera.AnyAsync(e => e.Id != equipamento.Id && e.ObraId == equipamento.ObraId && e.Nome == nome, ct))
            throw new InvalidOperationException($"Já existe um equipamento chamado \"{nome}\" nesta obra.");

        var host = request.Host.Trim();
        var hostMudou = !string.Equals(host, equipamento.Host, StringComparison.OrdinalIgnoreCase);
        var senhaInformada = !string.IsNullOrWhiteSpace(request.Senha);
        if (hostMudou && !senhaInformada)
            throw new InvalidOperationException(MensagemSenhaAoTrocarEndereco);

        EquipamentoCameraRegras.TentarNormalizarMac(request.EnderecoMac, out var mac);
        equipamento.AgenteCameraId = request.AgenteCameraId;
        equipamento.Nome = nome;
        equipamento.Host = host;
        if (senhaInformada) equipamento.SenhaCifrada = _cripto.Cifrar(request.Senha!);
        equipamento.PortaRtsp = request.PortaRtsp;
        equipamento.PortaOnvif = request.PortaOnvif;
        equipamento.EnderecoMac = mac;
        equipamento.Usuario = EquipamentoCameraRegras.LimparOpcional(request.Usuario);
        equipamento.Fabricante = EquipamentoCameraRegras.LimparOpcional(request.Fabricante);
        equipamento.Modelo = EquipamentoCameraRegras.LimparOpcional(request.Modelo);
        await _db.SaveChangesAsync(ct);
    }
}
