using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EntregasEpi.Commands;

public record CriarEntregaEpiCommand(
    Guid TrabalhadorId,
    Guid CatalogoEpiId,
    DateTime DataEntrega,
    DateTime? DataDevolucao,
    DateTime? DataValidade,
    int Quantidade,
    string? VistoConsorcioResponsavel,
    string? Motivo,
    string? Observacoes,
    MotivoEntregaEpi MotivoTipo,
    string? NumeroListaPresencaNr6,
    DateTime? DataTreinamentoNr6) : IRequest<Guid>;

public class CriarEntregaEpiCommandValidator : AbstractValidator<CriarEntregaEpiCommand>
{
    public CriarEntregaEpiCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.DataEntrega).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.MotivoTipo).IsInEnum();
        RuleFor(x => x.NumeroListaPresencaNr6).MaximumLength(50);
    }
}

public class CriarEntregaEpiCommandHandler : IRequestHandler<CriarEntregaEpiCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEntregaEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEntregaEpiCommand request, CancellationToken ct)
    {
        var catalogo = await _db.CatalogoEpis.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpiId, ct)
            ?? throw new KeyNotFoundException("EPI de catálogo não encontrado.");

        var trabalhador = await _db.Trabalhadores
            .Include(x => x.Funcao)
            .FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        if (!FuncaoSstClassifier.EhTecnicoSeguranca(trabalhador.Funcao?.Nome))
            await GarantirIntegracaoSegurancaAssinadaAsync(_db, request.TrabalhadorId, ct);

        // Bloqueio de entrega com CA vencido e de estoque insuficiente: decisões confirmadas com o
        // usuário — não apenas um aviso, a entrega não é registrada.
        if (catalogo.CertificadoAprovacaoValidade is not null && catalogo.CertificadoAprovacaoValidade < DateTime.UtcNow)
            throw new InvalidOperationException("Este EPI está com o Certificado de Aprovação (CA) vencido — não é possível registrar a entrega.");

        // Fase 3 — estoque segmentado por Obra: resolve a Obra do trabalhador e busca (ou cria, com
        // saldo zero) a linha de estoque desse EPI nessa Obra.
        var estoque = await _db.EstoquesEpi
            .FirstOrDefaultAsync(x => x.CatalogoEpiId == request.CatalogoEpiId && x.ObraId == trabalhador.ObraId, ct);
        var saldoAtual = estoque?.Saldo ?? 0;

        if (saldoAtual < request.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente para este EPI nesta obra (saldo atual: {saldoAtual}).");

        var entrega = new EntregaEpi
        {
            TrabalhadorId = request.TrabalhadorId,
            CatalogoEpiId = request.CatalogoEpiId,
            DataEntrega = request.DataEntrega,
            DataDevolucao = request.DataDevolucao,
            DataValidade = request.DataValidade,
            Quantidade = request.Quantidade,
            VistoConsorcioResponsavel = request.VistoConsorcioResponsavel,
            Motivo = request.Motivo,
            Observacoes = request.Observacoes,
            MotivoTipo = request.MotivoTipo,
            NumeroListaPresencaNr6 = request.NumeroListaPresencaNr6,
            DataTreinamentoNr6 = request.DataTreinamentoNr6,
        };
        _db.EntregasEpi.Add(entrega);

        estoque!.Saldo -= request.Quantidade;
        _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
        {
            EstoqueEpiId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpi.SaidaEntrega,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            EntregaEpiId = entrega.Id,
        });

        await _db.SaveChangesAsync(ct);
        return entrega.Id;
    }

    // Bloqueio pedido pelo usuário (21/09): sem a Integração de Segurança assinada, o trabalhador
    // não recebe EPI — mesmo espírito de CalculadoraLiberacaoTerceirizado (Terceirizados/), que já
    // usa CursoTreinamento.EhIntegracaoSeguranca como a regra fixa de "curso obrigatório para todo
    // mundo, independente da função". Diferença proposital em relação àquela calculadora: aqui não
    // basta o registro de Treinamento existir com validade em dia — exige que o próprio trabalhador
    // tenha assinado o certificado (DocumentoAssinatura EntidadeTipo="Treinamento"), a prova de que
    // ele de fato recebeu a integração, não só que alguém digitou a data no sistema.
    // Sem nenhum curso marcado como Integração de Segurança no catálogo ainda, não bloqueia nada —
    // evita travar obras que não configuraram esse curso.
    //
    // Exceção do lançamento retroativo (decisão do usuário, 23/09): obra que já está em andamento
    // tem gente treinada ANTES de o sistema existir, e essa assinatura nunca vai existir aqui. Um
    // certificado lançado como Externo E com o arquivo anexado vale como prova no lugar da
    // assinatura — o documento digitalizado é a evidência. Sem arquivo não vale: sobraria só a
    // palavra de quem digitou, e é justamente a evidência que a fiscalização pede.
    private static async Task GarantirIntegracaoSegurancaAssinadaAsync(IAppDbContext db, Guid trabalhadorId, CancellationToken ct)
    {
        var cursoIntegracao = await db.CursosTreinamento.FirstOrDefaultAsync(c => c.EhIntegracaoSeguranca, ct);
        if (cursoIntegracao is null) return;

        var hoje = DateTime.UtcNow.Date;
        // Todos os registros válidos, não só o mais recente: com lançamento retroativo o trabalhador
        // pode ter um certificado externo anexado E um registro interno ainda sem assinatura, e
        // olhar só o de data de realização mais recente bloquearia quem já tem a prova no sistema.
        var treinamentosValidos = await db.Treinamentos
            .Where(t => t.TrabalhadorId == trabalhadorId && t.CursoTreinamentoId == cursoIntegracao.Id && t.DataValidade.Date >= hoje)
            .Select(t => new
            {
                t.Id,
                t.OrigemCertificado,
                TemArquivo = t.ArquivoCertificado != null,
            })
            .ToListAsync(ct);
        if (treinamentosValidos.Count == 0)
            throw new InvalidOperationException($"Este funcionário ainda não tem o treinamento de Integração de Segurança (\"{cursoIntegracao.Nome}\") em dia — não é possível registrar a entrega de EPI.");

        if (treinamentosValidos.Any(t => t.OrigemCertificado == OrigemCertificadoTreinamento.Externo && t.TemArquivo))
            return;

        var idsValidos = treinamentosValidos.Select(t => t.Id).ToList();
        var documentoIds = await db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == "Treinamento" && idsValidos.Contains(d.EntidadeId))
            .Select(d => d.Id)
            .ToListAsync(ct);
        // O próprio trabalhador precisa ter assinado (Biometria ou ReconhecimentoFacial) — a
        // assinatura do instrutor (SessaoLogada) sozinha não comprova que ele recebeu a integração.
        var trabalhadorAssinou = documentoIds.Count > 0 && await db.DocumentoSignatarios
            .AnyAsync(s => documentoIds.Contains(s.DocumentoAssinaturaId) && s.MetodoAutenticacao != MetodoAutenticacaoAssinatura.SessaoLogada, ct);
        if (!trabalhadorAssinou)
            throw new InvalidOperationException(
                $"Este funcionário tem o treinamento de Integração de Segurança (\"{cursoIntegracao.Nome}\") registrado, mas ainda não o assinou — não é possível registrar a entrega de EPI até a assinatura ser feita. " +
                "Se o treinamento foi feito antes de a obra entrar no sistema, lance-o em Treinamentos › Certificados como certificado externo e anexe o arquivo digitalizado.");
    }
}
