using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento;

// O que a turma recebe por padrão ao escolher o curso (19/10): carga horária mínima (a maior entre
// a do curso e a da NR), validade, conteúdo programático (o do curso ou, vazio, o da NR) e local
// (nome da obra). Mesma regra na tela (via GET padrao) e no servidor (CriarSessaoTreinamento).
public sealed record PadraoTurmaTreinamento(
    string? Norma,
    int CargaHorariaMinima,
    int ValidadeEmMeses,
    string? ConteudoProgramatico,
    string? Local)
{
    public static PadraoTurmaTreinamento De(CursoTreinamento curso, string? obraNome)
    {
        var nr = CatalogoNormasRegulamentadoras.Obter(curso.NormaReferencia);
        var conteudo = string.IsNullOrWhiteSpace(curso.ConteudoProgramatico) ? nr?.ConteudoProgramaticoTexto : curso.ConteudoProgramatico;
        return new PadraoTurmaTreinamento(
            CatalogoNormasRegulamentadoras.NormalizarNumero(curso.NormaReferencia),
            Math.Max(curso.CargaHorariaMinima, nr?.CargaHorariaMinima ?? 0),
            curso.ValidadeEmMeses > 0 ? curso.ValidadeEmMeses : nr?.ValidadeEmMeses ?? 12,
            conteudo,
            obraNome);
    }
}

// Quem pode alterar os dados do certificado na turma (19/10): Técnico de Segurança e Administrador.
// Com o Entra ID desligado (desenvolvimento), libera — mesmo critério de ObterUsuarioLogadoQuery.
public static class PerfilTecnicoSeguranca
{
    public static async Task<bool> PodeEditarDadosTreinamentoAsync(IAppDbContext db, string? azureAdObjectId, bool autenticacaoHabilitada, CancellationToken ct)
    {
        if (!autenticacaoHabilitada) return true;
        if (string.IsNullOrWhiteSpace(azureAdObjectId)) return false;

        return await db.UsuariosPerfilObra.AnyAsync(v =>
            v.Ativo && v.Usuario!.AzureAdObjectId == azureAdObjectId && v.Usuario.Ativo && v.Usuario.Status == StatusUsuario.Ativo &&
            v.PerfilAcesso != null && v.PerfilAcesso.Ativo &&
            (v.PerfilAcesso.Tipo == TipoPerfilAcesso.TecnicoSeguranca || v.PerfilAcesso.Tipo == TipoPerfilAcesso.Administrador), ct);
    }
}
