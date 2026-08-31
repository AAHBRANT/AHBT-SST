using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Procedimento de Inspeção Técnica de Campo (§6.7/§10) — botão ENCERRAR: validação final do
// inspetor. Substitui o antigo AvancarStatusNaoConformidadeCommand (fluxo genérico de 3 passos) —
// aqui só o último passo (AguardandoValidacao → Encerrada) sobrevive como comando próprio, já que os
// passos intermediários agora são Enviar/Responder/RegistrarConclusao, cada um com sua própria regra.
// Mantém o mesmo bloqueio preventivo do comando antigo (nenhuma AcaoPlano vinculada pode estar
// pendente) e, sobre a(s) ação(ões) concluída(s) ainda sem validação, grava DataValidacao/
// ValidadoPorUsuarioId inline — mesmo efeito de ValidarAcaoPlanoCommand, mas sem reaproveitar aquele
// comando genérico (decisão de escopo: cada fluxo de NC manipula sua própria AcaoPlano vinculada,
// para não acoplar este módulo a chamadas do módulo genérico usado por outras origens, ex. Acidente).
//
// Motor de Assinatura Eletrônica (padrão pedido pelo usuário para todo documento assinável) — ao
// encerrar, garante (idempotente, mesma checagem de CriarDocumentoAssinaturaCommand) que exista um
// DocumentoAssinatura para esta ocorrência, para a tela de quiosque poder colher a assinatura
// biométrica do inspetor logo em seguida. Decisão própria de escopo: NÃO bloqueia o encerramento
// enquanto o documento não estiver Finalizado — o próprio módulo Dds (primeiro a usar o motor) já
// não impõe esse bloqueio em EncerrarDdsCommand, então criar aqui uma exigência mais rígida só para
// Não Conformidade seria inconsistente com o padrão já estabelecido no restante do sistema.
public record EncerrarNaoConformidadeCommand(
    Guid Id,
    Guid ValidadoPorUsuarioId,
    string? ObservacoesEncerramento) : IRequest;

public class EncerrarNaoConformidadeCommandValidator : AbstractValidator<EncerrarNaoConformidadeCommand>
{
    public EncerrarNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ValidadoPorUsuarioId).NotEmpty();
        RuleFor(x => x.ObservacoesEncerramento).MaximumLength(1000);
    }
}

public class EncerrarNaoConformidadeCommandHandler : IRequestHandler<EncerrarNaoConformidadeCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        if (nc.Status != StatusNaoConformidade.AguardandoValidacao)
            throw new InvalidOperationException(
                "Só é possível encerrar uma ocorrência que esteja Aguardando validação.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ValidadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.ValidadoPorUsuarioId} não encontrado.");

        var acoesVinculadas = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Domain.Entidades.NaoConformidade) && a.OrigemId == nc.Id)
            .ToListAsync(ct);

        if (acoesVinculadas.Any(a => a.Status != StatusControleRisco.Concluido))
            throw new InvalidOperationException(
                "Não é possível encerrar: existem ações do plano vinculadas ainda não concluídas.");

        var agora = DateTime.UtcNow;
        foreach (var acao in acoesVinculadas.Where(a => a.DataValidacao == null))
        {
            acao.DataValidacao = agora;
            acao.ValidadoPorUsuarioId = request.ValidadoPorUsuarioId;
        }

        nc.Status = StatusNaoConformidade.Encerrada;
        nc.DataConclusao = agora;
        nc.ObservacoesEncerramento = request.ObservacoesEncerramento;

        var documentoExistente = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == nameof(Domain.Entidades.NaoConformidade) && d.EntidadeId == nc.Id, ct);
        if (documentoExistente is null)
            _db.DocumentosAssinatura.Add(new DocumentoAssinatura
            {
                EntidadeTipo = nameof(Domain.Entidades.NaoConformidade),
                EntidadeId = nc.Id,
            });

        await _db.SaveChangesAsync(ct);
    }
}
