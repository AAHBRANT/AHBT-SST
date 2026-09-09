using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Upsert de Trabalhador a partir do cadastro do G-RH (Integração G-RH, 2026-09-09 — decisão do
// usuário: G-RH é fonte única de verdade para estes campos, o SST só reflete). Usado tanto pelo
// consumidor de eventos (ServiceBusColaboradorGrhProcessor, um colaborador por vez) quanto pela
// carga inicial (ImportarColaboradoresGrhCommand, em lote) — mesma lógica de correspondência nos
// dois casos, pra não duplicar.
//
// Correspondência: por CPF (via ICpfHashService, nunca decodificando o valor criptografado) —
// inclui trabalhadores soft-deletados (IgnoreQueryFilters), senão uma reativação criaria uma
// segunda linha e colidiria com o índice único de CpfHash. Cargo por Nome (cria Funcao se não
// existir). Obra por Nome — se não encontrada, uma atualização segue sem trocar a obra atual; uma
// criação nova FALHA (ObraId é obrigatório em Trabalhador) até a obra existir no SST com esse nome.
public record SincronizarColaboradorGrhCommand(
    string Cpf,
    string Nome,
    string? Pis,
    string? Ctps,
    DateTime? DataNascimento,
    string? NomeMae,
    string? Endereco,
    string? Municipio,
    string? Uf,
    string? Cep,
    string Matricula,
    DateTime DataAdmissao,
    DateTime? DataDemissao,
    string CargoNome,
    string? CargoCboCodigo,
    decimal? Salario,
    SituacaoTrabalhador Situacao,
    string? ObraNome,
    DateTime? DataFimExperiencia1,
    DateTime? DataFimExperiencia2,
    string? TamanhoBlusaEpi,
    string? TamanhoCalcaEpi,
    string? TamanhoCalcadoEpi) : IRequest<Guid>;

public class SincronizarColaboradorGrhCommandValidator : AbstractValidator<SincronizarColaboradorGrhCommand>
{
    public SincronizarColaboradorGrhCommandValidator()
    {
        RuleFor(x => x.Cpf).NotEmpty().Length(11).Matches("^[0-9]+$")
            .Must(CpfValidador.EhValido).WithMessage("CPF inválido.");
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Matricula).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DataAdmissao).NotEmpty();
        RuleFor(x => x.CargoNome).NotEmpty().MaximumLength(200);
    }
}

public class SincronizarColaboradorGrhCommandHandler : IRequestHandler<SincronizarColaboradorGrhCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICpfHashService _cpfHash;

    public SincronizarColaboradorGrhCommandHandler(IAppDbContext db, ICpfHashService cpfHash)
    {
        _db = db;
        _cpfHash = cpfHash;
    }

    public async Task<Guid> Handle(SincronizarColaboradorGrhCommand request, CancellationToken ct)
    {
        var hash = _cpfHash.CalcularHash(request.Cpf);
        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.CpfHash == hash, ct);

        var funcao = await _db.Funcoes.FirstOrDefaultAsync(f => f.Nome == request.CargoNome, ct);
        if (funcao is null)
        {
            funcao = new Funcao { Nome = request.CargoNome, CboCodigo = request.CargoCboCodigo };
            _db.Funcoes.Add(funcao);
        }
        else if (!string.IsNullOrWhiteSpace(request.CargoCboCodigo))
        {
            funcao.CboCodigo = request.CargoCboCodigo;
        }

        Obra? obra = null;
        if (!string.IsNullOrWhiteSpace(request.ObraNome))
            obra = await _db.Obras.FirstOrDefaultAsync(o => o.Nome == request.ObraNome, ct);

        if (trabalhador is null)
        {
            if (obra is null)
                throw new InvalidOperationException(
                    $"Obra '{request.ObraNome}' não encontrada no SST — não é possível criar o trabalhador '{request.Nome}' vindo do G-RH sem uma obra correspondente.");

            trabalhador = new Trabalhador
            {
                ObraId = obra.Id,
                FuncaoId = funcao.Id,
                Nome = request.Nome,
                Matricula = request.Matricula,
                Cpf = request.Cpf,
                DataAdmissao = request.DataAdmissao,
                DataDemissao = request.DataDemissao,
            };
            _db.Trabalhadores.Add(trabalhador);
        }
        else
        {
            trabalhador.Ativo = true; // reativa se veio de um soft-delete local
            trabalhador.FuncaoId = funcao.Id;
            trabalhador.Nome = request.Nome;
            trabalhador.Matricula = request.Matricula;
            trabalhador.DataAdmissao = request.DataAdmissao;
            trabalhador.DataDemissao = request.DataDemissao;
            if (obra is not null) trabalhador.ObraId = obra.Id;
        }

        trabalhador.Pis = request.Pis;
        trabalhador.Ctps = request.Ctps;
        trabalhador.DataNascimento = request.DataNascimento;
        trabalhador.NomeMae = request.NomeMae;
        trabalhador.Endereco = request.Endereco;
        trabalhador.Municipio = request.Municipio;
        trabalhador.Uf = request.Uf;
        trabalhador.Cep = request.Cep;
        trabalhador.Salario = request.Salario;
        trabalhador.Situacao = request.Situacao;
        trabalhador.DataFimExperiencia1 = request.DataFimExperiencia1;
        trabalhador.DataFimExperiencia2 = request.DataFimExperiencia2;
        trabalhador.TamanhoBlusaEpi = request.TamanhoBlusaEpi;
        trabalhador.TamanhoCalcaEpi = request.TamanhoCalcaEpi;
        trabalhador.TamanhoCalcadoEpi = request.TamanhoCalcadoEpi;

        // Desligado reflete em Ativo (mesmo comportamento visual de soft-delete manual já usado no
        // resto do sistema) — Ativo/Afastado continuam visíveis normalmente.
        if (request.Situacao == SituacaoTrabalhador.Desligado)
            trabalhador.Ativo = false;

        await TratamentoCpfDuplicado.SalvarAsync(_db, ct);
        return trabalhador.Id;
    }
}
