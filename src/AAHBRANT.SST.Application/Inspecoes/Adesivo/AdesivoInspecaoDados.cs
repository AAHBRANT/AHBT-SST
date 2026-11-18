using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

// Campos da inspeção que o adesivo precisa. Quem monta já resolveu o escopo (autenticado: Inspecoes com o filtro
// de obra; público: IgnoreQueryFilters + Ativo à mão).
public sealed record InspecaoParaAdesivo(
    Guid Id,
    Guid ObraId,
    DateTime Data,
    StatusInspecao Status,
    string? NumeroDocumento,
    Guid ResponsavelUsuarioId,
    Guid? VeiculoId,
    Guid? AlojamentoId,
    Guid? ItemInspecionavelId,
    TipoInspecao TipoInspecao,
    Guid? AtividadeId);

public sealed record AdesivoInspecaoDados(
    Guid InspecaoId,
    Guid ObraId,
    SituacaoAdesivo? Situacao,
    TipoEntidadeVinculada ObjetoTipo,
    Guid ObjetoId,
    string ObjetoIdentificacao,
    string? ObjetoDescricao,
    string Tema,
    string ObraNome,
    DateTime InspecaoEm,
    DateTime? ProximaInspecaoEm,
    string? InspetorNome,
    string? InspetorFuncao,
    string? Protocolo);

// Monta os dados do adesivo/da situação pública a partir de UMA inspeção. Tudo sai em consultas separadas, com
// IgnoreQueryFilters e projeção só dos campos usados: o filtro global de obra/Ativo (Obra, Usuário, Veículo,
// Alojamento, Item) nega tudo numa requisição anônima e, com Include/navegação obrigatória, faria o registro
// sumir em produção (dev sem Entra degrada para acesso global e esconde o erro). O acesso ao registro em si é
// decidido por quem chama, ANTES de chamar aqui.
public static class AdesivoInspecaoCarregador
{
    // Sem configuração global de alojamento, vale o mesmo padrão da entidade ConfiguracaoAlojamento.
    private const int DiasPadraoAlojamento = 30;

    public static async Task<AdesivoInspecaoDados?> MontarAsync(IAppDbContext db, InspecaoParaAdesivo inspecao, CancellationToken ct)
    {
        TipoEntidadeVinculada tipo;
        Guid objetoId;
        string identificacao;
        string? descricao;
        string tema;
        ModeloAdesivoInspecao modelo;
        DateTime? proxima;

        if (inspecao.VeiculoId is { } veiculoId)
        {
            var v = await db.Veiculos.IgnoreQueryFilters()
                .Where(x => x.Id == veiculoId)
                .Select(x => new { x.PlacaPrefixo, x.Tipo, x.MarcaModelo, x.PeriodicidadeInspecaoDias })
                .FirstOrDefaultAsync(ct);
            if (v is null) return null;

            tipo = TipoEntidadeVinculada.Veiculo;
            objetoId = veiculoId;
            identificacao = v.PlacaPrefixo;
            descricao = string.IsNullOrWhiteSpace(v.MarcaModelo)
                ? DescreverTipoVeiculo(v.Tipo)
                : $"{DescreverTipoVeiculo(v.Tipo)} · {v.MarcaModelo}";
            tema = "Veículos";
            // Veículo sempre sai LIBERADO (decisão do usuário): é equipamento de uso/operação.
            modelo = ModeloAdesivoInspecao.Liberado;
            proxima = PeriodicidadeInspecao.ProximaEm(v.PeriodicidadeInspecaoDias, inspecao.Data, inspecao.Data);
        }
        else if (inspecao.ItemInspecionavelId is { } itemId)
        {
            var item = await db.ItensInspecionaveis.IgnoreQueryFilters()
                .Where(x => x.Id == itemId)
                .Select(x => new { x.Identificacao, x.Descricao, x.TemaInspecaoId, x.PeriodicidadeInspecaoDias })
                .FirstOrDefaultAsync(ct);
            if (item is null) return null;

            var temaInfo = await db.TemasInspecao.IgnoreQueryFilters()
                .Where(t => t.Id == item.TemaInspecaoId)
                .Select(t => new { t.Nome, t.ModeloAdesivo })
                .FirstOrDefaultAsync(ct);

            tipo = TipoEntidadeVinculada.ItemInspecionavel;
            objetoId = itemId;
            identificacao = item.Identificacao;
            descricao = item.Descricao;
            tema = temaInfo?.Nome ?? "Inspeção";
            modelo = temaInfo?.ModeloAdesivo ?? ModeloAdesivoInspecao.Liberado;
            proxima = PeriodicidadeInspecao.ProximaEm(item.PeriodicidadeInspecaoDias, inspecao.Data, inspecao.Data);
        }
        else if (inspecao.AlojamentoId is { } alojamentoId)
        {
            var a = await db.Alojamentos.IgnoreQueryFilters()
                .Where(x => x.Id == alojamentoId)
                .Select(x => new { x.Nome, x.Endereco })
                .FirstOrDefaultAsync(ct);
            if (a is null) return null;

            // "Dias para inspeção atrasada" da configuração global é a periodicidade do alojamento.
            var dias = await db.ConfiguracoesAlojamento.IgnoreQueryFilters()
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.DiasParaInspecaoAtrasada)
                .FirstOrDefaultAsync(ct);

            tipo = TipoEntidadeVinculada.Alojamento;
            objetoId = alojamentoId;
            identificacao = a.Nome;
            descricao = a.Endereco;
            tema = "Alojamento";
            // Alojamento é local: INSPECIONADO (decisão padrão, a confirmar).
            modelo = ModeloAdesivoInspecao.Inspecionado;
            proxima = PeriodicidadeInspecao.ProximaEm(dias is > 0 ? dias : DiasPadraoAlojamento, inspecao.Data, inspecao.Data);
        }
        else
        {
            // Sem objeto físico (Obra, Canteiro, Comportamental...): toda inspeção sai com adesivo (decisão do
            // usuário em 18/11/2026). O QR aponta para a própria inspeção e o selo é INSPECIONADO (NÃO LIBERADO
            // com item Não Conforme); não há "próxima inspeção" porque não há periodicidade.
            string? atividade = null;
            if (inspecao.AtividadeId is { } atividadeId)
            {
                atividade = await db.Atividades.IgnoreQueryFilters()
                    .Where(a => a.Id == atividadeId)
                    .Select(a => a.Nome)
                    .FirstOrDefaultAsync(ct);
            }

            tipo = TipoEntidadeVinculada.Inspecao;
            objetoId = inspecao.Id;
            identificacao = AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.DescreverTipoInspecao(inspecao.TipoInspecao);
            descricao = atividade;
            tema = "Inspeção";
            modelo = ModeloAdesivoInspecao.Inspecionado;
            proxima = null;
        }

        var respostas = await db.InspecaoItemRespostas.IgnoreQueryFilters()
            .Where(r => r.InspecaoId == inspecao.Id && r.Ativo)
            .Select(r => r.StatusItem)
            .ToListAsync(ct);

        var situacao = ResultadoAdesivoInspecao.Calcular(inspecao.Status, respostas, modelo);

        var obraNome = await db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == inspecao.ObraId)
            .Select(o => o.Nome)
            .FirstOrDefaultAsync(ct);

        var inspetor = await db.Usuarios.IgnoreQueryFilters()
            .Where(u => u.Id == inspecao.ResponsavelUsuarioId)
            .Select(u => new { u.Nome, u.TrabalhadorId })
            .FirstOrDefaultAsync(ct);

        string? funcaoNome = null;
        if (inspetor?.TrabalhadorId is { } trabalhadorId)
        {
            var funcaoId = await db.Trabalhadores.IgnoreQueryFilters()
                .Where(t => t.Id == trabalhadorId)
                .Select(t => (Guid?)t.FuncaoId)
                .FirstOrDefaultAsync(ct);
            if (funcaoId is { } fid)
            {
                funcaoNome = await db.Funcoes.IgnoreQueryFilters()
                    .Where(f => f.Id == fid)
                    .Select(f => f.Nome)
                    .FirstOrDefaultAsync(ct);
            }
        }

        return new AdesivoInspecaoDados(
            inspecao.Id,
            inspecao.ObraId,
            situacao,
            tipo,
            objetoId,
            identificacao,
            string.IsNullOrWhiteSpace(descricao) ? null : descricao,
            tema,
            obraNome ?? string.Empty,
            inspecao.Data,
            proxima,
            string.IsNullOrWhiteSpace(inspetor?.Nome) ? null : inspetor!.Nome,
            string.IsNullOrWhiteSpace(funcaoNome) ? null : funcaoNome,
            inspecao.NumeroDocumento);
    }

    public static string DescreverTipoVeiculo(TipoVeiculo tipo) => tipo switch
    {
        TipoVeiculo.CaminhaoBasculante => "Caminhão basculante",
        TipoVeiculo.Retroescavadeira => "Retroescavadeira",
        TipoVeiculo.EscavadeiraHidraulica => "Escavadeira hidráulica",
        TipoVeiculo.CaminhaoCarroceria => "Caminhão carroceria",
        TipoVeiculo.CaminhaoMunck => "Caminhão munck",
        _ => "Veículo",
    };
}
