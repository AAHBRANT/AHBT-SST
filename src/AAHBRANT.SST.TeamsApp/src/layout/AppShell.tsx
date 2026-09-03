import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
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
  Person24Regular,
  WeatherSunny24Regular,
  WeatherMoon24Regular,
  Search24Regular,
} from '@fluentui/react-icons';
import { designTokens } from '../theme';
import { useThemeMode } from '../theme/ThemeModeContext';
import { useTeamsContext } from '../teams/useTeamsContext';
import { api, StatusAlerta } from '../lib/api';
import logoSst from '../assets/logo-sst.png';
import { SyncStatusBadge } from '../components/SyncStatusBadge';
import { ID_TOASTER_GLOBAL } from '../lib/toaster';
import { TrabalhadoresGaveta } from '../pages/pessoas/TrabalhadoresGaveta';

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
      color: designTokens.colorNeutralDark,
      backgroundColor: designTokens.colorNeutralLight,
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
    backgroundColor: designTokens.colorAdminButtonBackground,
    color: designTokens.colorAdminButtonInk,
    boxShadow: '0 4px 6px rgba(0,0,0,0.3)',
    ':hover': {
      backgroundColor: designTokens.colorAdminButtonBackgroundHover,
      color: designTokens.colorAdminButtonInk,
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
    backgroundColor: designTokens.colorSurface,
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '0 24px',
  },
  content: {
    gridRow: '2',
    gridColumn: '2',
    backgroundColor: designTokens.colorPageBackground,
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

// Item solto no topo, fora de qualquer módulo — Dashboard não pertence a nenhum pilar. Calendário
// saiu da sidebar (pedido do usuário, 01/09): virou redundante com o card de mini calendário no
// Dashboard, que já leva pra /calendario ao clicar — a rota continua existindo.
const itensAvulsos: Array<ItemNav & { icone: typeof Grid24Regular }> = [
  { rota: '/', rotulo: 'Dashboard', icone: Grid24Regular },
];

// Reformulação de navegação (pedido do usuário, 02/09, réplica de mockup dark-mode): a gaveta
// expansível de cada módulo (Gestão de SST/Operação/Pessoas/Ocorrências) virou um item único e
// direto — os itens que antes ficavam dentro da gaveta viraram abas da página de destino (ver
// GestaoSstPage/OperacaoPage/PessoasPillarPage/OcorrenciasPage), e as abas que essas páginas de
// destino já tinham viram sub-abas. A sidebar fica só com os 5 módulos (4 pilares + Administração,
// fixa no rodapé) e o Dashboard solto no topo.
const itensPilares: Array<ItemNav & { icone: typeof Grid24Regular }> = [
  { rota: '/gestao-sst', rotulo: 'Gestão de SST', icone: ShieldError24Regular },
  { rota: '/operacao', rotulo: 'Operação', icone: BuildingBank24Regular },
  { rota: '/pessoas', rotulo: 'Pessoas', icone: People24Regular },
  { rota: '/ocorrencias', rotulo: 'Ocorrências', icone: BriefcaseMedical24Regular },
];

// Administração fica fixa no rodapé do rail (mesmo padrão do mockup Hub Gênesis SST) — item único,
// não módulo: Obras/Controle de Acesso/Configurações/Trilha de Auditoria/Assinaturas são abas
// internas de AdministracaoPage em vez de destinos separados na sidebar (pedido do usuário, 01/09).
const itemAdministracao: ItemNav & { icone: typeof Grid24Regular } = {
  rota: '/administracao',
  rotulo: 'Administração',
  icone: Settings24Regular,
};

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

export function AppShell({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  const location = useLocation();
  const navigate = useNavigate();
  const { carregando, dentroDoTeams, contexto } = useTeamsContext();
  const { modo, alternarModo } = useThemeMode();
  const nomeUsuario = contexto?.user?.displayName ?? 'Usuário';
  const [alertasAbertos, setAlertasAbertos] = useState<number | null>(null);
  // Busca rápida de funcionários (pedido do usuário, 03/09) — reconecta TrabalhadoresGaveta, que
  // já existia pronta (busca por nome/função/obra, foto, status do ASO) mas tinha ficado sem
  // nenhum ponto de entrada na interface. Fica na topbar pra abrir de qualquer tela do app.
  const [gavetaFuncionariosAberta, setGavetaFuncionariosAberta] = useState(false);
  const [railExpandido, setRailExpandido] = useState<boolean>(
    () => localStorage.getItem(CHAVE_RAIL_EXPANDIDO) === '1',
  );

  useEffect(() => {
    localStorage.setItem(CHAVE_RAIL_EXPANDIDO, railExpandido ? '1' : '0');
  }, [railExpandido]);

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
        {itensPilares.map((item) => (
          <ItemRail key={item.rota} {...item} expandido={railExpandido} />
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
          <Button
            appearance="subtle"
            icon={<Search24Regular />}
            aria-label="Buscar funcionário"
            title="Buscar funcionário"
            onClick={() => setGavetaFuncionariosAberta(true)}
          />
          <Button
            appearance="subtle"
            icon={modo === 'dark' ? <WeatherSunny24Regular /> : <WeatherMoon24Regular />}
            aria-label={modo === 'dark' ? 'Mudar para modo claro' : 'Mudar para modo escuro'}
            title={modo === 'dark' ? 'Modo claro' : 'Modo escuro'}
            onClick={alternarModo}
          />
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
      <TrabalhadoresGaveta
        aberta={gavetaFuncionariosAberta}
        aoFechar={() => setGavetaFuncionariosAberta(false)}
      />
      <Toaster toasterId={ID_TOASTER_GLOBAL} />
    </div>
  );
}

export function CardGrid({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  return <div className={estilos.cardGrid}>{children}</div>;
}
