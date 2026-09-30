using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.IntegracaoGsupri;
using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AAHBRANT.SST.Infrastructure.Integracao.Gsupri;

public class IntegracaoGsupriService(SstDbContext db, ICurrentUserService usuario, IConfiguration config)
    : IIntegracaoGsupriService
{
    private static string Codigo(string? s) => (s ?? "").Trim().ToUpperInvariant();
    private void ExigirGlobal()
    {
        // Administração central: não expor recebimentos de obras ainda não vinculadas a perfis locais.
        if (!usuario.TemAcessoGlobal) throw new InvalidOperationException("A integração exige administração com acesso global.");
    }

    public async Task<RecebimentoGsupriDto> ReceberAsync(RecebimentoGsupriPayload entrada, CancellationToken ct)
    {
        ExigirGlobal();
        await new RecebimentoGsupriValidator().ValidateAndThrowAsync(entrada, ct);
        var dados = entrada with {
            EventoId = Codigo(entrada.EventoId), RecebimentoId = Codigo(entrada.RecebimentoId),
            ObraCodigo = Codigo(entrada.ObraCodigo),
            Itens = entrada.Itens.Select(i => i with { ItemId = Codigo(i.ItemId),
                ProdutoCodigo = Codigo(i.ProdutoCodigo), Unidade = Codigo(i.Unidade) })
                .OrderBy(i => i.ItemId, StringComparer.Ordinal).ToList()
        };
        var json = JsonSerializer.Serialize(dados);
        // EventoId não muda a identidade do conteúdo: novo envelope não duplica o recebimento.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(dados with { EventoId = "" }))));
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var evento = await db.Set<GsupriEvento>().SingleOrDefaultAsync(e => e.EventoExternoId == dados.EventoId, ct);
        if (evento != null)
        {
            if (evento.Hash != hash) throw new ConflitoGsupriException("EventoId reutilizado com conteúdo diferente.");
            return Dto(await CarregarAsync(evento.RecebimentoId, ct));
        }
        var recebimento = await db.Set<GsupriRecebimento>().Include(r => r.Itens).ThenInclude(i => i.ProdutoVinculo)
            .SingleOrDefaultAsync(r => r.RecebimentoExternoId == dados.RecebimentoId, ct);
        if (recebimento == null)
        {
            recebimento = new GsupriRecebimento { RecebimentoExternoId = dados.RecebimentoId, ObraCodigo = dados.ObraCodigo, Origem = OrigemRegistro.Importacao };
            db.Add(recebimento);
        }
        else
        {
            if (recebimento.ObraCodigo != dados.ObraCodigo)
                throw new ConflitoGsupriException("Não é permitido trocar a obra de um recebimento. Cancele a origem e crie outro recebimento.");
            if (dados.Versao < recebimento.Versao)
                throw new ConflitoGsupriException("Versão anterior à já recebida. Envie uma versão superior com o estado completo.");
            if (dados.Versao == recebimento.Versao && recebimento.Hash != hash)
                throw new ConflitoGsupriException("Mesma versão com conteúdo diferente. Incremente a versão para corrigir ou cancelar.");
        }
        recebimento.Versao = dados.Versao; recebimento.Hash = hash; recebimento.DadosJson = json;
        db.Add(new GsupriEvento { EventoExternoId = dados.EventoId, RecebimentoId = recebimento.Id,
            Hash = hash, DadosJson = json, Origem = OrigemRegistro.Importacao });
        await ProcessarAsync(recebimento, dados, ct);
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return Dto(recebimento);
    }

    public async Task<RecebimentoGsupriDto> ReprocessarAsync(Guid id, CancellationToken ct)
    {
        ExigirGlobal();
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var r = await CarregarAsync(id, ct);
        await ProcessarAsync(r, JsonSerializer.Deserialize<RecebimentoGsupriPayload>(r.DadosJson)!, ct);
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return Dto(r);
    }

    private async Task<GsupriRecebimento> CarregarAsync(Guid id, CancellationToken ct) =>
        await db.Set<GsupriRecebimento>().Include(r => r.Itens).ThenInclude(i => i.ProdutoVinculo)
            .SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException("Recebimento não encontrado.");

    private record Plano(GsupriRecebimentoItem Item, GsupriProduto Produto, int Alvo);
    private record Destino(string Categoria, Guid CatalogoId, string Tamanho);
    private record Saldo(int Quantidade, Action<int, string> Aplicar);

    private async Task ProcessarAsync(GsupriRecebimento r, RecebimentoGsupriPayload dados, CancellationToken ct)
    {
        r.Pendencia = null;
        if (dados.Cancelado && r.Itens.All(i => i.QuantidadeAplicada == 0))
        {
            r.Status = "Cancelado";
            foreach (var item in r.Itens) item.Pendencia = null;
            return;
        }
        var obra = await db.Set<GsupriObra>().SingleOrDefaultAsync(x => x.CodigoExterno == r.ObraCodigo, ct);
        if (obra == null || !await db.Obras.AnyAsync(o => o.Id == obra.ObraId, ct))
        { r.Status = "Pendente"; r.Pendencia = "Vincule o código da obra do G-SUPRI a uma obra ativa do SST."; return; }
        r.ObraId = obra.ObraId;
        var planos = new List<Plano>();
        foreach (var recebido in dados.Itens)
        {
            var item = r.Itens.SingleOrDefault(i => i.ItemExternoId == recebido.ItemId);
            if (item == null)
            {
                item = new GsupriRecebimentoItem { RecebimentoId = r.Id, ItemExternoId = recebido.ItemId,
                    ProdutoCodigo = recebido.ProdutoCodigo, Unidade = recebido.Unidade, Origem = OrigemRegistro.Importacao };
                r.Itens.Add(item);
                db.Add(item); // GUID já preenchido: marcar Added também ao reprocessar um pai persistido.
            }
            else if (item.ProdutoCodigo != recebido.ProdutoCodigo || item.Unidade != recebido.Unidade)
                throw new ConflitoGsupriException("Um ItemId não pode mudar de produto ou unidade. Use um novo ItemId na revisão.");
            item.Pendencia = null;
            var produto = item.ProdutoVinculo ?? await db.Set<GsupriProduto>()
                .SingleOrDefaultAsync(p => p.CodigoExterno == item.ProdutoCodigo && p.Unidade == item.Unidade, ct);
            if (produto == null)
            {
                if (!dados.Cancelado) item.Pendencia = "Produto e unidade aguardam vínculo com o catálogo ou classificação fora do escopo.";
                continue;
            }
            item.ProdutoVinculo = produto; item.ProdutoVinculoId = produto.Id;
            if (produto.Categoria == "IGNORAR") continue;
            if (!await CatalogoAtivoAsync(produto.Categoria, produto.CatalogoId, ct))
            { item.Pendencia = "Produto do catálogo está indisponível."; continue; }
            // Não inferir liberação de um item marcado com não conformidade.
            var liberada = dados.Cancelado ? 0 : recebido.QuantidadeLiberada ??
                (recebido.TemNaoConformidade ? 0 : recebido.QuantidadeRecebida);
            if (!dados.Cancelado && liberada < recebido.QuantidadeRecebida)
                item.Pendencia = "Há quantidade recebida aguardando liberação no G-SUPRI.";
            var convertida = liberada * produto.FatorConversao;
            if (convertida != decimal.Truncate(convertida) || convertida > int.MaxValue)
            { item.Pendencia = "A conversão deve resultar em quantidade inteira compatível com o estoque. Revise a unidade na origem."; continue; }
            planos.Add(new Plano(item, produto, (int)convertida));
        }
        // Estado completo: itens omitidos numa revisão ou cancelamento são estornados.
        foreach (var item in r.Itens.Where(i => dados.Cancelado || dados.Itens.All(p => p.ItemId != i.ItemExternoId)))
        {
            item.Pendencia = null;
            planos.RemoveAll(p => p.Item == item);
            if (item.ProdutoVinculo is { Categoria: not "IGNORAR" } produto)
                planos.Add(new Plano(item, produto, 0));
        }
        // Agrupar o delta evita perder entradas quando dois itens apontam para o mesmo saldo.
        var ajustes = new List<(Saldo Saldo, int Delta)>();
        foreach (var grupo in planos.GroupBy(p => new Destino(p.Produto.Categoria, p.Produto.CatalogoId!.Value, p.Produto.Tamanho)))
        {
            var delta = grupo.Sum(p => (long)p.Alvo - p.Item.QuantidadeAplicada);
            if (delta == 0) continue;
            var saldo = await ObterSaldoAsync(grupo.Key, obra.ObraId, ct);
            var futuro = saldo.Quantidade + delta;
            if (futuro < 0 || futuro > int.MaxValue || delta is > int.MaxValue or < int.MinValue)
            {
                r.Status = "PendenteRegularizacao";
                r.Pendencia = "A correção/cancelamento não pode ser aplicada ao saldo atual. Regularize as saídas ou o estoque e reprocesse. Nenhum ajuste desta revisão foi aplicado.";
                return;
            }
            ajustes.Add((saldo, (int)delta));
        }
        foreach (var ajuste in ajustes)
            ajuste.Saldo.Aplicar(ajuste.Delta, $"G-SUPRI recebimento {r.RecebimentoExternoId}, versão {r.Versao}, NF {dados.NumeroNota}. Ref. {r.Id}");
        foreach (var plano in planos) plano.Item.QuantidadeAplicada = plano.Alvo;
        r.Status = dados.Cancelado ? "Cancelado" : r.Itens.Any(i => i.Pendencia != null) ? "Pendente" : "Processado";
        if (r.Status == "Pendente") r.Pendencia = "Consulte as pendências dos itens. As quantidades liberadas e vinculadas foram processadas.";
    }

    private async Task<Saldo> ObterSaldoAsync(Destino d, Guid obraId, CancellationToken ct)
    {
        // Os estoques existentes têm RowVersion; a transação serializável protege também a primeira entrada.
        if (d.Categoria == "EPI")
        {
            var e = await db.EstoquesEpi.SingleOrDefaultAsync(x => x.CatalogoEpiId == d.CatalogoId && x.ObraId == obraId, ct);
            return new Saldo(e?.Saldo ?? 0, (delta, obs) => {
                if (e == null) { e = new EstoqueEpi { ObraId = obraId, CatalogoEpiId = d.CatalogoId }; db.Add(e); }
                e.Saldo += delta;
                db.Add(new MovimentacaoEstoqueEpi { EstoqueEpiId = e.Id, Tipo = TipoMovimentacaoEstoqueEpi.IntegracaoGsupri,
                    Quantidade = delta, SaldoResultante = e.Saldo, Observacao = obs, Origem = OrigemRegistro.Importacao });
            });
        }
        if (d.Categoria == "EPC")
        {
            var e = await db.EstoquesEpc.SingleOrDefaultAsync(x => x.CatalogoEpcId == d.CatalogoId && x.ObraId == obraId, ct);
            return new Saldo(e?.Saldo ?? 0, (delta, obs) => {
                if (e == null) { e = new EstoqueEpc { ObraId = obraId, CatalogoEpcId = d.CatalogoId }; db.Add(e); }
                e.Saldo += delta;
                db.Add(new MovimentacaoEstoqueEpc { EstoqueEpcId = e.Id, Tipo = TipoMovimentacaoEstoqueEpc.IntegracaoGsupri,
                    Quantidade = delta, SaldoResultante = e.Saldo, Observacao = obs, Origem = OrigemRegistro.Importacao });
            });
        }
        var u = await db.EstoquesUniforme.SingleOrDefaultAsync(x => x.CatalogoUniformeId == d.CatalogoId && x.ObraId == obraId && x.Tamanho == d.Tamanho, ct);
        return new Saldo(u?.Saldo ?? 0, (delta, obs) => {
            if (u == null) { u = new EstoqueUniforme { ObraId = obraId, CatalogoUniformeId = d.CatalogoId, Tamanho = d.Tamanho }; db.Add(u); }
            u.Saldo += delta;
            db.Add(new MovimentacaoEstoqueUniforme { EstoqueUniformeId = u.Id, Tipo = TipoMovimentacaoEstoqueUniforme.IntegracaoGsupri,
                Quantidade = delta, SaldoResultante = u.Saldo, Observacao = obs, Origem = OrigemRegistro.Importacao });
        });
    }

    public async Task VincularObraAsync(VincularObraGsupri dados, CancellationToken ct)
    {
        ExigirGlobal();
        var codigo = Codigo(dados.CodigoExterno);
        if (codigo.Length is 0 or > 100 || !await db.Obras.AnyAsync(o => o.Id == dados.ObraId, ct))
            throw new InvalidOperationException("Informe o código externo e uma obra ativa do SST.");
        var atual = await db.Set<GsupriObra>().SingleOrDefaultAsync(o => o.CodigoExterno == codigo, ct);
        if (atual != null)
        {
            if (atual.ObraId == dados.ObraId) return;
            throw new ConflitoGsupriException("O código já está vinculado a outra obra. Os vínculos são imutáveis para preservar o histórico.");
        }
        db.Add(new GsupriObra { CodigoExterno = codigo, ObraId = dados.ObraId });
        await db.SaveChangesAsync(ct);
    }

    public async Task VincularProdutoAsync(VincularProdutoGsupri dados, CancellationToken ct)
    {
        ExigirGlobal();
        var codigo = Codigo(dados.CodigoExterno); var unidade = Codigo(dados.Unidade);
        var categoria = Codigo(dados.Categoria); var tamanho = Codigo(dados.Tamanho);
        if (codigo.Length is 0 or > 100 || unidade.Length is 0 or > 10 || tamanho.Length > 20 ||
            dados.FatorConversao <= 0 || dados.FatorConversao > 1000000 || decimal.Round(dados.FatorConversao, 6) != dados.FatorConversao ||
            !new[] { "EPI", "EPC", "UNIFORME", "IGNORAR" }.Contains(categoria))
            throw new InvalidOperationException("Código, unidade, categoria ou fator de conversão inválido.");
        if (categoria != "IGNORAR" && !await CatalogoAtivoAsync(categoria, dados.CatalogoId, ct))
            throw new InvalidOperationException("Selecione um produto ativo do catálogo correspondente.");
        if (categoria == "UNIFORME" && tamanho.Length == 0)
            throw new InvalidOperationException("Informe o tamanho do uniforme.");
        if (categoria != "UNIFORME" && tamanho.Length != 0)
            throw new InvalidOperationException("Para EPI/EPC, selecione um cadastro específico para a variante; tamanho separado é suportado apenas em uniformes.");
        var catalogoId = categoria == "IGNORAR" ? null : dados.CatalogoId;
        var atual = await db.Set<GsupriProduto>().SingleOrDefaultAsync(p => p.CodigoExterno == codigo && p.Unidade == unidade, ct);
        if (atual != null)
        {
            if (atual.Categoria == categoria && atual.CatalogoId == catalogoId && atual.Tamanho == tamanho && atual.FatorConversao == dados.FatorConversao) return;
            throw new ConflitoGsupriException("Produto/unidade já vinculado. Os vínculos são imutáveis para preservar conversões e histórico.");
        }
        db.Add(new GsupriProduto { CodigoExterno = codigo, Unidade = unidade, Categoria = categoria,
            CatalogoId = catalogoId, Tamanho = tamanho, FatorConversao = dados.FatorConversao });
        await db.SaveChangesAsync(ct);
    }

    private Task<bool> CatalogoAtivoAsync(string categoria, Guid? id, CancellationToken ct) => categoria switch {
        "EPI" => db.CatalogoEpis.AnyAsync(c => c.Id == id, ct),
        "EPC" => db.CatalogoEpcs.AnyAsync(c => c.Id == id, ct),
        "UNIFORME" => db.CatalogoUniformes.AnyAsync(c => c.Id == id, ct),
        _ => Task.FromResult(false)
    };

    public async Task<PainelGsupriDto> PainelAsync(int pagina, CancellationToken ct)
    {
        ExigirGlobal();
        if (pagina is < 1 or > 100000) throw new InvalidOperationException("Página inválida.");
        var obras = await (from v in db.Set<GsupriObra>() join o in db.Obras on v.ObraId equals o.Id
            orderby o.Nome select new ObraGsupriDto(v.CodigoExterno, v.ObraId, o.Nome)).ToListAsync(ct);
        var produtos = await db.Set<GsupriProduto>().OrderBy(p => p.CodigoExterno)
            .Select(p => new ProdutoGsupriDto(p.CodigoExterno, p.Unidade, p.Categoria, p.CatalogoId, p.Tamanho, p.FatorConversao)).ToListAsync(ct);
        var recebimentos = await db.Set<GsupriRecebimento>().AsNoTracking().Include(r => r.Itens)
            .OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Skip((pagina - 1) * 25).Take(25).ToListAsync(ct);
        return new PainelGsupriDto(config.GetValue<bool>("IntegracaoGsupri:Habilitada"), obras, produtos,
            recebimentos.Select(Dto).ToList(), await db.Set<GsupriRecebimento>().CountAsync(ct));
    }

    public async Task<OpcoesGsupriDto> OpcoesAsync(CancellationToken ct)
    {
        ExigirGlobal();
        var obras = await db.Obras.OrderBy(o => o.Nome).Select(o => new OpcaoGsupriDto(o.Id, o.Nome)).ToListAsync(ct);
        var epi = await db.CatalogoEpis.OrderBy(p => p.Nome).Select(p => new OpcaoGsupriDto(p.Id,
            p.Nome + (p.Fabricante != null ? " · " + p.Fabricante : "") + (p.CertificadoAprovacaoNumero != null ? " · CA " + p.CertificadoAprovacaoNumero : ""))).ToListAsync(ct);
        var epc = await db.CatalogoEpcs.OrderBy(p => p.Nome).Select(p => new OpcaoGsupriDto(p.Id, p.Nome)).ToListAsync(ct);
        var uniforme = await db.CatalogoUniformes.OrderBy(p => p.Nome).Select(p => new OpcaoGsupriDto(p.Id, p.Nome)).ToListAsync(ct);
        return new(obras, new Dictionary<string, List<OpcaoGsupriDto>> { ["EPI"] = epi, ["EPC"] = epc, ["UNIFORME"] = uniforme });
    }

    private static RecebimentoGsupriDto Dto(GsupriRecebimento r)
    {
        var d = JsonSerializer.Deserialize<RecebimentoGsupriPayload>(r.DadosJson)!;
        return new(r.Id, r.RecebimentoExternoId, r.Versao, d.PedidoId, r.ObraCodigo, r.ObraId, d.NumeroNota,
            r.Status, r.Pendencia, d.RecebidoEm, d.Itens.Select(i => {
                var local = r.Itens.SingleOrDefault(x => x.ItemExternoId == i.ItemId);
                return new ItemRecebimentoGsupriDto(i.ItemId, i.ProdutoCodigo, i.Descricao, i.Unidade,
                    i.QuantidadeRecebida, i.QuantidadeLiberada, local?.QuantidadeAplicada ?? 0, local?.Pendencia);
            }).ToList());
    }
}
