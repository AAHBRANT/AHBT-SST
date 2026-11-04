using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.OrdensServico;

// Situação da OS emitida, tirada das assinaturas do Motor: o funcionário assina primeiro (digital ou
// facial) e o técnico de segurança depois (sessão logada). Só com as duas a OS é "Valida".
public static class SituacaoOrdemServico
{
    public const string AguardandoFuncionario = "AguardandoFuncionario";
    public const string AguardandoTecnico = "AguardandoTecnico";
    public const string Valida = "Valida";
}

public record AssinaturaOsDto(string Nome, string? Funcao, DateTime AssinadoEm, MetodoAutenticacaoAssinatura Metodo);

public class OrdemServicoEmitidaDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int Revisao { get; set; }
    public DateTime EmitidaEm { get; set; }
    public Guid ObraId { get; set; }
    public Guid? DocumentoAssinaturaId { get; set; }
    public string Situacao { get; set; } = SituacaoOrdemServico.AguardandoFuncionario;
    // Só a revisão mais recente vale; as anteriores ficam no histórico.
    public bool Vigente { get; set; }
    public AssinaturaOsDto? Funcionario { get; set; }
    public AssinaturaOsDto? Tecnico { get; set; }
}

internal static class AssinaturasOrdemServico
{
    // Assinaturas de cada OS, separadas entre a do funcionário (dono da OS, por digital/facial) e a
    // do técnico (sessão logada). IgnoreQueryFilters no trabalhador: quem assinou e depois saiu da obra
    // continua aparecendo.
    public static async Task<Dictionary<Guid, (Guid? DocumentoId, AssinaturaOsDto? Funcionario, AssinaturaOsDto? Tecnico)>> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<OrdemServico> ordens, CancellationToken ct)
    {
        var ids = ordens.Select(o => o.Id).ToList();
        var documentos = await db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == OrdemServico.EntidadeTipoAssinatura && ids.Contains(d.EntidadeId))
            .Select(d => new { d.Id, d.EntidadeId })
            .ToListAsync(ct);
        var idsDocumentos = documentos.Select(d => d.Id).ToList();
        var signatarios = await db.DocumentoSignatarios
            .Where(s => idsDocumentos.Contains(s.DocumentoAssinaturaId))
            .Join(db.Trabalhadores.IgnoreQueryFilters(), s => s.TrabalhadorId, t => t.Id,
                (s, t) => new { s.DocumentoAssinaturaId, s.TrabalhadorId, t.Nome, t.FuncaoId, s.AssinadoEm, s.MetodoAutenticacao })
            .ToListAsync(ct);
        var idsFuncoes = signatarios.Select(s => s.FuncaoId).Distinct().ToList();
        var funcoes = await db.Funcoes.IgnoreQueryFilters()
            .Where(f => idsFuncoes.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var resultado = new Dictionary<Guid, (Guid?, AssinaturaOsDto?, AssinaturaOsDto?)>();
        foreach (var os in ordens)
        {
            var documento = documentos.FirstOrDefault(d => d.EntidadeId == os.Id);
            var doDocumento = documento is null
                ? new()
                : signatarios.Where(s => s.DocumentoAssinaturaId == documento.Id).OrderBy(s => s.AssinadoEm).ToList();
            var funcionario = doDocumento.FirstOrDefault(s =>
                s.TrabalhadorId == os.TrabalhadorId && s.MetodoAutenticacao != MetodoAutenticacaoAssinatura.SessaoLogada);
            var tecnico = doDocumento.FirstOrDefault(s => s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada);
            resultado[os.Id] = (
                documento?.Id,
                funcionario is null ? null : new AssinaturaOsDto(funcionario.Nome, funcoes.GetValueOrDefault(funcionario.FuncaoId), funcionario.AssinadoEm, funcionario.MetodoAutenticacao),
                tecnico is null ? null : new AssinaturaOsDto(tecnico.Nome, funcoes.GetValueOrDefault(tecnico.FuncaoId), tecnico.AssinadoEm, tecnico.MetodoAutenticacao));
        }
        return resultado;
    }

    public static string Situacao(AssinaturaOsDto? funcionario, AssinaturaOsDto? tecnico) =>
        funcionario is null ? SituacaoOrdemServico.AguardandoFuncionario
        : tecnico is null ? SituacaoOrdemServico.AguardandoTecnico
        : SituacaoOrdemServico.Valida;
}
