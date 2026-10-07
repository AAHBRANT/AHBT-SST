using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Relatorios;

public record DestinatarioRelatorioDto(
    Guid Id,
    Guid UsuarioId,
    string UsuarioNome,
    string? UsuarioEmail,
    // Sem Acesso Teams (AzureAdObjectId) o sininho não chega a essa pessoa.
    bool TemAcessoTeams,
    Guid? ObraId,
    string? ObraNome,
    bool ListaPresenca,
    bool BoletimSemanal,
    bool Ocorrencia);

// Tela "Destinatários dos relatórios" (Administração): quem recebe quais relatórios de quais obras.
// ObraId nulo = todas as obras (diretoria, consolidado).
public record ListarDestinatariosRelatorioQuery : IRequest<List<DestinatarioRelatorioDto>>;

public class ListarDestinatariosRelatorioQueryHandler : IRequestHandler<ListarDestinatariosRelatorioQuery, List<DestinatarioRelatorioDto>>
{
    private readonly IAppDbContext _db;

    public ListarDestinatariosRelatorioQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DestinatarioRelatorioDto>> Handle(ListarDestinatariosRelatorioQuery request, CancellationToken ct)
    {
        // Colunas escalares e consultas separadas: o filtro global de Obra/Usuario não derruba a linha.
        var linhas = await _db.DestinatariosRelatorio
            .Select(d => new { d.Id, d.UsuarioId, d.ObraId, d.ListaPresenca, d.BoletimSemanal, d.Ocorrencia })
            .ToListAsync(ct);
        if (linhas.Count == 0) return new List<DestinatarioRelatorioDto>();

        var usuarioIds = linhas.Select(l => l.UsuarioId).Distinct().ToList();
        var usuarios = await _db.Usuarios.Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Email, Teams = u.AzureAdObjectId != null })
            .ToDictionaryAsync(u => u.Id, ct);
        var obraIds = linhas.Where(l => l.ObraId != null).Select(l => l.ObraId!.Value).Distinct().ToList();
        var obras = await _db.Obras.Where(o => obraIds.Contains(o.Id)).Select(o => new { o.Id, o.Nome }).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);

        return linhas
            .Select(l => new DestinatarioRelatorioDto(
                l.Id, l.UsuarioId,
                usuarios.TryGetValue(l.UsuarioId, out var u) ? u.Nome : "(usuário removido)",
                u?.Email, u?.Teams ?? false,
                l.ObraId, l.ObraId is { } oid && obras.TryGetValue(oid, out var nomeObra) ? nomeObra : null,
                l.ListaPresenca, l.BoletimSemanal, l.Ocorrencia))
            .OrderBy(d => d.UsuarioNome).ThenBy(d => d.ObraNome ?? string.Empty)
            .ToList();
    }
}

// Cria ou atualiza o cadastro de um usuário numa obra (ou em todas). Não duplica: um registro por
// usuário e obra.
public record SalvarDestinatarioRelatorioCommand(
    Guid UsuarioId,
    Guid? ObraId,
    bool ListaPresenca,
    bool BoletimSemanal,
    bool Ocorrencia) : IRequest<Guid>;

public class SalvarDestinatarioRelatorioCommandValidator : AbstractValidator<SalvarDestinatarioRelatorioCommand>
{
    public SalvarDestinatarioRelatorioCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x).Must(x => x.ListaPresenca || x.BoletimSemanal || x.Ocorrencia)
            .WithMessage("Marque ao menos um relatório. Para não receber nada, remova o destinatário.");
    }
}

public class SalvarDestinatarioRelatorioCommandHandler : IRequestHandler<SalvarDestinatarioRelatorioCommand, Guid>
{
    private readonly IAppDbContext _db;

    public SalvarDestinatarioRelatorioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(SalvarDestinatarioRelatorioCommand request, CancellationToken ct)
    {
        if (!await _db.Usuarios.AnyAsync(u => u.Id == request.UsuarioId, ct))
            throw new KeyNotFoundException("Usuário não encontrado.");
        if (request.ObraId is { } obraId && !await _db.Obras.AnyAsync(o => o.Id == obraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        var existente = await _db.DestinatariosRelatorio
            .FirstOrDefaultAsync(d => d.UsuarioId == request.UsuarioId && d.ObraId == request.ObraId, ct);
        if (existente is null)
        {
            existente = new DestinatarioRelatorio { UsuarioId = request.UsuarioId, ObraId = request.ObraId };
            _db.DestinatariosRelatorio.Add(existente);
        }
        existente.ListaPresenca = request.ListaPresenca;
        existente.BoletimSemanal = request.BoletimSemanal;
        existente.Ocorrencia = request.Ocorrencia;
        await _db.SaveChangesAsync(ct);
        return existente.Id;
    }
}

public record RemoverDestinatarioRelatorioCommand(Guid Id) : IRequest;

public class RemoverDestinatarioRelatorioCommandHandler : IRequestHandler<RemoverDestinatarioRelatorioCommand>
{
    private readonly IAppDbContext _db;

    public RemoverDestinatarioRelatorioCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverDestinatarioRelatorioCommand request, CancellationToken ct)
    {
        var d = await _db.DestinatariosRelatorio.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Destinatário não encontrado.");
        _db.DestinatariosRelatorio.Remove(d);
        await _db.SaveChangesAsync(ct);
    }
}

// Pré-preenche a lista a partir dos perfis: Técnico e Engenheiro de Segurança de cada obra (ou de todas, quando
// o perfil é global) passam a receber os três relatórios. Não mexe em quem já está cadastrado. Devolve quantos
// cadastros foram criados.
public record SugerirDestinatariosPorPerfilCommand : IRequest<int>;

public class SugerirDestinatariosPorPerfilCommandHandler : IRequestHandler<SugerirDestinatariosPorPerfilCommand, int>
{
    private static readonly TipoPerfilAcesso?[] PerfisSugeridos = { TipoPerfilAcesso.TecnicoSeguranca, TipoPerfilAcesso.EngenheiroSeguranca };

    private readonly IAppDbContext _db;

    public SugerirDestinatariosPorPerfilCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(SugerirDestinatariosPorPerfilCommand request, CancellationToken ct)
    {
        var vinculos = await _db.UsuariosPerfilObra
            .Where(v => PerfisSugeridos.Contains(v.PerfilAcesso!.Tipo) && v.Usuario!.Status == StatusUsuario.Ativo)
            .Select(v => new { v.UsuarioId, v.ObraId })
            .Distinct()
            .ToListAsync(ct);

        var existentes = (await _db.DestinatariosRelatorio.Select(d => new { d.UsuarioId, d.ObraId }).ToListAsync(ct)).ToHashSet();

        var criados = 0;
        foreach (var v in vinculos)
        {
            if (existentes.Contains(new { v.UsuarioId, v.ObraId })) continue;
            _db.DestinatariosRelatorio.Add(new DestinatarioRelatorio
            {
                UsuarioId = v.UsuarioId, ObraId = v.ObraId, ListaPresenca = true, BoletimSemanal = true, Ocorrencia = true,
            });
            criados++;
        }
        if (criados > 0) await _db.SaveChangesAsync(ct);
        return criados;
    }
}
