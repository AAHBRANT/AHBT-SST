using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

// Lista central da sub-aba "Certificados" (pedido do usuário, 22/09): as obras já estão em
// andamento e o técnico precisa lançar/conferir certificado de muita gente sem entrar no perfil de
// um trabalhador por vez. Query separada de ListarTreinamentosQuery de propósito — aquela alimenta
// a aba dentro do perfil (onde trabalhador/obra já são conhecidos) e mexer no contrato dela
// obrigaria a rever a entrega de EPI, o dashboard e o perfil consolidado.
public record ListarCertificadosTreinamentoQuery(
    Guid? ObraId = null,
    Guid? CursoTreinamentoId = null,
    Guid? TrabalhadorId = null) : IRequest<List<CertificadoTreinamentoDto>>;

public record CertificadoTreinamentoDto(
    Guid Id,
    Guid TrabalhadorId,
    string TrabalhadorNome,
    string? FuncaoNome,
    Guid ObraId,
    string? ObraNome,
    Guid CursoTreinamentoId,
    string CursoNome,
    string? NormaReferencia,
    bool AtendeNr6,
    DateTime DataRealizacao,
    DateTime DataValidade,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    string? NumeroCertificado,
    string? Local,
    string? InstrutorRegistroProfissional,
    OrigemCertificadoTreinamento OrigemCertificado,
    bool TemArquivo,
    string? NomeArquivo,
    string? ContentTypeArquivo);

public class ListarCertificadosTreinamentoQueryHandler : IRequestHandler<ListarCertificadosTreinamentoQuery, List<CertificadoTreinamentoDto>>
{
    private readonly IAppDbContext _db;
    public ListarCertificadosTreinamentoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CertificadoTreinamentoDto>> Handle(ListarCertificadosTreinamentoQuery request, CancellationToken ct)
    {
        var query = _db.Treinamentos.AsNoTracking().AsQueryable();

        if (request.TrabalhadorId is not null)
            query = query.Where(t => t.TrabalhadorId == request.TrabalhadorId.Value);
        if (request.ObraId is not null)
            query = query.Where(t => t.Trabalhador != null && t.Trabalhador.ObraId == request.ObraId.Value);
        if (request.CursoTreinamentoId is not null)
            query = query.Where(t => t.CursoTreinamentoId == request.CursoTreinamentoId.Value);

        // O conteúdo do arquivo NUNCA entra nesta projeção — só o fato de existir, o nome e o tipo.
        // É justamente para isso que o arquivo mora em tabela própria: a lista pode ter centenas de
        // linhas e arrastar os bytes de cada certificado aqui derrubaria a tela.
        return await query
            .OrderBy(t => t.Trabalhador!.Nome)
            .ThenByDescending(t => t.DataRealizacao)
            .Select(t => new CertificadoTreinamentoDto(
                t.Id,
                t.TrabalhadorId,
                t.Trabalhador!.Nome,
                t.Trabalhador.Funcao != null ? t.Trabalhador.Funcao.Nome : null,
                t.Trabalhador.ObraId,
                t.Trabalhador.Obra != null ? t.Trabalhador.Obra.Nome : null,
                t.CursoTreinamentoId,
                t.CursoTreinamento!.Nome,
                t.CursoTreinamento.NormaReferencia,
                t.CursoTreinamento.AtendeNr6,
                t.DataRealizacao,
                t.DataValidade,
                t.CargaHorariaRealizada,
                t.InstituicaoInstrutor,
                t.NumeroCertificado,
                t.Local,
                t.InstrutorRegistroProfissional,
                t.OrigemCertificado,
                t.ArquivoCertificado != null,
                t.ArquivoCertificado != null ? t.ArquivoCertificado.NomeArquivo : null,
                t.ArquivoCertificado != null ? t.ArquivoCertificado.ContentType : null))
            .ToListAsync(ct);
    }
}
