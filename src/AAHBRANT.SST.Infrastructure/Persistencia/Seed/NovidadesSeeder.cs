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
        new(
            Versao: "5.23.0",
            Titulo: "Administrador pode excluir entrega de EPI",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Botão para excluir uma entrega de EPI registrada por engano",
                    "Entrega lançada errada ficava na lista para sempre — não havia como apagar pela tela.",
                    "Quem tem perfil de Administrador vê um botão de lixeira em cada linha da lista de entregas. A confirmação diz qual entrega será apagada e quantas unidades voltam para o estoque. Para os demais perfis o botão não aparece, e o servidor recusa a exclusão."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Excluir uma entrega devolve o EPI ao estoque da obra",
                    "A exclusão apagava a entrega mas não devolvia nada ao saldo: cada entrega apagada tirava unidades do estoque em definitivo, sem deixar rastro.",
                    "O que ainda estava com o trabalhador volta para o saldo da obra, com uma movimentação de estorno registrada no histórico do EPI. O que já tinha sido devolvido antes não é somado duas vezes."),
            }),
        new(
            Versao: "5.24.0",
            Titulo: "Exclusão de registro passa a ser só do Administrador",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Apagar registro exige perfil de Administrador",
                    "Quem podia editar um registro também podia apagá-lo: APR, Permissão de Trabalho, instalação de EPC, treinamento/certificado e não conformidade sumiam de vez com um clique de qualquer perfil com permissão de edição.",
                    "Excluir passou a ser privilégio de Administrador nesses módulos e na entrega de EPI. Para os demais perfis o botão nem aparece. Ações de rotina continuam liberadas: trocar a foto de evidência do DDS, substituir o arquivo de um certificado e tirar um risco crítico de dentro da PT."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Excluir instalação de EPC devolve o material ao estoque",
                    "Assim como acontecia no EPI, instalar baixava o saldo e excluir não devolvia nada — o EPC sumia do estoque da obra em definitivo.",
                    "O que ainda estava instalado volta para o saldo, com movimentação de estorno no histórico. Instalação já removida não é somada de novo."),
            }),
        new(
            Versao: "5.25.0",
            Titulo: "Entrega de uniforme e inspeção também podem ser excluídas",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Excluir entrega de uniforme, com a peça voltando ao estoque",
                    "Entrega de uniforme não tinha como ser excluída de jeito nenhum — o registro ficava na lista para sempre.",
                    "O Administrador vê o botão de lixeira na lista de entregas. A peça volta para o estoque do tamanho correspondente na obra, com movimentação de estorno registrada."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Excluir inspeção, com proteção para as não conformidades",
                    "Inspeção também não tinha exclusão. E apagar uma inspeção em cascata levaria junto as não conformidades geradas a partir dela, que têm prazo, responsável e plano de ação próprios.",
                    "O Administrador pode excluir a inspeção e as respostas do checklist. Se a inspeção já gerou não conformidade, a exclusão é recusada e o sistema informa quantas são — resolva ou exclua essas não conformidades primeiro."),
            }),
        new(
            Versao: "5.26.0",
            Titulo: "Cadastro facial recusa foto ruim na hora",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "A foto do cadastro facial só é aceita se servir para reconhecimento",
                    "Qualquer foto era aceita no cadastro. Uma foto escura, tremida ou de longe entrava sem aviso — e o problema só aparecia semanas depois, quando o trabalhador tentava assinar e recebia \"rosto reconhecido com baixa confiança\", sem ninguém ligar uma coisa à outra.",
                    "A foto é conferida na hora do cadastro, com o trabalhador ainda na frente da câmera: resolução, um único rosto, rosto grande o suficiente no quadro e a avaliação de qualidade do próprio Azure. Quando recusa, a mensagem diz o que corrigir — iluminação, distância, boné ou óculos escuros — e a tela já mostra essas orientações antes da captura."),
            }),
        new(
            Versao: "5.27.0",
            Titulo: "A biometria confere se é a pessoa certa assinando",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Assinatura por rosto ou digital só vale se for o trabalhador do documento",
                    "O sistema registrava a assinatura de quem a biometria identificasse, sem conferir de quem era o documento. Se o reconhecimento facial trocasse duas pessoas parecidas, a entrega de um ficava assinada em nome do outro — e o registro parecia válido.",
                    "Antes de registrar, o sistema confere se a pessoa identificada é a mesma do documento (entrega de EPI, devolução, uniforme, treinamento e ficha de EPI). Quando não é, a assinatura é recusada dizendo de quem era o rosto ou a digital e de quem é o documento. O visto do responsável por sessão logada continua funcionando como antes, porque ali quem assina não é o trabalhador."),
            }),
        new(
            Versao: "5.28.0",
            Titulo: "Continuar inspeção voltou a abrir",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Inspeção em andamento abre mesmo depois de editar o checklist",
                    "Quando o checklist era editado no Catálogo de inspeções, as inspeções que já estavam em andamento com a versão anterior passavam a mostrar \"Not Found\" ao clicar em \"Continuar inspeção\" — no alojamento e nos demais tipos.",
                    "A inspeção abre normalmente e continua com os itens da versão do checklist com que foi iniciada. As próximas inspeções já usam a versão nova."),
            }),
        new(
            Versao: "5.29.0",
            Titulo: "Monte a equipe direto na APR",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Salvar os responsáveis marcados como equipe",
                    "O campo Equipe da APR ficava sempre em \"Nenhuma\": não havia onde cadastrar equipes, e a cada APR era preciso procurar e marcar os responsáveis um por um na lista.",
                    "Marque os responsáveis, clique em \"Salvar seleção como equipe\", dê um nome e, se quiser, escolha o encarregado. A equipe fica salva na obra da atividade. Quem já estava em outra equipe passa para a nova, e a tela avisa antes de salvar."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Escolher a equipe já marca os responsáveis",
                    "Selecionar uma equipe na APR não mudava nada na lista de responsáveis.",
                    "Ao escolher a equipe, todos os membros dela ficam marcados como responsáveis. Dá para desmarcar ou incluir alguém antes de salvar a APR."),
            }),
        new(
            Versao: "5.30.0",
            Titulo: "Botão Criar virou Atalhos",
            DataPublicacao: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Atalhos do dia a dia no topo da tela",
                    "O botão verde \"Criar\" no topo levava a PGR, APR, funcionário, ocorrência e treinamento — nem sempre o que se usa no campo todo dia.",
                    "O botão agora se chama \"Atalhos\" e leva direto a DDS, EPI, Treinamento, Inspeção e Ocorrência. O campo \"Buscar no sistema\", que só levava a algumas telas por palavra-chave, saiu do topo, assim como o ícone de Pendências, que repetia o sininho de alertas."),
            }),
        new(
            Versao: "5.31.0",
            Titulo: "Chamados do Suporte IA avisam no Teams",
            DataPublicacao: new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Todo chamado do Suporte IA chega no Teams do responsável",
                    "O responsável pelo suporte só era avisado no Teams quando a IA concluía que o chamado exigia mudança no sistema. Dúvidas e chamados respondidos pela própria IA chegavam apenas por fora do Teams.",
                    "Todo chamado aberto na Central de Suporte IA agora gera aviso no sininho do Teams do responsável e um evento no calendário dele, na data do prazo de atendimento: Crítica no mesmo dia, Alta em 1, Média em 3 e Baixa em 5 dias úteis. Quando o chamado é resolvido ou recusado, o evento sai do calendário."),
            }),
        new(
            Versao: "5.32.0",
            Titulo: "Visualize documentos sem baixar",
            DataPublicacao: new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Botão Visualizar ao lado de cada Baixar",
                    "Para ver um DDS, APR, PT, inspeção, ficha de EPI, certificado ou ata era preciso baixar o PDF. Dentro do Teams, a pré-visualização de certificados e de Documentos & Procedimentos abria em branco.",
                    "O documento abre numa janela dentro do próprio sistema, com todas as páginas, também no Teams. Se precisar do arquivo, é só clicar em Baixar na mesma janela."),
            }),
        new(
            Versao: "5.33.0",
            Titulo: "Botões de ação com cores da AAHBRANT",
            DataPublicacao: new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Cada ação agora tem sua cor",
                    "Os botões das tabelas eram só ícones soltos, sem fundo — Ver, Baixar e Excluir pareciam iguais.",
                    "Ver e Editar têm contorno vinho, Baixar é vinho sólido e Excluir é vermelho, em todas as telas do sistema."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Certificados: botão de anexar só quando falta o arquivo",
                    "O ícone de alerta para anexar aparecia também em certificados emitidos pelo sistema, que não precisam de anexo.",
                    "O botão de anexar (âmbar) só aparece em certificado externo que ainda não tem o arquivo escaneado, e some depois do envio."),
            }),
        new(
            Versao: "5.34.0",
            Titulo: "Assinatura e presença por digital ou facial",
            DataPublicacao: new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Presença no DDS por digital ou reconhecimento facial, à escolha do operador",
                    "A presença no DDS era confirmada só pela digital, e não havia como escolher quem da obra participaria.",
                    "Selecione os funcionários da obra no DDS e confirme a presença de cada um pelo botão \"Confirmar por digital\" ou \"Confirmar por facial\"."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "A APR agora pode ser assinada por digital ou reconhecimento facial",
                    "A aba Assinaturas da APR só registrava a ciência manualmente, sem confirmar a identidade de quem assinava.",
                    "Na aba Assinaturas da APR, o botão \"Assinar com digital ou facial\" abre a assinatura eletrônica, como já acontecia no DDS, na PT e nas inspeções."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Nova tela para registrar e acompanhar os leitores de digital das obras",
                    "Cada PC de obra com leitor de digital precisava ser configurado por fora do sistema.",
                    "Em Administração > \"Leitores de digital\" você registra o leitor de cada obra, acompanha se ele está conectado e revoga um PC perdido ou trocado."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Digital e facial disponíveis em todas as obras, com botões no mesmo padrão",
                    "A assinatura por digital ou facial só funcionava nas obras em que alguém tivesse ligado o método por trás do sistema.",
                    "Toda obra aceita os dois métodos, e o botão de digital tem o mesmo tamanho do de facial. Dá para restringir por obra em Administração > Obras > Editar."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Cadastro da digital com duas leituras e aviso sonoro",
                    "A digital era cadastrada com uma única leitura, e uma leitura ruim atrapalhava o reconhecimento nas assinaturas seguintes.",
                    "O cadastro pede duas leituras do mesmo dedo e só grava se elas coincidirem. Um bipe avisa quando retirar o dedo, e outro confirma cada assinatura ou presença aceita."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Cadastro facial com guia de captura e fotos guardadas no perfil",
                    "O cadastro facial abria a câmera direto, e a foto usada não ficava registrada no perfil do funcionário.",
                    "Antes de fotografar, um guia explica como tirar a foto (sem óculos, ambiente claro, rosto de frente) e só abre a câmera após o OK. As fotos aprovadas ficam no perfil, na aba Cofre de Assinaturas."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: horário da assinatura aparecia 3 horas adiantado",
                    "A tela mostrava, por exemplo, \"às 15:22\" para uma assinatura feita às 12:22.",
                    "O horário da assinatura aparece no horário local, igual ao relógio de quem assinou."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Correcao,
                    "Corrigido: funcionários mais antigos não apareciam na lista do DDS",
                    "Funcionários cadastrados antes de setembro não eram listados para seleção no DDS.",
                    "Todos os funcionários ativos da obra aparecem na lista do DDS."),
            }),
        new(
            Versao: "5.35.0",
            Titulo: "Escolher funcionários ficou mais fácil",
            DataPublicacao: new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Lista com busca para escolher responsáveis, equipe e participantes",
                    "Os funcionários apareciam como uma nuvem de botões soltos, difícil de percorrer e de achar um nome.",
                    "Na APR (Responsáveis), na Permissão de Trabalho (Equipe executante) e nas Turmas de treinamento (Participantes), os nomes aparecem numa lista com campo de busca, contador de selecionados e botões para marcar os resultados da busca ou limpar a seleção."),
                new NovidadeSeedItem(
                    CategoriaNovidade.Melhoria,
                    "Botão flutuante \"Suporte IA\" removido das telas",
                    "O botão ficava fixo no canto e cobria parte das telas.",
                    "A Central de Suporte IA continua disponível pelo item \"Suporte IA\" na barra lateral."),
            }),
        new(
            Versao: "5.36.0",
            Titulo: "Preparação da integração de estoque com o G-SUPRI",
            DataPublicacao: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Painel de recebimentos e vínculos do G-SUPRI em Administração",
                    "As entradas de estoque precisavam ser lançadas novamente no SST.",
                    "O SST está preparado para receber materiais pelo G-SUPRI, controlar reenvios e mostrar pendências de vínculo ou liberação. O envio depende da conexão e ativação pela equipe responsável."),
            }),
        new(
            Versao: "5.37.0",
            Titulo: "Catálogo de APR e PT por atividade",
            DataPublicacao: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Aba Catálogo em APR e em Permissão de Trabalho",
                    "Para emitir uma APR ou PT de uma atividade da obra, era preciso preencher tudo do zero.",
                    "A aba Catálogo lista as atividades da obra com os riscos do PGR e mostra quais estão sem APR ou PT vigente. Com um clique em \"+\", o sistema gera o documento em elaboração já preenchido com os riscos e as medidas de controle do PGR, para você revisar antes de aprovar ou liberar."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Aprovações e liberações usam o usuário logado",
                    "Ao aprovar uma APR ou liberar, suspender, revalidar e encerrar uma PT, a tela pedia para digitar o código (ID) do usuário responsável.",
                    "O sistema registra automaticamente o usuário que está logado, sem precisar digitar nada. Não é mais possível assinar em nome de outra pessoa."),
            }),
        new(
            Versao: "5.37.1",
            Titulo: "Biometria cadastrada uma única vez",
            DataPublicacao: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Digital e reconhecimento facial são cadastrados só uma vez por funcionário",
                    "Mesmo depois de cadastrada a digital ou o facial, os botões de cadastro continuavam disponíveis, permitindo cadastrar de novo.",
                    "Depois do cadastro concluído com sucesso, os botões somem e a tela mostra a data em que a biometria foi cadastrada. O sistema também recusa um segundo cadastro."),
            }),
        new(
            Versao: "5.37.2",
            Titulo: "Documento do DDS mais enxuto",
            DataPublicacao: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "PDF do DDS traz só os temas e a Lista de Presença",
                    "O PDF do DDS diário imprimia também o checklist de verificação, deixando o documento longo.",
                    "O PDF mostra apenas os temas do dia e a Lista de Presença, numerada. O checklist continua sendo preenchido no sistema, mas não sai mais no documento."),
            }),
        new(
            Versao: "5.37.3",
            Titulo: "DDS com fila de digitais",
            DataPublicacao: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Fila de digitais e lista completa no DDS",
                    "Era preciso selecionar os funcionários um a um e clicar em \"Confirmar por digital\" na linha de cada pessoa.",
                    "Ao abrir o DDS, todos os funcionários da obra já entram na lista. O botão \"Abrir fila\" deixa o leitor aberto: cada funcionário encosta o dedo e a presença é registrada sozinha, com bipe só quando der certo. Digital não reconhecida aparece como erro na tela. O reconhecimento facial continua em cada linha."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Aviso de quem faltou ao finalizar o DDS",
                    "Era possível finalizar o DDS sem perceber que havia funcionários da lista sem presença.",
                    "Antes de finalizar, o sistema avisa quantos e quais funcionários da lista ainda estão sem presença confirmada."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Erro ao criar DDS com tema de descrição longa",
                    "Escolher um tema com descrição muito longa (como o de câncer de mama) gerava \"erro inesperado\" ao criar o DDS.",
                    "O DDS é criado normalmente com temas de descrição longa."),
            }),
        new(
            Versao: "5.37.4",
            Titulo: "Fotos de evidência mais flexíveis e horas corrigidas",
            DataPublicacao: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Anexar foto da galeria nas evidências",
                    "Quando o aparelho não conseguia obter a localização, não era possível concluir as fotos de evidência nem finalizar o DDS.",
                    "No diálogo \"Tirar foto\" há o botão \"Anexar foto da galeria (sem geolocalização)\". A foto fica marcada como anexada da galeria e exige só a descrição do local. A localização também passou a ser pedida ao navegador quando o Teams não a oferece, e o diálogo não corta mais o texto."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Horário das assinaturas do DDS em horário de Brasília",
                    "A hora das assinaturas aparecia 3 horas adiantada, inclusive com horário futuro.",
                    "A lista de presença, o Cofre de Assinaturas e o comprovante em PDF mostram as horas no horário de Brasília. Comprovantes já gerados precisam ser gerados de novo."),
            }),
        new(
            Versao: "5.37.5",
            Titulo: "Só a NR-06 habilita a entrega de EPI",
            DataPublicacao: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Catálogo de cursos: habilitação de EPI fixa na NR-06",
                    "Cada curso do catálogo tinha um botão \"Habilita EPI (NR-06)\", e era possível marcar outro curso (como a NR-11) para liberar a entrega de EPI.",
                    "Apenas o curso da NR-06 habilita a entrega de EPI. O botão e a caixa de marcação foram removidos e o sistema não aceita mais outro curso nessa função."),
            }),
        new(
            Versao: "5.37.6",
            Titulo: "Canhoto de EPI volta a imprimir",
            DataPublicacao: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Imprimir o canhoto do carrinho de EPI",
                    "Ao clicar em imprimir o canhoto no carrinho de EPI, nada acontecia dentro do Teams.",
                    "O canhoto abre numa janela da própria tela, com as fotos dos EPIs, e o botão Imprimir envia para a impressora."),
            }),
        new(
            Versao: "5.38.0",
            Titulo: "Inspeção de veículos e equipamentos",
            DataPublicacao: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Nova aba Veículos em Inspeções",
                    "Não havia como inspecionar caminhões, retroescavadeiras e escavadeiras no sistema: o check list era feito só na planilha.",
                    "Em Operação > Inspeções > Veículos, escolha a obra e depois o tipo (caminhão basculante, retroescavadeira, escavadeira hidráulica, caminhão carroceria ou caminhão munck). Aparecem todos os veículos daquele tipo na obra, cada um com seu card, botão de inspeção, histórico e PDF."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Cadastro de veículos feito pelo próprio SST",
                    "Os veículos não vêm de nenhum outro sistema.",
                    "O Técnico cadastra o veículo (placa/prefixo, modelo, subcontratada, responsável) na própria aba e pode trocá-lo de obra sem perder o histórico. Só o Administrador exclui."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Checklist por tipo de veículo e foto só nas não conformidades",
                    "Os 5 check lists estavam apenas na planilha \"CHECK LIST - AT CUIA\".",
                    "Cada tipo tem seu checklist (Conforme, Não conforme ou Não aplicável), editável em Catálogo de inspeções. A foto é pedida somente nos itens marcados como Não conforme, uma por item, e o PDF mostra a identificação do veículo."),
            }),
        new(
            Versao: "5.38.1",
            Titulo: "Ficha de EPI com a tabela de entregas antes do termo",
            DataPublicacao: new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Nova ordem das seções da Ficha de EPI",
                    "A ficha trazia o Termo de Recebimento e Compromisso de Uso antes do Controle de Entrega de EPI.",
                    "O Controle de Entrega de EPI passou a ser o item 2 e o Termo de Recebimento e Compromisso de Uso o item 3. Só as fichas geradas a partir de agora saem na nova ordem."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Coluna \"Observação\" e fim do texto final da Ficha de EPI",
                    "A coluna da tabela de entregas se chamava \"Motivo\" e a ficha terminava com uma seção \"Observação\" de texto fixo.",
                    "A coluna agora se chama \"Observação\" e continua mostrando o motivo da entrega. A seção final de texto fixo foi removida."),
            }),
        new(
            Versao: "5.39.0",
            Titulo: "Nova tela Início",
            DataPublicacao: new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "O Dashboard agora se chama Início e está mais claro",
                    "A tela inicial mostrava sete cards separados, com um mini calendário ao lado e a barra de filtros em um quadro à parte.",
                    "O título \"Início\" e os filtros de período e obra ficam no topo, e os indicadores aparecem numa faixa única, mais fácil de ler de uma vez."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Calendário virou um item do menu",
                    "O calendário aparecia como uma miniatura no Dashboard.",
                    "O item \"Calendário\" é o último do menu lateral e abre a página completa, com as mesmas funções de antes."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Todas as ocorrências em um só card, com filtro por tipo",
                    "O Dashboard mostrava apenas os quase-acidentes.",
                    "O card de Ocorrências traz o total e a contagem de acidentes, incidentes, quase-acidentes, condições inseguras, atos inseguros e doenças ocupacionais, com a evolução dos últimos 6 meses. Clique num tipo para ver só ele no gráfico e nos últimos registros; o link do topo abre a tela de Ocorrências."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Aptidão por treinamento",
                    "O indicador \"Treinamentos em dia\" mostrava só um percentual, sem dizer quem estava com o curso vencido.",
                    "Escolha o curso (NR-35, NR-18, NR-10 e outros) e veja quantos trabalhadores que precisam dele estão em dia, vencem em 30 dias, estão vencidos ou nunca fizeram o curso. A conta usa a Matriz de Treinamento por função."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Taxa de Gravidade em card próprio",
                    "A Taxa de Gravidade dividia a faixa de indicadores e mostrava só o número.",
                    "O card mostra a taxa, a comparação com a meta, a evolução mensal (quando há horas-homem lançadas em pelo menos dois meses) e os dias perdidos, dias debitados e horas-homem usados no cálculo."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Conformidade por obra",
                    "Não havia como comparar as obras num só lugar.",
                    "Um novo card mostra a conformidade de cada obra (média de EPI, treinamentos e ASO em dia), da mais atrasada para a melhor, para indicar onde agir primeiro."),
            }),
        new(
            Versao: "5.40.0",
            Titulo: "Início: indicadores de segurança mais claros",
            DataPublicacao: new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Novo indicador: Podem trabalhar hoje",
                    "Não havia como saber, de relance, quantos trabalhadores estavam liberados para trabalhar.",
                    "O Início mostra quantos trabalhadores ativos estão liberados (ASO válido, treinamentos da função em dia e EPIs obrigatórios válidos) e quantos estão bloqueados, com o motivo de cada bloqueio. Uma pessoa pode ter mais de um motivo."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "EPI vencido por número de trabalhadores",
                    "O card mostrava só um percentual de conformidade de EPI, sem dizer quantas pessoas estavam com EPI vencido.",
                    "O card mostra quantos trabalhadores têm EPI vencido e quantas entregas estão vencidas."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Não conformidades em atraso em destaque",
                    "O card mostrava o total de não conformidades abertas, sem separar as que estouraram o prazo.",
                    "O card mostra quantas estão em atraso e o maior atraso, e informa o total de abertas logo abaixo."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Gráfico de ASO agora considera a validade",
                    "Um ASO apto que já tinha vencido continuava contado como \"apto\" no gráfico.",
                    "O gráfico separa aptos válidos, os que vencem em 30 dias e os vencidos, além de inaptos, restrição temporária e sem ASO."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Dias sem acidente com afastamento",
                    "A Taxa de Gravidade aparecia sem indicar há quanto tempo não havia acidente com afastamento.",
                    "O card da Taxa de Gravidade mostra os dias desde o último acidente com afastamento, com a data e a obra."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Pior indicador de cada obra",
                    "A conformidade por obra mostrava só a média, que escondia o ponto mais fraco.",
                    "Cada obra mostra também o pior indicador entre EPI, treinamentos e ASO."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Rótulos \"hoje\", \"no período\" e \"acumulado\" nos cards",
                    "Não ficava claro quais cards obedecem ao filtro de período.",
                    "Cada card informa se mostra a situação de hoje, segue o filtro de período ou é o acumulado."),
            }),
        new(
            Versao: "5.40.1",
            Titulo: "Fotos do DDS no documento de presença",
            DataPublicacao: new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "As 3 fotos obrigatórias do DDS agora saem no PDF",
                    "O documento da Lista de Presença trazia só os temas e as assinaturas, sem as fotos tiradas no encerramento.",
                    "Uma nova seção, \"Registro Fotográfico\", mostra as 3 fotos lado a lado, depois da lista de presença e antes da assinatura do responsável. Cada foto traz data e hora, local e coordenadas da captura; fotos anexadas da galeria aparecem como \"Sem geolocalização\". Vale para os PDFs gerados a partir de agora."),
            }),
        new(
            Versao: "5.40.2",
            Titulo: "Empresa contratante com o nome da obra",
            DataPublicacao: new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Ficha de EPI e termos mostram o nome da obra como contratante",
                    "A Ficha de EPI trazia \"não informado\" em \"Empresa contratante\", e os termos de EPI e Uniforme citavam sempre o mesmo consórcio.",
                    "A empresa contratante e o texto \"Declaro ter recebido do...\" passam a usar o nome da obra do funcionário."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Visualização de PDF não falha mais após uma atualização do sistema",
                    "Com o app aberto durante uma atualização, a Ficha de EPI mostrava o erro \"Failed to fetch dynamically imported module\".",
                    "O app se recarrega sozinho uma vez e abre o documento normalmente."),
            }),
        new(
            Versao: "5.41.0",
            Titulo: "Vários funcionários numa mesma ocorrência",
            DataPublicacao: new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Funcionários envolvidos no registro de acidente/incidente",
                    "Só dava para indicar um funcionário por ocorrência. Quando mais de uma pessoa estava envolvida, era preciso registrar a mesma ocorrência várias vezes.",
                    "Escolha a obra e marque todos os funcionários envolvidos, com busca por nome. A ocorrência continua sendo um único registro, com uma só investigação, e aparece no perfil de cada envolvido. Na lista, aparece o nome do primeiro e o total de envolvidos (ex.: \"Carlos +1\")."),
            }),
        new(
            Versao: "5.41.1",
            Titulo: "Nova inspeção depois de excluir a anterior",
            DataPublicacao: new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "\"Nova inspeção\" de veículo e alojamento voltou a funcionar após uma exclusão",
                    "Depois de excluir uma inspeção em andamento, abrir uma nova inspeção do mesmo veículo ou alojamento mostrava \"Ocorreu um erro inesperado\".",
                    "A inspeção excluída deixa de ocupar a vaga e a nova inspeção abre normalmente."),
            }),
        new(
            Versao: "5.42.0",
            Titulo: "Imagem da digital e foto no Cofre de Assinaturas",
            DataPublicacao: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Imagem da digital e foto do rosto como evidência da assinatura",
                    "No Cofre de Assinaturas, as assinaturas por digital apareciam como \"Sem foto\", sem nenhum registro visual do que foi lido.",
                    "As novas assinaturas por digital guardam a imagem da impressão lida pelo leitor, e as presenças por reconhecimento facial no DDS guardam a foto do rosto. As duas abrem pelo botão de imagem no Cofre. Assinaturas anteriores continuam sem imagem e agora mostram \"Anterior ao registro de imagem\"."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "IP sempre registrado nas assinaturas",
                    "Presenças no DDS e certificados de treinamento eram gravados com o IP \"Não registrado\".",
                    "O IP do dispositivo que realizou a assinatura passa a ser gravado em todas as assinaturas novas. Registros antigos não têm como ser corrigidos."),
            }),
        new(
            Versao: "5.43.0",
            Titulo: "Cards e gráficos no relatório de fiscalização",
            DataPublicacao: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Cards e gráficos do funcionário no relatório de fiscalização (PDF)",
                    "O relatório de fiscalização trazia só textos e listas, sem os cards e gráficos que aparecem no perfil do funcionário.",
                    "O PDF agora tem uma seção de resumo logo depois dos dados gerais, com os cards (EPIs ativos, presença em DDS, trocas de EPI no ano e treinamentos válidos) e os gráficos de status dos EPIs, assiduidade em DDS, motivo das trocas e frequência de trocas por EPI."),
            }),
        new(
            Versao: "5.44.0",
            Titulo: "Log de assinaturas na Ficha de EPI",
            DataPublicacao: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Ficha de EPI com foto do funcionário e log completo das assinaturas",
                    "A ficha mostrava só o resultado das assinaturas (assinado ou pendente), sem o detalhe de como cada uma foi feita.",
                    "A ficha ganhou a foto de cadastro no cabeçalho (o CPF sai completo e o turno saiu da grade) e, depois do termo, páginas novas com o log de todas as assinaturas de EPI do funcionário. Cada assinatura mostra o cupom da entrega, o método, o IP, o equipamento, a localização informada pelo aparelho e as imagens de cadastro e da assinatura lado a lado, com marca d'água de confidencial. As assinaturas por reconhecimento facial destacam a validação do Microsoft Azure AI Face. Assinaturas anteriores mostram \"anterior à implantação\" nos dados que ainda não eram guardados."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Mais rastro nas novas assinaturas",
                    "Só o IP e o método ficavam registrados em cada assinatura.",
                    "As novas assinaturas guardam também o equipamento, a geolocalização (quando o usuário permite) e, nas faciais, os dados da validação no Azure. Quem cadastra a digital passa a ter a imagem do cadastro guardada de forma criptografada."),
            }),
        new(
            Versao: "5.45.0",
            Titulo: "Câmera facial com detector de rosto e recadastro pelo técnico",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Detector de rosto na câmera do reconhecimento facial",
                    "A câmera só mostrava um oval de guia. Foto ruim (rosto longe, de lado, no escuro ou contra a luz) só era recusada depois de enviada, com a mensagem de baixa confiança.",
                    "Um quadrado acompanha o rosto na tela e avisa na hora: sem rosto, mais de um rosto, rosto longe, fora do centro, pouca luz, luz forte demais ou contraluz. O botão Capturar só libera quando o rosto está bom. Vale no cadastro facial, na presença do DDS e do treinamento e na assinatura. É uma ajuda: a conferência do rosto continua sendo feita pelo Azure."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Técnico pode refazer o cadastro facial do funcionário da sua obra",
                    "Quando a foto de cadastro saía ruim, não havia como refazer pelo sistema.",
                    "Na aba Assinatura do funcionário, o botão Refazer cadastro facial apaga o cadastro no Azure e libera uma nova captura. É obrigatório informar o motivo, e fica registrado quem refez e quando. As fotos antigas ficam arquivadas, então as assinaturas já feitas continuam com a prova no log da Ficha de EPI."),
            }),
        new(
            Versao: "5.46.0",
            Titulo: "Fila facial no DDS e cadastros faciais para revisar",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Fila facial na presença do DDS",
                    "A presença por rosto era confirmada funcionário por funcionário, escolhendo a pessoa na linha antes de abrir a câmera.",
                    "O botão Abrir fila facial, ao lado da fila da digital, abre a câmera em tela cheia (próprio para tablet). Cada funcionário olha para a câmera e a presença é confirmada sozinha, com bipe e o nome em letras grandes. Quem já teve a presença confirmada recebe o aviso, sem registrar de novo. Se o rosto for parecido com o de outra pessoa, a leitura é recusada e o funcionário usa a digital, para nunca confirmar o colega errado."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Lista de cadastros faciais para revisar",
                    "Quem tinha um cadastro facial fraco só era descoberto quando falhava na hora de assinar ou confirmar presença.",
                    "Em Pessoas aparece a lista de funcionários com 3 ou mais falhas de reconhecimento nos últimos 30 dias, com o botão para abrir o cadastro e refazer a foto. Quem refaz o cadastro sai da lista sozinho. As falhas são guardadas sem a foto, por 90 dias."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Resumo do DDS no Telegram",
                    "O encerramento do DDS não avisava ninguém sobre as falhas do reconhecimento facial.",
                    "Ao encerrar o DDS, um resumo vai para o grupo de SST do Telegram: presenças por facial e digital, quantidade de falhas e as matrículas que precisam refazer o cadastro. Sem nome, sem CPF e sem foto. Só envia quando o chat estiver configurado no ambiente."),
            }),
        new(
            Versao: "5.47.0",
            Titulo: "Relatório do DDS em imagem no Telegram",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Resumo do DDS no Telegram agora é uma imagem no padrão AAHBRANT",
                    "O resumo que chegava ao encerrar o DDS era só texto corrido.",
                    "O resumo chega como uma imagem com o cabeçalho da marca, os cards de presença (total, facial e digital), a barra por método, o alerta de falhas do reconhecimento facial e as matrículas que precisam refazer o cadastro, com uma legenda curta. Continua sem nome, CPF ou foto. Se a imagem não puder ser gerada ou enviada, o resumo vai em texto, como antes."),
            }),
        new(
            Versao: "5.48.0",
            Titulo: "Lista de presença diária do DDS e página Relatórios",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Lista de presença do DDS todo dia às 8h, no Telegram e no sininho",
                    "A presença do DDS só podia ser conferida abrindo o DDS ou o PDF.",
                    "Todo dia às 08:00 o sistema envia, para cada DDS encerrado ainda não informado, uma imagem no padrão AAHBRANT com quem estava, a hora de cada assinatura e quem faltou (com aviso de falta repetida), mais o PDF do DDS. O mesmo aviso chega no sininho do Teams de quem está cadastrado como destinatário da obra."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Duração do DDS",
                    "Não havia como saber quanto tempo cada DDS durou.",
                    "O relatório mostra a duração do DDS (da primeira à última assinatura de participante) e também a hora em que o DDS foi fechado e quanto se demorou para fechar. O fechamento passa a ser registrado a partir desta versão."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Página Relatórios e tela Destinatários dos relatórios",
                    "Os avisos enviados não ficavam guardados em lugar nenhum.",
                    "A nova página Relatórios guarda cada relatório gerado, com a imagem e o PDF. Em Administração, a aba Destinatários dos relatórios define quem recebe o quê em cada obra (ou em todas, para a diretoria), com o botão Sugerir pelos perfis para começar pelos técnicos e engenheiros de segurança."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Sininho do Teams para quem ainda não abriu o app",
                    "Quem nunca tinha entrado no app pelo Teams ficava sem o aviso no sininho.",
                    "O aviso agora também chega a quem ainda não entrou no app, usando o e-mail do usuário. O app do SST precisa estar instalado no Teams dessa pessoa."),
            }),
        new(
            Versao: "5.49.0",
            Titulo: "Câmera do cadastro facial mais confiável e captura automática",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Câmera do cadastro facial que abria sem imagem",
                    "Em alguns computadores a luz da câmera acendia, mas a janela ficava em branco e presa em \"Preparando o detector de rosto\".",
                    "A imagem agora aparece assim que a janela abre. Se o detector de rosto demorar mais de 15 segundos para carregar, a câmera segue só com o oval, sem travar."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Foto tirada sozinha quando o rosto fica verde",
                    "O técnico precisava clicar em Capturar depois do rosto ficar aprovado.",
                    "Quando o rosto fica aprovado (verde) por um segundo e meio seguido, a foto é tirada automaticamente, com um anel de contagem na tela. Se a pessoa se mexer, a contagem recomeça. A chave \"Captura automática\" permite desligar, e o botão Capturar continua disponível."),
            }),
        new(
            Versao: "5.50.0",
            Titulo: "Foto do cadastro facial vira a foto do perfil",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Foto do perfil atualizada no cadastro e no recadastro facial",
                    "A foto do perfil do funcionário e a foto do cadastro facial eram duas coisas separadas, e a do perfil ficava desatualizada ou vazia.",
                    "Quando o cadastro ou o recadastro facial é concluído, a foto aprovada passa a ser também a foto do perfil, substituindo a anterior. A foto biométrica continua guardada à parte, com hash, para auditoria. Quem já tinha facial cadastrado não é alterado agora: a foto só muda no próximo cadastro ou recadastro."),
            }),
        new(
            Versao: "5.51.0",
            Titulo: "Lista de funcionários com mais informação",
            DataPublicacao: new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Nova tabela de funcionários em Pessoas",
                    "A lista mostrava só o nome (com matrícula e função em letra pequena) e uma coluna de situação.",
                    "A lista agora tem as colunas Nome, Matrícula, Função, Regime (CLT, Terceirizado etc.) e ASO, com o resultado e a validade do último ASO, ou o aviso de ASO vencido ou sem ASO. A foto ficou maior e o nome aparece completo, em uma linha."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Botões de foto, Excluir e digital saíram da lista",
                    "Cada linha da lista tinha botões de foto, Excluir e cadastro de digital.",
                    "Esses botões não aparecem mais na lista, para evitar cliques por engano. A foto e o cadastro de digital ficam no perfil do funcionário."),
            }),
        new(
            Versao: "5.52.0",
            Titulo: "Foto do funcionário na ficha de EPI",
            DataPublicacao: new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Foto 3x4 na ficha de EPI",
                    "A ficha de EPI mostrava a foto pequena, dentro de uma moldura e com o texto \"Foto de cadastro\".",
                    "A foto agora aparece solta, no formato 3x4, sem moldura e sem o texto, ao lado dos dados do trabalhador."),
            }),
        new(
            Versao: "5.53.0",
            Titulo: "Localização das fotos mais rápida",
            DataPublicacao: new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Localização da foto no iPhone e no computador",
                    "No iPhone e no computador, a localização da foto demorava e muitas vezes terminava em \"Tempo de localização esgotado\".",
                    "A localização começa a ser buscada assim que a câmera abre e fica se atualizando sozinha. A tela mostra se ainda está buscando, se a precisão ainda passa de 100 m ou se a localização está pronta, e explica como liberar a permissão no iPhone e no Windows."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Sem o campo \"Local da foto\"",
                    "Para tirar a foto era preciso escrever o local (ex.: galpão 2) e, sem isso, a foto ficava com pendência.",
                    "O campo saiu da tela da câmera e não é mais exigido. Fotos antigas continuam mostrando o local que foi escrito."),
            }),
        new(
            Versao: "5.54.0",
            Titulo: "Suporte IA: abra chamado falando",
            DataPublicacao: new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Relatar por voz na Central de Suporte IA",
                    "Para abrir um chamado era preciso digitar e escolher tipo, severidade, título, módulo e descrição.",
                    "Clique em \"Relatar por voz\", fale o problema e clique em \"Parar e preencher\". A IA preenche o chamado inteiro; você confere, corrige se quiser e clica em \"Abrir chamado\". O áudio não é salvo."),
            }),
        new(
            Versao: "5.55.0",
            Titulo: "Ocorrências: relate e a IA monta o registro e o plano de ação",
            DataPublicacao: new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Relato por voz ou por escrito no registro de ocorrência",
                    "O registro de acidente ou incidente era preenchido campo a campo.",
                    "Escolha a obra, clique em \"Relatar por voz\" ou \"Escrever relato\" e conte o que aconteceu. A IA preenche tipo, gravidade, atividade, local, data, hora, lesão, atendimento e afastamento, localiza os funcionários citados no cadastro da obra e pergunta o que faltou. Você revisa e registra."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Análise de causas e plano de ação já prontos para revisar",
                    "A investigação e o plano de ação eram montados à mão depois do registro.",
                    "A IA sugere a análise preliminar de causas e de 3 a 5 ações com responsável, prazo e a base de cada uma (PGR da obra, requisito legal cadastrado ou hierarquia de prevenção da NR-01). Ação sem base cadastrada aparece marcada para validação."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Tema de DDS obrigatório para o dia seguinte",
                    "Não havia ligação entre a ocorrência e o DDS da obra.",
                    "Toda ocorrência registrada por relato gera um tema e um roteiro de DDS para o próximo dia útil da obra. No DDS daquele dia o tema já vem fixo, e ao encerrar o DDS a ação do plano é concluída sozinha."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Reunião de análise no Teams em todo acidente",
                    "A reunião de análise do acidente era marcada por fora do sistema.",
                    "Em todo acidente e doença ocupacional, o sistema marca a reunião de análise no Teams no próximo dia útil, no primeiro horário livre da sua agenda, com o Engenheiro de Segurança, o Gestor de Obra e o Gestor QSMS. O link fica na tela da ocorrência."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Aviso de prazo da ação vai para o responsável",
                    "O alerta de ação de plano atrasada ia para um responsável fixo do módulo.",
                    "O aviso de prazo das ações de plano (ocorrências e não conformidades) vai para o responsável da própria ação. Ação sem responsável continua avisando o responsável do módulo."),
            }),
        new(
            Versao: "5.56.0",
            Titulo: "Requisitos Legais: texto oficial das NRs pronto para validar",
            DataPublicacao: new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "59 requisitos das NRs já carregados para revisão",
                    "O cadastro de requisitos legais começava vazio e cada item era digitado à mão.",
                    "Os principais itens das NR-01, 06, 07, 10, 11, 12, 18, 33 e 35 para canteiro de obras já estão cadastrados com o texto oficial do gov.br e o link do PDF. Eles entram como \"Em revisão\" e já vêm ligados aos perigos do PGR correspondentes (ex.: NR-35 ao trabalho em altura)."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Validar e ativar requisito legal",
                    "Não havia como registrar que o QSMS conferiu o requisito.",
                    "Em Gestão SST › Requisitos Legais, abra o requisito, confira o texto oficial e os perigos ligados e clique em \"Validar e ativar\". Fica registrado quem validou e quando, e só então o requisito passa a ser usado no plano de ação sugerido pela IA nas ocorrências."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Filtro por status e edição do texto do requisito",
                    "A lista mostrava todos os requisitos juntos e o texto não aparecia na tela.",
                    "A lista abre filtrada no que falta validar, com filtros por status. Ao abrir um requisito você vê o texto completo e pode ajustá-lo em \"Editar texto\"."),
            }),
        new(
            Versao: "5.57.0",
            Titulo: "GHE do PGR e exames por função do PCMSO",
            DataPublicacao: new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Aba GHE no PGR da obra",
                    "Os grupos homogêneos de exposição só existiam no PDF do PGR.",
                    "Em Gestão SST › PGR, a nova aba \"GHE\" mostra cada grupo com as funções expostas, o ambiente e os riscos do mais grave ao mais leve, com EPI e EPC. Clique no GHE para abrir os riscos."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Exames por função no PCMSO",
                    "O quadro de exames do PCMSO só podia ser consultado no PDF.",
                    "No PCMSO da obra, a nova aba \"Exames por função\" mostra cada função com os exames e a periodicidade do periódico. Clique na função para ver em quais ASOs cada exame é pedido."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "GHE e exames no perfil do trabalhador",
                    "Era preciso vincular os riscos de cada trabalhador à mão.",
                    "O perfil do trabalhador mostra o GHE e os exames previstos pela função dele na obra, sem vínculo manual: o GHE na aba \"Riscos & OS\" e os exames na aba \"Geral & ASO\"."),
            }),
        new(
            Versao: "5.58.0",
            Titulo: "Alerta de exames do PCMSO pela função",
            DataPublicacao: new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Campo \"Exame do PCMSO\" no exame complementar",
                    "O exame complementar era registrado só com a categoria (ex.: Laboratoriais), sem dizer qual exame foi feito.",
                    "Ao registrar um exame complementar, escolha o exame na lista do PCMSO da função do funcionário. O tipo é preenchido e a validade é sugerida pela periodicidade; os dois continuam editáveis."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Alerta de exames da função",
                    "Não havia aviso de exame periódico vencendo pelo PCMSO.",
                    "A tela de Alertas mostra, por trabalhador, os exames do PCMSO da função que estão vencidos, vencendo ou nunca registrados. Para receber no Teams, escolha o responsável do módulo \"Exames da função (PCMSO)\" em Configurações."),
            }),
        new(
            Versao: "5.59.0",
            Titulo: "PGR e PCMSO: nova revisão e leitura com IA",
            DataPublicacao: new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Nova revisão guarda o PDF anterior",
                    "Substituir o PDF do PGR ou do PCMSO apagava o arquivo anterior, sem histórico.",
                    "O botão \"Substituir documento\" virou \"Nova revisão\": você informa data e motivo, e o PDF anterior fica guardado. A aba \"Revisões\" (agora também no PCMSO) mostra quem registrou cada revisão e abre o PDF de cada uma."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Ler PGR e PCMSO com IA",
                    "A estrutura de SST da obra (GHE, funções, riscos e exames) era digitada à mão a partir do PDF.",
                    "Com o PDF anexado, clique em \"Ler com IA\". A leitura roda em segundo plano e avisa no sininho quando termina. Na tela de revisão você confere o que foi lido, as divergências encontradas e o que muda em relação à estrutura atual, escolhe a função do sistema para cada função do documento e só então clica em \"Cadastrar estrutura\"."),
            }),
        new(
            Versao: "5.60.0",
            Titulo: "Assinatura facial sem travar o botão Capturar",
            DataPublicacao: new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Botão Capturar sempre disponível na câmera facial",
                    "Com pouca luz, câmera de baixa resolução ou rosto fora do centro, o quadrado não ficava verde e o botão Capturar continuava bloqueado, impedindo a assinatura.",
                    "O quadrado e a mensagem continuam orientando (aproximar, centralizar, melhorar a luz), mas o botão Capturar fica sempre liberado. Se a foto não servir, o reconhecimento facial recusa e informa o motivo para tentar de novo."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Reconhecimento facial de quem muda de obra",
                    "Quando o funcionário era transferido de obra (pela edição do cadastro ou pelo G-RH), o rosto dele continuava cadastrado só na obra antiga e a assinatura facial respondia \"Rosto não reconhecido\" na obra nova.",
                    "Na troca de obra, o cadastro facial agora é levado para a obra nova com a mesma foto já aprovada, sem nova captura. Para quem foi transferido antes desta versão, use \"Refazer cadastro facial\" no perfil do funcionário."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Documentos assinados passam a ser finalizados",
                    "Nenhum documento era fechado na prática: continuava aceitando assinaturas para sempre, e o comprovante e o código de verificação eram refeitos a cada exportação.",
                    "A inspeção é finalizada assim que é assinada, o DDS ao ser encerrado e o certificado de treinamento ao encerrar a turma. Nos demais documentos (entrega e devolução de EPI, uniforme, APR, PT, termo) aparece o botão \"Finalizar documento\" na tela de assinatura. Ao finalizar, o comprovante é gerado com todas as assinaturas, com o código de verificação do arquivo, e o QR de validação já impresso continua valendo. Quem assinou e depois foi desligado continua no comprovante."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Leitura de digital protegida contra falsificação",
                    "O resultado da leitura de digital (quem foi reconhecido e com que nota) passava pelo navegador sem proteção e podia ser alterado antes de chegar ao servidor.",
                    "Cada leitura agora é assinada pelo próprio Agente Biométrico do computador da obra, com uma chave que nunca sai daquele computador, e vale uma única vez. O servidor confere essa assinatura antes de aceitar. É preciso atualizar o Agente Biométrico em cada computador com leitor; enquanto isso, os leitores ainda não atualizados continuam funcionando como antes."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Reconhecimento facial com modelo mais preciso",
                    "O reconhecimento usava o modelo mais antigo do Azure, e funcionários com cadastro válido ficavam abaixo da confiança exigida e precisavam tentar várias vezes.",
                    "Os cadastros faciais passam para o modelo mais recente do Azure automaticamente, de madrugada, usando as fotos de cadastro já aprovadas — ninguém precisa ser fotografado de novo. Se a foto de algum funcionário não for aceita pelo modelo novo, o cadastro dele fica liberado para ser refeito."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Captura automática desligada por padrão e só no treinamento e no DDS",
                    "A foto podia ser tirada sozinha em qualquer tela de assinatura facial.",
                    "A chave \"Captura automática\" aparece apenas na confirmação de presença do treinamento e do DDS e começa desligada toda vez que a câmera abre. Nas demais telas (entrega de EPI, quiosque e cadastro facial) a foto é tirada só pelo botão Capturar. A fila facial do DDS continua funcionando como antes."),
            }),
        new(
            Versao: "5.61.0",
            Titulo: "Assinaturas mais confiáveis sem internet e mensagens mais claras",
            DataPublicacao: new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Fila sem internet não perde mais a foto da assinatura",
                    "Quando o envio de uma assinatura feita sem internet falhava algumas vezes (login expirado, reconhecimento facial ocupado ou recusa), a ação era descartada e a foto se perdia.",
                    "Login expirado e falhas temporárias ficam na fila e são reenviados sozinhos. Uma recusa (ex.: rosto não reconhecido) aparece no aviso de sincronização com a mensagem do motivo e os botões \"Tentar de novo\" e \"Descartar\"."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Hora em que a foto foi tirada",
                    "A assinatura feita sem internet ficava registrada só com a hora em que chegou ao servidor, que podia ser horas depois.",
                    "O registro guarda também a hora em que a foto foi tirada no aparelho."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Mensagens do reconhecimento facial",
                    "As recusas não diziam o que fazer, e falhas do serviço apareciam como códigos técnicos (ex.: \"TooManyRequests\").",
                    "Cada mensagem diz o que fazer a seguir, incluindo usar a digital, e serviço ocupado ou fora do ar aparece com uma mensagem clara."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Documento \"adulterado\" por engano",
                    "Quando quem assinou era desligado ou mudava de obra, a verificação de integridade acusava o documento como adulterado, e a pessoa sumia da página pública de validação.",
                    "Quem assinou continua contando como signatário mesmo depois de desligado ou transferido."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Confiança de cada assinatura registrada",
                    "Só as recusas do reconhecimento ficavam registradas, sem saber com que margem as assinaturas aceitas passaram.",
                    "Cada assinatura guarda a confiança da identificação (facial) ou a nota da leitura (digital)."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Falhas do reconhecimento facial e alerta para refazer o cadastro",
                    "Para ver quem estava falhando no reconhecimento facial, e com que confiança, era preciso consultar o banco de dados.",
                    "Na página Pessoas, o botão \"Ver falhas do reconhecimento facial\" mostra cada falha com data, obra, funcionário, motivo e confiança, com filtro e exportação para planilha. Funcionário com 3 ou mais falhas em 30 dias gera o alerta \"Cadastro facial para refazer\" (módulo Cadastro facial, em Alertas)."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Registro assinado não pode mais ser alterado",
                    "Uma entrega de EPI já assinada ainda podia ser editada ou excluída, e a assinatura passava a valer para um conteúdo diferente.",
                    "Entrega e devolução de EPI, entrega de uniforme e inspeção travam assim que são assinadas; APR, PT, DDS e certificado de treinamento travam quando o documento é finalizado. Para corrigir, registre um novo."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Consentimento de biometria com revogação (LGPD)",
                    "O consentimento era só uma data, sem a versão do termo, e não havia como o trabalhador retirá-lo.",
                    "O aceite guarda a versão do termo e, na aba Assinatura do funcionário, o botão \"Revogar consentimento\" desativa a digital e o facial dele (com o motivo registrado). O consentimento também passa a ser conferido na hora de assinar pelo facial."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Proteção das fotos biométricas (LGPD)",
                    "Fotos de rosto e de digital ficavam sem criptografia no banco, e o cadastro facial de quem era desligado continuava ativo.",
                    "As fotos passam a ser guardadas criptografadas, inclusive as antigas. Funcionário desligado ou excluído tem o cadastro facial removido e as digitais e fotos de cadastro arquivadas. Cada assinatura também registra o usuário que conduziu a assinatura."),
            }),
        new(
            Versao: "5.62.0",
            Titulo: "ASOs: situação de hoje separada do parecer do médico",
            DataPublicacao: new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Coluna \"Situação hoje\" na lista de ASOs",
                    "A linha mostrava \"Vencido\" na validade e \"Apto\" no resultado lado a lado, e parecia que o trabalhador estava liberado.",
                    "A nova coluna \"Situação hoje\" diz se o trabalhador pode trabalhar: Vencido, Não liberado, Aguardando parecer, Vence em X dias, Liberado com restrição ou Liberado. A coluna \"Resultado\" passou a se chamar \"Parecer do médico\" e, quando o exame venceu, aparece apagada com a data do exame."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "ASOs repetidos do G-RH numa linha só",
                    "Quando o G-RH tinha o mesmo ASO em vários documentos, a lista mostrava uma linha igual para cada um.",
                    "O mesmo exame (mesmo funcionário, tipo, data e validade) aparece numa linha só, com a indicação de quantos documentos iguais existem. Nenhum registro é apagado."),
            }),
        new(
            Versao: "5.63.0",
            Titulo: "ASO dentro da validade aparece como Apto",
            DataPublicacao: new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "ASO do G-RH sem aptidão não fica mais \"Pendente\"",
                    "ASOs vindos do G-RH sem o campo de aptidão apareciam como \"Pendente\" ou \"Aguardando parecer\", mesmo dentro da validade.",
                    "Na lista de ASOs, esses exames aparecem como \"Apto\", e a situação de hoje mostra \"Liberado\" enquanto o ASO estiver válido. O cadastro manual de ASO também começa com o resultado \"Apto\"."),
            }),
        new(
            Versao: "5.64.0",
            Titulo: "Motivo do bloqueio por ASO no Início",
            DataPublicacao: new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Por que o funcionário está sem ASO válido",
                    "O card \"Podem trabalhar hoje\" mostrava só o total \"sem ASO válido\", sem dizer o que faltava.",
                    "Agora o card separa em: ASO sem aptidão no G-RH, ASO vencido, inaptos e sem ASO. Cada funcionário entra em um só motivo."),
            }),
        new(
            Versao: "5.65.0",
            Titulo: "PGR e PCMSO preenchidos pela IA",
            DataPublicacao: new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Envie o PDF e a IA preenche o cadastro",
                    "Para cadastrar um PGR ou PCMSO era preciso digitar nome, datas e responsáveis à mão e só depois anexar o PDF.",
                    "Agora o primeiro passo é enviar o PDF: a IA lê a capa e preenche obra, nome, datas, revisão e responsável (no PCMSO, médico e CRM). Confira os campos marcados e clique em Adicionar — o PDF já fica salvo e a leitura completa começa sozinha."),
            }),
        new(
            Versao: "5.66.0",
            Titulo: "Painéis dos módulos com o mesmo visual da tela Início",
            DataPublicacao: new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Indicadores numa faixa única",
                    "Nos painéis de APR, PT, Inspeções, Ocorrências, Não conformidades, Riscos, PGR, Pessoas, Alertas e Identificação, cada indicador era um card separado com borda vinho.",
                    "Os indicadores agora ficam juntos numa faixa única, igual à tela Início. Nenhum número ou gráfico mudou."),
            }),
        new(
            Versao: "5.67.0",
            Titulo: "ASOs do G-RH com a classificação certa",
            DataPublicacao: new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Admissional, periódico e demissional vindos do G-RH",
                    "Todo ASO importado do G-RH aparecia como Periódico, mesmo quando era admissional ou demissional.",
                    "O SST agora usa a classificação do exame cadastrada no G-RH. ASOs antigos sem classificação continuam como estavam."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Funcionário apto não aparece mais como \"Pendente\"",
                    "Exames complementares (como a Audiometria Tonal) cadastrados no G-RH junto com o ASO entravam no SST como um segundo ASO sem resultado, e o funcionário aparecia como \"Pendente\" mesmo estando apto.",
                    "Esses exames não contam mais como ASO. A lista de Pessoas e os painéis passam a mostrar o resultado do ASO de verdade."),
            }),
        new(
            Versao: "5.68.0",
            Titulo: "Novo modelo de certificado por NR",
            DataPublicacao: new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Certificado de treinamento com novo visual",
                    "O certificado tinha cabeçalho de documento e a norma num selo azul no canto.",
                    "Agora tem moldura AAHBRANT, o nome do colaborador e a NR em destaque (número e nome da norma) e, no verso, o conteúdo programático com carga horária, data e local."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Conteúdo programático padrão para cada NR",
                    "Curso sem conteúdo programático cadastrado gerava certificado sem verso.",
                    "Ao digitar a NR no cadastro do curso (NR-05, 06, 10, 12, 18, 33 e 35), o sistema preenche o conteúdo, a carga horária e a validade da norma. Cursos já cadastrados sem conteúdo usam o padrão da NR no certificado."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Turma já vem preenchida com o padrão da NR",
                    "Na nova turma era preciso digitar data, carga horária e instrutor, e qualquer perfil podia mudar esses dados.",
                    "Ao escolher o curso, a turma já vem com a data do dia, carga horária mínima, validade, conteúdo e local. Só o Técnico de Segurança altera, e a carga horária nunca fica abaixo do mínimo da NR."),
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Fila contínua de presença na turma",
                    "Na turma era preciso clicar a cada participante para ler a digital.",
                    "Agora tem Abrir fila (digital) e Abrir fila facial, igual ao DDS: cada participante registra a presença um após o outro, sozinho."),
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Número de certificado próprio para cada participante",
                    "Todos os participantes da turma recebiam o mesmo número de certificado.",
                    "Cada certificado tem número único, com a NR no prefixo (ex.: AAH-NR35-2026-0001), e mostra o hash de cada assinatura."),
            }),
        new(
            Versao: "5.69.0",
            Titulo: "DDS com próprios e terceirizados na mesma semana",
            DataPublicacao: new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Nova opção \"Empregados Próprios e Terceirizados\" no DDS semanal",
                    "Ao abrir a semana de DDS era preciso escolher entre empregados próprios ou terceirizados.",
                    "Agora dá para escolher os dois ao mesmo tempo no campo Tipo. Basta informar a empresa terceirizada na abertura e o responsável dela no encerramento."),
            }),
        new(
            Versao: "5.70.0",
            Titulo: "Contrato de empresa terceirizada cadastrado pelo técnico",
            DataPublicacao: new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Novo contrato na ficha da empresa",
                    "O contrato da empresa terceirizada só chegava pelo G-Juri. Sem contrato, não havia vagas e ninguém conseguia cadastrar as pessoas da empresa.",
                    "Na ficha da empresa, o botão Novo contrato abre o cadastro com obra, número, vigência e vagas por função. Depois é só cadastrar as pessoas em cada vaga."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Editar e encerrar o contrato",
                    "Não havia como corrigir vigência, ajustar vagas ou encerrar um contrato pelo SST.",
                    "O contrato cadastrado no SST pode ser editado e encerrado na tela do contrato. Encerrar não desliga ninguém: o sistema avisa quantas pessoas continuam ativas. Contratos vindos do G-Juri continuam só para consulta."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Cadastro pelo contrato em PDF, lido pela IA",
                    "Cadastrar uma terceirizada exigia digitar empresa, contrato, vagas e cada pessoa à mão.",
                    "Em Terceirizado > Empresas, o botão Cadastrar pelo contrato (PDF) lê o contrato com IA e preenche empresa, contrato, vagas e as pessoas listadas. O técnico confere, completa o que faltar e cadastra tudo de uma vez. Contrato digitalizado (sem texto) continua sendo preenchido à mão."),
            }),
        new(
            Versao: "5.71.0",
            Titulo: "Avaliação dos riscos e medidas de prevenção por fase no PGR",
            DataPublicacao: new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Medidas de prevenção separadas em antes, durante e após o trabalho",
                    "Os controles de cada risco do PGR ficavam num texto corrido, sem dizer em que momento do trabalho cada um se aplica.",
                    "Em PGR › Inventário de riscos, clique no risco e em \"Separar em antes / durante / após\". O que já estava escrito no PGR entra validado; o que a IA acrescentar aparece em amarelo como sugestão, para a SST aceitar, editar ou descartar. O texto original do PGR continua guardado."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Confirmação da avaliação do risco",
                    "Não havia registro de quem conferiu a probabilidade e a severidade de um risco.",
                    "Riscos lançados à mão ganham o botão \"Confirmar avaliação\", que registra quem confirmou e quando. Riscos vindos da leitura do PGR já entram como avaliados. Se a probabilidade, a severidade ou a consequência mudarem, a confirmação é apagada."),
            }),
        new(
            Versao: "5.72.0",
            Titulo: "Procedimentos, políticas e detalhes da atividade para a Ordem de Serviço",
            DataPublicacao: new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Procedimentos, instruções de trabalho e políticas em Documentos & Procedimentos",
                    "A tela só guardava materiais de apoio, sem código, revisão ou situação.",
                    "Ao enviar um documento, escolha o tipo (procedimento, instrução de trabalho, política ou material de apoio) e informe o código SGI, a revisão e a situação. Só documentos vigentes vão para a Ordem de Serviço, e a política marcada com \"Incluir em todas as Ordens de Serviço\" entra em todas. Os materiais de apoio que já existiam continuam iguais."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Detalhes da atividade para a Ordem de Serviço",
                    "Não havia onde registrar as condições, ferramentas, EPCs, procedimentos e situações de parada de cada atividade.",
                    "Em PGR › Inventário de riscos, clique em \"Detalhes para a OS\" na atividade. As condições já vêm do GHE do PGR e os treinamentos da Matriz de Treinamento. Você vincula EPCs e procedimentos vigentes e registra quando interromper o trabalho, o que não fazer sem autorização e as liberações exigidas. A IA pode sugerir esses itens, que aparecem em amarelo até a SST aceitar, editar ou descartar."),
            }),
        new(
            Versao: "5.73.0",
            Titulo: "Ordem de Serviço no perfil do trabalhador",
            DataPublicacao: new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Aba Ordem de Serviço no perfil do trabalhador",
                    "O sistema não montava a Ordem de Serviço de SST exigida pela NR-01.",
                    "No perfil do trabalhador, a aba \"Ordem de Serviço\" monta a OS da obra dele com as atividades da função conforme o PGR, os riscos e as medidas validadas, os EPIs, os treinamentos, o ASO e as políticas. Clique em \"Ver prévia\" para ler a OS completa."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Pendências que impedem a emissão",
                    "Não havia como saber o que faltava para orientar o trabalhador por escrito.",
                    "Enquanto faltar algo (risco sem avaliação, sugestão da IA sem validação, procedimento não vinculado, treinamento vencido ou ASO sem aptidão), a OS fica como minuta e um pop-up explica cada pendência, por que ela bloqueia, quem resolve e onde corrigir."),
            }),
        new(
            Versao: "5.74.0",
            Titulo: "Gerar o PGR pela EAP da obra",
            DataPublicacao: new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "PGR montado a partir da EAP",
                    "O PGR só entrava no sistema pelo PDF da consultoria.",
                    "Em Gestão SST › PGR, clique em \"Gerar pela EAP\" e envie a EAP da obra (Excel, XML do MS Project ou PDF). A IA lê as etapas, agrupa as atividades em GHE com as funções do cadastro, escreve a descrição das atividades de cada função e monta o inventário de riscos com as medidas antes, durante e após o trabalho. Você confere e ajusta em 5 passos."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Aprovação do Engenheiro de Segurança",
                    "Não havia uma etapa formal de aprovação do PGR dentro do sistema.",
                    "Tudo o que a IA gera fica como sugestão até o Engenheiro de Segurança aprovar ou devolver para ajustes. Na aprovação, o PGR passa a vigente, com os riscos avaliados e as medidas validadas, e começa a alimentar a Ordem de Serviço. O PGR pelo PDF da consultoria continua funcionando igual."),
            }),
        new(
            Versao: "5.75.0",
            Titulo: "Logo da AAHBRANT no topo do sistema",
            DataPublicacao: new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Cabeçalho com a marca AAHBRANT",
                    "O topo mostrava só o ícone do colete e o nome \"SST\", sem a logo da empresa.",
                    "Agora o canto esquerdo mostra a logo oficial da AAHBRANT e o nome G-SST, no mesmo padrão do G-QUALI. Clique na marca para voltar ao Início."),
            }),
        new(
            Versao: "5.76.0",
            Titulo: "Cursos obrigatórios por função saem do PGR",
            DataPublicacao: new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Cursos obrigatórios por obra, a partir do PGR",
                    "Os cursos obrigatórios eram marcados à mão, função por função, e valiam igual para todas as obras.",
                    "Em Treinamentos › Cursos obrigatórios por obra, o sistema lê os riscos e atividades do PGR e sugere os cursos pelas NRs, com o motivo de cada um. O Engenheiro de Segurança aprova ou recusa com justificativa. Só o aprovado conta na Aptidão por treinamento do Início."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Regras por NR editáveis pelo QSMS",
                    "Não havia como dizer ao sistema quando uma NR passa a ser obrigatória.",
                    "Em Treinamentos › Regras por NR fica a tabela: NR-06 e NR-18 para todas as funções e, para as demais, as palavras do PGR que tornam o curso obrigatório (altura → NR-35, choque elétrico → NR-10 e outras)."),
            }),
        new(
            Versao: "5.77.0",
            Titulo: "Escopo do PCMSO para o médico, a partir do PGR",
            DataPublicacao: new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Escopo para o médico elaborar o PCMSO",
                    "Para o médico começar o PCMSO era preciso juntar à mão as funções, os GHE e os riscos do PGR da obra.",
                    "Em Saúde Ocupacional › Escopo para o médico, escolha a obra: o sistema monta o documento com a identificação da obra, as funções por GHE, o inventário de riscos e as atividades críticas. Dá para visualizar, baixar o PDF ou enviar ao médico por e-mail. Exames, periodicidade e critérios de aptidão ficam com o médico."),
            }),
        new(
            Versao: "5.78.0",
            Titulo: "Relatar por voz com o ditado do próprio aparelho",
            DataPublicacao: new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Ditado por voz direto no navegador",
                    "No Suporte IA e nas Ocorrências, a fala era gravada e enviada ao servidor para virar texto.",
                    "Agora o próprio Teams/navegador transforma a fala em texto enquanto você fala; o áudio não sai do aparelho. A IA continua preenchendo o formulário a partir do texto. Se o seu aparelho não tiver ditado, escreva o relato."),
            }),
        new(
            Versao: "5.79.0",
            Titulo: "Câmera do reconhecimento facial mais simples",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Clique e capture, sem posicionamento",
                    "A câmera do facial mostrava círculo, quadrado verde e avisos de luz e posição, capturava sozinha e tinha uma fila facial no DDS e no Treinamento.",
                    "Agora a câmera abre, você clica em Capturar e pronto. A fila facial saiu: cada funcionário confirma a presença pelo próprio botão. O sistema continua conferindo o rosto depois da foto; se não servir, é só tirar outra. A fila da digital continua igual."),
            }),
        new(
            Versao: "5.80.0",
            Titulo: "Fotos também em incidentes e quase-acidentes",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Fotos do local em todas as ocorrências",
                    "Só o acidente tinha espaço para fotos do local.",
                    "Agora incidentes e quase-acidentes também aceitam até três fotos do local, com data e localização. Nesses dois tipos as fotos são opcionais e não travam o encerramento. No acidente continuam as três fotos obrigatórias."),
            }),
        new(
            Versao: "5.81.0",
            Titulo: "Relato por voz não para mais sozinho",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Ditado continua até você clicar em parar",
                    "No Relatar por voz, uma pausa na fala fazia o ditado se desligar sozinho.",
                    "Agora o ditado segue ouvindo mesmo com pausas e só termina quando você clica em parar."),
            }),
        new(
            Versao: "5.82.0",
            Titulo: "Relato por voz volta a gravar o áudio",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Transcrição mais precisa",
                    "O ditado feito pelo próprio navegador errava muitas palavras.",
                    "O Relatar por voz voltou a gravar o áudio e transcrever no servidor, que entende melhor a fala. Grave, clique em parar e a IA preenche o formulário."),
            }),
        new(
            Versao: "5.83.0",
            Titulo: "Leitura do contrato de terceirizada pela IA voltou",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Cadastro pelo contrato preenche de novo",
                    "Ao enviar o PDF do contrato no Terceirizado, aparecia \"A IA não conseguiu analisar o relato agora\" e nada era preenchido.",
                    "Agora a IA volta a ler o contrato e preencher empresa, vigência, efetivo e pessoas. Confira os campos antes de cadastrar."),
            }),
        new(
            Versao: "5.84.0",
            Titulo: "Importar a pasta inteira da terceirizada",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Uma pasta, tudo cadastrado",
                    "Cada documento da terceirizada (contrato, PGR, ASO, certificados) era cadastrado um por um.",
                    "Em Terceirizado > Empresas, clique em Importar pasta e arraste a pasta da empresa, mesmo misturada. A IA separa o contrato, os documentos da empresa e o ASO e os certificados de cada funcionário. Você confere tudo e clica em Cadastrar tudo. O ASO agora também guarda o PDF."),
            }),
        new(
            Versao: "5.85.0",
            Titulo: "Reunião de análise em toda ocorrência e relatório em PDF",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Reunião de análise na tela da ocorrência",
                    "A reunião de análise só aparecia no relato com IA de acidente e doença ocupacional. Num quase acidente, por exemplo, não havia onde marcar.",
                    "Toda ocorrência agora tem o cartão Reunião de análise. Agende pelo Teams, remarque se precisar e, depois, registre que aconteceu com os presentes e a ata. A investigação só pode ser finalizada com a reunião realizada."),
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "A investigação vira documento",
                    "A investigação de acidentes, incidentes e quase acidentes ficava só na tela, sem um documento para imprimir ou arquivar.",
                    "Abra a ocorrência e clique em Visualizar relatório ou Baixar PDF. Sai tudo num documento só: identificação, descrição, consequências, causas, plano de ação, reunião de análise com a ata, fotos e espaço para as assinaturas do técnico e do engenheiro."),
            }),
        new(
            Versao: "5.86.0",
            Titulo: "Plano de ação da ocorrência: concluir com confirmação e reabrir",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Melhoria,
                    "Nada é concluído sem querer",
                    "O botão Validar das ações do plano concluía a ação com um clique, sem perguntar, e não havia como desfazer.",
                    "Agora cada ação manual tem Marcar como concluída, que pede para descrever o que foi feito e registra quem concluiu. Ações concluídas por engano podem ser reabertas com o botão Reabrir. A coluna Como se resolve mostra as ações que o sistema conclui sozinho, como o DDS do tema e a reunião de análise."),
            }),
        new(
            Versao: "5.87.0",
            Titulo: "Ações do plano que se resolvem sozinhas",
            DataPublicacao: new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Novidade,
                    "Treinamento, inspeção e EPI concluem a ação",
                    "Ações como \"reciclar NR-35 dos envolvidos\" dependiam de alguém lembrar de marcar como concluída.",
                    "Ao criar uma ação na ocorrência, escolha Como se resolve: Treinamento concluído (curso), Inspeção finalizada (tipo) ou Entrega de EPI assinada (EPI). A ação só se conclui quando aquele fato acontece — turma do curso com os envolvidos presentes, inspeção do tipo na obra ou entrega assinada por cada envolvido. Ações já criadas podem ser vinculadas pelo botão Alterar."),
            }),
        new(
            Versao: "5.88.0",
            Titulo: "Trocar a logo da obra pelo arquivo",
            DataPublicacao: new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Logo da obra abre o seletor de arquivos",
                    "Ao clicar na logo na tela de Obras, o sistema abria a câmera e não deixava escolher um arquivo.",
                    "Agora o clique abre direto o seletor de arquivos (PNG ou JPEG), para enviar de novo a logo que aparece no cabeçalho dos documentos."),
            }),
        new(
            Versao: "5.89.0",
            Titulo: "Reunião de análise volta a ser criada no Teams",
            DataPublicacao: new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(CategoriaNovidade.Correcao,
                    "Reunião de análise de acidente criada pela caixa do SST",
                    "Ao registrar um acidente ou quase acidente, a reunião de análise aparecia como \"Fora do Teams\" com erro de acesso negado.",
                    "Agora o convite sai da caixa do SST, com o link do Teams, e os participantes entram como convidados. Reuniões que falharam podem ser criadas de novo pelo botão Tentar de novo no Teams."),
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
