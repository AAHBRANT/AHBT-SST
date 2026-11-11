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
// ViaIntegracao: só o atendimento (Avançar etapa) encerra a turma que é etapa de integração; pela tela comum é recusado.
public record EncerrarSessaoTreinamentoCommand(Guid Id, string? AzureAdObjectId = null, bool ViaIntegracao = false) : IRequest;

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
    private readonly IGeradorNumeroDocumentoService _geradorNumero;
    private readonly ILogger<EncerrarSessaoTreinamentoCommandHandler> _logger;

    public EncerrarSessaoTreinamentoCommandHandler(
        IAppDbContext db,
        IMediator mediator,
        IRegistradorAssinaturaService registrador,
        ILogger<EncerrarSessaoTreinamentoCommandHandler> logger,
        IGeradorNumeroDocumentoService geradorNumero)
    {
        _geradorNumero = geradorNumero;
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

        if (sessao.IntegracaoAtendimentoId is not null && !request.ViaIntegracao)
            throw new InvalidOperationException("Esta turma é uma etapa de integração e só é encerrada ao avançar a etapa em \"Realizar integração\".");

        var totalFotos = await _db.FotosEvidenciaSessaoTreinamento.CountAsync(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo, ct);
        if (totalFotos < TotalFotosObrigatorias)
            throw new InvalidOperationException(
                $"São necessárias {TotalFotosObrigatorias} fotos de evidência para encerrar a turma (faltam {TotalFotosObrigatorias - totalFotos}).");

        var fotos = await _db.FotosEvidenciaSessaoTreinamento.Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo).OrderBy(f => f.Ordem).ToListAsync(ct);
        foreach (var foto in fotos)
            AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompletaSeInformada(foto.FotoMetadadosJson, $"Foto {foto.Ordem}");

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .ToListAsync(ct);

        var validadeEmMeses = sessao.ValidadeEmMeses > 0 ? sessao.ValidadeEmMeses : sessao.CursoTreinamento!.ValidadeEmMeses;
        // Número de certificado próprio por participante (19/10) — antes a turma inteira saía com o
        // mesmo número. Prefixo pela NR (AAH-NR35-2026-0001) para identificar de qual norma é.
        var nr = CatalogoNormasRegulamentadoras.NormalizarNumero(sessao.CursoTreinamento!.NormaReferencia);
        var prefixoCertificado = nr is null ? "AAH-CERT" : $"AAH-{nr.Replace("-", string.Empty)}";
        var geradosParaAssinar = new List<(Guid TreinamentoId, ParticipanteSessaoTreinamento Participante)>();
        foreach (var participante in participantes.Where(p => p.PresencaConfirmadaEm is not null && p.TreinamentoGeradoId is null))
        {
            // Realizar integração (Q6): a data do certificado é a do dia em que a pessoa assinou (presença biométrica) este curso.
            var dataCertificado = sessao.IntegracaoAtendimentoId is not null && participante.PresencaConfirmadaEm is { } assinouEm
                ? AAHBRANT.SST.Application.PlanosIntegracao.AgendaTreinamentosIntegracao.DataBrasilia(assinouEm)
                : sessao.DataRealizacao;
            var treinamento = new Treinamento
            {
                TrabalhadorId = participante.TrabalhadorId,
                CursoTreinamentoId = sessao.CursoTreinamentoId,
                DataRealizacao = dataCertificado,
                DataValidade = dataCertificado.AddMonths(validadeEmMeses),
                CargaHorariaRealizada = sessao.CargaHorariaRealizada,
                InstituicaoInstrutor = sessao.InstituicaoInstrutor,
                InstrutorRegistroProfissional = sessao.InstrutorRegistroProfissional,
                Local = sessao.Local,
                ConteudoProgramatico = sessao.ConteudoProgramatico,
                NumeroCertificado = await _geradorNumero.GerarAsync(prefixoCertificado, ct),
                SessaoTreinamentoId = sessao.Id,
            };
            _db.Treinamentos.Add(treinamento);
            participante.TreinamentoGeradoId = treinamento.Id;
            geradosParaAssinar.Add((treinamento.Id, participante));
        }

        sessao.Status = StatusSessaoTreinamento.Concluida;
        sessao.DataEncerramento = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Ações do plano de ocorrências que se resolvem com uma turma deste curso.
        var ocorrencias = await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.OcorrenciasComTreinamentoAsync(_db, sessao.CursoTreinamentoId, ct);
        await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(_db, ocorrencias, ct);

        foreach (var (treinamentoId, participante) in geradosParaAssinar)
        {
            var trabalhadorId = participante.TrabalhadorId;
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
                // 05/11: a assinatura leva o método real da presença (antes era sempre "digital") e a
                // evidência guardada na hora — imagem, IP, aparelho, localização e validação —, não os
                // dados de quem está encerrando a turma. A imagem sai do participante depois de copiada.
                var resultadoTrabalhador = new ResultadoAutenticacaoAssinatura(
                    trabalhadorId,
                    participante.MetodoPresenca ?? MetodoAutenticacaoAssinatura.Biometria,
                    participante.DispositivoAgenteId,
                    participante.ValidacaoModelo,
                    participante.ValidacaoGrupoId,
                    participante.ValidacaoRequisicaoId,
                    participante.ScoreConfianca,
                    participante.PresencaConfirmadaEm);
                var localizacao = new LocalizacaoAssinatura(participante.LocalizacaoStatus, participante.Latitude, participante.Longitude, participante.PrecisaoMetros);
                await _registrador.RegistrarAsync(documentoId.Value, resultadoTrabalhador, participante.IpAddress, ct,
                    participante.EvidenciaConteudo, participante.EvidenciaContentType,
                    localizacao: localizacao, userAgent: participante.UserAgent);
                if (participante.EvidenciaConteudo is not null)
                {
                    participante.EvidenciaConteudo = null;
                    await _db.SaveChangesAsync(ct);
                }
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

            // Certificado com as assinaturas da turma encerrada: fecha o documento.
            await FinalizacaoAutomaticaDocumento.TentarAsync(_db, _mediator, _logger, documentoId.Value, ct);
        }

        // Quem concluiu o curso que faltava para uma integração já concluída recebe o registro "Integração realizada" (melhor esforço).
        foreach (var (_, participante) in geradosParaAssinar)
        {
            try { await AAHBRANT.SST.Application.IntegracoesAtendimento.RegistroIntegracaoSincronizador.SincronizarPorTrabalhadorAsync(_db, participante.TrabalhadorId, sessao.ObraId, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Não foi possível sincronizar o registro de integração do trabalhador {TrabalhadorId}.", participante.TrabalhadorId);
            }
        }
    }
}
