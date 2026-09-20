using AAHBRANT.SST.Application.SuporteIa.Queries;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.SuporteIa;

// Mapeamento entidade -> DTO usado pelos comandos de transição (Aprovar/Concluir/Recusar/Validar),
// que sempre têm a entidade já materializada em mãos após salvar. As queries de listagem continuam
// com a própria projeção (LINQ-to-SQL não materializado) por performance — não vale a troca aqui.
public static class SuporteIaMapeador
{
    public static SuporteIaSolicitacaoDto Mapear(
        SuporteIaSolicitacao s, Guid? solicitanteUsuarioIdAtual, string? solicitanteEmailAtual)
    {
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
            dto, solicitanteUsuarioIdAtual, solicitanteEmailAtual);

        return dto;
    }
}
