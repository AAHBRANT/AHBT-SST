using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

// Leitura pública (sem login) do QR do adesivo de inspeção. Só campos públicos: nunca Id, foto, texto das não
// conformidades nem CPF; do inspetor sai apenas o nome.
public class EquipamentoPublicoDto
{
    // Discriminadores: "Equipamento" é o do contrato; tipoRecurso segue o padrão de AreaPublicaDto/TrabalhadorPublicoDto.
    public string Tipo => "Equipamento";
    public string TipoRecurso => "equipamento";

    // "Liberado" | "Inspecionado" | "NaoLiberado" — a situação da inspeção Concluída MAIS RECENTE do objeto
    // (não a do adesivo que foi lido).
    public string Situacao { get; set; } = string.Empty;
    public string ObjetoIdentificacao { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public string ObraNome { get; set; } = string.Empty;
    // Datas em yyyy-MM-dd.
    public string UltimaInspecaoEm { get; set; } = string.Empty;
    public string? ProximaInspecaoEm { get; set; }
    // Protocolo da inspeção que gerou o adesivo lido (?i= do QR), só quando for uma inspeção Concluída deste mesmo
    // objeto; sem ?i= ou com valor que não confere, repete o protocoloAtual (a tela não acusa adesivo antigo).
    public string Protocolo { get; set; } = string.Empty;
    // Protocolo da inspeção Concluída mais recente do objeto.
    public string ProtocoloAtual { get; set; } = string.Empty;
    public string? InspetorNome { get; set; }
}

public record ResolverEquipamentoPublicoQuery(string Uid, string? ProtocoloAdesivo = null) : IRequest<EquipamentoPublicoDto?>;

public class ResolverEquipamentoPublicoQueryHandler : IRequestHandler<ResolverEquipamentoPublicoQuery, EquipamentoPublicoDto?>
{
    private const int TamanhoMaximoProtocolo = 40;

    private readonly IAppDbContext _db;

    public ResolverEquipamentoPublicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EquipamentoPublicoDto?> Handle(ResolverEquipamentoPublicoQuery request, CancellationToken ct)
    {
        // Tag só tem o filtro de Ativo (sem RBAC), como nas outras rotas públicas de tag.
        var tag = await _db.TagsIdentificacao
            .Where(t => t.Uid == request.Uid)
            .Select(t => new { t.EntidadeVinculadaTipo, t.EntidadeVinculadaId, t.Status })
            .FirstOrDefaultAsync(ct);
        // Tag Desativada ou Perdida não resolve o adesivo público (a etiqueta pode estar em mãos erradas).
        // As rotas públicas de Área e Trabalhador seguem como estão.
        if (tag is null || tag.Status is StatusTag.Desativada or StatusTag.Perdida) return null;
        if (tag is not { EntidadeVinculadaId: { } objetoId, EntidadeVinculadaTipo: { } tipo }
            || !AAHBRANT.SST.Application.TagsIdentificacao.NomeEntidadeVinculada.EhObjetoDeInspecao(tipo))
            return null;

        // IgnoreQueryFilters() + Ativo reaplicado à mão em TODAS as consultas abaixo: o filtro global de obra nega
        // tudo para requisição anônima (EscopoPorObraMiddleware) e, com Include/navegação obrigatória, o registro
        // some só em produção. Objeto excluído (soft delete) não resolve mais.
        var objetoAtivo = tipo switch
        {
            TipoEntidadeVinculada.Veiculo => await _db.Veiculos.IgnoreQueryFilters().AnyAsync(v => v.Id == objetoId && v.Ativo, ct),
            TipoEntidadeVinculada.Alojamento => await _db.Alojamentos.IgnoreQueryFilters().AnyAsync(a => a.Id == objetoId && a.Ativo, ct),
            TipoEntidadeVinculada.Inspecao => await _db.Inspecoes.IgnoreQueryFilters().AnyAsync(i => i.Id == objetoId && i.Ativo, ct),
            _ => await _db.ItensInspecionaveis.IgnoreQueryFilters().AnyAsync(i => i.Id == objetoId && i.Ativo, ct),
        };
        if (!objetoAtivo) return null;

        var atual = await InspecoesConcluidasDoObjeto(tipo, objetoId)
            .OrderByDescending(i => i.Data)
            .ThenByDescending(i => i.CreatedAtUtc)
            .Select(i => new InspecaoParaAdesivo(i.Id, i.ObraId, i.Data, i.Status, i.NumeroDocumento,
                i.ResponsavelUsuarioId, i.VeiculoId, i.AlojamentoId, i.ItemInspecionavelId, i.TipoInspecao, i.AtividadeId))
            .FirstOrDefaultAsync(ct);
        if (atual is null) return null;

        var dados = await AdesivoInspecaoCarregador.MontarAsync(_db, atual, ct);
        if (dados?.Situacao is not { } situacao) return null;

        var protocoloAtual = dados.Protocolo ?? string.Empty;
        var protocoloAdesivo = protocoloAtual;
        var pedido = request.ProtocoloAdesivo?.Trim();
        if (!string.IsNullOrEmpty(pedido) && pedido.Length <= TamanhoMaximoProtocolo
            && !string.Equals(pedido, protocoloAtual, StringComparison.OrdinalIgnoreCase))
        {
            var confere = await InspecoesConcluidasDoObjeto(tipo, objetoId)
                .Where(i => i.NumeroDocumento == pedido)
                .Select(i => i.NumeroDocumento)
                .FirstOrDefaultAsync(ct);
            if (confere is not null) protocoloAdesivo = confere;
        }

        return new EquipamentoPublicoDto
        {
            Situacao = ResultadoAdesivoInspecao.Texto(situacao),
            ObjetoIdentificacao = dados.ObjetoIdentificacao,
            Tema = dados.Tema,
            ObraNome = dados.ObraNome,
            UltimaInspecaoEm = dados.InspecaoEm.ToString("yyyy-MM-dd"),
            ProximaInspecaoEm = dados.ProximaInspecaoEm?.ToString("yyyy-MM-dd"),
            Protocolo = protocoloAdesivo,
            ProtocoloAtual = protocoloAtual,
            InspetorNome = dados.InspetorNome,
        };
    }

    private IQueryable<Inspecao> InspecoesConcluidasDoObjeto(TipoEntidadeVinculada tipo, Guid objetoId)
    {
        var query = _db.Inspecoes.IgnoreQueryFilters()
            .Where(i => i.Ativo && i.Status == StatusInspecao.Concluida);

        return tipo switch
        {
            TipoEntidadeVinculada.Veiculo => query.Where(i => i.VeiculoId == objetoId),
            TipoEntidadeVinculada.Alojamento => query.Where(i => i.AlojamentoId == objetoId),
            // Adesivo sem objeto físico: a "situação atual" é a da própria inspeção.
            TipoEntidadeVinculada.Inspecao => query.Where(i => i.Id == objetoId),
            _ => query.Where(i => i.ItemInspecionavelId == objetoId),
        };
    }
}
