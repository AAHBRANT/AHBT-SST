using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.OrdensServico;

// Monta a minuta da OS do trabalhador na obra dele, a partir do PGR vigente da obra, do GHE da função,
// das medidas e itens validados, da Matriz de EPI/Treinamento, do ASO e das políticas. Tudo o que falta
// ou não está validado vira pendência (AvaliadorPendenciasOs). Consultas simples por tabela, sem
// projetar navegação obrigatória (INNER JOIN + soft delete esconde registro).
public record ObterMinutaOrdemServicoQuery(Guid TrabalhadorId) : IRequest<MinutaOrdemServicoDto?>;

public class ObterMinutaOrdemServicoQueryHandler : IRequestHandler<ObterMinutaOrdemServicoQuery, MinutaOrdemServicoDto?>
{
    private readonly IAppDbContext _db;

    public ObterMinutaOrdemServicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MinutaOrdemServicoDto?> Handle(ObterMinutaOrdemServicoQuery request, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow.Date;
        var t = await _db.Trabalhadores.NoEscopoDoTrabalhador(_db, x => x.Id)
            .FirstOrDefaultAsync(x => x.Id == request.TrabalhadorId, ct);
        if (t is null) return null;

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == t.ObraId, ct);
        var funcao = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == t.FuncaoId, ct);

        var minuta = new MinutaOrdemServicoDto
        {
            TrabalhadorId = t.Id,
            Obra = new ObraOsDto
            {
                Id = t.ObraId,
                Nome = obra?.Nome ?? string.Empty,
                Cliente = obra?.Cliente,
                Cnpj = obra?.Cnpj,
                Endereco = string.Join(" — ", new[] { obra?.Endereco, obra?.Cidade, obra?.Uf }.Where(s => !string.IsNullOrWhiteSpace(s))),
            },
            Trabalhador = new TrabalhadorOsDto
            {
                Nome = t.Nome,
                Matricula = t.Matricula,
                CpfMascarado = MascararCpf(t.Cpf),
                Funcao = funcao?.Nome ?? string.Empty,
                Cbo = funcao?.CboCodigo,
                DataAdmissao = t.DataAdmissao,
            },
        };

        // PGR vigente da obra (o mais recente, se houver mais de um).
        var pgr = await _db.Pgrs.Where(p => p.ObraId == t.ObraId && p.Status == StatusPgr.Vigente)
            .OrderByDescending(p => p.DataElaboracao).FirstOrDefaultAsync(ct);
        if (pgr is not null)
        {
            var revisao = await _db.PgrRevisoes.Where(r => r.PgrId == pgr.Id).Select(r => (int?)r.NumeroRevisao).MaxAsync(ct);
            minuta.Pgr = new PgrOsDto(pgr.Id, pgr.Nome, pgr.DataElaboracao, revisao);
        }

        // GHEs da obra em que a função está.
        var ghesDaObra = await _db.Ghes.Where(g => g.ObraId == t.ObraId).Select(g => new { g.Id, g.Numero }).ToListAsync(ct);
        var gheIds = ghesDaObra.Select(g => g.Id).ToList();
        var gheFuncoes = await _db.GheFuncoes.Where(gf => gheIds.Contains(gf.GheId) && gf.FuncaoId == t.FuncaoId)
            .Select(gf => new { gf.GheId, gf.DescricaoAtividades }).ToListAsync(ct);
        var ghesDaFuncao = gheFuncoes.Select(gf => gf.GheId).ToHashSet();
        minuta.Ghes = gheFuncoes
            .Select(gf => new GheOsDto(ghesDaObra.First(g => g.Id == gf.GheId).Numero, gf.DescricaoAtividades))
            .OrderBy(g => g.Numero).ToList();

        // Atividades dos GHEs da função, com riscos, medidas e detalhes.
        var atividades = await _db.Atividades.EmUso(_db)
            .Where(a => a.GheId != null && ghesDaFuncao.Contains(a.GheId.Value))
            .Include(a => a.Ghe)
            .Include(a => a.Riscos).ThenInclude(r => r.Perigo)
            .Include(a => a.Riscos).ThenInclude(r => r.Medidas)
            .Include(a => a.Epcs).ThenInclude(e => e.CatalogoEpc)
            .Include(a => a.Documentos).ThenInclude(d => d.MaterialApoio)
            .Include(a => a.ItensOs)
            .OrderBy(a => a.Nome)
            .ToListAsync(ct);

        var avaliacao = new List<AvaliadorPendenciasOs.AtividadeAvaliada>();
        foreach (var a in atividades)
        {
            var dto = new AtividadeOsDto
            {
                Id = a.Id,
                Nome = a.Nome,
                Condicoes = a.CondicoesExecucao ?? CondicoesDoGhe(a.Ghe?.DescricaoAmbiente, a.Ghe?.JornadaTrabalho),
                Ferramentas = string.IsNullOrWhiteSpace(a.Ferramentas)
                    ? new List<string>()
                    : a.Ferramentas.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList(),
                Epcs = a.Epcs.Where(e => e.CatalogoEpc is not null).Select(e => e.CatalogoEpc!.Nome).OrderBy(n => n).ToList(),
                Procedimentos = a.Documentos
                    .Where(d => d.MaterialApoio is { Situacao: SituacaoDocumentoSst.Vigente })
                    .Select(d => $"{d.MaterialApoio!.Codigo} — {d.MaterialApoio.Nome} (rev. {d.MaterialApoio.Revisao})")
                    .OrderBy(s => s).ToList(),
            };

            var validados = a.ItensOs.Where(i => i.Status == StatusMedidaPrevencao.Validada).ToList();
            dto.InterromperQuando = validados.Where(i => i.Tipo == TipoItemAtividadeOs.InterromperQuando).Select(i => i.Texto).ToList();
            dto.Restricoes = validados.Where(i => i.Tipo == TipoItemAtividadeOs.Restricao).Select(i => new RestricaoOsDto(i.Texto, i.Requisito)).ToList();
            dto.Autorizacoes = validados.Where(i => i.Tipo == TipoItemAtividadeOs.Autorizacao).Select(i => i.Texto).ToList();

            foreach (var r in a.Riscos.OrderByDescending(r => r.NivelRisco))
            {
                var medidas = r.Medidas.Where(m => m.Status == StatusMedidaPrevencao.Validada).OrderBy(m => m.Ordem).ToList();
                dto.Riscos.Add(new RiscoOsDto
                {
                    Id = r.Id,
                    Perigo = r.Perigo?.Nome ?? string.Empty,
                    Nivel = r.NivelRisco,
                    Consequencia = r.Consequencia,
                    Antes = medidas.Where(m => m.Fase == FaseMedidaPrevencao.Antes).Select(m => m.Texto).ToList(),
                    Durante = medidas.Where(m => m.Fase == FaseMedidaPrevencao.Durante).Select(m => m.Texto).ToList(),
                    Apos = medidas.Where(m => m.Fase == FaseMedidaPrevencao.Apos).Select(m => m.Texto).ToList(),
                });
            }

            minuta.Atividades.Add(dto);
            avaliacao.Add(new AvaliadorPendenciasOs.AtividadeAvaliada(
                a.Id,
                a.Nome,
                a.Riscos.Select(r => new AvaliadorPendenciasOs.RiscoAvaliado(
                    r.Id,
                    r.Perigo?.Nome ?? string.Empty,
                    r.AvaliadoEmUtc is not null,
                    r.Medidas.Count(m => m.Status == StatusMedidaPrevencao.Validada),
                    r.Medidas.Count(m => m.Status == StatusMedidaPrevencao.Pendente))).ToList(),
                a.ItensOs.Count(i => i.Status == StatusMedidaPrevencao.Pendente),
                dto.Procedimentos.Count));
        }

        // EPIs da função (Matriz de EPI).
        var epiIds = await _db.MatrizEpiFuncoes.Where(m => m.FuncaoId == t.FuncaoId).Select(m => m.CatalogoEpiId).ToListAsync(ct);
        minuta.Epis = await _db.CatalogoEpis.Where(e => epiIds.Contains(e.Id))
            .OrderBy(e => e.Nome).Select(e => new EpiOsDto(e.Nome, e.CertificadoAprovacaoNumero)).ToListAsync(ct);

        // Treinamentos exigidos pela função e situação do trabalhador. Técnico de Segurança é isento,
        // como na liberação para o trabalho.
        var isento = funcao is not null && FuncaoSstClassifier.EhTecnicoSeguranca(funcao.Nome);
        var cursoIds = isento
            ? new List<Guid>()
            : await _db.MatrizTreinamentoFuncoes.Where(m => m.FuncaoId == t.FuncaoId).Select(m => m.CursoTreinamentoId).Distinct().ToListAsync(ct);
        var cursos = await _db.CursosTreinamento.Where(c => cursoIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nome, c.NormaReferencia }).ToListAsync(ct);
        var feitos = await _db.Treinamentos.Where(x => x.TrabalhadorId == t.Id && cursoIds.Contains(x.CursoTreinamentoId))
            .Select(x => new { x.CursoTreinamentoId, x.DataValidade }).ToListAsync(ct);
        foreach (var c in cursos.OrderBy(c => c.Nome))
        {
            var validade = feitos.Where(f => f.CursoTreinamentoId == c.Id).Select(f => (DateTime?)f.DataValidade).Max();
            var situacao = validade is null ? "NaoRealizado" : validade.Value.Date >= hoje ? "Valido" : "Vencido";
            minuta.Treinamentos.Add(new TreinamentoOsDto(c.Nome, c.NormaReferencia, validade, situacao));
        }

        // ASO mais recente; válido = apto (ou apto com restrição) dentro da validade.
        var aso = await _db.Asos.Where(a => a.TrabalhadorId == t.Id)
            .OrderByDescending(a => a.DataExame).Select(a => new { a.ResultadoStatus, a.DataValidade }).FirstOrDefaultAsync(ct);
        if (aso is not null)
        {
            var valido = aso.DataValidade.Date >= hoje
                && (aso.ResultadoStatus == ResultadoAso.Apto || aso.ResultadoStatus == ResultadoAso.AptoComRestricao);
            minuta.Aso = new AsoOsDto(aso.ResultadoStatus, aso.DataValidade, valido);
        }

        minuta.Politicas = await _db.MateriaisApoio
            .Where(m => m.Tipo == TipoDocumentoSst.Politica && m.Situacao == SituacaoDocumentoSst.Vigente && m.IncluirEmTodasOs)
            .OrderBy(m => m.Codigo)
            .Select(m => new PoliticaOsDto(m.Nome, m.Codigo, m.Revisao, m.FormaConsulta))
            .ToListAsync(ct);

        minuta.Pendencias = AvaliadorPendenciasOs.Avaliar(new AvaliadorPendenciasOs.Entrada(
            minuta.Pgr?.Id,
            ghesDaFuncao.Count > 0,
            gheFuncoes.Any(gf => !string.IsNullOrWhiteSpace(gf.DescricaoAtividades)),
            avaliacao,
            minuta.Treinamentos,
            minuta.Aso));

        return minuta;
    }

    private static string? CondicoesDoGhe(string? ambiente, string? jornada)
    {
        var partes = new[]
        {
            string.IsNullOrWhiteSpace(ambiente) ? null : $"Ambiente: {ambiente.Trim()}",
            string.IsNullOrWhiteSpace(jornada) ? null : $"Jornada: {jornada.Trim()}",
        }.Where(p => p is not null);
        var texto = string.Join("\n", partes);
        return texto.Length == 0 ? null : texto;
    }

    // LGPD: a OS mostra só os 2 últimos dígitos do CPF.
    private static string MascararCpf(string? cpf)
    {
        var digitos = new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        return digitos.Length < 2 ? "***.***.***-**" : $"***.***.***-{digitos[^2..]}";
    }
}
