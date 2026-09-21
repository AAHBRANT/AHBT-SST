using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente (mesmo padrão do RegraAlertaSeeder) que publica o changelog do pop-up de
// novidades (ver NovidadeVersao) sem depender de alguém logar e cadastrar manualmente pela tela de
// Administração. Regra do usuário (18/09): toda vez que uma mudança visível ao usuário for
// deployada, adicionar uma entrada aqui ANTES do commit/push — o seeder aplica no start da
// aplicação (Program.cs, depois das migrations), então a novidade já existe assim que o deploy sobe
// em qualquer ambiente (dev/hml/produção), sem chamada HTTP autenticada.
//
// Checa por Versao (não por linha exata) — se a versão já existe, não insere de novo. Se algum dia
// for preciso corrigir uma novidade já publicada, isso é feito pela tela de Administração (edição
// manual), não editando uma entrada já semeada aqui.
public record NovidadeSeedItem(CategoriaNovidade Categoria, string Descricao, string? Antes, string? Agora);

public record NovidadeSeed(string Versao, string Titulo, DateTime DataPublicacao, NovidadeSeedItem[] Itens);

public static class NovidadesSeeder
{
    private static readonly NovidadeSeed[] Novidades =
    {
        new(
            Versao: "5.11.0",
            Titulo: "Novidade: pop-up de atualizações!",
            DataPublicacao: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Agora você vê um resumo do que mudou a cada atualização do sistema",
                    "Não havia nenhum aviso sobre o que mudava quando o sistema era atualizado.",
                    "Um pop-up aparece automaticamente ao entrar, mostrando o que foi corrigido, melhorado ou adicionado — clique em cada item para ver o antes e depois."),
            }),
        new(
            Versao: "5.11.1",
            Titulo: "Correção no pop-up de boas-vindas",
            DataPublicacao: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: o pop-up de novidades não mostrava o nome do usuário",
                    "O pop-up aparecia com \"Bem-vindo de volta, !\", sem o nome preenchido.",
                    "O pop-up mostra corretamente o seu primeiro nome."),
            }),
        new(
            Versao: "5.12.0",
            Titulo: "Novo módulo: Terceirizado",
            DataPublicacao: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Novo módulo para gerenciar empresas e pessoas terceirizadas",
                    "Empresas terceirizadas, contratos e as pessoas alocadas por elas não tinham um espaço próprio no sistema.",
                    "Cadastre empresas terceirizadas e acompanhe seus contratos — que chegam automaticamente do G-Juri assim que validados — direto no novo item \"Terceirizado\" do menu."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Controle automático de EPI e treinamento obrigatório por pessoa terceirizada",
                    "Não havia como saber rapidamente se uma pessoa terceirizada estava liberada para trabalhar (EPI entregue, Integração de Segurança em dia).",
                    "Ao cadastrar uma pessoa terceirizada numa vaga do contrato, o sistema reserva os EPIs obrigatórios da função automaticamente e mostra o status de liberação (Liberada ou Pendente) com a lista de pendências."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Painel de pendências consolidado do módulo Terceirizado",
                    "Não havia uma visão única de pessoas terceirizadas bloqueadas, contratos encerrados com pessoas ainda ativas e alertas de estoque de EPI.",
                    "Um novo painel de pendências reúne tudo isso em um só lugar, para facilitar a ação dos técnicos de segurança."),
            }),
        new(
            Versao: "5.12.1",
            Titulo: "Módulo Terceirizado: edição de empresa e ficha completa",
            DataPublicacao: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Agora é possível editar os dados de uma empresa terceirizada já cadastrada",
                    "Depois de cadastrada, a empresa terceirizada não podia ser corrigida — só inativada.",
                    "A ficha da empresa (item Terceirizado → Empresas) ganhou um botão \"Editar\", com os mesmos campos do cadastro."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Ficha da empresa terceirizada agora mostra os dados cadastrais completos",
                    "A ficha da empresa só mostrava razão social, CNPJ, status e os contratos — nome fantasia, serviço prestado e contato ficavam escondidos.",
                    "Um novo card \"Dados cadastrais\" na ficha da empresa exibe nome fantasia, tipo de serviço prestado e contato (nome, telefone, e-mail)."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Nova aba \"Terceirizado\" na ficha do funcionário terceirizado",
                    "A ficha de um funcionário terceirizado não mostrava a empresa/contrato dele nem o status de liberação — era preciso ir até o módulo Terceirizado separadamente.",
                    "Funcionários com vínculo Terceirizado agora têm uma aba própria na ficha (Pessoas → funcionário) mostrando empresa, contrato e o status de liberação com as pendências."),
            }),
        new(
            Versao: "5.12.2",
            Titulo: "Correção no pop-up de boas-vindas",
            DataPublicacao: new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: o pop-up de novidades mostrava \"Usuário\" em vez do seu nome",
                    "O pop-up de boas-vindas às vezes mostrava \"Bem-vindo de volta, Usuário!\" em vez do seu primeiro nome, mesmo com o nome certo aparecendo no cabeçalho do app.",
                    "O pop-up agora usa a mesma fonte de nome já usada no cabeçalho, então mostra corretamente o seu primeiro nome."),
            }),
        new(
            Versao: "5.12.3",
            Titulo: "Fotos clicáveis e ampliáveis",
            DataPublicacao: new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Agora é possível clicar em qualquer foto do sistema para ampliá-la",
                    "As miniaturas de foto (catálogo de EPI/EPC/Uniforme, funcionários, obras, evidências etc.) só podiam ser vistas em tamanho pequeno.",
                    "Clique em qualquer miniatura para ver a foto ampliada em uma janela; feche clicando no X ou fora da imagem."),
            }),
        new(
            Versao: "5.12.4",
            Titulo: "Acabamento visual do Dashboard",
            DataPublicacao: new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Dashboard com visual mais consistente",
                    "O card de Taxa de Gravidade destoava dos outros 6 indicadores (sem a faixa vinho no topo, padding diferente) e o selo de meta usava um estilo genérico, fora do padrão do resto do sistema.",
                    "Os 7 indicadores do topo agora têm o mesmo acabamento, e o selo de meta usa o mesmo padrão visual de status do resto do app."),
            }),
        new(
            Versao: "5.13.0",
            Titulo: "Sistema com novo visual (redesign completo)",
            DataPublicacao: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Novo cabeçalho, mais compacto e com busca rápida",
                    "A marca do sistema ficava no menu lateral e não havia como buscar direto por um cadastro, aba ou seção sem navegar até ela.",
                    "O cabeçalho agora reúne a marca, uma busca rápida (digite e aperte Enter para ir direto a APR, PT, PGR, treinamentos, ocorrências, pendências etc.), o botão \"Criar\" com os atalhos mais usados e o status de sincronização."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Menu lateral com novo destaque para a seção ativa",
                    "A seção do menu lateral em que você estava não se destacava claramente das demais.",
                    "A seção ativa do menu agora aparece em uma pastilha verde com texto branco, clara no modo claro e escura no modo escuro, sempre com o mesmo destaque verde."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Novo visual para cartões, indicadores e abas em todo o sistema",
                    "Os cartões, indicadores (KPIs) e abas do sistema usavam um visual mais denso, sem um padrão único de cores e ícones.",
                    "Cartões e KPIs ganharam uma faixa vinho fina no topo e ícones em caixas suaves; abas e subabas ganharam ícones e a seção ativa aparece destacada em verde; o fundo das páginas ganhou uma grade sutil em tom lilás."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Dashboard com filtro de período",
                    "O Dashboard só mostrava os números do mês atual, sem opção de ver outros períodos.",
                    "Uma nova barra de período permite alternar entre Última semana, Último mês, Último ano ou Tudo (selecionado por padrão), além do filtro por obra."),
            }),
        new(
            Versao: "5.14.0",
            Titulo: "Suporte IA: chamados clicáveis e fluxo de aprovação",
            DataPublicacao: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Agora é possível abrir o detalhe de um chamado do Suporte IA",
                    "Os chamados listados em \"Fila recente\" só podiam ser vistos por cima; não havia como abrir um chamado específico.",
                    "Clique em qualquer chamado (na fila recente ou na fila do responsável) para abrir a ficha completa, com a esteira de etapas e as ações disponíveis."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Etapas 3 (Aprovação e execução) e 4 (Validação do solicitante) agora funcionam",
                    "A esteira do Suporte IA mostrava 4 etapas, mas só as duas primeiras aconteciam de fato — não havia como aprovar, concluir ou validar um chamado.",
                    "Quem administra o Suporte IA pode aprovar um chamado encaminhado, executá-lo e concluir a execução; quem abriu o chamado confirma se a orientação ou correção resolveu, ou reabre o chamado para nova análise."),
            }),
        new(
            Versao: "5.14.1",
            Titulo: "Sistema utilizável no celular",
            DataPublicacao: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: o sistema não se adaptava à tela do celular",
                    "No celular, o menu lateral não recolhia e os cartões do Dashboard ficavam espremidos e sobrepostos, tornando o sistema difícil de usar fora do computador.",
                    "Em telas estreitas, o menu vira uma gaveta que abre por um botão no cabeçalho, e os cartões do Dashboard se reorganizam em uma coluna só, sem sobreposição."),
            }),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var versoesExistentes = await db.NovidadesVersao
            .IgnoreQueryFilters()
            .Select(n => n.Versao)
            .ToListAsync(ct);

        foreach (var novidade in Novidades)
        {
            if (versoesExistentes.Contains(novidade.Versao)) continue;

            var entidade = new NovidadeVersao
            {
                Titulo = novidade.Titulo,
                Versao = novidade.Versao,
                DataPublicacao = novidade.DataPublicacao,
            };

            var ordem = 0;
            foreach (var item in novidade.Itens)
            {
                entidade.Itens.Add(new NovidadeVersaoItem
                {
                    Categoria = item.Categoria,
                    Descricao = item.Descricao,
                    Antes = item.Antes,
                    Agora = item.Agora,
                    Ordem = ordem++,
                });
            }

            db.NovidadesVersao.Add(entidade);
        }

        await db.SaveChangesAsync(ct);
    }
}
