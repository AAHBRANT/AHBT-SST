using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Presença por biometria (item 3 da proposta) — mesmo mecanismo já usado no DDS
// (RegistrarParticipanteCommand): reaproveita IAutenticacaoBiometriaLocalService, o match 1:N já
// aconteceu no agente local (leitor Futronic FS80H). Diferente do DDS, o participante já existe
// (pré-selecionado na criação da turma) — aqui só se confirma a presença dele, não se cria a linha.
public record RegistrarPresencaSessaoTreinamentoCommand(
    Guid SessaoTreinamentoId,
    Guid TrabalhadorId,
    Guid DispositivoId,
    string SegredoDispositivo,
    double Score,
    string? Desafio = null,
    string? AssinaturaLeitura = null,
    // 05/11: evidência da presença (imagem lida pelo leitor e localização), como na assinatura de EPI.
    byte[]? ImagemDigital = null,
    LocalizacaoAssinatura? Localizacao = null) : IRequest;

public class RegistrarPresencaSessaoTreinamentoCommandValidator : AbstractValidator<RegistrarPresencaSessaoTreinamentoCommand>
{
    public RegistrarPresencaSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        // Com leitura assinada pelo agente o segredo não trafega mais pelo navegador.
        RuleFor(x => x.SegredoDispositivo).NotEmpty().When(x => string.IsNullOrEmpty(x.Desafio));
        RuleFor(x => x.AssinaturaLeitura).NotEmpty().When(x => !string.IsNullOrEmpty(x.Desafio));
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarPresencaSessaoTreinamentoCommandHandler : IRequestHandler<RegistrarPresencaSessaoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IClienteIpProvider? _clienteIp;

    public RegistrarPresencaSessaoTreinamentoCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao,
        IClienteIpProvider? clienteIp = null)
    {
        _db = db;
        _autenticacao = autenticacao;
        _clienteIp = clienteIp;
    }

    public async Task Handle(RegistrarPresencaSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var participante = await _db.ParticipantesSessaoTreinamento
            .FirstOrDefaultAsync(p => p.SessaoTreinamentoId == request.SessaoTreinamentoId && p.TrabalhadorId == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Este trabalhador não está inscrito nesta turma.");

        if (participante.PresencaConfirmadaEm is not null)
            throw new InvalidOperationException("Presença já confirmada para este participante.");

        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score,
            LeituraDo(request.Desafio, request.AssinaturaLeitura), ct);

        participante.PresencaConfirmadaEm = DateTime.UtcNow;
        participante.ScoreConfianca = request.Score;
        participante.MetodoPresenca = MetodoAutenticacaoAssinatura.Biometria;
        EvidenciaPresencaTreinamento.Aplicar(participante, resultado, request.ImagemDigital, "image/png", request.Localizacao, _clienteIp);

        await _db.SaveChangesAsync(ct);
    }

    private static LeituraBiometricaAssinada? LeituraDo(string? desafio, string? assinatura) =>
        string.IsNullOrEmpty(desafio) ? null : new LeituraBiometricaAssinada(desafio, assinatura ?? string.Empty);
}
