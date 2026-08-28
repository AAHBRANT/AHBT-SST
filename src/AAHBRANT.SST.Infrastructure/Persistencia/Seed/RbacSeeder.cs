using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente rodado a cada start da API (checa antes de inserir — não é EF Core
// migration HasData, para não hardcodar GUIDs em código de migração). Duas responsabilidades:
//
// 1. Os 12 perfis de sistema da §44 da Base de Conhecimento (TipoPerfilAcesso), EhSistema=true.
// 2. Catálogo baseline de Permissao (Codigo/Modulo/Acao) — UM código por módulo já implementado
//    no backend hoje. Os códigos/ações abaixo NÃO são citação literal da Base de Conhecimento
//    (que não define um vocabulário de permissão granular) — são proposta própria, alinhada aos
//    módulos que já existem em código, no mesmo espírito de docs/RBAC-Matrix.md (o próprio
//    RBAC-Matrix.md já se declara "rascunho técnico de partida" pendente de validação da
//    Diretoria/Gestor QSMS).
//
// Deliberadamente NÃO semeamos PerfilAcessoPermissao (quais permissões cada perfil recebe, em
// qual escopo): essa é exatamente a decisão de negócio que a tela "Perfis & Matriz de
// Permissões" existe para capturar, e RBAC-Matrix.md é só um rascunho — hardcodar isso no
// seeder equivaleria a tratar um rascunho não validado como regra de produção.
public static class RbacSeeder
{
    private static readonly (TipoPerfilAcesso Tipo, string Nome)[] PerfisDeSistema =
    {
        (TipoPerfilAcesso.Administrador, "Administrador"),
        (TipoPerfilAcesso.Diretor, "Diretor"),
        (TipoPerfilAcesso.GestorQsms, "Gestor QSMS"),
        (TipoPerfilAcesso.EngenheiroSeguranca, "Engenheiro de Segurança"),
        (TipoPerfilAcesso.TecnicoSeguranca, "Técnico de Segurança"),
        (TipoPerfilAcesso.MedicoDoTrabalho, "Médico do Trabalho"),
        (TipoPerfilAcesso.Rh, "RH"),
        (TipoPerfilAcesso.GestorDeObra, "Gestor de Obra"),
        (TipoPerfilAcesso.Encarregado, "Encarregado"),
        (TipoPerfilAcesso.Trabalhador, "Trabalhador"),
        (TipoPerfilAcesso.Auditor, "Auditor"),
        (TipoPerfilAcesso.Terceiro, "Terceiro"),
    };

    private static readonly (string Codigo, string Modulo, string Acao, string Descricao)[] CatalogoPermissoes =
    {
        ("organizacional:ver", "Organizacional", "Ver", "Ver Obra/Setor/Equipe/Função"),
        ("organizacional:criar", "Organizacional", "Criar", "Criar Obra/Setor/Equipe/Função"),
        ("organizacional:editar", "Organizacional", "Editar", "Editar Obra/Setor/Equipe/Função"),
        ("organizacional:excluir", "Organizacional", "Excluir", "Excluir Obra/Setor/Equipe/Função"),

        ("trabalhador:ver", "Trabalhador", "Ver", "Ver cadastro de trabalhador"),
        ("trabalhador:criar", "Trabalhador", "Criar", "Criar cadastro de trabalhador"),
        ("trabalhador:editar", "Trabalhador", "Editar", "Editar cadastro de trabalhador"),
        ("trabalhador:excluir", "Trabalhador", "Excluir", "Excluir cadastro de trabalhador"),
        ("trabalhador:telegram", "Trabalhador", "Telegram", "Gerar vínculo de Telegram do trabalhador"),
        ("trabalhador:assinatura", "Trabalhador", "Assinatura", "Configurar assinatura eletrônica do trabalhador (PIN, Termo de Aceite, consentimento biométrico)"),

        ("aso:ver_status", "Aso", "VerStatus", "Ver status do ASO (Apto/Inapto/etc.), sem detalhe clínico"),
        ("aso:ver_clinico", "Aso", "VerClinico", "Ver conteúdo clínico completo do ASO"),
        ("aso:criar", "Aso", "Criar", "Criar/agendar ASO"),
        ("aso:editar", "Aso", "Editar", "Editar ASO"),
        ("aso:homologar", "Aso", "Homologar", "Homologar resultado clínico do ASO"),

        // PR-SST-003 — Saúde Ocupacional: PCMSO (documento + campos específicos), exames
        // complementares e aptidões para atividade crítica, além do ASO já existente acima.
        ("pcmso:ver", "Pcmso", "Ver", "Ver PCMSO"),
        ("pcmso:criar", "Pcmso", "Criar", "Criar PCMSO"),
        ("pcmso:editar", "Pcmso", "Editar", "Editar PCMSO"),

        ("examecomplementar:ver", "ExameComplementar", "Ver", "Ver exames complementares (audiometria, acuidade visual etc.)"),
        ("examecomplementar:criar", "ExameComplementar", "Criar", "Registrar exame complementar"),
        ("examecomplementar:editar", "ExameComplementar", "Editar", "Editar exame complementar"),

        ("aptidao:ver", "Aptidao", "Ver", "Ver aptidões para atividade crítica"),
        ("aptidao:criar", "Aptidao", "Criar", "Registrar aptidão para atividade crítica"),
        ("aptidao:editar", "Aptidao", "Editar", "Editar aptidão para atividade crítica"),

        ("treinamento:ver", "Treinamento", "Ver", "Ver treinamentos"),
        ("treinamento:criar", "Treinamento", "Criar", "Criar treinamento/curso"),
        ("treinamento:editar", "Treinamento", "Editar", "Editar treinamento/curso"),

        ("epi:ver", "Epi", "Ver", "Ver catálogo/entregas de EPI"),
        ("epi:criar", "Epi", "Criar", "Criar catálogo/entrega de EPI"),
        ("epi:editar", "Epi", "Editar", "Editar catálogo/entrega de EPI"),

        ("risco:ver", "Risco", "Ver", "Ver atividades/perigos/riscos"),
        ("risco:criar", "Risco", "Criar", "Criar atividade/perigo/risco"),
        ("risco:editar", "Risco", "Editar", "Editar atividade/perigo/risco"),
        ("risco:aprovar_liberacao", "Risco", "AprovarLiberacao", "Aprovar liberação de atividade de risco (§45 — motor de elegibilidade)"),

        ("pgr:ver", "Pgr", "Ver", "Ver PGR/plano de ação/revisões"),
        ("pgr:criar", "Pgr", "Criar", "Criar PGR/plano de ação"),
        ("pgr:editar", "Pgr", "Editar", "Editar PGR/plano de ação"),

        ("identificacao:ver", "Identificacao", "Ver", "Ver áreas/tags NTAG-QR"),
        ("identificacao:criar", "Identificacao", "Criar", "Criar área/tag"),
        ("identificacao:editar", "Identificacao", "Editar", "Editar área/tag"),

        ("apr:ver", "Apr", "Ver", "Ver APR"),
        ("apr:criar", "Apr", "Criar", "Criar/elaborar APR"),
        ("apr:editar", "Apr", "Editar", "Editar etapas/riscos da APR"),
        ("apr:aprovar", "Apr", "Aprovar", "Aprovar/reprovar APR"),

        ("auditoria:ver_trilha", "Auditoria", "VerTrilha", "Ver trilha de auditoria"),
        ("auditoria:ver_evidencias", "Auditoria", "VerEvidencias", "Ver evidências anexadas"),

        ("usuario:ver", "Usuario", "Ver", "Ver usuários"),
        ("usuario:criar", "Usuario", "Criar", "Criar/pré-provisionar usuário"),
        ("usuario:editar", "Usuario", "Editar", "Editar usuário (status, vínculo)"),
        ("usuario:excluir", "Usuario", "Excluir", "Excluir/desativar usuário"),

        ("perfilacesso:ver", "PerfilAcesso", "Ver", "Ver perfis de acesso"),
        ("perfilacesso:criar", "PerfilAcesso", "Criar", "Criar perfil de acesso customizado"),
        ("perfilacesso:editar", "PerfilAcesso", "Editar", "Editar perfil de acesso"),
        ("perfilacesso:excluir", "PerfilAcesso", "Excluir", "Excluir perfil de acesso customizado"),
        ("perfilacesso:gerenciar_permissoes", "PerfilAcesso", "GerenciarPermissoes", "Definir a matriz de permissões de um perfil"),

        // Códigos adicionados quando o enforcement de autorização (PermissaoAuthorizationHandler)
        // foi implementado — mesma natureza de proposta própria dos códigos acima (não citação
        // literal da Base de Conhecimento), cobrindo os módulos implementados depois do catálogo
        // original (PT, Inspeções, NC, Acidentes, Matriz Legal, Gestão Documental, Alertas).
        ("pt:ver", "PermissaoTrabalho", "Ver", "Ver Permissão de Trabalho"),
        ("pt:criar", "PermissaoTrabalho", "Criar", "Elaborar Permissão de Trabalho"),
        ("pt:editar", "PermissaoTrabalho", "Editar", "Editar perigos/controles/requisitos da PT"),
        ("pt:autorizar", "PermissaoTrabalho", "Autorizar", "Autorizar/liberar a Permissão de Trabalho"),
        ("pt:encerrar", "PermissaoTrabalho", "Encerrar", "Encerrar a Permissão de Trabalho"),

        ("inspecao:ver", "Inspecao", "Ver", "Ver inspeções e modelos de checklist"),
        ("inspecao:criar", "Inspecao", "Criar", "Criar inspeção"),
        ("inspecao:responder", "Inspecao", "Responder", "Responder itens do checklist da inspeção"),
        ("inspecao:encerrar", "Inspecao", "Encerrar", "Encerrar inspeção"),

        ("checklist:ver", "Checklist", "Ver", "Ver modelos de checklist"),
        ("checklist:gerenciar", "Checklist", "Gerenciar", "Criar/versionar modelo de checklist"),

        ("nc:ver", "NaoConformidade", "Ver", "Ver não conformidades"),
        ("nc:criar", "NaoConformidade", "Criar", "Registrar não conformidade"),
        ("nc:editar", "NaoConformidade", "Editar", "Editar não conformidade"),
        ("nc:avancar_status", "NaoConformidade", "AvancarStatus", "Avançar status da não conformidade"),

        ("acidente:ver", "Acidente", "Ver", "Ver acidentes/incidentes"),
        ("acidente:criar", "Acidente", "Criar", "Registrar acidente/incidente"),
        ("acidente:editar", "Acidente", "Editar", "Editar registro de acidente/investigação"),
        ("acidente:avancar_status", "Acidente", "AvancarStatus", "Avançar status do acidente"),

        ("hht:ver", "RegistroHht", "Ver", "Ver registros mensais de HHT por obra"),
        ("hht:criar", "RegistroHht", "Criar", "Lançar registro mensal de HHT"),
        ("hht:editar", "RegistroHht", "Editar", "Editar registro mensal de HHT"),
        ("hht:excluir", "RegistroHht", "Excluir", "Excluir registro mensal de HHT"),

        ("alerta:ver", "Alerta", "Ver", "Ver alertas"),
        ("alerta:criar", "Alerta", "Criar", "Criar alerta manualmente"),
        ("alerta:editar", "Alerta", "Editar", "Editar/excluir registro de alerta"),
        ("alerta:tratar", "Alerta", "Tratar", "Iniciar tratamento/resolver/ignorar alerta"),
        ("alerta:escalonar", "Alerta", "Escalonar", "Escalonar alerta"),

        ("planoacao:ver", "AcaoPlano", "Ver", "Ver ações de plano vinculadas (NC/Acidente)"),
        ("planoacao:criar", "AcaoPlano", "Criar", "Criar ação de plano"),
        ("planoacao:editar", "AcaoPlano", "Editar", "Editar ação de plano"),
        ("planoacao:validar", "AcaoPlano", "Validar", "Validar/concluir ação de plano"),

        ("dds:ver", "Dds", "Ver", "Ver DDS (Diálogo Diário de Segurança)"),
        ("dds:criar", "Dds", "Criar", "Criar DDS a partir das atividades do dia"),
        ("dds:conduzir", "Dds", "Conduzir", "Marcar itens do checklist e registrar participantes durante a condução do DDS"),
        ("dds:encerrar", "Dds", "Encerrar", "Encerrar DDS"),
        ("dds:exportar", "Dds", "Exportar", "Exportar DDS em PDF e enviar via Telegram"),

        // Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 6) — genérico,
        // usado pela tela de quiosque de qualquer módulo (Dds hoje, Treinamento/EPI/APR/PT/Inspeções
        // depois), por isso módulo próprio em vez de reaproveitar "dds:conduzir".
        ("assinatura:ver", "Assinatura", "Ver", "Ver status e signatários de um documento de assinatura eletrônica"),
        ("assinatura:assinar", "Assinatura", "Assinar", "Assinar um documento no quiosque (crachá/QR + PIN ou biometria)"),
        ("assinatura:finalizar", "Assinatura", "Finalizar", "Fechar um documento de assinatura para novas assinaturas e gerar o hash de integridade (etapa 8)"),


        ("ativo:ver", "Ativo", "Ver", "Ver cadastro de ativos de SST (extintores/equipamentos)"),
        ("ativo:criar", "Ativo", "Criar", "Cadastrar ativo de SST"),
        ("ativo:editar", "Ativo", "Editar", "Editar ativo de SST"),
        ("ativo:excluir", "Ativo", "Excluir", "Excluir ativo de SST"),

        ("regraalerta:ver", "RegraAlerta", "Ver", "Ver limiares de antecedência/severidade do Motor Central de Alertas"),
        ("regraalerta:criar", "RegraAlerta", "Criar", "Cadastrar limiar de antecedência/severidade por módulo"),
        ("regraalerta:editar", "RegraAlerta", "Editar", "Editar limiar de antecedência/severidade por módulo"),
        ("regraalerta:excluir", "RegraAlerta", "Excluir", "Excluir limiar de antecedência/severidade por módulo"),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var tiposExistentes = await db.PerfisAcesso
            .IgnoreQueryFilters()
            .Where(p => p.Tipo != null)
            .Select(p => p.Tipo!.Value)
            .ToListAsync(ct);

        foreach (var (tipo, nome) in PerfisDeSistema)
        {
            if (tiposExistentes.Contains(tipo)) continue;

            db.PerfisAcesso.Add(new PerfilAcesso
            {
                Tipo = tipo,
                Nome = nome,
                EhSistema = true
            });
        }

        var codigosExistentes = await db.Permissoes
            .IgnoreQueryFilters()
            .Select(p => p.Codigo)
            .ToListAsync(ct);

        foreach (var (codigo, modulo, acao, descricao) in CatalogoPermissoes)
        {
            if (codigosExistentes.Contains(codigo)) continue;

            db.Permissoes.Add(new Permissao
            {
                Codigo = codigo,
                Modulo = modulo,
                Acao = acao,
                Descricao = descricao
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
