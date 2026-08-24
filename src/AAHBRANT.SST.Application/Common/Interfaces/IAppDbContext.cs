using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração que a Application depende (não referencia Infrastructure/EF Core diretamente,
// preservando a regra de dependência do Clean Architecture). Implementada por SstDbContext.
public interface IAppDbContext
{
    DbSet<Obra> Obras { get; }
    DbSet<Setor> Setores { get; }
    DbSet<Equipe> Equipes { get; }
    DbSet<Funcao> Funcoes { get; }
    DbSet<Trabalhador> Trabalhadores { get; }

    DbSet<PerfilAcesso> PerfisAcesso { get; }
    DbSet<Permissao> Permissoes { get; }
    DbSet<PerfilAcessoPermissao> PerfisAcessoPermissoes { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<UsuarioPerfilObra> UsuariosPerfilObra { get; }
    DbSet<TrilhaAuditoria> TrilhaAuditoria { get; }

    DbSet<Aso> Asos { get; }
    DbSet<CursoTreinamento> CursosTreinamento { get; }
    DbSet<Treinamento> Treinamentos { get; }
    DbSet<CatalogoEpi> CatalogoEpis { get; }
    DbSet<EntregaEpi> EntregasEpi { get; }
    DbSet<Alerta> Alertas { get; }
    DbSet<AlertaHistoricoEnvio> AlertaHistoricoEnvios { get; }
    DbSet<Evidencia> Evidencias { get; }

    DbSet<Atividade> Atividades { get; }
    DbSet<Perigo> Perigos { get; }
    DbSet<Risco> Riscos { get; }
    DbSet<RiscoTrabalhadorExposto> RiscoTrabalhadorExpostos { get; }
    DbSet<MatrizRiscoConfig> MatrizRiscoConfigs { get; }
    DbSet<MatrizRiscoCelula> MatrizRiscoCelulas { get; }

    DbSet<Pgr> Pgrs { get; }
    DbSet<PlanoAcaoItem> PlanoAcaoItens { get; }
    DbSet<PgrRevisao> PgrRevisoes { get; }

    DbSet<TagIdentificacao> TagsIdentificacao { get; }
    DbSet<AreaSst> AreasSst { get; }

    DbSet<Apr> Aprs { get; }
    DbSet<AprEtapa> AprEtapas { get; }
    DbSet<AprEtapaRisco> AprEtapaRiscos { get; }
    DbSet<AprResponsavel> AprResponsaveis { get; }
    DbSet<AprAssinatura> AprAssinaturas { get; }

    DbSet<PermissaoTrabalho> PermissoesTrabalho { get; }
    DbSet<PermissaoTrabalhoPerigo> PermissaoTrabalhoPerigos { get; }
    DbSet<PermissaoTrabalhoControle> PermissaoTrabalhoControles { get; }
    DbSet<PermissaoTrabalhoRequisito> PermissaoTrabalhoRequisitos { get; }
    DbSet<PermissaoTrabalhoResponsavel> PermissaoTrabalhoResponsaveis { get; }

    DbSet<ChecklistModelo> ChecklistModelos { get; }
    DbSet<ChecklistModeloItem> ChecklistModeloItens { get; }
    DbSet<Inspecao> Inspecoes { get; }
    DbSet<InspecaoItemResposta> InspecaoItemRespostas { get; }

    // Qualificação explícita necessária: "Dds" sem prefixo é ambíguo aqui — a namespace
    // AAHBRANT.SST.Application.Dds (Commands/Queries deste módulo) sombreia o tipo importado por
    // using, já que ela é encontrada em um nível de namespace mais interno.
    DbSet<Domain.Entidades.Dds> Dds { get; }
    DbSet<DdsAtividade> DdsAtividades { get; }
    DbSet<DdsItemChecklist> DdsItensChecklist { get; }
    DbSet<DdsParticipante> DdsParticipantes { get; }
    DbSet<DdsTelegramEnvio> DdsTelegramEnvios { get; }

    DbSet<NaoConformidade> NaoConformidades { get; }
    DbSet<AcaoPlano> AcoesPlano { get; }

    DbSet<Acidente> Acidentes { get; }

    DbSet<RequisitoLegal> RequisitosLegais { get; }

    DbSet<DocumentoGestao> DocumentosGestao { get; }
    DbSet<DocumentoRevisao> DocumentoRevisoes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
