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

// Encerramento da turma (pedido do usuário: 3 fotos obrigatórias liberam o botão) — gera 1
// Treinamento (e, portanto, 1 certificado) para cada participante que confirmou presença por
// biometria. Quem não confirmou fica registrado na turma como ausente, sem certificado — decisão
// assumida para não inventar uma regra de "falta justificada" que não foi pedida.
//
// Certificado já sai assinado (04/09, pedido do usuário: "não dá certo assinar duas vezes") — a
// digital capturada na presença (ver RegistrarPresencaSessaoTreinamentoCommand) já autenticou o
// trabalhador; aqui, ao gerar o certificado, essa MESMA autenticação vira a assinatura dele no
// documento, sem pedir uma segunda leitura. A assinatura do instrutor/responsável (sessão logada,
// já era 1 clique) também é automática, usando quem está logado encerrando a turma. As duas são
// melhor esforço: se falharem por qualquer motivo (ex.: usuário não vinculado a um Trabalhador), o
// certificado é gerado do mesmo jeito, sem assinatura — a tela de "Assinar" no detalhe da turma
// continua disponível como reforço/correção manual.
public record EncerrarSessaoTreinamentoCommand(Guid Id, string? AzureAdObjectId = null) : IRequest;

public class EncerrarSessaoTreinamentoCommandValidator : AbstractValidator<EncerrarSessaoTreinamentoCommand>
{
    public EncerrarSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarSessaoTreinamentoCommandHandler : IRequestHandler<EncerrarSessaoTreinamentoCommand>
{
    private const int TotalFotosObrigatorias = 3;
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IRegistradorAssinaturaService _registrador;
    private readonly ILogger<EncerrarSessaoTreinamentoCommandHandler> _logger;

    public EncerrarSessaoTreinamentoCommandHandler(
        IAppDbContext db,
        IMediator mediator,
        IRegistradorAssinaturaService registrador,
        ILogger<EncerrarSessaoTreinamentoCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _registrador = registrador;
        _logger = logger;
    }

    public async Task Handle(EncerrarSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Turma de treinamento {request.Id} não encontrada.");

        if (sessao.Status == StatusSessaoTreinamento.Concluida)
            throw new InvalidOperationException("Esta turma já foi encerrada.");

        var totalFotos = await _db.FotosEvidenciaSessaoTreinamento.CountAsync(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo, ct);
        if (totalFotos < TotalFotosObrigatorias)
            throw new InvalidOperationException(
                $"São necessárias {TotalFotosObrigatorias} fotos de evidência para encerrar a turma (faltam {TotalFotosObrigatorias - totalFotos}).");

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .ToListAsync(ct);

        var validadeEmMeses = sessao.CursoTreinamento!.ValidadeEmMeses;
        var geradosParaAssinar = new List<(Guid TreinamentoId, Guid TrabalhadorId)>();
        foreach (var participante in participantes.Where(p => p.PresencaConfirmadaEm is not null && p.TreinamentoGeradoId is null))
        {
            var treinamento = new Treinamento
            {
                TrabalhadorId = participante.TrabalhadorId,
                CursoTreinamentoId = sessao.CursoTreinamentoId,
                DataRealizacao = sessao.DataRealizacao,
                DataValidade = sessao.DataRealizacao.AddMonths(validadeEmMeses),
                CargaHorariaRealizada = sessao.CargaHorariaRealizada,
                InstituicaoInstrutor = sessao.InstituicaoInstrutor,
                NumeroCertificado = sessao.NumeroCertificado,
                SessaoTreinamentoId = sessao.Id,
            };
            _db.Treinamentos.Add(treinamento);
            participante.TreinamentoGeradoId = treinamento.Id;
            geradosParaAssinar.Add((treinamento.Id, participante.TrabalhadorId));
        }

        sessao.Status = StatusSessaoTreinamento.Concluida;
        sessao.DataEncerramento = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        foreach (var (treinamentoId, trabalhadorId) in geradosParaAssinar)
        {
            Guid? documentoId = null;
            try
            {
                documentoId = await _mediator.Send(new CriarDocumentoAssinaturaCommand(nameof(Treinamento), treinamentoId), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao preparar o documento de assinatura do certificado {TreinamentoId} ao encerrar a turma {SessaoId}.", treinamentoId, sessao.Id);
                continue;
            }

            try
            {
                var resultadoTrabalhador = new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.Biometria);
                await _registrador.RegistrarAsync(documentoId.Value, resultadoTrabalhador, ipAddress: null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao registrar a assinatura automática do trabalhador no certificado {TreinamentoId}.", treinamentoId);
            }

            if (!string.IsNullOrEmpty(request.AzureAdObjectId))
            {
                try
                {
                    await _mediator.Send(new RegistrarAssinaturaSessaoLogadaCommand(documentoId.Value, request.AzureAdObjectId), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao registrar a assinatura automática do instrutor no certificado {TreinamentoId}.", treinamentoId);
                }
            }
        }
    }
}
