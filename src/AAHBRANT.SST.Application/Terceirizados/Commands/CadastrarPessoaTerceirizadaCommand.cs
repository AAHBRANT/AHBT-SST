using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Commands;

public record CadastrarPessoaTerceirizadaCommand(
    Guid ContratoId,
    Guid FuncaoId,
    string Nome,
    string Matricula,
    string Cpf,
    DateTime DataAdmissao) : IRequest<Guid>;

public class CadastrarPessoaTerceirizadaCommandValidator : AbstractValidator<CadastrarPessoaTerceirizadaCommand>
{
    public CadastrarPessoaTerceirizadaCommandValidator()
    {
        RuleFor(x => x.ContratoId).NotEmpty();
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Matricula).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Cpf).NotEmpty().Length(11).Matches("^[0-9]+$")
            .Must(CpfValidador.EhValido).WithMessage("CPF inválido.");
        RuleFor(x => x.DataAdmissao).NotEmpty();
    }
}

public class CadastrarPessoaTerceirizadaCommandHandler : IRequestHandler<CadastrarPessoaTerceirizadaCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicosSegurancaPorObra;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;

    public CadastrarPessoaTerceirizadaCommandHandler(
        IAppDbContext db, ITecnicosSegurancaPorObraService tecnicosSegurancaPorObra, IFilaNotificacaoTeams filaNotificacaoTeams)
    {
        _db = db;
        _tecnicosSegurancaPorObra = tecnicosSegurancaPorObra;
        _filaNotificacaoTeams = filaNotificacaoTeams;
    }

    public async Task<Guid> Handle(CadastrarPessoaTerceirizadaCommand request, CancellationToken ct)
    {
        var contrato = await _db.Contratos.FirstOrDefaultAsync(c => c.Id == request.ContratoId, ct)
            ?? throw new KeyNotFoundException("Contrato não encontrado.");
        if (contrato.Status != StatusContrato.Validado)
            throw new InvalidOperationException("Este contrato não está com status Validado.");

        var vaga = await _db.ContratoVagasFuncao
            .FirstOrDefaultAsync(v => v.ContratoId == request.ContratoId && v.FuncaoId == request.FuncaoId, ct)
            ?? throw new KeyNotFoundException("Não há vaga cadastrada para esta função neste contrato.");
        if (vaga.QuantidadePreenchidas >= vaga.QuantidadeVagas)
            throw new InvalidOperationException("Não há vagas disponíveis para esta função neste contrato.");

        var funcao = await _db.Funcoes.FirstOrDefaultAsync(f => f.Id == request.FuncaoId, ct)
            ?? throw new KeyNotFoundException("Função não encontrada.");

        var trabalhador = new Trabalhador
        {
            ObraId = contrato.ObraId,
            FuncaoId = request.FuncaoId,
            Nome = request.Nome,
            Matricula = request.Matricula,
            Cpf = request.Cpf,
            Vinculo = TipoVinculo.Terceirizado,
            DataAdmissao = request.DataAdmissao,
            EmpresaId = contrato.EmpresaId,
            ContratoId = contrato.Id,
        };
        _db.Trabalhadores.Add(trabalhador);
        vaga.QuantidadePreenchidas += 1;

        var episObrigatorios = await _db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == request.FuncaoId)
            .Select(m => m.CatalogoEpiId)
            .ToListAsync(ct);

        var alertasEstoqueInsuficiente = new List<Alerta>();
        foreach (var catalogoEpiId in episObrigatorios)
        {
            var estoque = await _db.EstoquesEpi
                .FirstOrDefaultAsync(e => e.CatalogoEpiId == catalogoEpiId && e.ObraId == contrato.ObraId, ct);
            var saldoAtual = estoque?.Saldo ?? 0;

            if (saldoAtual >= 1)
            {
                var entrega = new EntregaEpi
                {
                    TrabalhadorId = trabalhador.Id,
                    CatalogoEpiId = catalogoEpiId,
                    DataEntrega = DateTime.UtcNow,
                    Quantidade = 1,
                    MotivoTipo = MotivoEntregaEpi.Inicial,
                    Confirmada = false,
                };
                _db.EntregasEpi.Add(entrega);

                estoque!.Saldo -= 1;
                _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
                {
                    EstoqueEpiId = estoque.Id,
                    Tipo = TipoMovimentacaoEstoqueEpi.SaidaEntrega,
                    Quantidade = 1,
                    SaldoResultante = estoque.Saldo,
                    EntregaEpiId = entrega.Id,
                });
            }
            else
            {
                var catalogo = await _db.CatalogoEpis.FirstAsync(c => c.Id == catalogoEpiId, ct);
                var tecnicos = await _tecnicosSegurancaPorObra.ObterUsuarioIdsAsync(contrato.ObraId, ct);
                foreach (var usuarioId in tecnicos)
                {
                    var alerta = new Alerta
                    {
                        Tipo = TipoAlerta.EpiEstoqueInsuficiente,
                        Severidade = SeveridadeAlerta.Critico,
                        Titulo = $"Estoque insuficiente de {catalogo.Nome} para {trabalhador.Nome}",
                        Descricao = $"Saldo atual: {saldoAtual}. Necessário: 1 unidade para liberar {trabalhador.Nome} ({funcao.Nome}).",
                        EntidadeOrigemTipo = "Trabalhador",
                        EntidadeOrigemId = trabalhador.Id,
                        TrabalhadorId = trabalhador.Id,
                        ObraId = contrato.ObraId,
                        DestinatarioUsuarioId = usuarioId,
                    };
                    _db.Alertas.Add(alerta);
                    alertasEstoqueInsuficiente.Add(alerta);
                }
            }
        }

        await TratamentoCpfDuplicado.SalvarAsync(_db, ct);

        // Envio proativo no Teams — enfileira e segue em frente, mesmo princípio de AlertaEngineService.
        foreach (var alerta in alertasEstoqueInsuficiente)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }

        return trabalhador.Id;
    }
}
