using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Queries;

// CPF e telefone em claro de uma pessoa externa — a única saída desses dados sem máscara (LGPD).
// Exige a política acidente:ver_dados_pessoais (no controller) e grava auditoria a cada consulta.
// UsuarioId vem do token, nunca do corpo; o valor em claro não vai para a auditoria nem para log.
public record ObterDadosPessoaisPessoaExternaQuery(Guid AcidenteId, Guid PessoaId, Guid? UsuarioId) : IRequest<DadosPessoaisPessoaExternaDto>;

public record DadosPessoaisPessoaExternaDto(Guid Id, string Nome, string? Cpf, string? Telefone);

public class ObterDadosPessoaisPessoaExternaQueryHandler : IRequestHandler<ObterDadosPessoaisPessoaExternaQuery, DadosPessoaisPessoaExternaDto>
{
    public const string AcaoAuditoria = "VerDadosPessoaisPessoaExterna";

    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ObterDadosPessoaisPessoaExternaQueryHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<DadosPessoaisPessoaExternaDto> Handle(ObterDadosPessoaisPessoaExternaQuery request, CancellationToken ct)
    {
        // O query filter já restringe ao escopo por obra do usuário: pessoa de outra obra vira "não encontrada".
        var pessoa = await _db.AcidentesPessoasExternas.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PessoaId && p.AcidenteId == request.AcidenteId, ct)
            ?? throw new KeyNotFoundException("Pessoa externa não encontrada nesta ocorrência.");

        await _auditoria.RegistrarAsync(AcaoAuditoria, "AcidentePessoaExterna", pessoa.Id,
            request.UsuarioId, trabalhadorId: null, dadosDepois: new { request.AcidenteId }, ct);
        await _db.SaveChangesAsync(ct);

        return new DadosPessoaisPessoaExternaDto(pessoa.Id, pessoa.Nome, pessoa.Cpf, pessoa.Telefone);
    }
}
