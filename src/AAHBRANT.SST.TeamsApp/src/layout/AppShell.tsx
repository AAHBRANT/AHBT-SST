import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { makeStyles, mergeClasses, Text, Badge, Button, Tooltip } from '@fluentui/react-components';
import {
  Grid24Regular,
  ShieldError24Regular,
  BuildingBank24Regular,
  Warning24Regular,
  Settings24Regular,
  Alert24Regular,
  ShieldCheckmark24Regular,
  DocumentCheckmark24Regular,
  DocumentError24Regular,
  BriefcaseMedical24Regular,
  People24Regular,
  CalendarLtr24Regular,
} from '@fluentui/react-icons';
import { designTokens } from '../theme';
import { useTeamsContext } from '../teams/useTeamsContext';
import { api, StatusAlerta } from '../lib/api';
import logoSst from '../assets/logo-sst.png';
import { SyncStatusBadge } from '../components/SyncStatusBadge';

// Rail de navegação (Hub Gênesis SST — design decidido em sessão anterior): faixa fina só com
// ícones + tooltip ao passar o mouse/focar, no lugar do menu largo com texto. Mesma faixa em
// desktop e mobile — sem estado de expandir/recolher.
const LARGURA_RAIL = '66px';

const useStyles = makeStyles({
  root: {
    display: 'grid',
    gridTemplateColumns: `${LARGURA_RAIL} 1fr`,
    gridTemplateRows: '64px 1fr',
    height: '100vh',
    width: '100%',
  },
  rail: {
    gridRow: '1 / span 2',
    gridColumn: '1',
    background: designTokens.colorRailBackground,
    backdropFilter: 'blur(6px)',
    borderRight: `1px solid ${designTokens.colorRailBorder}`,
    color: designTokens.colorRailInk,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    padding: '18px 0',
    gap: '6px',
    overflowY: 'auto',
    overflowX: 'hidden',
  },
  marca: {
    width: '34px',
    height: '34px',
    borderRadius: '8px',
    marginBottom: '14px',
    flexShrink: 0,
  },
  navItem: {
    position: 'relative',
    width: '42px',
    height: '42px',
    borderRadius: '10px',
    color: designTokens.colorRailInkMuted,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    textDecoration: 'none',
    transition: 'background-color 0.15s ease, color 0.15s ease',
    flexShrink: 0,
  },
  navItemHover: {
    ':hover': {
      color: designTokens.colorRailInk,
      backgroundColor: 'rgba(16,163,90,0.14)',
    },
  },
  navItemActive: {
    color: designTokens.colorRailActiveInk,
    backgroundColor: designTokens.colorRailActiveBackground,
  },
  navSeparador: {
    width: '28px',
    borderTop: `1px solid ${designTokens.colorRailBorder}`,
    margin: '6px 0',
    flexShrink: 0,
  },
  railRodape: {
    marginTop: 'auto',
  },
  sinoAlertas: {
    position: 'relative',
  },
  sinoContador: {
    position: 'absolute',
    top: '-4px',
    right: '-4px',
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

// Navegação consolidada (pedido do usuário em 24/08, revisada em 26 e 28/08): Dashboard + módulos-
// pilar (Procedimentos & Planos [ex-Prevenção]/Operação, cada um com abas internas — ver
// PillarLayout) + itens de 1º nível próprios. Riscos e Pessoas (antes abas de Prevenção/Operação)
// e Não Conformidades/Acidentes & Incidentes (antes abas do extinto módulo Melhoria Contínua)
// viraram itens de 1º nível na sidebar, cada um abrindo direto sua tela (que já tem título + abas
// internas próprias, mesmo padrão de EPI).
const secoesNavegacao: Array<{ pilar: string | null; itens: Array<{ rota: string; rotulo: string; icone: typeof Grid24Regular }> }> = [
  { pilar: null, itens: [{ rota: '/', rotulo: 'Dashboard', icone: Grid24Regular }] },
  // Calendário do Teams dentro do app (requisito do usuário, 2026-08-29) — agenda pessoal, por
  // isso item de 1º nível próprio (não aba de nenhum pilar), logo depois do Dashboard.
  { pilar: null, itens: [{ rota: '/calendario', rotulo: 'Calendário', icone: CalendarLtr24Regular }] },
  { pilar: null, itens: [{ rota: '/prevencao', rotulo: 'Procedimentos & Planos', icone: ShieldError24Regular }] },
  { pilar: null, itens: [{ rota: '/riscos', rotulo: 'Riscos', icone: Warning24Regular }] },
  { pilar: null, itens: [{ rota: '/pessoas', rotulo: 'Pessoas', icone: People24Regular }] },
  { pilar: null, itens: [{ rota: '/operacao', rotulo: 'Operação', icone: BuildingBank24Regular }] },
  { pilar: null, itens: [{ rota: '/nao-conformidades', rotulo: 'Não Conformidades', icone: DocumentError24Regular }] },
  { pilar: null, itens: [{ rota: '/acidentes', rotulo: 'Acidentes & Incidentes', icone: BriefcaseMedical24Regular }] },
  // Módulo de Requisitos Legais — Motor de Aplicabilidade Legal (requisito do usuário, 2026-08-29).
  { pilar: null, itens: [{ rota: '/requisitos-legais', rotulo: 'Requisitos Legais', icone: DocumentCheckmark24Regular }] },
  // EPI ficou fora dos módulos-pilar (sidebar fixa própria) por decisão do usuário: catálogo/estoque
  // e entregas são dado operacional/compartilhado, não pessoal — não caberia como aba de um pilar.
  { pilar: null, itens: [{ rota: '/epi', rotulo: 'EPI', icone: ShieldCheckmark24Regular }] },
];

// Administração fica fixa no rodapé do rail (mesmo padrão do mockup Hub Gênesis SST).
const itemAdministracao = { rota: '/administracao', rotulo: 'Administração', icone: Settings24Regular };

const itensNavegacaoFlat = [...secoesNavegacao.flatMap((secao) => secao.itens), itemAdministracao];

function tituloDaRota(pathname: string): string {
  if (pathname === '/alertas') return 'Alertas';
  if (pathname.startsWith('/prevencao')) return 'Procedimentos & Planos';
  if (pathname.startsWith('/operacao')) return 'Operação';
  if (pathname.startsWith('/riscos')) return 'Riscos';
  if (pathname.startsWith('/pessoas')) return 'Pessoas';
  if (pathname.startsWith('/nao-conformidades')) return 'Não Conformidades';
  if (pathname.startsWith('/acidentes')) return 'Acidentes & Incidentes';
  if (pathname.startsWith('/requisitos-legais')) return 'Requisitos Legais';
  if (pathname.startsWith('/epi')) return 'EPI';
  const item = itensNavegacaoFlat.find((i) => i.rota === pathname);
  return item?.rotulo ?? 'AAHBRANT SST';
}

function ItemRail({ rota, rotulo, icone: Icone }: { rota: string; rotulo: string; icone: typeof Grid24Regular }) {
  const estilos = useStyles();
  return (
    <Tooltip content={rotulo} relationship="label" positioning="after">
      <NavLink
        to={rota}
        end={rota === '/'}
        aria-label={rotulo}
        className={({ isActive }) =>
          mergeClasses(estilos.navItem, !isActive && estilos.navItemHover, isActive && estilos.navItemActive)
        }
      >
        <Icone />
      </NavLink>
    </Tooltip>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const { carregando, dentroDoTeams } = useTeamsContext();
  const [alertasAbertos, setAlertasAbertos] = useState<number | null>(null);

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
    <div className={estilos.root}>
      <nav className={estilos.rail} aria-label="Navegação principal">
        <img src={logoSst} alt="AAHBRANT SST" className={estilos.marca} />
        {secoesNavegacao.map((secao, indice) => (
          <div key={secao.pilar ?? `sem-pilar-${indice}`} style={{ display: 'contents' }}>
            {indice > 0 && secao.pilar === null && <div className={estilos.navSeparador} />}
            {secao.itens.map((item) => (
              <ItemRail key={item.rota} {...item} />
            ))}
          </div>
        ))}
        <div className={estilos.railRodape}>
          <ItemRail {...itemAdministracao} />
        </div>
      </nav>

      <header className={estilos.header}>
        <Text className="brand-title" size={500} weight="semibold">
          {tituloDaRota(location.pathname)}
        </Text>
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
        </div>
      </header>

      <main className={estilos.content}>{children}</main>
    </div>
  );
}

export function CardGrid({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  return <div className={estilos.cardGrid}>{children}</div>;
}
