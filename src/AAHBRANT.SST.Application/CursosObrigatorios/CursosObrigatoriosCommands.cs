using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.GeracaoPgr;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosObrigatorios;

// "Regerar a partir do PGR": também roda sozinho ao aprovar o PGR gerado pela EAP.
public record GerarCursosObrigatoriosObraCommand(Guid ObraId) : IRequest<ResultadoGeracaoCursosObrigatoriosDto>;

public class GerarCursosObrigatoriosObraCommandHandler : IRequestHandler<GerarCursosObrigatoriosObraCommand, ResultadoGeracaoCursosObrigatoriosDto>
{
    private readonly IAppDbContext _db;
    public GerarCursosObrigatoriosObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ResultadoGeracaoCursosObrigatoriosDto> Handle(GerarCursosObrigatoriosObraCommand request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId) || !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");
        return await GeradorCursosObrigatorios.GerarAsync(_db, request.ObraId, ct);
    }
}

// Aprovar ou recusar uma sugestão (Engenheiro de Segurança). Recusa exige justificativa.
public record DecidirCursoObrigatorioCommand(Guid Id, bool Aprovar, string? Justificativa, Guid? UsuarioId, string? NomeUsuario) : IRequest;

public class DecidirCursoObrigatorioCommandValidator : AbstractValidator<DecidirCursoObrigatorioCommand>
{
    public DecidirCursoObrigatorioCommandValidator()
    {
        RuleFor(x => x.Justificativa).NotEmpty().When(x => !x.Aprovar)
            .WithMessage("Informe por que o curso não é obrigatório para esta função.");
        RuleFor(x => x.Justificativa).MaximumLength(500);
    }
}

public class DecidirCursoObrigatorioCommandHandler : IRequestHandler<DecidirCursoObrigatorioCommand>
{
    private readonly IAppDbContext _db;
    public DecidirCursoObrigatorioCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DecidirCursoObrigatorioCommand request, CancellationToken ct)
    {
        var item = await _db.CursosObrigatoriosFuncaoObra.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Curso obrigatório {request.Id} não encontrado.");
        item.Status = request.Aprovar ? StatusCursoObrigatorio.Aprovado : StatusCursoObrigatorio.Recusado;
        item.JustificativaRecusa = request.Aprovar ? null : request.Justificativa!.Trim();
        item.DecididoPorUsuarioId = request.UsuarioId;
        item.DecididoPorNome = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        item.DecididoEmUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

// "Aprovar todas as sugestões" da obra. Devolve quantas foram aprovadas.
public record AprovarSugestoesCursosObrigatoriosCommand(Guid ObraId, Guid? UsuarioId, string? NomeUsuario) : IRequest<int>;

public class AprovarSugestoesCursosObrigatoriosCommandHandler : IRequestHandler<AprovarSugestoesCursosObrigatoriosCommand, int>
{
    private readonly IAppDbContext _db;
    public AprovarSugestoesCursosObrigatoriosCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(AprovarSugestoesCursosObrigatoriosCommand request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");
        var sugeridos = await _db.CursosObrigatoriosFuncaoObra
            .Where(c => c.ObraId == request.ObraId && c.Status == StatusCursoObrigatorio.Sugerido).ToListAsync(ct);
        var nome = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        var agora = DateTime.UtcNow;
        foreach (var item in sugeridos)
        {
            item.Status = StatusCursoObrigatorio.Aprovado;
            item.DecididoPorUsuarioId = request.UsuarioId;
            item.DecididoPorNome = nome;
            item.DecididoEmUtc = agora;
        }
        await _db.SaveChangesAsync(ct);
        return sugeridos.Count;
    }
}

// "+ Adicionar curso manualmente": entra já aprovado por quem adicionou. Se o par já existia
// (sugerido ou recusado), passa a aprovado em vez de duplicar.
public record AdicionarCursoObrigatorioCommand(Guid ObraId, Guid FuncaoId, Guid CursoTreinamentoId, Guid? UsuarioId, string? NomeUsuario) : IRequest<Guid>;

public class AdicionarCursoObrigatorioCommandHandler : IRequestHandler<AdicionarCursoObrigatorioCommand, Guid>
{
    private readonly IAppDbContext _db;
    public AdicionarCursoObrigatorioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AdicionarCursoObrigatorioCommand request, CancellationToken ct)
    {
        if (!_db.ObraNoEscopo(request.ObraId) || !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");
        if (!await _db.Funcoes.AnyAsync(f => f.Id == request.FuncaoId, ct))
            throw new KeyNotFoundException($"Função {request.FuncaoId} não encontrada.");
        if (!await _db.CursosTreinamento.AnyAsync(c => c.Id == request.CursoTreinamentoId, ct))
            throw new KeyNotFoundException($"Curso {request.CursoTreinamentoId} não encontrado.");

        var item = await _db.CursosObrigatoriosFuncaoObra.IgnoreQueryFilters().FirstOrDefaultAsync(c =>
            c.ObraId == request.ObraId && c.FuncaoId == request.FuncaoId && c.CursoTreinamentoId == request.CursoTreinamentoId, ct);
        if (item is null)
        {
            item = new CursoObrigatorioFuncaoObra
            {
                ObraId = request.ObraId,
                FuncaoId = request.FuncaoId,
                CursoTreinamentoId = request.CursoTreinamentoId,
                OrigemSugestao = OrigemCursoObrigatorio.Manual,
                Motivo = "Adicionado manualmente",
            };
            _db.CursosObrigatoriosFuncaoObra.Add(item);
        }
        item.Ativo = true;
        item.Status = StatusCursoObrigatorio.Aprovado;
        item.JustificativaRecusa = null;
        item.DecididoPorUsuarioId = request.UsuarioId;
        item.DecididoPorNome = await SerializacaoGeracao.NomeUsuarioAsync(_db, request.UsuarioId, request.NomeUsuario, ct);
        item.DecididoEmUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }
}

// Regras risco → NR → curso (QSMS). Id nulo cria; Ativo=false tira a regra de uso sem apagar.
public record SalvarRegraCursoObrigatorioCommand(
    Guid? Id,
    string Norma,
    Guid? CursoTreinamentoId,
    string Descricao,
    bool AplicaTodasFuncoes,
    string? PalavrasChave,
    bool Ativo) : IRequest<Guid>;

public class SalvarRegraCursoObrigatorioCommandValidator : AbstractValidator<SalvarRegraCursoObrigatorioCommand>
{
    public SalvarRegraCursoObrigatorioCommandValidator()
    {
        RuleFor(x => x.Norma).NotEmpty().MaximumLength(20)
            .Must(n => GeradorCursosObrigatorios.NumeroNr(n) is not null).WithMessage("Informe a NR no formato NR-35.");
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PalavrasChave).MaximumLength(1000);
        RuleFor(x => x.PalavrasChave).NotEmpty().When(x => !x.AplicaTodasFuncoes)
            .WithMessage("Informe as palavras do PGR que tornam a NR obrigatória, ou marque \"todas as funções\".");
    }
}

public class SalvarRegraCursoObrigatorioCommandHandler : IRequestHandler<SalvarRegraCursoObrigatorioCommand, Guid>
{
    private readonly IAppDbContext _db;
    public SalvarRegraCursoObrigatorioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(SalvarRegraCursoObrigatorioCommand request, CancellationToken ct)
    {
        if (request.CursoTreinamentoId is { } cursoId && !await _db.CursosTreinamento.AnyAsync(c => c.Id == cursoId, ct))
            throw new KeyNotFoundException($"Curso {cursoId} não encontrado.");

        RegraCursoObrigatorio regra;
        if (request.Id is { } id)
            regra = await _db.RegrasCursoObrigatorio.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new KeyNotFoundException($"Regra {id} não encontrada.");
        else
            _db.RegrasCursoObrigatorio.Add(regra = new RegraCursoObrigatorio());

        regra.Norma = request.Norma.Trim().ToUpperInvariant();
        regra.CursoTreinamentoId = request.CursoTreinamentoId;
        regra.Descricao = request.Descricao.Trim();
        regra.AplicaTodasFuncoes = request.AplicaTodasFuncoes;
        regra.PalavrasChave = request.AplicaTodasFuncoes ? null : request.PalavrasChave?.Trim();
        regra.Ativo = request.Ativo;
        await _db.SaveChangesAsync(ct);
        return regra.Id;
    }
}
