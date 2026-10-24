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

        // Nenhum dado de outra tabela entra na projeção, e o motivo é uma regressão real (23/09):
        // projetar `t.CursoTreinamento!.Nome` e `t.Trabalhador!.Funcao` faz o EF gerar INNER JOIN em
        // navegação obrigatória, e "excluir" aqui é soft delete (SstDbContext marca Ativo=false, e
        // cada entidade tem filtro global por Ativo). Resultado: bastava o curso sair do catálogo —
        // ou a função do trabalhador ser excluída — para o certificado inteiro sumir desta lista,
        // embora o registro exista e continue aparecendo na aba Treinamentos do perfil (que não faz
        // join). Como esta query também alimenta as travas da entrega de EPI, isso bloqueava a
        // entrega dizendo que o trabalhador "não tem nenhum treinamento cadastrado".
        // Os nomes vêm de consultas separadas, resolvidas em memória: curso, função e obra são
        // buscados ignorando o filtro de Ativo (o certificado vale mesmo que o curso tenha saído do
        // catálogo depois), e só o Trabalhador continua respeitando o filtro — trabalhador excluído
        // some da lista de propósito.
        //
        // O conteúdo do arquivo NUNCA entra na projeção — só o fato de existir, o nome e o tipo.
        // É justamente para isso que o arquivo mora em tabela própria: a lista pode ter centenas de
        // linhas e arrastar os bytes de cada certificado aqui derrubaria a tela.
        var treinamentos = await query
            .Select(t => new
            {
                t.Id,
                t.TrabalhadorId,
                t.CursoTreinamentoId,
                t.DataRealizacao,
                t.DataValidade,
                t.CargaHorariaRealizada,
                t.InstituicaoInstrutor,
                t.NumeroCertificado,
                t.Local,
                t.InstrutorRegistroProfissional,
                t.OrigemCertificado,
                TemArquivo = t.ArquivoCertificado != null,
                NomeArquivo = t.ArquivoCertificado != null ? t.ArquivoCertificado.NomeArquivo : null,
                ContentTypeArquivo = t.ArquivoCertificado != null ? t.ArquivoCertificado.ContentType : null,
            })
            .ToListAsync(ct);

        if (treinamentos.Count == 0) return new List<CertificadoTreinamentoDto>();

        var trabalhadorIds = treinamentos.Select(t => t.TrabalhadorId).Distinct().ToList();
        var trabalhadores = await _db.Trabalhadores.AsNoTracking()
            .Where(x => trabalhadorIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Nome, x.ObraId, x.FuncaoId })
            .ToListAsync(ct);
        var trabalhadorPorId = trabalhadores.ToDictionary(x => x.Id);

        var cursoIds = treinamentos.Select(t => t.CursoTreinamentoId).Distinct().ToList();
        var cursoPorId = (await _db.CursosTreinamento.AsNoTracking().IgnoreQueryFilters()
                .Where(c => cursoIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Nome, c.NormaReferencia, c.AtendeNr6 })
                .ToListAsync(ct))
            .ToDictionary(c => c.Id);

        var obraIds = trabalhadores.Select(x => x.ObraId).Distinct().ToList();
        var nomeObraPorId = (await _db.Obras.AsNoTracking().IgnoreQueryFilters()
                .Where(o => obraIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Nome })
                .ToListAsync(ct))
            .ToDictionary(o => o.Id, o => o.Nome);

        var funcaoIds = trabalhadores.Select(x => x.FuncaoId).Distinct().ToList();
        var nomeFuncaoPorId = (await _db.Funcoes.AsNoTracking().IgnoreQueryFilters()
                .Where(f => funcaoIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Nome })
                .ToListAsync(ct))
            .ToDictionary(f => f.Id, f => f.Nome);

        return treinamentos
            // Trabalhador ausente = trabalhador excluído (o filtro global o removeu): o certificado
            // dele sai da lista junto, ao contrário do curso.
            .Where(t => trabalhadorPorId.ContainsKey(t.TrabalhadorId))
            .Select(t =>
            {
                var trabalhador = trabalhadorPorId[t.TrabalhadorId];
                cursoPorId.TryGetValue(t.CursoTreinamentoId, out var curso);
                return new CertificadoTreinamentoDto(
                    t.Id,
                    t.TrabalhadorId,
                    trabalhador.Nome,
                    nomeFuncaoPorId.GetValueOrDefault(trabalhador.FuncaoId),
                    trabalhador.ObraId,
                    nomeObraPorId.GetValueOrDefault(trabalhador.ObraId),
                    t.CursoTreinamentoId,
                    curso?.Nome ?? "Curso removido do catálogo",
                    curso?.NormaReferencia,
                    curso?.AtendeNr6 ?? false,
                    t.DataRealizacao,
                    t.DataValidade,
                    t.CargaHorariaRealizada,
                    t.InstituicaoInstrutor,
                    t.NumeroCertificado,
                    t.Local,
                    t.InstrutorRegistroProfissional,
                    t.OrigemCertificado,
                    t.TemArquivo,
                    t.NomeArquivo,
                    t.ContentTypeArquivo);
            })
            .OrderBy(c => c.TrabalhadorNome)
            .ThenByDescending(c => c.DataRealizacao)
            .ToList();
    }
}
