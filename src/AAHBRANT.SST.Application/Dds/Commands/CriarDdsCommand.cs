using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Registro diário dentro de uma DdsSemanal (31/08, reformulação para o modelo em papel do usuário;
// 01/09, temas simultâneos) — a partir das Atividades do dia selecionadas pelo gestor, cruza com os
// Riscos já cadastrados (Atividade → Risco → Perigo) para gerar o checklist do roteiro (inalterado)
// e um bloco de tema por atividade (snapshot do Risco de maior NivelRisco DELA, não do conjunto
// todo — mesmo raciocínio que já existia para o antigo "tema automático"). Tema livre (catálogo)
// é opcional e aditivo, nunca substitui os temas das atividades.
// ObraId/ResponsavelUsuarioId não são mais parâmetros — vêm da DdsSemanal (a obra e o responsável
// pelo DDS já são fixos pela semana inteira).
public record CriarDdsCommand(
    Guid DdsSemanalId,
    List<Guid> AtividadesIds,
    DateTime Data,
    Guid? CatalogoTemaDdsId) : IRequest<Guid>;

public class CriarDdsCommandValidator : AbstractValidator<CriarDdsCommand>
{
    public CriarDdsCommandValidator()
    {
        RuleFor(x => x.DdsSemanalId).NotEmpty();
        RuleFor(x => x.AtividadesIds).NotEmpty().WithMessage("Selecione ao menos uma atividade do dia.");
    }
}

public class CriarDdsCommandHandler : IRequestHandler<CriarDdsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDdsCommand request, CancellationToken ct)
    {
        var semanal = await _db.DdsSemanais.FirstOrDefaultAsync(s => s.Id == request.DdsSemanalId, ct)
            ?? throw new KeyNotFoundException($"DDS semanal {request.DdsSemanalId} não encontrado.");
        if (semanal.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("Esta semana já foi encerrada — não é possível criar novos registros diários.");
        if (request.Data.Date < semanal.DataInicioSemana.Date || request.Data.Date > semanal.DataFimSemana.Date)
            throw new InvalidOperationException("A data do registro precisa estar dentro da semana selecionada (segunda a sexta).");

        var jaExisteNoDia = await _db.Dds.AnyAsync(d => d.DdsSemanalId == semanal.Id && d.Data.Date == request.Data.Date, ct);
        if (jaExisteNoDia)
            throw new InvalidOperationException("Já existe um registro de DDS para este dia da semana.");

        var atividadesIds = request.AtividadesIds.Distinct().ToList();
        var atividadesCarregadas = await _db.Atividades
            .Where(a => atividadesIds.Contains(a.Id) && a.ObraId == semanal.ObraId)
            .ToListAsync(ct);
        if (atividadesCarregadas.Count != atividadesIds.Count)
            throw new KeyNotFoundException("Uma ou mais atividades selecionadas não pertencem a esta obra ou não existem.");
        // Preserva a ordem de seleção do gestor (AtividadesIds), não a ordem de retorno do banco.
        var atividadesOrdenadas = atividadesIds.Select(id => atividadesCarregadas.First(a => a.Id == id)).ToList();

        var riscos = await _db.Riscos
            .Include(r => r.Perigo)
            .Where(r => atividadesIds.Contains(r.AtividadeId))
            .OrderByDescending(r => r.NivelRisco)
            .ToListAsync(ct);

        var dds = new Domain.Entidades.Dds
        {
            ObraId = semanal.ObraId,
            DdsSemanalId = semanal.Id,
            Data = request.Data.Date,
            ResponsavelUsuarioId = semanal.ResponsavelUsuarioId,
        };

        if (request.CatalogoTemaDdsId.HasValue)
        {
            var catalogo = await _db.CatalogosTemaDds.FirstOrDefaultAsync(c => c.Id == request.CatalogoTemaDdsId.Value, ct)
                ?? throw new KeyNotFoundException("Tema do catálogo não encontrado.");
            dds.CatalogoTemaDdsId = catalogo.Id;
            dds.TemaLivreNome = catalogo.Nome;
            dds.TemaLivreDescricao = catalogo.Descricao;
        }

        foreach (var (atividade, indice) in atividadesOrdenadas.Select((a, i) => (a, i)))
        {
            var maiorRisco = riscos.Where(r => r.AtividadeId == atividade.Id).OrderByDescending(r => r.NivelRisco).FirstOrDefault();
            dds.Atividades.Add(new Domain.Entidades.DdsAtividade
            {
                AtividadeId = atividade.Id,
                Ordem = indice + 1,
                AtividadeNome = atividade.Nome,
                PerigoNome = maiorRisco?.Perigo?.Nome,
                PerigoDescricao = maiorRisco?.Perigo?.Descricao,
                Consequencia = maiorRisco?.Consequencia,
                ControlesExistentes = maiorRisco?.ControlesExistentes,
                ControlesAdicionais = maiorRisco?.ControlesAdicionais,
            });
        }

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
