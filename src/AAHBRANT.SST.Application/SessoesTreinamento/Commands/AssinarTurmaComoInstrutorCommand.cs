using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// O instrutor da turma assina, com a digital dele, todos os certificados dela de uma vez (pedido do
// usuário, 28/10). Antes a "assinatura de instrutor" era a de quem estivesse logado ao encerrar a turma
// (ex.: o técnico da obra), não a do instrutor.
//
// Uma leitura de digital só (o desafio do leitor é de uso único): autentica uma vez e aplica o mesmo
// resultado a cada certificado. O instrutor pode ser de outra obra; isso só vale para quem tem perfil de
// instrutor autorizado a assinar em outras obras (ver FutronicAutenticacaoStrategy).
//
// Certificados de turma já encerrada saem finalizados (hash, PDF e QR). Para o instrutor assinar, o
// documento é reaberto, assinado e finalizado de novo; o token de validação pública não muda, então o QR
// já impresso continua valendo. A reabertura fica na auditoria com o hash anterior.
public record AssinarTurmaComoInstrutorCommand(
    Guid SessaoTreinamentoId,
    Guid DispositivoId,
    string SegredoDispositivo,
    Guid TrabalhadorId,
    double Score,
    string? Desafio = null,
    string? AssinaturaLeitura = null,
    string? IpAddress = null,
    byte[]? ImagemDigital = null,
    LocalizacaoAssinatura? Localizacao = null) : IRequest<ResultadoAssinaturaInstrutor>;

public record ResultadoAssinaturaInstrutor(int Assinados, int JaAssinados, int Falhas, string? PrimeiraFalha);

public class AssinarTurmaComoInstrutorCommandValidator : AbstractValidator<AssinarTurmaComoInstrutorCommand>
{
    public AssinarTurmaComoInstrutorCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty().When(x => string.IsNullOrEmpty(x.Desafio));
        RuleFor(x => x.AssinaturaLeitura).NotEmpty().When(x => !string.IsNullOrEmpty(x.Desafio));
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class AssinarTurmaComoInstrutorCommandHandler : IRequestHandler<AssinarTurmaComoInstrutorCommand, ResultadoAssinaturaInstrutor>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IRegistradorAssinaturaService _registrador;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<AssinarTurmaComoInstrutorCommandHandler> _logger;

    public AssinarTurmaComoInstrutorCommandHandler(
        IAppDbContext db, IMediator mediator, IAutenticacaoBiometriaLocalService autenticacao,
        IRegistradorAssinaturaService registrador, IAuditoriaService auditoria, ILogger<AssinarTurmaComoInstrutorCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _autenticacao = autenticacao;
        _registrador = registrador;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<ResultadoAssinaturaInstrutor> Handle(AssinarTurmaComoInstrutorCommand request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Participantes)
            .FirstOrDefaultAsync(s => s.Id == request.SessaoTreinamentoId, ct)
            ?? throw new KeyNotFoundException("Turma não encontrada.");

        if (sessao.Status != StatusSessaoTreinamento.Concluida)
            throw new InvalidOperationException("Os certificados só existem depois de encerrar a turma.");
        if (sessao.InstrutorTrabalhadorId is null)
            throw new InvalidOperationException("Escolha o instrutor da turma antes de assinar.");
        // A digital lida precisa ser a do instrutor escolhido: sem isso, qualquer perfil de instrutor
        // assinaria no lugar de outro.
        if (sessao.InstrutorTrabalhadorId != request.TrabalhadorId)
            throw new InvalidOperationException("A digital lida não é a do instrutor desta turma.");

        var certificados = sessao.Participantes
            .Where(p => p.TreinamentoGeradoId is not null)
            .Select(p => p.TreinamentoGeradoId!.Value)
            .ToList();
        if (certificados.Count == 0)
            throw new InvalidOperationException("Esta turma não tem certificados gerados.");

        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score,
            string.IsNullOrEmpty(request.Desafio) ? null : new LeituraBiometricaAssinada(request.Desafio, request.AssinaturaLeitura ?? string.Empty),
            ct, permitirInstrutorDeOutraObra: true);

        int assinados = 0, jaAssinados = 0, falhas = 0;
        string? primeiraFalha = null;

        foreach (var treinamentoId in certificados)
        {
            try
            {
                var documento = await _db.DocumentosAssinatura
                    .Where(d => d.EntidadeTipo == nameof(Treinamento) && d.EntidadeId == treinamentoId
                        && d.Status != StatusDocumentoAssinatura.Cancelado)
                    .OrderByDescending(d => d.CreatedAtUtc)
                    .FirstOrDefaultAsync(ct);
                if (documento is null)
                {
                    var novoId = await _mediator.Send(new CriarDocumentoAssinaturaCommand(nameof(Treinamento), treinamentoId), ct);
                    documento = await _db.DocumentosAssinatura.FirstAsync(d => d.Id == novoId, ct);
                }

                if (await _db.DocumentoSignatarios.AnyAsync(s => s.DocumentoAssinaturaId == documento.Id
                        && s.TrabalhadorId == request.TrabalhadorId && s.Papel == PapelAssinatura.Instrutor, ct))
                {
                    jaAssinados++;
                    continue;
                }

                if (documento.Status == StatusDocumentoAssinatura.Finalizado)
                {
                    var hashAnterior = documento.ConteudoHash;
                    documento.Status = StatusDocumentoAssinatura.EmAndamento;
                    await _auditoria.RegistrarAsync(
                        "Documento.ReabertoParaInstrutor", documento.EntidadeTipo, documento.EntidadeId,
                        usuarioId: null, trabalhadorId: request.TrabalhadorId,
                        dadosDepois: new { DocumentoAssinaturaId = documento.Id, ConteudoHashAnterior = hashAnterior }, ct);
                    await _db.SaveChangesAsync(ct);
                }

                await _registrador.RegistrarAsync(
                    documento.Id, resultado, request.IpAddress, ct,
                    request.ImagemDigital, request.ImagemDigital is null ? null : "image/png",
                    papel: PapelAssinatura.Instrutor, localizacao: request.Localizacao);
                assinados++;

                await FinalizacaoAutomaticaDocumento.TentarAsync(_db, _mediator, _logger, documento.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                primeiraFalha ??= ex.Message;
                _logger.LogWarning(ex, "Falha ao registrar a assinatura do instrutor no certificado {TreinamentoId} da turma {SessaoId}.", treinamentoId, sessao.Id);
            }
        }

        return new ResultadoAssinaturaInstrutor(assinados, jaAssinados, falhas, primeiraFalha);
    }
}
