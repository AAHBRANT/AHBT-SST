using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Riscos.Medidas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ghes.AtividadesFuncao;

public record DecisaoAtividadeFuncao(Guid AtividadeId, Guid FuncaoId, bool Executa);

// Engenheiro de Segurança confirma (Executa = true → Validada) ou descarta (false → Descartada) os pares
// atividade × função. Quem decidiu vem do token (preenchido pelo controller), nunca do corpo. Cria o
// registro quando ainda não existe (Origem Manual); descartar não apaga nada.
// ExigirEngenheiroNaObra: a decisão libera a OS, então só vale para quem é Engenheiro de Segurança NA obra
// da rota (vínculo ativo com a obra ou global). A policy de perfil sozinha aceita engenheiro de qualquer
// obra. O controller desliga a checagem só com o Entra ID desligado (dev, sem identidade real).
public record DecidirAtividadesFuncaoCommand(Guid ObraId, List<DecisaoAtividadeFuncao> Itens, Guid? UsuarioId, string? NomeUsuario,
    bool ExigirEngenheiroNaObra) : IRequest<int>;

public class DecidirAtividadesFuncaoCommandValidator : AbstractValidator<DecidirAtividadesFuncaoCommand>
{
    public DecidirAtividadesFuncaoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Itens).NotEmpty().Must(i => i.Count <= 2500).WithMessage("Decida no máximo 2500 pares por vez.");
        RuleForEach(x => x.Itens).ChildRules(i =>
        {
            i.RuleFor(x => x.AtividadeId).NotEmpty();
            i.RuleFor(x => x.FuncaoId).NotEmpty();
        });
    }
}

public class DecidirAtividadesFuncaoCommandHandler : IRequestHandler<DecidirAtividadesFuncaoCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public DecidirAtividadesFuncaoCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<int> Handle(DecidirAtividadesFuncaoCommand request, CancellationToken ct)
    {
        if (request.ExigirEngenheiroNaObra && !await EhEngenheiroNaObraAsync(request, ct))
            throw new AcessoNegadoException("Só o Engenheiro de Segurança desta obra pode confirmar as atividades da função.");

        // Mesmo par repetido no lote: vale o último.
        var itens = request.Itens.GroupBy(i => (i.AtividadeId, i.FuncaoId)).Select(g => g.Last()).ToList();
        var atividadeIds = itens.Select(i => i.AtividadeId).Distinct().ToList();

        // Atividade fora do escopo do usuário, de outra obra ou de GHE desativado = não encontrada (404).
        var atividades = await _db.Atividades.NoEscopoDaAtividade(_db, a => a.Id)
            .EmUso(_db)
            .Where(a => atividadeIds.Contains(a.Id) && a.ObraId == request.ObraId)
            .Select(a => new { a.Id, a.GheId }).ToDictionaryAsync(a => a.Id, a => a.GheId, ct);
        var ausente = atividadeIds.FirstOrDefault(id => !atividades.ContainsKey(id));
        if (ausente != Guid.Empty) throw new KeyNotFoundException($"Atividade {ausente} não encontrada.");

        var gheIds = atividades.Values.Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();
        var funcoesDosGhes = (await _db.GheFuncoes.Where(gf => gheIds.Contains(gf.GheId))
                .Select(gf => new { gf.GheId, gf.FuncaoId }).ToListAsync(ct))
            .Select(gf => (gf.GheId, gf.FuncaoId)).ToHashSet();
        foreach (var i in itens)
        {
            var ghe = atividades[i.AtividadeId];
            if (ghe is null || !funcoesDosGhes.Contains((ghe.Value, i.FuncaoId)))
                throw new InvalidOperationException("A função não pertence ao GHE da atividade; só funções do GHE podem ser vinculadas.");
        }

        var existentes = await _db.AtividadesFuncoes.Where(x => atividadeIds.Contains(x.AtividadeId)).ToListAsync(ct);
        var porPar = existentes.ToDictionary(x => (x.AtividadeId, x.FuncaoId));
        var nome = await NomeUsuarioMedidas.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        var agora = DateTime.UtcNow;
        var alteradas = new List<object>();

        foreach (var i in itens)
        {
            var novoStatus = i.Executa ? StatusMedidaPrevencao.Validada : StatusMedidaPrevencao.Descartada;
            if (!porPar.TryGetValue((i.AtividadeId, i.FuncaoId), out var registro))
            {
                registro = new AtividadeFuncao
                {
                    ObraId = request.ObraId,
                    AtividadeId = i.AtividadeId,
                    FuncaoId = i.FuncaoId,
                    OrigemVinculo = OrigemMedidaPrevencao.Manual,
                };
                _db.AtividadesFuncoes.Add(registro);
            }
            else if (registro.Status == novoStatus)
                continue; // mesma decisão: não regrava quem decidiu nem a data.

            registro.Status = novoStatus;
            registro.ValidadoEmUtc = agora;
            registro.ValidadoPorUsuarioId = request.UsuarioId;
            registro.ValidadoPorNome = nome;
            alteradas.Add(new { i.AtividadeId, i.FuncaoId, Status = novoStatus.ToString() });
        }

        // Uma linha de trilha por lote: várias chamadas seguidas no mesmo SaveChanges leriam o mesmo hash
        // anterior e bifurcariam a cadeia.
        if (alteradas.Count > 0)
        {
            await _auditoria.RegistrarAsync("AtividadeFuncao.Decidida", "AtividadeFuncao", request.ObraId,
                request.UsuarioId, null, new { request.ObraId, Decisoes = alteradas }, ct);
            await ConflitoAtividadesFuncao.SalvarAsync(_db, ct);
        }
        return alteradas.Count;
    }

    // Administrador não entra: a policy de perfil também não o aceita (PermissaoAuthorizationHandler).
    private async Task<bool> EhEngenheiroNaObraAsync(DecidirAtividadesFuncaoCommand request, CancellationToken ct)
    {
        if (request.UsuarioId is not { } usuarioId) return false;
        return await _db.UsuariosPerfilObra.AnyAsync(v => v.UsuarioId == usuarioId && v.Ativo
            && (v.ObraId == null || v.ObraId == request.ObraId)
            && v.PerfilAcesso != null && v.PerfilAcesso.Ativo && v.PerfilAcesso.Tipo == TipoPerfilAcesso.EngenheiroSeguranca, ct);
    }
}

// Duplo clique ou duas pessoas ao mesmo tempo: o índice único (AtividadeId, FuncaoId) rejeita o segundo
// INSERT. Vira resposta de negócio (400) em vez de 500, e as alterações pendentes são descartadas.
internal static class ConflitoAtividadesFuncao
{
    public const string Mensagem = "Outra pessoa acabou de alterar estas atividades. Atualize e tente de novo.";

    // Mesmo critério de TratamentoCpfDuplicado: a mensagem do SQL Server cita o índice violado.
    public static bool EhChaveDuplicada(DbUpdateException ex)
        => ex is not DbUpdateConcurrencyException
            && (ex.InnerException?.Message ?? ex.Message).Contains("IX_AtividadesFuncoes_AtividadeId_FuncaoId");

    public static async Task SalvarAsync(IAppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (EhChaveDuplicada(ex))
        {
            if (db is DbContext contexto) contexto.ChangeTracker.Clear();
            throw new InvalidOperationException(Mensagem, ex);
        }
    }
}
