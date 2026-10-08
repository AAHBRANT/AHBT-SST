using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ideias.Commands;

// §16 — vincula a sugestão a uma ideia já existente (duplicidade). Não descarta nem apaga nada: a
// ideia vinculada continua no banco, apontando para a principal.
public record VincularIdeiaCommand(Guid IdeiaId, Guid PrincipalId, AutorIdeia Autor) : IRequest<IdeiaDetalheDto>;

public class VincularIdeiaCommandValidator : AbstractValidator<VincularIdeiaCommand>
{
    public VincularIdeiaCommandValidator()
    {
        RuleFor(x => x.IdeiaId).NotEmpty();
        RuleFor(x => x.PrincipalId).NotEmpty().NotEqual(x => x.IdeiaId).WithMessage("Uma ideia não pode ser vinculada a ela mesma.");
    }
}

public class VincularIdeiaCommandHandler : IRequestHandler<VincularIdeiaCommand, IdeiaDetalheDto>
{
    private readonly IAppDbContext _db;

    public VincularIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaDetalheDto> Handle(VincularIdeiaCommand r, CancellationToken ct)
    {
        await VinculoIdeia.AplicarAsync(_db, r.IdeiaId, r.PrincipalId, r.Autor, ct);
        await _db.SaveChangesAsync(ct);
        return await IdeiaDetalheLoader.CarregarAsync(_db, r.IdeiaId, ct);
    }
}

internal static class VinculoIdeia
{
    // Não chama SaveChanges: o chamador persiste junto com o resto da operação.
    public static async Task AplicarAsync(IAppDbContext db, Guid ideiaId, Guid principalId, AutorIdeia autor, CancellationToken ct)
    {
        var ideia = await db.Ideias.FirstOrDefaultAsync(x => x.Id == ideiaId, ct)
            ?? throw new KeyNotFoundException($"Ideia {ideiaId} não encontrada.");
        var principal = await db.Ideias.FirstOrDefaultAsync(x => x.Id == principalId, ct)
            ?? throw new KeyNotFoundException($"Ideia {principalId} não encontrada.");

        if (principal.IdeiaPrincipalId is not null)
            throw new InvalidOperationException($"{principal.Codigo} já está vinculada a outra ideia. Vincule à ideia principal.");
        if (await db.Ideias.AnyAsync(x => x.IdeiaPrincipalId == ideiaId, ct))
            throw new InvalidOperationException($"{ideia.Codigo} já é principal de outras ideias e não pode ser vinculada a outra.");

        ideia.IdeiaPrincipalId = principal.Id;
        ideia.IdeiaSemelhanteId = null;
        ideia.PerguntaPendente = TipoPerguntaIdeia.Nenhuma;
        ideia.PerguntaMensagemId = null;

        IdeiaMapeador.Historico(db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Vinculo,
            $"{autor.Nome} vinculou esta ideia à {principal.Codigo} ({principal.Titulo}).", autor);
        IdeiaMapeador.Historico(db.IdeiaHistoricos, principal, TipoHistoricoIdeia.Vinculo,
            $"{ideia.Codigo} ({ideia.Titulo}) foi vinculada a esta ideia.", autor);
        db.IdeiaComentarios.Add(new IdeiaComentario
        {
            IdeiaId = principal.Id,
            AutorUsuarioId = autor.UsuarioId,
            AutorNome = string.IsNullOrWhiteSpace(autor.Nome) ? "Usuário" : autor.Nome,
            Texto = $"Nova sugestão vinculada: {ideia.Codigo} — {ideia.Titulo}\n\n{ideia.MensagemOriginal}"
        });
    }
}

// §10 — requisito funcional a partir de uma ideia aprovada.
public record CriarRequisitoIdeiaCommand(
    Guid IdeiaId, string Titulo, string Descricao, string CriteriosAceite, AutorIdeia Autor) : IRequest<IdeiaRequisitoDto>;

public class CriarRequisitoIdeiaCommandValidator : AbstractValidator<CriarRequisitoIdeiaCommand>
{
    public CriarRequisitoIdeiaCommandValidator()
    {
        RuleFor(x => x.IdeiaId).NotEmpty();
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.CriteriosAceite).NotEmpty().WithMessage("Defina ao menos um critério de aceite.").MaximumLength(4000);
    }
}

public class CriarRequisitoIdeiaCommandHandler : IRequestHandler<CriarRequisitoIdeiaCommand, IdeiaRequisitoDto>
{
    private static readonly StatusIdeia[] StatusQuePermitem =
        [StatusIdeia.Aprovada, StatusIdeia.Priorizada, StatusIdeia.EmDesenvolvimento, StatusIdeia.EmTesteValidacao];

    private readonly IAppDbContext _db;

    public CriarRequisitoIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaRequisitoDto> Handle(CriarRequisitoIdeiaCommand r, CancellationToken ct)
    {
        var ideia = await _db.Ideias.FirstOrDefaultAsync(x => x.Id == r.IdeiaId, ct)
            ?? throw new KeyNotFoundException($"Ideia {r.IdeiaId} não encontrada.");
        if (!StatusQuePermitem.Contains(ideia.Status))
            throw new InvalidOperationException("Só é possível criar requisito para uma ideia aprovada.");

        var requisito = new IdeiaRequisito
        {
            IdeiaId = ideia.Id,
            Titulo = r.Titulo.Trim(),
            Descricao = r.Descricao.Trim(),
            CriteriosAceite = r.CriteriosAceite.Trim()
        };
        _db.IdeiaRequisitos.Add(requisito);
        IdeiaMapeador.Historico(_db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Requisito,
            $"{r.Autor.Nome} criou o requisito \"{requisito.Titulo}\".", r.Autor);
        await _db.SaveChangesAsync(ct);

        return new IdeiaRequisitoDto
        {
            Id = requisito.Id, Titulo = requisito.Titulo, Descricao = requisito.Descricao,
            CriteriosAceite = requisito.CriteriosAceite, Status = requisito.Status
        };
    }
}

public record AprovarRequisitoIdeiaCommand(Guid RequisitoId, AutorIdeia Autor) : IRequest<IdeiaRequisitoDto>;

public class AprovarRequisitoIdeiaCommandHandler : IRequestHandler<AprovarRequisitoIdeiaCommand, IdeiaRequisitoDto>
{
    private readonly IAppDbContext _db;

    public AprovarRequisitoIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaRequisitoDto> Handle(AprovarRequisitoIdeiaCommand r, CancellationToken ct)
    {
        var requisito = await _db.IdeiaRequisitos.Include(x => x.Ideia)
            .FirstOrDefaultAsync(x => x.Id == r.RequisitoId, ct)
            ?? throw new KeyNotFoundException($"Requisito {r.RequisitoId} não encontrado.");
        if (requisito.Status == StatusRequisitoIdeia.Aprovado)
            throw new InvalidOperationException("O requisito já está aprovado.");

        requisito.Status = StatusRequisitoIdeia.Aprovado;
        requisito.AprovadoEmUtc = DateTime.UtcNow;
        requisito.AprovadoPorNome = r.Autor.Nome;
        if (requisito.Ideia is not null)
            IdeiaMapeador.Historico(_db.IdeiaHistoricos, requisito.Ideia, TipoHistoricoIdeia.Requisito,
                $"{r.Autor.Nome} aprovou o requisito \"{requisito.Titulo}\".", r.Autor);
        await _db.SaveChangesAsync(ct);

        return new IdeiaRequisitoDto
        {
            Id = requisito.Id, Titulo = requisito.Titulo, Descricao = requisito.Descricao,
            CriteriosAceite = requisito.CriteriosAceite, Status = requisito.Status,
            AprovadoEmUtc = requisito.AprovadoEmUtc, AprovadoPorNome = requisito.AprovadoPorNome
        };
    }
}

// §11 — CRIAR DEMANDA DE DESENVOLVIMENTO: IDEIA → REQUISITO → DEMANDA → FUNCIONALIDADE.
public record CriarDemandaDesenvolvimentoCommand(
    Guid RequisitoId, string? Titulo, string? Descricao, Guid? ResponsavelUsuarioId, string? ResponsavelNome, AutorIdeia Autor)
    : IRequest<DemandaDesenvolvimentoDto>;

public class CriarDemandaDesenvolvimentoCommandValidator : AbstractValidator<CriarDemandaDesenvolvimentoCommand>
{
    public CriarDemandaDesenvolvimentoCommandValidator()
    {
        RuleFor(x => x.RequisitoId).NotEmpty();
        RuleFor(x => x.Titulo).MaximumLength(180);
        RuleFor(x => x.Descricao).MaximumLength(4000);
        RuleFor(x => x.ResponsavelNome).MaximumLength(160);
    }
}

public class CriarDemandaDesenvolvimentoCommandHandler : IRequestHandler<CriarDemandaDesenvolvimentoCommand, DemandaDesenvolvimentoDto>
{
    private readonly IAppDbContext _db;

    public CriarDemandaDesenvolvimentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<DemandaDesenvolvimentoDto> Handle(CriarDemandaDesenvolvimentoCommand r, CancellationToken ct)
    {
        var requisito = await _db.IdeiaRequisitos.Include(x => x.Ideia)
            .FirstOrDefaultAsync(x => x.Id == r.RequisitoId, ct)
            ?? throw new KeyNotFoundException($"Requisito {r.RequisitoId} não encontrado.");
        if (requisito.Status != StatusRequisitoIdeia.Aprovado)
            throw new InvalidOperationException("A demanda só pode ser criada a partir de um requisito aprovado.");

        var numero = await RegistrarIdeiaCommandHandler.ProximoNumeroAsync(_db, "DEMANDA-IDEIA", ct);
        var demanda = new DemandaDesenvolvimento
        {
            Codigo = $"DEM-{numero:D4}",
            IdeiaId = requisito.IdeiaId,
            RequisitoId = requisito.Id,
            Titulo = IdeiaMapeador.Limpar(r.Titulo, 180) ?? requisito.Titulo,
            Descricao = IdeiaMapeador.Limpar(r.Descricao, 4000) ?? requisito.Descricao,
            ResponsavelUsuarioId = r.ResponsavelUsuarioId,
            ResponsavelNome = IdeiaMapeador.Limpar(r.ResponsavelNome, 160)
        };
        _db.DemandasDesenvolvimento.Add(demanda);
        if (requisito.Ideia is not null)
            IdeiaMapeador.Historico(_db.IdeiaHistoricos, requisito.Ideia, TipoHistoricoIdeia.Demanda,
                $"{r.Autor.Nome} criou a demanda {demanda.Codigo} a partir do requisito \"{requisito.Titulo}\".", r.Autor);
        await _db.SaveChangesAsync(ct);
        return IdeiaDetalheLoader.Demanda(demanda);
    }
}

public record AtualizarDemandaDesenvolvimentoCommand(
    Guid DemandaId, StatusDemandaDesenvolvimento Status, Guid? ResponsavelUsuarioId, string? ResponsavelNome,
    string? FuncionalidadeEntregue, AutorIdeia Autor) : IRequest<DemandaDesenvolvimentoDto>;

public class AtualizarDemandaDesenvolvimentoCommandValidator : AbstractValidator<AtualizarDemandaDesenvolvimentoCommand>
{
    public AtualizarDemandaDesenvolvimentoCommandValidator()
    {
        RuleFor(x => x.DemandaId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ResponsavelNome).MaximumLength(160);
        RuleFor(x => x.FuncionalidadeEntregue).MaximumLength(500);
    }
}

public class AtualizarDemandaDesenvolvimentoCommandHandler : IRequestHandler<AtualizarDemandaDesenvolvimentoCommand, DemandaDesenvolvimentoDto>
{
    private readonly IAppDbContext _db;

    public AtualizarDemandaDesenvolvimentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<DemandaDesenvolvimentoDto> Handle(AtualizarDemandaDesenvolvimentoCommand r, CancellationToken ct)
    {
        var demanda = await _db.DemandasDesenvolvimento.Include(x => x.Ideia)
            .FirstOrDefaultAsync(x => x.Id == r.DemandaId, ct)
            ?? throw new KeyNotFoundException($"Demanda {r.DemandaId} não encontrada.");

        var mudouStatus = demanda.Status != r.Status;
        demanda.Status = r.Status;
        demanda.ResponsavelUsuarioId = r.ResponsavelUsuarioId;
        demanda.ResponsavelNome = IdeiaMapeador.Limpar(r.ResponsavelNome, 160);
        demanda.FuncionalidadeEntregue = IdeiaMapeador.Limpar(r.FuncionalidadeEntregue, 500);
        if (r.Status == StatusDemandaDesenvolvimento.Concluida) demanda.ConcluidaEmUtc ??= DateTime.UtcNow;

        if (mudouStatus && demanda.Ideia is not null)
            IdeiaMapeador.Historico(_db.IdeiaHistoricos, demanda.Ideia, TipoHistoricoIdeia.Demanda,
                $"{r.Autor.Nome} atualizou a demanda {demanda.Codigo} para {r.Status}.", r.Autor);
        await _db.SaveChangesAsync(ct);
        return IdeiaDetalheLoader.Demanda(demanda);
    }
}

// §12 — anexos (documentos, imagens, prints, fluxogramas).
public record AnexarArquivoIdeiaCommand(
    Guid IdeiaId, string NomeArquivo, string? ContentType, byte[] Conteudo, AutorIdeia Autor) : IRequest<IdeiaAnexoDto>;

public class AnexarArquivoIdeiaCommandValidator : AbstractValidator<AnexarArquivoIdeiaCommand>
{
    public const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarArquivoIdeiaCommandValidator()
    {
        RuleFor(x => x.IdeiaId).NotEmpty();
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Conteudo).NotNull().Must(c => c.Length > 0).WithMessage("O arquivo está vazio.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O arquivo excede o limite de 5 MB.");
    }
}

public class AnexarArquivoIdeiaCommandHandler : IRequestHandler<AnexarArquivoIdeiaCommand, IdeiaAnexoDto>
{
    private readonly IAppDbContext _db;

    public AnexarArquivoIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaAnexoDto> Handle(AnexarArquivoIdeiaCommand r, CancellationToken ct)
    {
        var ideia = await _db.Ideias.FirstOrDefaultAsync(x => x.Id == r.IdeiaId, ct)
            ?? throw new KeyNotFoundException($"Ideia {r.IdeiaId} não encontrada.");

        // Nome sem caminho (evita "..\x" gravado como nome de arquivo).
        var nome = Path.GetFileName(r.NomeArquivo.Replace('\\', '/'));
        var anexo = new IdeiaAnexo
        {
            IdeiaId = ideia.Id,
            NomeArquivo = string.IsNullOrWhiteSpace(nome) ? "arquivo" : nome,
            ContentType = IdeiaMapeador.Limpar(r.ContentType, 120) ?? "application/octet-stream",
            Tamanho = r.Conteudo.LongLength,
            Conteudo = r.Conteudo,
            EnviadoPorNome = IdeiaMapeador.Limpar(r.Autor.Nome, 160)
        };
        _db.IdeiaAnexos.Add(anexo);
        IdeiaMapeador.Historico(_db.IdeiaHistoricos, ideia, TipoHistoricoIdeia.Anexo,
            $"{r.Autor.Nome} anexou o arquivo \"{anexo.NomeArquivo}\".", r.Autor);
        await _db.SaveChangesAsync(ct);

        return new IdeiaAnexoDto
        {
            Id = anexo.Id, NomeArquivo = anexo.NomeArquivo, ContentType = anexo.ContentType,
            Tamanho = anexo.Tamanho, EnviadoPorNome = anexo.EnviadoPorNome, CreatedAtUtc = anexo.CreatedAtUtc
        };
    }
}
