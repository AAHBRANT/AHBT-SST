using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes;

// Entrada de uma pessoa externa (terceiro, visitante, transeunte...). Id preenchido = atualiza a linha
// existente; vazio = cria. Cpf/Telefone: nulo ou valor mascarado (o que a tela recebeu) = mantém o que já
// está gravado; "" = apaga; dígitos novos = substitui.
public record PessoaExternaEntrada(
    Guid? Id,
    string Nome,
    VinculoPessoaExterna Vinculo,
    string? Empresa = null,
    string? Cpf = null,
    string? Telefone = null,
    int? Idade = null,
    string? Lesao = null,
    string? Atendimento = null,
    ResultadoPessoaExterna Resultado = ResultadoPessoaExterna.SemLesao);

public class PessoaExternaEntradaValidator : AbstractValidator<PessoaExternaEntrada>
{
    public PessoaExternaEntradaValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("Informe o nome da pessoa externa.").MaximumLength(200);
        RuleFor(x => x.Vinculo).IsInEnum();
        RuleFor(x => x.Resultado).IsInEnum();
        RuleFor(x => x.Empresa).MaximumLength(200);
        RuleFor(x => x.Lesao).MaximumLength(500);
        RuleFor(x => x.Atendimento).MaximumLength(500);
        RuleFor(x => x.Idade).InclusiveBetween(0, 120).When(x => x.Idade.HasValue)
            .WithMessage("Idade da pessoa externa inválida.");
        RuleFor(x => x.Cpf)
            .Must(c => CpfValidador.EhValido(CpfMascarador.ApenasDigitos(c)))
            .WithMessage("CPF da pessoa externa inválido.")
            .When(x => !AcidentePessoasExternasSync.MantemValorGravado(x.Cpf) && !string.IsNullOrEmpty(x.Cpf));
        RuleFor(x => x.Telefone)
            .Must(t => AcidentePessoasExternasSync.Digitos(t).Length is >= 10 and <= 13)
            .WithMessage("Telefone da pessoa externa inválido (informe DDD e número).")
            .When(x => !AcidentePessoasExternasSync.MantemValorGravado(x.Telefone) && !string.IsNullOrEmpty(x.Telefone));
    }
}

// Mesmo padrão de AcidenteEnvolvidosSync: Resolver (normaliza), Validar (regra do tipo), Carregar (DTOs
// mascarados) e Sincronizar (grava).
public static class AcidentePessoasExternasSync
{
    public const int MaximoPessoas = 20;

    public static List<PessoaExternaEntrada> Resolver(IEnumerable<PessoaExternaEntrada>? entradas) =>
        (entradas ?? Enumerable.Empty<PessoaExternaEntrada>()).ToList();

    // Valor nulo ou mascarado (tem '*') significa "não mexer".
    public static bool MantemValorGravado(string? valor) => valor is null || valor.Contains('*');

    public static string Digitos(string? valor) => CpfMascarador.ApenasDigitos(valor);

    // Regra do tipo OcorrenciaComPessoaExterna: precisa de pelo menos uma pessoa externa e não pode ter
    // funcionário com papel Envolvido (testemunha pode). Os demais tipos não têm essa restrição.
    public static void ValidarRegraDoTipo(TipoOcorrencia tipo, int totalPessoasExternas, int totalFuncionariosEnvolvidos)
    {
        if (tipo != TipoOcorrencia.OcorrenciaComPessoaExterna) return;
        if (totalPessoasExternas == 0)
            throw new InvalidOperationException("Informe ao menos uma pessoa externa envolvida na ocorrência.");
        if (totalFuncionariosEnvolvidos > 0)
            throw new InvalidOperationException(
                "Ocorrência com pessoa externa não tem funcionário envolvido; funcionários que presenciaram entram como testemunha.");
    }

    // Tipo 7 nunca tem afastamento nem CAT (a vítima não é empregada): gravidade e dias debitados fixos.
    public static void AplicarPadraoDoTipo(Acidente acidente)
    {
        if (acidente.Tipo != TipoOcorrencia.OcorrenciaComPessoaExterna) return;
        acidente.Gravidade = GravidadeAcidente.SemAfastamento;
        acidente.DiasDebitados = 0;
        acidente.HouveAfastamento = false;
        acidente.DiasAfastamento = null;
        acidente.NumeroCat = null;
    }

    public static async Task<Dictionary<Guid, List<AcidentePessoaExternaDto>>> CarregarAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> acidenteIds, CancellationToken ct)
    {
        var linhas = await db.AcidentesPessoasExternas.AsNoTracking()
            .Where(p => acidenteIds.Contains(p.AcidenteId))
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(ct);
        return linhas.GroupBy(p => p.AcidenteId).ToDictionary(g => g.Key, g => g.Select(Mapear).ToList());
    }

    public static AcidentePessoaExternaDto Mapear(AcidentePessoaExterna p) => new(
        p.Id, p.Nome, p.Vinculo, p.Empresa,
        string.IsNullOrEmpty(p.Cpf) ? null : CpfMascarador.Mascarar(p.Cpf),
        TelefoneMascarador.Mascarar(p.Telefone),
        p.Idade, p.Lesao, p.Atendimento, p.Resultado);

    // entradas == null: não mexe nas pessoas já gravadas (clientes que salvam outros dados). Lista (mesmo
    // vazia) substitui o conjunto. Devolve o total de pessoas ativas após a operação.
    public static async Task<int> SincronizarAsync(IAppDbContext db, Acidente acidente, List<PessoaExternaEntrada>? entradas, CancellationToken ct)
    {
        var atuais = await db.AcidentesPessoasExternas.Where(p => p.AcidenteId == acidente.Id).ToListAsync(ct);
        if (entradas is null) return atuais.Count;

        var idsMantidos = entradas.Where(e => e.Id.HasValue).Select(e => e.Id!.Value).ToHashSet();
        foreach (var inexistente in idsMantidos.Where(id => atuais.All(a => a.Id != id)))
            throw new KeyNotFoundException($"Pessoa externa {inexistente} não pertence a esta ocorrência.");

        db.AcidentesPessoasExternas.RemoveRange(atuais.Where(a => !idsMantidos.Contains(a.Id)));

        foreach (var entrada in entradas)
        {
            var pessoa = entrada.Id.HasValue ? atuais.First(a => a.Id == entrada.Id.Value) : null;
            var nova = pessoa is null;
            pessoa ??= new AcidentePessoaExterna { AcidenteId = acidente.Id };

            pessoa.Nome = entrada.Nome.Trim();
            pessoa.Vinculo = entrada.Vinculo;
            pessoa.Empresa = Limpar(entrada.Empresa);
            pessoa.Idade = entrada.Idade;
            pessoa.Lesao = Limpar(entrada.Lesao);
            pessoa.Atendimento = Limpar(entrada.Atendimento);
            pessoa.Resultado = entrada.Resultado;
            if (!MantemValorGravado(entrada.Cpf)) pessoa.Cpf = entrada.Cpf!.Length == 0 ? null : Digitos(entrada.Cpf);
            if (!MantemValorGravado(entrada.Telefone)) pessoa.Telefone = entrada.Telefone!.Length == 0 ? null : Digitos(entrada.Telefone);

            if (nova) db.AcidentesPessoasExternas.Add(pessoa);
        }
        return entradas.Count;
    }

    private static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

// Telefone sai só com os 4 últimos dígitos (LGPD), no mesmo espírito de CpfMascarador.Mascarar.
public static class TelefoneMascarador
{
    public static string? Mascarar(string? telefone)
    {
        var digitos = CpfMascarador.ApenasDigitos(telefone);
        if (digitos.Length == 0) return null;
        return digitos.Length <= 4 ? new string('*', digitos.Length) : $"{new string('*', digitos.Length - 4)}{digitos[^4..]}";
    }
}
