using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Contratos.Commands;

// Contrato cadastrado à mão pelo técnico de segurança (18/10/2026). Até aqui o contrato só nascia pelo
// webhook do G-Juri, que ainda não existe — sem contrato não havia vaga, e sem vaga não havia como
// cadastrar pessoa terceirizada. O contrato manual segue as mesmas regras do que vem do G-Juri: uma
// obra, vigência e vagas por função; quem preenche as vagas continua sendo CadastrarPessoaTerceirizadaCommand.
//
// Contrato que veio do G-Juri não é editado nem encerrado por aqui: o G-Juri é a fonte dele, e um
// reenvio do webhook desfaria a edição manual.

public record VagaContratoInput(Guid FuncaoId, int Quantidade);

public interface IDadosContratoManual
{
    string NumeroContrato { get; }
    DateOnly DataInicioVigencia { get; }
    DateOnly DataFimVigencia { get; }
    List<VagaContratoInput> Vagas { get; }
}

public record CriarContratoCommand(
    Guid EmpresaId,
    Guid ObraId,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    List<VagaContratoInput> Vagas) : IRequest<Guid>, IDadosContratoManual;

public record AtualizarContratoCommand(
    Guid Id,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    List<VagaContratoInput> Vagas) : IRequest, IDadosContratoManual;

public class DadosContratoManualValidator : AbstractValidator<IDadosContratoManual>
{
    public DadosContratoManualValidator()
    {
        RuleFor(x => x.NumeroContrato).NotEmpty().WithMessage("Informe o número do contrato.")
            .MaximumLength(50).WithMessage("O número do contrato tem no máximo 50 caracteres.");
        RuleFor(x => x.DataInicioVigencia).NotEmpty().WithMessage("Informe o início da vigência.");
        RuleFor(x => x.DataFimVigencia).NotEmpty().WithMessage("Informe o fim da vigência.")
            .GreaterThanOrEqualTo(x => x.DataInicioVigencia).WithMessage("O fim da vigência não pode ser anterior ao início.");
        RuleFor(x => x.Vagas).NotEmpty().WithMessage("Informe ao menos uma vaga por função.")
            .Must(l => l is null || l.Select(i => i.FuncaoId).Distinct().Count() == l.Count)
            .WithMessage("Cada função deve aparecer uma vez só nas vagas.");
        RuleForEach(x => x.Vagas).ChildRules(vaga =>
        {
            vaga.RuleFor(i => i.FuncaoId).NotEmpty().WithMessage("Escolha a função da vaga.");
            vaga.RuleFor(i => i.Quantidade).GreaterThan(0).WithMessage("A quantidade de vagas deve ser maior que zero.");
        });
    }
}

internal static class RegrasContratoManual
{
    public static async Task GarantirFuncoesAsync(IAppDbContext db, List<VagaContratoInput> vagas, CancellationToken ct)
    {
        var ids = vagas.Select(v => v.FuncaoId).Distinct().ToList();
        var encontradas = await db.Funcoes.CountAsync(f => ids.Contains(f.Id), ct);
        if (encontradas != ids.Count)
            throw new KeyNotFoundException("Função não encontrada.");
    }

    public static async Task GarantirNumeroUnicoAsync(IAppDbContext db, Guid empresaId, string numero, Guid? ignorarId, CancellationToken ct)
    {
        var repetido = await db.Contratos.IgnoreQueryFilters()
            .AnyAsync(c => c.Ativo && c.EmpresaId == empresaId && c.NumeroContrato == numero && c.Id != ignorarId, ct);
        if (repetido)
            throw new InvalidOperationException($"Esta empresa já tem um contrato com o número {numero}.");
    }

    public static async Task<Contrato> ObterManualEditavelAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var contrato = await db.Contratos.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Contrato não encontrado.");
        if (contrato.VeioDoGJuri)
            throw new InvalidOperationException("Este contrato veio do G-Juri. Alterações devem ser feitas lá.");
        if (contrato.Status != StatusContrato.Validado)
            throw new InvalidOperationException("Contrato encerrado ou cancelado não pode ser alterado.");
        return contrato;
    }
}

public class CriarContratoCommandValidator : AbstractValidator<CriarContratoCommand>
{
    public CriarContratoCommandValidator()
    {
        RuleFor(x => x.EmpresaId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty().WithMessage("Escolha a obra do contrato.");
        Include(new DadosContratoManualValidator());
    }
}

public class CriarContratoCommandHandler : IRequestHandler<CriarContratoCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarContratoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarContratoCommand request, CancellationToken ct)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == request.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");
        if (empresa.Status != StatusEmpresa.Ativa)
            throw new InvalidOperationException("Empresa inativa não pode receber contrato novo.");

        // _db.Obras já vem filtrado pelo escopo do usuário: técnico só abre contrato na obra dele.
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        await RegrasContratoManual.GarantirFuncoesAsync(_db, request.Vagas, ct);
        var numero = request.NumeroContrato.Trim();
        await RegrasContratoManual.GarantirNumeroUnicoAsync(_db, empresa.Id, numero, null, ct);

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id,
            ObraId = request.ObraId,
            NumeroContrato = numero,
            DataInicioVigencia = request.DataInicioVigencia,
            DataFimVigencia = request.DataFimVigencia,
            Status = StatusContrato.Validado,
        };
        _db.Contratos.Add(contrato);
        foreach (var vaga in request.Vagas)
        {
            _db.ContratoVagasFuncao.Add(new ContratoVagaFuncao
            {
                ContratoId = contrato.Id, FuncaoId = vaga.FuncaoId, QuantidadeVagas = vaga.Quantidade,
            });
        }

        await _db.SaveChangesAsync(ct);
        return contrato.Id;
    }
}

public class AtualizarContratoCommandValidator : AbstractValidator<AtualizarContratoCommand>
{
    public AtualizarContratoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new DadosContratoManualValidator());
    }
}

public class AtualizarContratoCommandHandler : IRequestHandler<AtualizarContratoCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarContratoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarContratoCommand request, CancellationToken ct)
    {
        var contrato = await RegrasContratoManual.ObterManualEditavelAsync(_db, request.Id, ct);
        await RegrasContratoManual.GarantirFuncoesAsync(_db, request.Vagas, ct);
        var numero = request.NumeroContrato.Trim();
        await RegrasContratoManual.GarantirNumeroUnicoAsync(_db, contrato.EmpresaId, numero, contrato.Id, ct);

        contrato.NumeroContrato = numero;
        contrato.DataInicioVigencia = request.DataInicioVigencia;
        contrato.DataFimVigencia = request.DataFimVigencia;

        // IgnoreQueryFilters: a vaga removida antes fica inativa (soft delete) e o índice único
        // (ContratoId, FuncaoId) não deixaria criar outra para a mesma função — ela é reativada.
        var vagasAtuais = await _db.ContratoVagasFuncao.IgnoreQueryFilters()
            .Where(v => v.ContratoId == contrato.Id)
            .ToListAsync(ct);
        var funcoesPedidas = request.Vagas.ToDictionary(v => v.FuncaoId, v => v.Quantidade);

        foreach (var vaga in vagasAtuais.Where(v => v.Ativo && !funcoesPedidas.ContainsKey(v.FuncaoId)))
        {
            if (vaga.QuantidadePreenchidas > 0)
                throw new InvalidOperationException("Não é possível remover uma função que já tem pessoas cadastradas no contrato.");
            vaga.Ativo = false;
        }

        foreach (var (funcaoId, quantidade) in funcoesPedidas)
        {
            var vaga = vagasAtuais.FirstOrDefault(v => v.FuncaoId == funcaoId);
            if (vaga is null)
            {
                _db.ContratoVagasFuncao.Add(new ContratoVagaFuncao { ContratoId = contrato.Id, FuncaoId = funcaoId, QuantidadeVagas = quantidade });
                continue;
            }
            if (quantidade < vaga.QuantidadePreenchidas)
                throw new InvalidOperationException(
                    $"A quantidade de vagas não pode ficar abaixo das {vaga.QuantidadePreenchidas} pessoa(s) já cadastrada(s) nesta função.");
            vaga.Ativo = true;
            vaga.QuantidadeVagas = quantidade;
        }

        await _db.SaveChangesAsync(ct);
    }
}

// Encerrar não desliga ninguém (mesma regra do encerramento vindo do G-Juri): devolve quantas pessoas
// seguem ativas para a tela avisar o técnico de que o desligamento é à parte.
public record EncerrarContratoCommand(Guid Id, DateOnly DataEncerramento) : IRequest<int>;

public class EncerrarContratoCommandValidator : AbstractValidator<EncerrarContratoCommand>
{
    public EncerrarContratoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DataEncerramento).NotEmpty().WithMessage("Informe a data de encerramento.");
    }
}

public class EncerrarContratoCommandHandler : IRequestHandler<EncerrarContratoCommand, int>
{
    private readonly IAppDbContext _db;
    public EncerrarContratoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(EncerrarContratoCommand request, CancellationToken ct)
    {
        var contrato = await RegrasContratoManual.ObterManualEditavelAsync(_db, request.Id, ct);
        if (request.DataEncerramento < contrato.DataInicioVigencia)
            throw new InvalidOperationException("A data de encerramento não pode ser anterior ao início da vigência.");

        contrato.Status = StatusContrato.Encerrado;
        contrato.DataEncerramento = request.DataEncerramento;
        await _db.SaveChangesAsync(ct);

        return await _db.Trabalhadores.IgnoreQueryFilters()
            .CountAsync(t => t.ContratoId == contrato.Id && t.Ativo && t.DataDemissao == null, ct);
    }
}
