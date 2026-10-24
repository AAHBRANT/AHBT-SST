using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SuporteIa.Queries;

// Detalhe de um chamado (tela clicável a partir da fila) — mesma regra de visibilidade da listagem:
// quem administra vê qualquer chamado, quem não administra só o próprio (por usuário ou e-mail).
public record ObterSolicitacaoSuporteIaQuery(
    Guid Id,
    Guid? SolicitanteUsuarioId,
    string? SolicitanteEmail,
    bool IncluirTodos) : IRequest<SuporteIaSolicitacaoDto>;

public class ObterSolicitacaoSuporteIaQueryHandler : IRequestHandler<ObterSolicitacaoSuporteIaQuery, SuporteIaSolicitacaoDto>
{
    private readonly IAppDbContext _db;

    public ObterSolicitacaoSuporteIaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<SuporteIaSolicitacaoDto> Handle(ObterSolicitacaoSuporteIaQuery request, CancellationToken ct)
    {
        var s = await _db.SuporteIaSolicitacoes.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Chamado {request.Id} não encontrado.");

        var dto = new SuporteIaSolicitacaoDto
        {
            Id = s.Id,
            Tipo = s.Tipo,
            SeveridadeInformada = s.SeveridadeInformada,
            Status = s.Status,
            Titulo = s.Titulo,
            Descricao = s.Descricao,
            Modulo = s.Modulo,
            UrlContexto = s.UrlContexto,
            SolicitanteUsuarioId = s.SolicitanteUsuarioId,
            SolicitanteNome = s.SolicitanteNome,
            SolicitanteEmail = s.SolicitanteEmail,
            ResultadoTriagem = s.ResultadoTriagem,
            RequerAlteracaoCodigo = s.RequerAlteracaoCodigo,
            RespostaAoUsuario = s.RespostaAoUsuario,
            DemandaReduzida = s.DemandaReduzida,
            SolucaoProposta = s.SolucaoProposta,
            EvidenciasTecnicas = s.EvidenciasTecnicas,
            CreatedAtUtc = s.CreatedAtUtc,
            TriadoEmUtc = s.TriadoEmUtc,
            EncaminhadoEmUtc = s.EncaminhadoEmUtc,
            ResponsavelUsuarioId = s.ResponsavelUsuarioId,
            ResponsavelNome = s.ResponsavelNome,
            AprovadoEmUtc = s.AprovadoEmUtc,
            NotaFechamento = s.NotaFechamento,
            ConcluidoEmUtc = s.ConcluidoEmUtc,
            ValidacaoConfirmada = s.ValidacaoConfirmada,
            ComentarioValidacao = s.ComentarioValidacao,
            ValidadoEmUtc = s.ValidadoEmUtc,
        };

        dto.SouSolicitante = ListarSolicitacoesSuporteIaQueryHandler.EhOSolicitante(
            dto, request.SolicitanteUsuarioId, request.SolicitanteEmail);

        if (!request.IncluirTodos && !dto.SouSolicitante)
            throw new KeyNotFoundException($"Chamado {request.Id} não encontrado.");

        return dto;
    }
}
