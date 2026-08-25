import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { makeStyles, mergeClasses, tokens, Text, Badge, Button } from '@fluentui/react-components';
import {
  Grid24Regular,
  ShieldError24Regular,
  BuildingBank24Regular,
  Warning24Regular,
  Settings24Regular,
  Alert24Regular,
  Gavel24Regular,
  ChevronLeft24Regular,
  ChevronRight24Regular,
} from '@fluentui/react-icons';
import { designTokens } from '../theme';
import { useTeamsContext } from '../teams/useTeamsContext';
import { api, StatusAlerta } from '../lib/api';
import logoSst from '../assets/logo-sst.png';

const useStyles = makeStyles({
  root: {
    display: 'grid',
    gridTemplateRows: '64px 1fr',
    height: '100vh',
    width: '100%',
    transition: 'grid-template-columns 0.15s ease',
  },
  rootExpandido: {
    gridTemplateColumns: '240px 1fr',
  },
  rootColapsado: {
    gridTemplateColumns: '64px 1fr',
  },
  sidebar: {
    gridRow: '1 / span 2',
    gridColumn: '1',
    backgroundColor: designTokens.colorNeutralDark,
    color: designTokens.colorWhite,
    display: 'flex',
    flexDirection: 'column',
    padding: '20px 12px',
    gap: '4px',
    overflowX: 'hidden',
  },
  sidebarColapsada: {
    padding: '20px 8px',
    alignItems: 'center',
  },
  sidebarFlutuanteMobile: {
    position: 'fixed',
    top: 0,
    left: 0,
    height: '100vh',
    zIndex: 1000,
    transition: 'width 0.2s ease',
  },
  sidebarFlutuanteRecolhida: {
    width: '64px',
  },
  sidebarFlutuanteAberta: {
    width: '240px',
    boxShadow: '2px 0 16px rgba(0,0,0,0.35)',
  },
  panoDeFundo: {
    position: 'fixed',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0,0,0,0.4)',
    zIndex: 999,
  },
  brand: {
    padding: '0 8px 24px 8px',
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: '8px',
  },
  brandColapsada: {
    flexDirection: 'column',
    alignItems: 'center',
    padding: '0 0 24px 0',
  },
  botaoRecolher: {
    color: 'rgba(255,255,255,0.82)',
    minWidth: 'auto',
    flexShrink: 0,
  },
  navItem: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    padding: '10px 12px',
    borderRadius: '6px',
    color: 'rgba(255,255,255,0.82)',
    textDecoration: 'none',
    fontSize: '14px',
    fontWeight: 500,
    whiteSpace: 'nowrap',
  },
  navItemColapsado: {
    justifyContent: 'center',
    padding: '10px',
    width: '40px',
  },
  navItemActive: {
    backgroundColor: designTokens.colorPrimary,
    color: designTokens.colorWhite,
  },
  navSecaoTitulo: {
    color: 'rgba(255,255,255,0.45)',
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
    fontSize: '11px',
    fontWeight: 600,
    padding: '14px 12px 4px 12px',
  },
  navSeparador: {
    borderTop: '1px solid rgba(255,255,255,0.12)',
    margin: '8px 12px',
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
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
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

// Navegação consolidada em 5-6 módulos principais (pedido do usuário em 24/08): Dashboard +
// os 4 pilares (cada um agora é 1 item de sidebar, com abas internas — ver PillarLayout) +
// Administração. Os antigos 12+ itens soltos (Riscos, PGR, PT, Pessoas etc.) viraram abas
// dentro do módulo-pilar correspondente em vez de entradas próprias na sidebar.
const secoesNavegacao: Array<{ pilar: string | null; itens: Array<{ rota: string; rotulo: string; icone: typeof Grid24Regular }> }> = [
  { pilar: null, itens: [{ rota: '/', rotulo: 'Dashboard', icone: Grid24Regular }] },
  { pilar: null, itens: [{ rota: '/conformidade', rotulo: 'Conformidade', icone: Gavel24Regular }] },
  { pilar: null, itens: [{ rota: '/prevencao', rotulo: 'Prevenção', icone: ShieldError24Regular }] },
  { pilar: null, itens: [{ rota: '/operacao', rotulo: 'Operação', icone: BuildingBank24Regular }] },
  { pilar: null, itens: [{ rota: '/melhoria', rotulo: 'Melhoria Contínua', icone: Warning24Regular }] },
  { pilar: null, itens: [{ rota: '/administracao', rotulo: 'Administração', icone: Settings24Regular }] },
];

const itensNavegacaoFlat = secoesNavegacao.flatMap((secao) => secao.itens);

function tituloDaRota(pathname: string): string {
  if (pathname === '/alertas') return 'Alertas';
  if (pathname.startsWith('/conformidade')) return 'Conformidade';
  if (pathname.startsWith('/prevencao')) return 'Prevenção';
  if (pathname.startsWith('/operacao')) return 'Operação';
  if (pathname.startsWith('/melhoria')) return 'Melhoria Contínua';
  const item = itensNavegacaoFlat.find((i) => i.rota === pathname);
  return item?.rotulo ?? 'AAHBRANT SST';
}

const CHAVE_SIDEBAR_COLAPSADA = 'sst.sidebarColapsada';
const LARGURA_MOBILE = 768;

export function AppShell({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const { carregando, dentroDoTeams } = useTeamsContext();
  const [alertasAbertos, setAlertasAbertos] = useState<number | null>(null);
  const [ehMobile, setEhMobile] = useState<boolean>(() => window.innerWidth < LARGURA_MOBILE);
  // Desktop: preferência persistida que empurra o layout (240px <-> 64px).
  const [sidebarColapsada, setSidebarColapsada] = useState<boolean>(
    () => localStorage.getItem(CHAVE_SIDEBAR_COLAPSADA) === '1',
  );
  // Mobile: a sidebar fica sempre como faixa fina fixa; abrir "flutua" por cima do
  // conteúdo (com pano de fundo) em vez de empurrar o layout, sem alterar a largura reservada.
  const [overlayAberto, setOverlayAberto] = useState(false);

  useEffect(() => {
    const aoRedimensionar = () => setEhMobile(window.innerWidth < LARGURA_MOBILE);
    window.addEventListener('resize', aoRedimensionar);
    return () => window.removeEventListener('resize', aoRedimensionar);
  }, []);

  useEffect(() => {
    localStorage.setItem(CHAVE_SIDEBAR_COLAPSADA, sidebarColapsada ? '1' : '0');
  }, [sidebarColapsada]);

  const sidebarExpandidaVisualmente = ehMobile ? overlayAberto : !sidebarColapsada;

  function alternarSidebar() {
    if (ehMobile) {
      setOverlayAberto((atual) => !atual);
    } else {
      setSidebarColapsada((atual) => !atual);
    }
  }

  function aoNavegarPeloMenu() {
    if (ehMobile) setOverlayAberto(false);
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

  const sidebarRecolhidaVisualmente = !sidebarExpandidaVisualmente;
  const larguraReservada = ehMobile || sidebarColapsada ? estilos.rootColapsado : estilos.rootExpandido;

  return (
    <div className={mergeClasses(estilos.root, larguraReservada)}>
      {ehMobile && overlayAberto && <div className={estilos.panoDeFundo} onClick={() => setOverlayAberto(false)} />}
      <aside
        className={mergeClasses(
          estilos.sidebar,
          sidebarRecolhidaVisualmente && estilos.sidebarColapsada,
          ehMobile && estilos.sidebarFlutuanteMobile,
          ehMobile && (overlayAberto ? estilos.sidebarFlutuanteAberta : estilos.sidebarFlutuanteRecolhida),
        )}
      >
        <div className={mergeClasses(estilos.brand, sidebarRecolhidaVisualmente && estilos.brandColapsada)}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <img
              src={logoSst}
              alt="AAHBRANT SST"
              style={{ width: '32px', height: '32px', borderRadius: '6px', flexShrink: 0 }}
            />
            {sidebarExpandidaVisualmente && (
              <Text size={200} style={{ color: 'rgba(255,255,255,0.6)' }}>
                Segurança e Saúde no Trabalho
              </Text>
            )}
          </div>
          <Button
            appearance="subtle"
            className={estilos.botaoRecolher}
            icon={sidebarRecolhidaVisualmente ? <ChevronRight24Regular /> : <ChevronLeft24Regular />}
            aria-label={sidebarRecolhidaVisualmente ? 'Mostrar menu' : 'Esconder menu'}
            title={sidebarRecolhidaVisualmente ? 'Mostrar menu' : 'Esconder menu'}
            onClick={alternarSidebar}
          />
        </div>
        {secoesNavegacao.map((secao, indice) => (
          <div key={secao.pilar ?? `sem-pilar-${indice}`}>
            {indice > 0 && secao.pilar === null && <div className={estilos.navSeparador} />}
            {secao.pilar && sidebarExpandidaVisualmente && <div className={estilos.navSecaoTitulo}>{secao.pilar}</div>}
            {secao.itens.map(({ rota, rotulo, icone: Icone }) => (
              <NavLink
                key={rota}
                to={rota}
                end={rota === '/'}
                title={sidebarRecolhidaVisualmente ? rotulo : undefined}
                onClick={aoNavegarPeloMenu}
                className={({ isActive }) =>
                  mergeClasses(
                    estilos.navItem,
                    sidebarRecolhidaVisualmente && estilos.navItemColapsado,
                    isActive && estilos.navItemActive,
                  )
                }
              >
                <Icone />
                {sidebarExpandidaVisualmente && rotulo}
              </NavLink>
            ))}
          </div>
        ))}
      </aside>

      <header className={estilos.header}>
        <Text className="brand-title" size={500} weight="semibold">
          {tituloDaRota(location.pathname)}
        </Text>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
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
