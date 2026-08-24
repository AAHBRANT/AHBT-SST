using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Núcleo da automação pedida pelo usuário em 2026-08-24 (Fase 1): a partir das Atividades do dia
// selecionadas pelo gestor, cruza com os Riscos já cadastrados (Atividade → Risco → Perigo) para
// gerar automaticamente o tópico principal (Perigo do Risco de maior NivelRisco) e os itens do
// checklist do roteiro (um item por linha de ControlesExistentes/ControlesAdicionais de cada
// Risco vinculado — ver disclosure em DdsItemChecklist.cs sobre a fonte desse checklist).
public record CriarDdsCommand(
    Guid ObraId,
    List<Guid> AtividadesIds,
    DateTime Data,
    Guid ResponsavelUsuarioId) : IRequest<Guid>;

public class CriarDdsCommandValidator : AbstractValidator<CriarDdsCommand>
{
    public CriarDdsCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.AtividadesIds).NotEmpty().WithMessage("Selecione ao menos uma atividade do dia.");
        RuleFor(x => x.ResponsavelUsuarioId).NotEmpty();
    }
}

public class CriarDdsCommandHandler : IRequestHandler<CriarDdsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDdsCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        var atividadesIds = request.AtividadesIds.Distinct().ToList();
        var atividades = await _db.Atividades
            .Where(a => atividadesIds.Contains(a.Id) && a.ObraId == request.ObraId)
            .ToListAsync(ct);
        if (atividades.Count != atividadesIds.Count)
            throw new KeyNotFoundException("Uma ou mais atividades selecionadas não pertencem a esta obra ou não existem.");

        var riscos = await _db.Riscos
            .Include(r => r.Perigo)
            .Where(r => atividadesIds.Contains(r.AtividadeId))
            .OrderByDescending(r => r.NivelRisco)
            .ToListAsync(ct);

        var dds = new Domain.Entidades.Dds
        {
            ObraId = request.ObraId,
            Data = request.Data,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            TopicoPrincipal = riscos.FirstOrDefault()?.Perigo?.Nome
                ?? "Nenhum risco cadastrado para as atividades selecionadas — revisar Matriz de Riscos.",
        };

        foreach (var atividade in atividades)
            dds.Atividades.Add(new Domain.Entidades.DdsAtividade { AtividadeId = atividade.Id });

        foreach (var risco in riscos)
        {
            foreach (var controle in ExtrairControles(risco.ControlesExistentes))
                dds.ItensChecklist.Add(new Domain.Entidades.DdsItemChecklist { RiscoId = risco.Id, Descricao = controle });
            foreach (var controle in ExtrairControles(risco.ControlesAdicionais))
                dds.ItensChecklist.Add(new Domain.Entidades.DdsItemChecklist { RiscoId = risco.Id, Descricao = controle });
        }

        _db.Dds.Add(dds);
        await _db.SaveChangesAsync(ct);
        return dds.Id;
    }

    // Controles são texto livre no cadastro de Risco — cada linha não vazia vira um item de
    // checklist independente para check-off na condução do DDS.
    private static IEnumerable<string> ExtrairControles(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) yield break;
        foreach (var linha in texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return linha;
    }
}
