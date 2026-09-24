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
        new(
            Versao: "5.14.2",
            Titulo: "Carrinho na entrega de EPI e bloqueio por Integração de Segurança",
            DataPublicacao: new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Entregue vários EPIs de uma vez, com uma assinatura só",
                    "Cada EPI entregue exigia registrar e assinar separadamente, um de cada vez — mesmo quando o funcionário recebia vários itens na mesma visita.",
                    "Na tela Entregas de EPI, ao escolher o funcionário já aparecem todos os EPIs vinculados à função dele. Escolha quantos quiser, revise no carrinho ao lado e confirme tudo de uma vez — a assinatura (digital ou facial) cobre a entrega inteira numa única interação."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Entrega de EPI bloqueada sem a Integração de Segurança assinada",
                    "Era possível registrar a entrega de EPI a um funcionário mesmo que ele ainda não tivesse assinado o treinamento de Integração de Segurança.",
                    "Se o funcionário não tem a Integração de Segurança em dia e assinada por ele, a tela avisa e a entrega de EPI fica bloqueada até isso ser resolvido."),
            }),
        new(
            Versao: "5.14.3",
            Titulo: "Canhoto em cupom para entrega de EPI",
            DataPublicacao: new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Canhoto do carrinho de EPI agora sai em formato de cupom",
                    "O canhoto de conferência da entrega era grande, parecido com um recibo comum, e ainda trazia espaço de assinatura, mesmo não sendo a ficha oficial.",
                    "Ao imprimir o canhoto do carrinho, ele sai compacto no formato de cupom, com título \"EPIs recebidos\" e foto dos itens quando houver imagem cadastrada no catálogo."),
            }),
        new(
            Versao: "5.15.0",
            Titulo: "Certificados de treinamento: lançamento retroativo",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Nova aba Certificados em Treinamentos",
                    "Para registrar o treinamento de quem já estava na obra antes do sistema, era preciso entrar no perfil de um funcionário por vez, e o certificado em si não tinha onde ser guardado.",
                    "A aba Certificados lista os certificados de todos os funcionários, com filtros por obra, curso, situação e busca por nome. O botão \"Lançar certificado\" registra os dados e anexa o documento de uma vez."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Anexe o certificado em PDF ou tire foto do papel",
                    "O certificado do funcionário ficava fora do sistema — em pasta de rede, e-mail ou arquivo físico.",
                    "Cada certificado guarda o arquivo digitalizado (PDF, JPEG ou PNG, até 10 MB). Dá para subir o PDF original ou fotografar o certificado impresso pela câmera, visualizar na tela e baixar quando precisar."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "O sistema separa treinamento da AAHBRANT de treinamento externo",
                    "Todo treinamento cadastrado gerava certificado no modelo AAHBRANT, mesmo quando o curso tinha sido ministrado por outra instituição.",
                    "Ao lançar, você informa a origem. Externo: o documento válido é o arquivo anexado, e o sistema não emite certificado em nome da AAHBRANT — a empresa não atesta treinamento que não ministrou. AAHBRANT: segue emitindo o modelo próprio."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Certificado sem numeração não trava mais a entrega de EPI",
                    "A entrega de EPI exigia o nº da lista de presença do treinamento de NR-06. Certificado antigo de instituição externa costuma não ter número, e isso bloqueava a entrega de quem tinha o treinamento em dia.",
                    "Agora basta o treinamento de NR-06 estar cadastrado e dentro da validade. O nº da lista de presença entra na ficha quando existir."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "NR-06 vencida passa a bloquear a entrega de EPI",
                    "A entrega era liberada mesmo com o treinamento de NR-06 fora da validade — perante a fiscalização, é o mesmo que não ter treinamento.",
                    "A tela avisa assim que você escolhe o funcionário: vencida bloqueia a entrega, e vencendo em até 30 dias aparece como aviso para programar a reciclagem."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Marque no catálogo quais cursos habilitam a entrega de EPI",
                    "O sistema adivinhava se um curso era de NR-06 lendo o texto digitado no campo Norma de referência. Escrever a norma de um jeito diferente fazia o curso deixar de ser reconhecido.",
                    "No Catálogo de Cursos existe agora a marcação \"Este curso atende à NR-06\". Os cursos de NR-06 já cadastrados foram marcados automaticamente."),
            }),
        new(
            Versao: "5.16.0",
            Titulo: "Assinatura eletrônica com data e hora",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "A ficha de EPI mostra quando cada assinatura foi coletada",
                    "A ficha e as telas de assinatura diziam apenas \"Assinado\". Numa fiscalização, saber que existe assinatura sem saber a data reduz o valor de prova do documento.",
                    "Onde antes aparecia \"Assinado\", agora aparece \"Assinado digitalmente em 04/09/2026 às 10:27\" — na ficha em PDF, no pop-up de entrega e na tela de coleta de assinatura. Entrega ainda não assinada continua marcada como Pendente."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Termo de Compromisso da ficha sai com a data preenchida",
                    "O Termo de Recebimento e Compromisso de Uso saía com o campo de data em branco, para preencher à caneta.",
                    "A data é preenchida na emissão e, quando o funcionário já assinou eletronicamente, o termo registra quem assinou e quando."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Cadastro de biometria explica os termos que o funcionário assinou",
                    "A tela de cadastro digital só pedia a confirmação de que os termos em papel existiam, sem dizer o que cada um autoriza.",
                    "A tela agora lista os dois termos obrigatórios — Aceite de Assinatura Eletrônica e Consentimento LGPD para biometria — e deixa explícito que o consentimento cobre digital e reconhecimento facial."),
            }),
        new(
            Versao: "5.17.0",
            Titulo: "Página de validação do QR Code mais clara",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: a ficha de EPI parecia não ter assinatura nenhuma",
                    "Ao escanear o QR da Ficha de EPI, a página dizia \"Nenhuma assinatura eletrônica registrada\" — mesmo com a ficha impressa mostrando assinatura em cada entrega. A ficha reúne várias entregas e quem assina é cada entrega, não a ficha.",
                    "A página agora explica que o documento é consolidado e lista as assinaturas das entregas que ele reúne. O mesmo vale para a Ata de Sessão de Treinamento e o Registro Semanal de DDS."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Cada documento aparece com o nome de verdade",
                    "A página de validação e o painel de assinaturas mostravam o nome técnico da tabela, como \"FichaEpiTrabalhador\" ou \"PermissaoTrabalho\".",
                    "Agora aparece \"Ficha de EPI\", \"Permissão de Trabalho\", \"Certificado de Treinamento\" e assim por diante — inclusive no comprovante de assinatura em PDF."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: textos colados na página de validação",
                    "O título e o texto saíam grudados, como em \"Assinaturas registradasNenhuma assinatura...\".",
                    "Cada informação aparece na sua própria linha."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: assinatura por reconhecimento facial no comprovante",
                    "O comprovante em PDF imprimia \"ReconhecimentoFacial\" e mostrava \"Obra não identificada\" no topo.",
                    "O comprovante mostra \"Reconhecimento facial (Azure Face API)\" e, no topo, o nome do documento a que se refere."),
            }),
        new(
            Versao: "5.18.0",
            Titulo: "O QR Code agora prova que o documento não foi alterado",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Impressão digital do documento (SHA-256) para conferência independente",
                    "O código exibido cobria apenas a lista de quem assinou. Alterar o conteúdo do documento não mudava esse código, então o QR validava igual antes e depois de uma adulteração.",
                    "A página passa a exibir a impressão digital do próprio documento emitido. Qualquer alteração de um único caractere muda o código — e a conferência não depende do sistema: quem tem o arquivo calcula o SHA-256 dele e compara com o publicado."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "A página diz exatamente o que cada código prova",
                    "Havia um único \"Hash de integridade\", o que dava a entender que ele cobria o documento inteiro — quando na verdade cobria só a lista de signatários.",
                    "Agora são dois campos identificados: a impressão digital do documento (cobre todo o conteúdo) e a chave do registro de assinaturas (cobre quem assinou, quando e por qual método)."),
            }),
        new(
            Versao: "5.19.0",
            Titulo: "Certificado sem página em branco e origem da assinatura",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: certificado de treinamento saía com uma terceira página quase em branco",
                    "Quando o certificado tinha duas ou mais assinaturas, o bloco de assinaturas não cabia no verso e era partido ao meio: a última assinatura caía sozinha numa terceira página, sem cabeçalho.",
                    "O certificado sai em duas páginas, com as assinaturas inteiras no verso. Se algum dia o conteúdo programático for longo demais, o bloco vai inteiro para a página seguinte em vez de se dividir."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "A página do QR Code mostra a origem de rede de cada assinatura",
                    "A página informava quem assinou, quando e por qual método, mas não trazia nenhum registro de onde a assinatura partiu.",
                    "Cada assinatura passa a exibir a origem de rede registrada. O endereço aparece parcialmente oculto (ex.: 10.20.0.xxx), porque a página é pública — o endereço completo continua disponível apenas para quem tem acesso ao painel de assinaturas."),
            }),
        new(
            Versao: "5.20.0",
            Titulo: "Entrega de EPI volta a reconhecer o treinamento de NR-06",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: a entrega de EPI acusava falta de NR-06 mesmo com o certificado lançado",
                    "A tela só reconhecia o curso quando a norma estava escrita exatamente como \"NR-6\". Cursos cadastrados como \"NR-06\" (ou \"NR-06 e NR-18\") não eram reconhecidos, e a entrega ficava bloqueada por mais certificados que fossem lançados.",
                    "Qualquer forma de escrever a norma é reconhecida — NR-06, NR 6, NR06 e normas que citam mais de uma NR."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Dá para marcar \"Habilita EPI\" em curso que já estava cadastrado",
                    "O marcador que libera a entrega de EPI só podia ser definido no momento de criar o curso. Curso antigo ficava sem ele e não havia como corrigir pela tela.",
                    "No Catálogo de Cursos, cada curso tem o botão \"Habilita EPI (NR-06)\" para marcar ou retirar a qualquer momento."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "O aviso de NR-06 agora diz o que fazer",
                    "O aviso era o mesmo tanto para quem não tinha treinamento nenhum quanto para quem tinha o certificado lançado em um curso que não habilita EPI.",
                    "O aviso diferencia os dois casos e indica onde resolver: lançar o certificado em Treinamentos › Certificados, ou marcar o curso no Catálogo de Cursos."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Certificado externo com arquivo anexado vale como prova da Integração de Segurança",
                    "A entrega de EPI só era liberada se o trabalhador tivesse assinado a Integração de Segurança dentro do sistema (digital ou facial). Quem fez o treinamento antes de a obra entrar no sistema nunca teria essa assinatura e ficava bloqueado.",
                    "Lançando o treinamento em Treinamentos › Certificados como certificado externo e anexando o arquivo digitalizado, o EPI é liberado — o documento anexado é a prova no lugar da assinatura. Sem o arquivo anexado, a assinatura continua sendo exigida."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: obra sem curso de Integração de Segurança travava a entrega de EPI",
                    "Se nenhum curso estivesse marcado como Integração de Segurança, a tela nem chegava a consultar os treinamentos e acusava falta de NR-06 em todo mundo.",
                    "A verificação da NR-06 é feita sempre, independente de a obra ter configurado o curso de Integração de Segurança."),
            }),
        new(
            Versao: "5.21.0",
            Titulo: "Certificado de curso removido do catálogo volta a aparecer",
            DataPublicacao: new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: certificado sumia da lista quando o curso saía do Catálogo de Cursos",
                    "Se o curso do treinamento (ou a função do trabalhador) fosse excluído depois, o certificado desaparecia da sub-aba Certificados — e a entrega de EPI passava a dizer que o funcionário não tinha treinamento nenhum, mesmo com o certificado visível na aba Treinamentos do perfil dele.",
                    "O certificado continua na lista e continua liberando a entrega de EPI. Se o curso tiver saído do catálogo, a linha mostra \"Curso removido do catálogo\" em vez de esconder o registro."),
            }),
        new(
            Versao: "5.22.0",
            Titulo: "Documentos saem com o horário certo",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: \"Emitido em\" dos PDFs vinha 3 horas adiantado",
                    "O servidor trabalha em horário universal (UTC), e o rodapé carimbava a hora dele em vez da hora do canteiro. Um documento emitido às 14h saía marcado como 17h — em APR, Ata de treinamento, Certificado, CIPA, DDS, Ficha de EPI, Inspeção e Permissão de Trabalho.",
                    "Todos os documentos passam a carimbar o horário de Brasília. Documentos emitidos antes desta correção continuam com a hora antiga registrada."),
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
