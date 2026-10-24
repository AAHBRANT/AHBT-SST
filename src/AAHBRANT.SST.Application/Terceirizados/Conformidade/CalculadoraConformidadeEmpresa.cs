using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Conformidade;

// Checklist e percentual de adequação da empresa terceirizada (especificação 21/10/2026, Módulo 4).
// Três níveis, todos contados juntos: adequação = itens atendidos ÷ itens exigidos × 100.
//  - Empresa: cada documento exigido ativo (PGR, PCMSO...) enviado e dentro da validade.
//  - Funções: cada curso obrigatório (matriz de treinamento) de cada função das vagas dos contratos
//    vigentes; atendido quando todas as pessoas da empresa naquela função têm o curso válido — e há
//    ao menos uma pessoa (vaga sem ninguém ainda não comprova nada).
//  - Pessoas: cada pessoa dos contratos vigentes liberada pelo mesmo cálculo de hoje
//    (CalculadoraLiberacaoTerceirizado: ASO, integração, cursos e EPI) e com biometria facial e
//    digital cadastrada (sem ela não assina a ficha de EPI nem a presença).
public static class CalculadoraConformidadeEmpresa
{
    public const string NivelEmpresa = "empresa";
    public const string NivelFuncoes = "funcoes";
    public const string NivelPessoas = "pessoas";

    // Documento que vence nos próximos 30 dias continua atendido, mas aparece como "vence em breve".
    public const int DiasAvisoVencimento = 30;

    public static async Task<ConformidadeEmpresaDto?> CalcularAsync(IAppDbContext db, Guid empresaId, CancellationToken ct = default)
    {
        var empresa = await db.Empresas
            .Where(e => e.Id == empresaId)
            .Select(e => new { e.Id, e.RazaoSocial, e.Cnpj, e.Status })
            .FirstOrDefaultAsync(ct);
        if (empresa is null) return null;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var itens = new List<ItemConformidadeDto>();

        // Nível 1 — documentos da empresa.
        var exigidos = await db.DocumentosExigidosEmpresa
            .Where(d => d.Ativo)
            .OrderBy(d => d.Ordem).ThenBy(d => d.Nome)
            .Select(d => new { d.Id, d.Nome, d.ExigeValidade })
            .ToListAsync(ct);
        var enviados = await db.DocumentosEmpresaTerceirizada
            .Where(d => d.EmpresaId == empresaId)
            .Select(d => new { d.Id, d.DocumentoExigidoEmpresaId, d.DataValidade, d.NomeArquivo })
            .ToListAsync(ct);
        foreach (var exigido in exigidos)
        {
            var doc = enviados.FirstOrDefault(d => d.DocumentoExigidoEmpresaId == exigido.Id);
            string situacao;
            string? detalhe;
            if (doc is null)
            {
                situacao = SituacaoItemConformidade.Pendente;
                detalhe = "Documento não enviado.";
            }
            else if (exigido.ExigeValidade && doc.DataValidade is null)
            {
                situacao = SituacaoItemConformidade.Pendente;
                detalhe = "Enviado sem data de validade. Informe a validade.";
            }
            else if (doc.DataValidade is { } validade && validade < hoje)
            {
                situacao = SituacaoItemConformidade.Vencido;
                detalhe = $"Venceu em {validade:dd/MM/yyyy}.";
            }
            else if (doc.DataValidade is { } validadeProxima && validadeProxima <= hoje.AddDays(DiasAvisoVencimento))
            {
                situacao = SituacaoItemConformidade.VenceEmBreve;
                detalhe = $"Vence em {validadeProxima:dd/MM/yyyy}.";
            }
            else
            {
                situacao = SituacaoItemConformidade.Ok;
                detalhe = doc.DataValidade is { } v ? $"Válido até {v:dd/MM/yyyy}." : "Enviado.";
            }
            itens.Add(new ItemConformidadeDto(
                NivelEmpresa, exigido.Nome, SituacaoItemConformidade.Atende(situacao), situacao, detalhe,
                DocumentoExigidoId: exigido.Id, DocumentoId: doc?.Id, DataValidade: doc?.DataValidade, ArquivoNome: doc?.NomeArquivo,
                ExigeValidade: exigido.ExigeValidade));
        }

        // Pessoas dos contratos vigentes (base dos níveis 2 e 3).
        var pessoas = await db.Trabalhadores
            .Where(t => t.Vinculo == TipoVinculo.Terceirizado && t.EmpresaId == empresaId
                && t.ContratoId != null && t.Contrato!.Status == StatusContrato.Validado)
            .OrderBy(t => t.Nome)
            .Select(t => new { t.Id, t.Nome, t.FuncaoId, FuncaoNome = t.Funcao!.Nome })
            .ToListAsync(ct);

        // Nível 2 — requisitos das funções das vagas.
        var funcoesDasVagas = await db.ContratoVagasFuncao
            .Where(v => v.Contrato!.EmpresaId == empresaId && v.Contrato.Status == StatusContrato.Validado)
            .Select(v => new { v.FuncaoId, FuncaoNome = v.Funcao!.Nome })
            .Distinct()
            .ToListAsync(ct);
        var hojeData = DateTime.UtcNow.Date;
        foreach (var funcao in funcoesDasVagas.OrderBy(f => f.FuncaoNome))
        {
            if (FuncaoSstClassifier.EhTecnicoSeguranca(funcao.FuncaoNome)) continue;
            var cursos = await db.MatrizTreinamentoFuncoes
                .Where(m => m.FuncaoId == funcao.FuncaoId)
                .Select(m => new { m.CursoTreinamentoId, Nome = m.CursoTreinamento!.Nome })
                .ToListAsync(ct);
            var pessoasDaFuncao = pessoas.Where(p => p.FuncaoId == funcao.FuncaoId).Select(p => p.Id).ToList();
            foreach (var curso in cursos.OrderBy(c => c.Nome))
            {
                var comCurso = pessoasDaFuncao.Count == 0
                    ? 0
                    : await db.Treinamentos
                        .Where(t => pessoasDaFuncao.Contains(t.TrabalhadorId)
                            && t.CursoTreinamentoId == curso.CursoTreinamentoId && t.DataValidade.Date >= hojeData)
                        .Select(t => t.TrabalhadorId)
                        .Distinct()
                        .CountAsync(ct);
                var atende = pessoasDaFuncao.Count > 0 && comCurso == pessoasDaFuncao.Count;
                var detalhe = pessoasDaFuncao.Count == 0
                    ? "Nenhuma pessoa cadastrada nesta função ainda."
                    : $"{comCurso} de {pessoasDaFuncao.Count} pessoa(s) com o curso válido.";
                itens.Add(new ItemConformidadeDto(
                    NivelFuncoes, $"{funcao.FuncaoNome} · {curso.Nome}", atende,
                    atende ? SituacaoItemConformidade.Ok : SituacaoItemConformidade.Pendente, detalhe,
                    FuncaoId: funcao.FuncaoId));
            }
        }

        // Nível 3 — pessoas liberadas.
        foreach (var pessoa in pessoas)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(db, pessoa.Id, pessoa.FuncaoId, ct);
            var temFacial = await db.FotosCadastroFacial.AnyAsync(f => f.TrabalhadorId == pessoa.Id, ct);
            var temDigital = await db.TemplatesBiometricoFutronic.AnyAsync(b => b.TrabalhadorId == pessoa.Id, ct);
            if (!temFacial || !temDigital)
                pendencias.Add($"Biometria pendente (facial {(temFacial ? "ok" : "pendente")}, digital {(temDigital ? "ok" : "pendente")})");
            var atende = pendencias.Count == 0;
            itens.Add(new ItemConformidadeDto(
                NivelPessoas, $"{pessoa.Nome} · {pessoa.FuncaoNome}", atende,
                atende ? SituacaoItemConformidade.Ok : SituacaoItemConformidade.Pendente,
                atende ? "Liberada." : string.Join("; ", pendencias),
                TrabalhadorId: pessoa.Id));
        }

        var niveis = new[]
        {
            (NivelEmpresa, "Documentos da empresa"),
            (NivelFuncoes, "Requisitos das funções"),
            (NivelPessoas, "Pessoas liberadas"),
        }.Select(n =>
        {
            var doNivel = itens.Where(i => i.Nivel == n.Item1).ToList();
            var atendidosNivel = doNivel.Count(i => i.Atendido);
            return new NivelConformidadeDto(n.Item1, n.Item2, atendidosNivel, doNivel.Count, Percentual(atendidosNivel, doNivel.Count));
        }).ToList();

        var atendidos = itens.Count(i => i.Atendido);
        var percentual = Percentual(atendidos, itens.Count);
        return new ConformidadeEmpresaDto(
            empresa.Id, empresa.RazaoSocial, empresa.Cnpj, empresa.Status == StatusEmpresa.Ativa,
            atendidos, itens.Count, percentual, Faixa(percentual), niveis, itens);
    }

    // Arredonda para baixo: 99,6% não pode aparecer como 100% "apta" com item pendente.
    public static int? Percentual(int atendidos, int exigidos) =>
        exigidos == 0 ? null : (int)Math.Floor(atendidos * 100.0 / exigidos);

    // Faixas aprovadas na especificação: 100% apta · 80 a 99% quase apta · abaixo de 80% parcialmente apta.
    public static string Faixa(int? percentual) => percentual switch
    {
        null => FaixaConformidade.SemItens,
        100 => FaixaConformidade.Apta,
        >= 80 => FaixaConformidade.QuaseApta,
        _ => FaixaConformidade.ParcialmenteApta,
    };
}

public static class SituacaoItemConformidade
{
    public const string Ok = "ok";
    public const string VenceEmBreve = "vence";
    public const string Vencido = "vencido";
    public const string Pendente = "pendente";

    public static bool Atende(string situacao) => situacao is Ok or VenceEmBreve;
}

public static class FaixaConformidade
{
    public const string Apta = "apta";
    public const string QuaseApta = "quase";
    public const string ParcialmenteApta = "parcial";
    public const string SemItens = "sem-itens";
}

public record ItemConformidadeDto(
    string Nivel,
    string Titulo,
    bool Atendido,
    string Situacao,
    string? Detalhe,
    Guid? DocumentoExigidoId = null,
    Guid? DocumentoId = null,
    DateOnly? DataValidade = null,
    string? ArquivoNome = null,
    Guid? FuncaoId = null,
    Guid? TrabalhadorId = null,
    bool ExigeValidade = false);

public record NivelConformidadeDto(string Chave, string Titulo, int Atendidos, int Exigidos, int? Percentual);

public record ConformidadeEmpresaDto(
    Guid EmpresaId,
    string RazaoSocial,
    string Cnpj,
    bool EmpresaAtiva,
    int Atendidos,
    int Exigidos,
    int? Percentual,
    string Faixa,
    List<NivelConformidadeDto> Niveis,
    List<ItemConformidadeDto> Itens);
