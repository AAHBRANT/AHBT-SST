import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { makeStyles, mergeClasses, Text, Badge, Button, Toaster, Tooltip } from '@fluentui/react-components';
import {
  Grid24Regular,
  ShieldError24Regular,
  BuildingBank24Regular,
  Settings24Regular,
  Alert24Regular,
  BriefcaseMedical24Regular,
  People24Regular,
  ChevronLeft24Regular,
  ChevronRight24Regular,
  ChevronDown16Regular,
  ChevronUp16Regular,
  Person24Regular,
} from '@fluentui/react-icons';
import { designTokens } from '../theme';
import { useTeamsContext } from '../teams/useTeamsContext';
import { api, StatusAlerta } from '../lib/api';
import logoSst from '../assets/logo-sst.png';
import { SyncStatusBadge } from '../components/SyncStatusBadge';
import { ID_TOASTER_GLOBAL } from '../lib/toaster';

// Rail de navegação (Hub Gênesis SST — design decidido em sessão anterior): faixa fina só com
// ícones + tooltip ao passar o mouse/focar, no lugar do menu largo com texto. O botão de
// expandir/recolher (removido na reformulação Hub Gênesis, pedido de volta pelo usuário em 31/08)
// alterna entre essa faixa fina e uma versão larga com rótulos visíveis, sem o overlay flutuante
// de mobile da versão antiga (app roda majoritariamente dentro do Teams desktop/browser).
const LARGURA_RAIL_COLAPSADO = '66px';
const LARGURA_RAIL_EXPANDIDO = '220px';
const CHAVE_RAIL_EXPANDIDO = 'sst.railExpandido';

const useStyles = makeStyles({
  root: {
    display: 'grid',
    gridTemplateRows: '64px 1fr',
    height: '100vh',
    width: '100%',
    transition: 'grid-template-columns 0.15s ease',
  },
  rootColapsado: {
    gridTemplateColumns: `${LARGURA_RAIL_COLAPSADO} 1fr`,
  },
  rootExpandido: {
    gridTemplateColumns: `${LARGURA_RAIL_EXPANDIDO} 1fr`,
  },
  rail: {
    gridRow: '1 / span 2',
    gridColumn: '1',
    background: designTokens.colorRailBackground,
    borderRight: `1px solid ${designTokens.colorRailBorder}`,
    color: designTokens.colorRailInk,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'stretch',
    padding: '18px 0',
    gap: '6px',
    overflowY: 'auto',
    overflowX: 'hidden',
  },
  cabecalhoRail: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '0 12px',
    marginBottom: '14px',
  },
  cabecalhoRailColapsado: {
    flexDirection: 'column',
    gap: '10px',
    padding: '0 12px',
  },
  marca: {
    width: '34px',
    height: '34px',
    borderRadius: '8px',
    flexShrink: 0,
  },
  botaoAlternarRail: {
    color: designTokens.colorRailInkMuted,
    minWidth: 'auto',
    flexShrink: 0,
  },
  navItem: {
    position: 'relative',
    width: '42px',
    height: '42px',
    marginLeft: '12px',
    borderRadius: '10px',
    color: designTokens.colorRailInkMuted,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    textDecoration: 'none',
    transition: 'background-color 0.15s ease, color 0.15s ease',
    flexShrink: 0,
  },
  navItemExpandido: {
    width: 'calc(100% - 24px)',
    justifyContent: 'flex-start',
    gap: '10px',
    padding: '0 12px',
    whiteSpace: 'nowrap',
  },
  navItemHover: {
    ':hover': {
      color: designTokens.colorRailInk,
      backgroundColor: '#D4EDDA',
    },
  },
  navItemActive: {
    color: designTokens.colorRailActiveInk,
    backgroundColor: designTokens.colorRailActiveBackground,
  },
  navRotulo: {
    fontSize: '15px',
    fontWeight: 700,
    flex: 1,
  },
  grupoCabecalho: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    width: '100%',
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    fontFamily: 'inherit',
  },
  grupoChevron: {
    flexShrink: 0,
    opacity: 0.7,
  },
  grupoFilhos: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    marginTop: '2px',
  },
  // Só usado com o rail expandido (filhos ficam ocultos quando colapsado) — por isso já define
  // largura junto com o recuo, sem precisar compor com navItemExpandido.
  navItemFilho: {
    marginLeft: '30px',
    width: 'calc(100% - 42px)',
    height: '36px',
    fontSize: '13px',
  },
  navSeparador: {
    width: '28px',
    borderTop: `1px solid ${designTokens.colorRailBorder}`,
    margin: '6px 0 6px 12px',
    flexShrink: 0,
  },
  navSeparadorExpandido: {
    width: 'calc(100% - 24px)',
  },
  railRodape: {
    marginTop: 'auto',
  },
  // Administração vem como botão sólido no rodapé, não mais um item de lista igual aos outros —
  // pedido do usuário (02/09) pra destacar como ação, não como mais um destino de navegação.
  itemAdministracaoBotao: {
    width: '100%',
    height: '48px',
    borderRadius: '12px',
    backgroundColor: designTokens.colorRailActiveBackground,
    color: designTokens.colorRailActiveInk,
    boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
    ':hover': {
      backgroundColor: '#178A3F',
      color: designTokens.colorRailActiveInk,
    },
  },
  sinoAlertas: {
    position: 'relative',
  },
  sinoContador: {
    position: 'absolute',
    top: '-4px',
    right: '-4px',
  },
  divisorTopbar: {
    width: '1px',
    height: '26px',
    backgroundColor: designTokens.colorCardBorder,
  },
  usuarioChip: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    backgroundColor: 'transparent',
    border: 'none',
    padding: '4px 6px 4px 4px',
    borderRadius: '10px',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: designTokens.colorNeutralLight,
    },
  },
  usuarioNome: {
    fontSize: '13px',
    fontWeight: 600,
    color: designTokens.colorNeutralDark,
    whiteSpace: 'nowrap',
  },
  usuarioAvatar: {
    width: '32px',
    height: '32px',
    borderRadius: '50%',
    flexShrink: 0,
    backgroundColor: designTokens.colorNeutralLight,
    border: `1.5px dashed ${designTokens.colorCardBorder}`,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    color: designTokens.colorNeutralMedium,
  },
  header: {
    gridRow: '1',
    gridColumn: '2',
    backgroundColor: designTokens.colorWhite,
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '0 24px',
  },
  content: {
    gridRow: '2',
    gridColumn: '2',
    backgroundColor: designTokens.colorNeutralLight,
    overflowY: 'auto',
    padding: '24px',
  },
  cardGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: '16px',
  },
});

interface ItemNav {
  rota: string;
  rotulo: string;
}

interface GrupoNav {
  chave: string;
  titulo: string;
  icone: typeof Grid24Regular;
  itens: ItemNav[];
}

// Item solto no topo, fora de qualquer grupo — Dashboard não pertence a nenhum módulo temático.
// Calendário saiu da sidebar (pedido do usuário, 01/09): virou redundante com o card de mini
// calendário no Dashboard, que já leva pra /calendario ao clicar — a rota continua existindo.
const itensAvulsos: Array<ItemNav & { icone: typeof Grid24Regular }> = [
  { rota: '/', rotulo: 'Dashboard', icone: Grid24Regular },
];

// Reorganização de sidebar em grupos temáticos (pedido do usuário, 2026-08-31) — substitui a lista
// achatada por módulo-pilar da consolidação de 24/08-28/08.
//
// Decisões não-literais assumidas (o pedido do usuário não cobria estes pontos):
// - PCMSO / "ASO & Exames" apontam pra SaudeOcupacionalPage (que já tem essas abas internamente),
//   usando ?aba= pra abrir direto na aba certa — não duplicamos a tela.
// - Acidentes / Incidentes / Quase-acidentes apontam pra AcidentesPage (que já tem filtro por
//   tipo), usando ?tipo= (ver TipoOcorrencia em lib/api.ts) — mesma lógica.
// - "Trabalhadores" e "Histórico" (Pessoas) apontam pra mesma rota /pessoas — não existe uma
//   listagem de histórico separada da lista de trabalhadores hoje.
// - "Usuários" e "Permissões" (Administração) apontam pra mesma rota /administracao (aba Controle
//   de Acesso) — o sistema não distingue essas duas telas hoje.
// - Documentos & Procedimentos e Configurações não têm tela própria no sistema — apontam pra
//   EmConstrucaoPage (ver App.tsx) em vez de inventar uma funcionalidade que não existe.
//
// Reestruturação de navegação (02/09, pedido do usuário): "Riscos" deixou de ser item próprio —
// virou aba de PGR/GRO (Riscos é a matriz que o PGR consome, ver PgrRiscosPage). "Treinamentos"
// deixou de apontar pra dentro de Pessoas (?aba=cursos) e ganhou módulo próprio (/treinamentos,
// ver TreinamentosPage) — cada item da sidebar deve abrir só o que é dele.
const gruposNavegacao: GrupoNav[] = [
  {
    chave: 'gestao-sst',
    titulo: 'Gestão de SST',
    icone: ShieldError24Regular,
    itens: [
      { rota: '/prevencao/pgr', rotulo: 'PGR / GRO' },
      { rota: '/operacao/saude-ocupacional?aba=pcmso', rotulo: 'PCMSO' },
      { rota: '/treinamentos', rotulo: 'Treinamentos' },
      { rota: '/epi', rotulo: 'EPI / EPC' },
      { rota: '/operacao/cipa', rotulo: 'CIPA' },
      { rota: '/prevencao/dds', rotulo: 'DDS' },
      { rota: '/gestao-sst/documentos', rotulo: 'Documentos & Procedimentos' },
      { rota: '/requisitos-legais', rotulo: 'Requisitos Legais' },
    ],
  },
  {
    chave: 'operacao',
    titulo: 'Operação',
    icone: BuildingBank24Regular,
    itens: [
      { rota: '/operacao/apr', rotulo: 'APR' },
      { rota: '/operacao/pt', rotulo: 'PT' },
      { rota: '/prevencao/inspecoes', rotulo: 'Inspeções' },
      { rota: '/operacao/identificacao', rotulo: 'Outros controles operacionais' },
    ],
  },
  {
    chave: 'pessoas',
    titulo: 'Pessoas',
    icone: People24Regular,
    itens: [
      { rota: '/pessoas', rotulo: 'Trabalhadores' },
      { rota: '/operacao/saude-ocupacional?aba=aso', rotulo: 'ASO & Exames' },
      { rota: '/pessoas', rotulo: 'Histórico' },
    ],
  },
  {
    chave: 'ocorrencias',
    titulo: 'Ocorrências',
    icone: BriefcaseMedical24Regular,
    itens: [
      { rota: '/acidentes?tipo=1', rotulo: 'Acidentes' },
      { rota: '/acidentes?tipo=2', rotulo: 'Incidentes' },
      { rota: '/acidentes?tipo=3', rotulo: 'Quase-acidentes' },
      { rota: '/nao-conformidades', rotulo: 'Não conformidades' },
    ],
  },
];

// Administração fica fixa no rodapé do rail (mesmo padrão do mockup Hub Gênesis SST) — item único,
// não grupo: Obras/Controle de Acesso/Configurações/Trilha de Auditoria/Assinaturas viraram abas
// internas de AdministracaoPage em vez de destinos separados na sidebar (pedido do usuário, 01/09 —
// reverte a versão em grupo de 31/08, que tinha 4 sub-itens aqui).
const itemAdministracao: ItemNav & { icone: typeof Grid24Regular } = {
  rota: '/administracao',
  rotulo: 'Administração',
  icone: Settings24Regular,
};

const todosGrupos = gruposNavegacao;
const CHAVE_GRUPOS_ABERTOS = 'sst.gruposNavAbertos';

// Compara rota-alvo com a localização atual. Itens com querystring (?aba=/?tipo=) exigem
// correspondência exata (senão "PCMSO" e "ASO & Exames" ficariam ativos ao mesmo tempo); itens só
// de caminho aceitam sub-rotas (ex.: /pessoas/:id mantém "Trabalhadores" ativo).
function estaAtivo(rota: string, pathname: string, search: string): boolean {
  if (rota.includes('?')) {
    return `${pathname}${search}` === rota;
  }
  if (rota === '/') return pathname === '/';
  // search === '' evita que um item sem querystring (ex.: "Trabalhadores" em /pessoas) fique ativo
  // junto com outro item da MESMA rota-base que usa ?aba= (ex.: "Treinamentos" em /pessoas?aba=cursos).
  return (pathname === rota && search === '') || pathname.startsWith(`${rota}/`);
}

function ItemRail({
  rota,
  rotulo,
  icone: Icone,
  expandido,
  destaque,
}: {
  rota: string;
  rotulo: string;
  icone: typeof Grid24Regular;
  expandido: boolean;
  destaque?: boolean;
}) {
  const estilos = useStyles();
  const link = (
    <NavLink
      to={rota}
      end={rota === '/'}
      aria-label={rotulo}
      className={({ isActive }) =>
        mergeClasses(
          estilos.navItem,
          expandido && estilos.navItemExpandido,
          !isActive && estilos.navItemHover,
          isActive && !destaque && estilos.navItemActive,
          destaque && estilos.itemAdministracaoBotao,
        )
      }
    >
      <Icone />
      {expandido && <span className={estilos.navRotulo}>{rotulo}</span>}
    </NavLink>
  );
  // Tooltip só é útil quando colapsado (ícone sem rótulo visível) — expandido já mostra o texto.
  return expandido ? link : (
    <Tooltip content={rotulo} relationship="label" positioning="after">
      {link}
    </Tooltip>
  );
}

function GrupoRail({
  grupo,
  expandido,
  aberto,
  aoAlternarAberto,
  pathname,
  search,
}: {
  grupo: GrupoNav;
  expandido: boolean;
  aberto: boolean;
  aoAlternarAberto: () => void;
  pathname: string;
  search: string;
}) {
  const estilos = useStyles();
  const Icone = grupo.icone;
  const algumFilhoAtivo = grupo.itens.some((item) => estaAtivo(item.rota, pathname, search));
  const destacarCabecalho = algumFilhoAtivo && (!expandido || !aberto);

  const cabecalho = (
    <button
      type="button"
      aria-label={grupo.titulo}
      aria-expanded={aberto}
      onClick={aoAlternarAberto}
      className={mergeClasses(
        estilos.navItem,
        estilos.grupoCabecalho,
        expandido && estilos.navItemExpandido,
        !destacarCabecalho && estilos.navItemHover,
        destacarCabecalho && estilos.navItemActive,
      )}
    >
      <Icone />
      {expandido && (
        <>
          <span className={estilos.navRotulo}>{grupo.titulo}</span>
          {aberto ? <ChevronUp16Regular className={estilos.grupoChevron} /> : <ChevronDown16Regular className={estilos.grupoChevron} />}
        </>
      )}
    </button>
  );

  return (
    <div style={{ display: 'contents' }}>
      {expandido ? (
        cabecalho
      ) : (
        <Tooltip content={grupo.titulo} relationship="label" positioning="after">
          {cabecalho}
        </Tooltip>
      )}
      {expandido && aberto && (
        <div className={estilos.grupoFilhos}>
          {grupo.itens.map((item, indice) => (
            <Link
              key={`${item.rota}-${indice}`}
              to={item.rota}
              className={mergeClasses(
                estilos.navItem,
                estilos.navItemExpandido,
                estilos.navItemFilho,
                estilos.navItemHover,
                estaAtivo(item.rota, pathname, search) && estilos.navItemActive,
              )}
            >
              <span className={estilos.navRotulo}>{item.rotulo}</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const { carregando, dentroDoTeams, contexto } = useTeamsContext();
  const nomeUsuario = contexto?.user?.displayName ?? 'Usuário';
  const [alertasAbertos, setAlertasAbertos] = useState<number | null>(null);
  const [railExpandido, setRailExpandido] = useState<boolean>(
    () => localStorage.getItem(CHAVE_RAIL_EXPANDIDO) === '1',
  );
  // Todos os grupos abrem por padrão (reorganização pedida em 31/08) — usuário pode fechar os que
  // não usa, preferência persistida como as demais do rail.
  const [gruposAbertos, setGruposAbertos] = useState<Set<string>>(() => {
    try {
      const salvo = localStorage.getItem(CHAVE_GRUPOS_ABERTOS);
      if (salvo) return new Set(JSON.parse(salvo) as string[]);
    } catch {
      // JSON inválido no localStorage — cai no default abaixo
    }
    return new Set(todosGrupos.map((g) => g.chave));
  });

  useEffect(() => {
    localStorage.setItem(CHAVE_RAIL_EXPANDIDO, railExpandido ? '1' : '0');
  }, [railExpandido]);

  useEffect(() => {
    localStorage.setItem(CHAVE_GRUPOS_ABERTOS, JSON.stringify([...gruposAbertos]));
  }, [gruposAbertos]);

  // Colapsado, clicar num grupo expande o rail inteiro e já abre esse grupo (não faz sentido
  // "abrir" um grupo sem espaço pra mostrar os filhos).
  function alternarGrupo(chave: string) {
    if (!railExpandido) {
      setRailExpandido(true);
      setGruposAbertos((atual) => new Set(atual).add(chave));
      return;
    }
    setGruposAbertos((atual) => {
      const novo = new Set(atual);
      if (novo.has(chave)) {
        novo.delete(chave);
      } else {
        novo.add(chave);
      }
      return novo;
    });
  }

  useEffect(() => {
    let cancelado = false;
    api.alertas
      .listar({ status: StatusAlerta.Aberto })
      .then((lista) => {
        if (!cancelado) setAlertasAbertos(lista.length);
      })
      .catch(() => {
        if (!cancelado) setAlertasAbertos(null);
      });
    return () => {
      cancelado = true;
    };
  }, [location.pathname]);

  return (
    <div className={mergeClasses(estilos.root, railExpandido ? estilos.rootExpandido : estilos.rootColapsado)}>
      <nav className={estilos.rail} aria-label="Navegação principal">
        <div className={mergeClasses(estilos.cabecalhoRail, !railExpandido && estilos.cabecalhoRailColapsado)}>
          <img src={logoSst} alt="AAHBRANT SST" className={estilos.marca} />
          <Button
            appearance="subtle"
            className={estilos.botaoAlternarRail}
            icon={railExpandido ? <ChevronLeft24Regular /> : <ChevronRight24Regular />}
            aria-label={railExpandido ? 'Recolher menu' : 'Expandir menu'}
            title={railExpandido ? 'Recolher menu' : 'Expandir menu'}
            onClick={() => setRailExpandido((atual) => !atual)}
          />
        </div>
        {itensAvulsos.map((item) => (
          <ItemRail key={item.rota} {...item} expandido={railExpandido} />
        ))}
        <div className={mergeClasses(estilos.navSeparador, railExpandido && estilos.navSeparadorExpandido)} />
        {gruposNavegacao.map((grupo) => (
          <GrupoRail
            key={grupo.chave}
            grupo={grupo}
            expandido={railExpandido}
            aberto={gruposAbertos.has(grupo.chave)}
            aoAlternarAberto={() => alternarGrupo(grupo.chave)}
            pathname={location.pathname}
            search={location.search}
          />
        ))}
        <div className={estilos.railRodape}>
          <ItemRail {...itemAdministracao} expandido={railExpandido} destaque />
        </div>
      </nav>

      <header className={estilos.header}>
        <div />
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <SyncStatusBadge />
          {!carregando && (
            <Badge color={dentroDoTeams ? 'success' : 'informative'} appearance="tint">
              {dentroDoTeams ? 'Executando no Teams' : 'Modo standalone (dev)'}
            </Badge>
          )}
          <div className={estilos.sinoAlertas}>
            <Button
              appearance="subtle"
              icon={<Alert24Regular />}
              aria-label="Alertas"
              title="Alertas"
              onClick={() => navigate('/alertas')}
            />
            {!!alertasAbertos && (
              <Badge className={estilos.sinoContador} color="danger" size="small" shape="circular">
                {alertasAbertos > 99 ? '99+' : alertasAbertos}
              </Badge>
            )}
          </div>
          <div className={estilos.divisorTopbar} />
          <button className={estilos.usuarioChip} title={nomeUsuario}>
            <Text className={estilos.usuarioNome}>{nomeUsuario}</Text>
            <div className={estilos.usuarioAvatar} title="Foto de perfil (em breve)">
              <Person24Regular fontSize={17} />
            </div>
          </button>
        </div>
      </header>

      <main className={estilos.content}>{children}</main>
      <Toaster toasterId={ID_TOASTER_GLOBAL} />
    </div>
  );
}

export function CardGrid({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  return <div className={estilos.cardGrid}>{children}</div>;
}
