using AAHBRANT.SST.Application.Asos;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanosIntegracao.Queries;

public static class SituacaoPasso
{
    public const string Feito = "feito";
    public const string Pendente = "pendente";
    public const string Agendado = "agendado";
    // Depende de um passo anterior (o kit só sai depois da NR-06).
    public const string Aguardando = "aguardando";
    // Curso não cadastrado no catálogo: não trava o plano, mas o técnico precisa saber.
    public const string NaoConfigurado = "naoConfigurado";
}

// SessaoTreinamentoId/DataTurma: preenchidos quando o passo é uma turma agendada — o pop-up deixa
// ajustar a data dali mesmo.
public record PassoPlanoIntegracaoDto(string Codigo, string Titulo, string Situacao, string? Detalhe, string? Rota,
    Guid? SessaoTreinamentoId = null, DateTime? DataTurma = null);
public record ItemKitEpiDto(Guid CatalogoEpiId, string Nome, string? Tamanho, string Situacao);
public record PlanoIntegracaoDto(
    Guid Id, Guid TrabalhadorId, string Nome, string? Matricula, string Funcao, Guid ObraId, string ObraNome,
    string? EquipeNome, DateTime DataAdmissao, bool TemFoto, MotivoPlanoIntegracao Motivo, DateTime AbertoEm,
    string? ResponsavelNome, DateTime? AssumidoEm, int PassosConcluidos, IReadOnlyList<PassoPlanoIntegracaoDto> Passos,
    IReadOnlyList<ItemKitEpiDto> Kit);

// Pop-up "Funcionário novo" (especificação 21/10/2026, Módulo 1). Só Técnico de Segurança e
// Administrador recebem os planos (mesmo critério de quem altera dados de treinamento); o escopo por
// obra vem do filtro global de PlanoIntegracao. Os passos são recalculados a cada consulta a partir
// dos dados reais — quando todos estão feitos, o plano é encerrado como concluído aqui mesmo.
public record ListarPlanosIntegracaoPendentesQuery(
    Guid? PlanoId = null, string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false) : IRequest<List<PlanoIntegracaoDto>>;

public class ListarPlanosIntegracaoPendentesQueryHandler : IRequestHandler<ListarPlanosIntegracaoPendentesQuery, List<PlanoIntegracaoDto>>
{
    private readonly IAppDbContext _db;
    public ListarPlanosIntegracaoPendentesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PlanoIntegracaoDto>> Handle(ListarPlanosIntegracaoPendentesQuery request, CancellationToken ct)
    {
        if (!await PerfilTecnicoSeguranca.PodeEditarDadosTreinamentoAsync(_db, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct))
            return new List<PlanoIntegracaoDto>();

        var consulta = _db.PlanosIntegracao.Where(p => p.Status == StatusPlanoIntegracao.Aberto);
        if (request.PlanoId is { } planoId) consulta = consulta.Where(p => p.Id == planoId);
        // Projeção (e não Include do Trabalhador): a foto de perfil fica fora da memória.
        var planos = await consulta
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(50)
            .Select(p => new
            {
                p.Id, p.ObraId, p.Motivo, p.CreatedAtUtc, p.AssumidoEm,
                FuncaoNome = p.Funcao!.Nome,
                ObraNome = p.Obra!.Nome,
                ResponsavelNome = p.ResponsavelUsuario != null ? p.ResponsavelUsuario.Nome : null,
                Trabalhador = new
                {
                    p.Trabalhador!.Id, p.Trabalhador.Nome, p.Trabalhador.Matricula, p.Trabalhador.ObraId, p.Trabalhador.FuncaoId,
                    p.Trabalhador.DataAdmissao, EquipeNome = p.Trabalhador.Equipe != null ? p.Trabalhador.Equipe.Nome : null,
                    TemFoto = p.Trabalhador.FotoConteudo != null,
                },
            })
            .ToListAsync(ct);
        if (planos.Count == 0) return new List<PlanoIntegracaoDto>();

        var cursos = await _db.CursosTreinamento
            .Select(c => new { c.Id, c.Nome, c.EhIntegracaoSeguranca, c.AtendeNr6 })
            .ToListAsync(ct);
        var resultado = new List<PlanoIntegracaoDto>();
        var concluidosAgora = new List<Guid>();

        foreach (var plano in planos)
        {
            var t = plano.Trabalhador;
            var passos = new List<PassoPlanoIntegracaoDto>();
            var hoje = DateTime.UtcNow.Date;
            var rotaPerfil = $"/pessoas/{t.Id}";

            // 1. ASO admissional apto
            var asoApto = AsoVigente.EstaLiberado(AsoVigente.Eleger(await _db.Asos.Where(a => a.TrabalhadorId == t.Id)
                .Select(a => new AsoResumo(a.Id, a.TrabalhadorId, a.DataExame, a.DataValidade, a.ResultadoStatus)).ToListAsync(ct)), hoje);
            passos.Add(new("aso", "ASO admissional apto", asoApto ? SituacaoPasso.Feito : SituacaoPasso.Pendente,
                asoApto ? null : "Sem ASO apto registrado. Vem do RH ou pode ser lançado no perfil.", rotaPerfil));

            // 2. Biometria facial e digital — necessária para assinar qualquer documento
            var temDigital = await _db.TemplatesBiometricoFutronic.AnyAsync(b => b.TrabalhadorId == t.Id, ct);
            var temFacial = await _db.FotosCadastroFacial.AnyAsync(f => f.TrabalhadorId == t.Id, ct);
            passos.Add(new("biometria", "Cadastrar biometria facial e digital",
                temDigital && temFacial ? SituacaoPasso.Feito : SituacaoPasso.Pendente,
                temDigital && temFacial ? null : $"Facial {(temFacial ? "cadastrada" : "pendente")} · digital {(temDigital ? "cadastrada" : "pendente")}.",
                rotaPerfil));

            var validos = await _db.Treinamentos
                .Where(tr => tr.TrabalhadorId == t.Id && tr.DataValidade.Date >= hoje)
                .Select(tr => tr.CursoTreinamentoId)
                .Distinct()
                .ToListAsync(ct);
            var turmasAbertas = await _db.ParticipantesSessaoTreinamento
                .Where(p => p.TrabalhadorId == t.Id && p.Ativo
                    && p.SessaoTreinamento!.Ativo && p.SessaoTreinamento.Status == StatusSessaoTreinamento.EmAndamento)
                .Select(p => new { p.SessaoTreinamentoId, p.SessaoTreinamento!.CursoTreinamentoId, p.SessaoTreinamento.DataRealizacao })
                .ToListAsync(ct);

            PassoPlanoIntegracaoDto PassoCurso(string codigo, string titulo, IReadOnlyCollection<Guid> idsCurso, string semCurso)
            {
                if (idsCurso.Count == 0)
                    return new(codigo, titulo, SituacaoPasso.NaoConfigurado, semCurso, "/gestao-sst?secao=treinamentos");
                if (idsCurso.Any(validos.Contains))
                    return new(codigo, titulo, SituacaoPasso.Feito, null, rotaPerfil);
                var turma = turmasAbertas.Where(x => idsCurso.Contains(x.CursoTreinamentoId)).OrderBy(x => x.DataRealizacao).FirstOrDefault();
                return turma is not null
                    ? new(codigo, titulo, SituacaoPasso.Agendado, $"Turma em {turma.DataRealizacao:dd/MM/yyyy}.", $"/treinamentos/turma/{turma.SessaoTreinamentoId}",
                        turma.SessaoTreinamentoId, turma.DataRealizacao)
                    : new(codigo, titulo, SituacaoPasso.Pendente, "Ainda sem turma.", "/gestao-sst?secao=treinamentos");
            }

            // 3. Integração de Segurança e 4. NR-06
            var idsIntegracao = cursos.Where(c => c.EhIntegracaoSeguranca).Select(c => c.Id).ToList();
            var idsNr6 = cursos.Where(c => c.AtendeNr6).Select(c => c.Id).ToList();
            passos.Add(PassoCurso("integracao", "Integração de Segurança", idsIntegracao,
                "Nenhum curso marcado como Integração de Segurança no catálogo."));
            var passoNr6 = PassoCurso("nr06", "NR-06 (uso de EPI)", idsNr6, "Nenhum curso marcado como NR-06 no catálogo.");
            passos.Add(passoNr6);

            // 5. Demais cursos obrigatórios da função
            var outrosCursos = await _db.MatrizTreinamentoFuncoes
                .Where(m => m.FuncaoId == t.FuncaoId)
                .Select(m => new { m.CursoTreinamentoId, Nome = m.CursoTreinamento!.Nome })
                .ToListAsync(ct);
            outrosCursos = outrosCursos.Where(c => !idsIntegracao.Contains(c.CursoTreinamentoId) && !idsNr6.Contains(c.CursoTreinamentoId)).ToList();
            var pendentesCurso = outrosCursos.Where(c => !validos.Contains(c.CursoTreinamentoId)).ToList();
            if (outrosCursos.Count == 0 || pendentesCurso.Count == 0)
            {
                passos.Add(new("cursos", "Demais cursos da função", SituacaoPasso.Feito,
                    outrosCursos.Count == 0 ? "A função não exige outros cursos." : null, rotaPerfil));
            }
            else
            {
                var agendados = pendentesCurso.Where(c => turmasAbertas.Any(x => x.CursoTreinamentoId == c.CursoTreinamentoId)).ToList();
                var semTurma = pendentesCurso.Except(agendados).ToList();
                passos.Add(new("cursos", "Demais cursos da função",
                    semTurma.Count == 0 ? SituacaoPasso.Agendado : SituacaoPasso.Pendente,
                    semTurma.Count == 0
                        ? $"Agendados: {string.Join(", ", agendados.Select(c => c.Nome))}."
                        : $"Sem turma: {string.Join(", ", semTurma.Select(c => c.Nome))}.",
                    "/gestao-sst?secao=treinamentos"));
            }

            // 6. Kit de EPI
            var reservas = await _db.ReservasEpi
                .Where(r => r.TrabalhadorId == t.Id && r.ObraId == t.ObraId
                    && (r.Status == StatusReservaEpi.Ativa || (r.Status == StatusReservaEpi.Entregue && r.CreatedAtUtc >= plano.CreatedAtUtc.AddDays(-1))))
                .Select(r => new { r.Id, r.CatalogoEpiId, Nome = r.CatalogoEpi!.Nome, r.Tamanho, r.Status })
                .ToListAsync(ct);
            var separadas = await SituacaoReservasEpi.SeparadasAsync(_db, t.ObraId,
                reservas.Where(r => r.Status == StatusReservaEpi.Ativa).Select(r => r.CatalogoEpiId).Distinct().ToList(), ct);
            var kit = reservas
                .Select(r => new ItemKitEpiDto(r.CatalogoEpiId, r.Nome, r.Tamanho,
                    r.Status == StatusReservaEpi.Entregue ? "entregue" : separadas.GetValueOrDefault(r.Id) ? "separado" : "emFalta"))
                .OrderBy(i => i.Situacao == "emFalta" ? 0 : i.Situacao == "separado" ? 1 : 2).ThenBy(i => i.Nome)
                .ToList();
            var ativas = kit.Count(i => i.Situacao != "entregue");
            var emFalta = kit.Count(i => i.Situacao == "emFalta");
            var kitSituacao = ativas == 0 ? SituacaoPasso.Feito
                : passoNr6.Situacao is SituacaoPasso.Feito or SituacaoPasso.NaoConfigurado ? SituacaoPasso.Pendente
                : SituacaoPasso.Aguardando;
            var kitDetalhe = ativas == 0
                ? (kit.Count == 0 ? "Nenhum EPI a entregar." : null)
                : kitSituacao == SituacaoPasso.Aguardando
                    ? "Liberado depois da NR-06."
                    : emFalta > 0 ? $"{emFalta} item(ns) em falta no estoque." : "Kit separado, pronto para entregar.";
            passos.Add(new("kit", "Retirar o kit de EPI e assinar", kitSituacao, kitDetalhe, "/operacao?secao=epi"));

            var concluidos = passos.Count(p => p.Situacao is SituacaoPasso.Feito or SituacaoPasso.NaoConfigurado);
            if (concluidos == passos.Count)
            {
                concluidosAgora.Add(plano.Id);
                continue;
            }

            resultado.Add(new PlanoIntegracaoDto(
                plano.Id, t.Id, t.Nome, t.Matricula, plano.FuncaoNome, plano.ObraId, plano.ObraNome,
                t.EquipeNome, t.DataAdmissao, t.TemFoto, plano.Motivo, plano.CreatedAtUtc,
                plano.ResponsavelNome, plano.AssumidoEm, concluidos, passos, kit));
        }

        if (concluidosAgora.Count > 0)
        {
            var paraEncerrar = await _db.PlanosIntegracao.Where(p => concluidosAgora.Contains(p.Id)).ToListAsync(ct);
            foreach (var p in paraEncerrar)
            {
                p.Status = StatusPlanoIntegracao.Concluido;
                p.EncerradoEm = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
        }
        return resultado;
    }
}
