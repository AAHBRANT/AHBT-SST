using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Criar uma Inspecao a partir de um ChecklistModelo já gera uma InspecaoItemResposta "em branco"
// (StatusItem=null) por item vigente do checklist, para que a execução vire só preencher cada
// linha (ver disclosure em Inspecao.cs sobre o nullable de StatusItem).
public record CriarInspecaoCommand(
    Guid ChecklistModeloId,
    Guid ObraId,
    Guid? AtividadeId,
    DateTime Data,
    Guid ResponsavelUsuarioId,
    Guid? IdCliente = null,
    List<RespostaInspecaoCliente>? RespostasCliente = null,
    int? VersaoChecklistEsperada = null) : IRequest<InspecaoCriadaResultado>;

public record RespostaInspecaoCliente(Guid ChecklistModeloItemId, Guid Id);
public record InspecaoCriadaResultado(Guid Id, string? NumeroDocumento, Dictionary<Guid, string?> VersoesRespostas);

public class CriarInspecaoCommandValidator : AbstractValidator<CriarInspecaoCommand>
{
    public CriarInspecaoCommandValidator()
    {
        RuleFor(x => x.ChecklistModeloId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.ResponsavelUsuarioId).NotEmpty();
    }
}

public class CriarInspecaoCommandHandler : IRequestHandler<CriarInspecaoCommand, InspecaoCriadaResultado>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarInspecaoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<InspecaoCriadaResultado> Handle(CriarInspecaoCommand request, CancellationToken ct)
    {
        var checklist = await _db.ChecklistModelos
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == request.ChecklistModeloId, ct)
            ?? throw new KeyNotFoundException($"Checklist {request.ChecklistModeloId} não encontrado.");

        // Inspeção de Alojamento tem fluxo próprio (aba Alojamento ->
        // ObterOuCriarInspecaoAlojamentoCommandHandler), que exige AlojamentoId e resolve o
        // responsável pelo usuário logado. Este handler genérico não sabe preencher AlojamentoId —
        // deixar passar criaria uma Inspecao órfã (AlojamentoId nulo) que nunca aparece em nenhum
        // card de alojamento, porque a query de status filtra por AlojamentoId.
        if (checklist.TipoInspecao == TipoInspecao.Alojamento)
            throw new InvalidOperationException(
                "Inspeções de Alojamento devem ser criadas pela aba Alojamento, não pelo painel genérico de Nova Inspeção.");

        // Idem para Veículos: a inspeção nasce do card do veículo (ObterOuCriarInspecaoVeiculoCommand),
        // que preenche VeiculoId.
        if (checklist.TipoInspecao == TipoInspecao.Veiculo)
            throw new InvalidOperationException(
                "Inspeções de Veículos devem ser criadas pela aba Veículos, não pelo painel genérico de Nova Inspeção.");

        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        if (request.AtividadeId.HasValue && !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId && a.ObraId == request.ObraId, ct))
            throw new InvalidOperationException("A atividade não pertence à obra selecionada ou não está disponível.");
        if (request.VersaoChecklistEsperada.HasValue && checklist.Versao != request.VersaoChecklistEsperada)
            throw new DbUpdateConcurrencyException("O modelo de checklist mudou. Revise a inspeção preparada offline.");

        var itensAtivos = checklist.Itens.Where(i => i.Ativo).ToList();
        var idsCliente = request.RespostasCliente;
        if (request.IdCliente.HasValue || idsCliente is not null)
        {
            if (!request.IdCliente.HasValue || request.IdCliente == Guid.Empty || idsCliente is null
                || idsCliente.Count != itensAtivos.Count || idsCliente.Any(i => i.Id == Guid.Empty)
                || idsCliente.Select(i => i.Id).Distinct().Count() != idsCliente.Count
                || !idsCliente.Select(i => i.ChecklistModeloItemId).Order().SequenceEqual(itensAtivos.Select(i => i.Id).Order()))
                throw new InvalidOperationException("O checklist preparado offline está incompleto ou contém identificadores inválidos.");
            var ids = idsCliente.Select(i => i.Id).ToList();
            if (await _db.Inspecoes.IgnoreQueryFilters().AnyAsync(i => i.Id == request.IdCliente, ct)
                || await _db.InspecaoItemRespostas.IgnoreQueryFilters().AnyAsync(r => ids.Contains(r.Id), ct))
                throw new InvalidOperationException("Os identificadores desta criação já estão em uso. Confira a sincronização antes de tentar criar novamente.");
        }

        var inspecao = new Inspecao
        {
            Id = request.IdCliente ?? Guid.NewGuid(),
            TipoInspecao = checklist.TipoInspecao,
            ObraId = request.ObraId,
            AtividadeId = request.AtividadeId,
            ChecklistModeloId = checklist.Id,
            Data = request.Data,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            NumeroDocumento = await _geradorNumero.GerarAsync("INSP", ct),
        };

        foreach (var item in itensAtivos)
        {
            inspecao.Respostas.Add(new InspecaoItemResposta
            {
                Id = idsCliente?.Single(i => i.ChecklistModeloItemId == item.Id).Id ?? Guid.NewGuid(),
                ChecklistModeloItemId = item.Id,
            });
        }

        _db.Inspecoes.Add(inspecao);
        await _db.SaveChangesAsync(ct);
        return new(inspecao.Id, inspecao.NumeroDocumento, inspecao.Respostas.ToDictionary(r => r.Id,
            r => r.RowVersion is null ? null : Convert.ToBase64String(r.RowVersion)));
    }
}
