import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import {
  makeStyles,
  mergeClasses,
  Text,
  Badge,
  Button,
  Toast,
  ToastTitle,
  Toaster,
  Tooltip,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  Input,
  useToastController,
} from '@fluentui/react-components';
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
  WeatherSunny24Regular,
  WeatherMoon24Regular,
  ChevronDown16Regular,
  Checkmark20Regular,
  Fingerprint24Regular,
  PersonAvailable24Regular,
  Camera20Regular,
} from '@fluentui/react-icons';
import { designTokens } from '@ui';
import { useThemeMode } from '../theme/ThemeModeContext';
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
const VERSAO_APP = '5.10.0';
const TAMANHO_MAXIMO_FOTO_PERFIL_BYTES = 8 * 1024 * 1024;
const DIMENSAO_MAXIMA_FOTO_PERFIL = 256;

// Fotos de celular (vários MB, resolução alta) não cabem cruas no localStorage (quota ~5-10MB por
// origem) e travavam o perfil quando o setItem estourava a quota. Redimensiona para um avatar
// pequeno e recomprime em JPEG antes de persistir.
function redimensionarFotoPerfil(dataUrlOriginal: string, dimensaoMaxima: number): Promise<string> {
  return new Promise((resolve, reject) => {
    const imagem = new Image();
    imagem.onload = () => {
      const escala = Math.min(1, dimensaoMaxima / Math.max(imagem.width, imagem.height));
      const largura = Math.max(1, Math.round(imagem.width * escala));
      const altura = Math.max(1, Math.round(imagem.height * escala));
      const canvas = document.createElement('canvas');
      canvas.width = largura;
      canvas.height = altura;
      const contexto = canvas.getContext('2d');
      if (!contexto) {
        reject(new Error('Não foi possível processar a imagem.'));
        return;
      }
      contexto.drawImage(imagem, 0, 0, largura, altura);
      resolve(canvas.toDataURL('image/jpeg', 0.82));
    };
    imagem.onerror = () => reject(new Error('Arquivo de imagem inválido.'));
    imagem.src = dataUrlOriginal;
  });
}

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
    padding: '16px 0',
    gap: '4px',
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
    borderRadius: '6px',
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
    borderRadius: '6px',
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
      backgroundColor: 'rgba(255, 255, 255, 0.08)',
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
    borderRadius: '6px',
    backgroundColor: designTokens.colorAdminButtonBackground,
    color: designTokens.colorAdminButtonInk,
    boxShadow: 'none',
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
    borderRadius: '6px',
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
    maxWidth: '180px',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
  },
  usuarioResumo: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-end',
    gap: '1px',
    minWidth: 0,
  },
  usuarioEmailTopo: {
    fontSize: '11px',
    lineHeight: '14px',
    color: designTokens.colorNeutralMedium,
    maxWidth: '180px',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
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
  usuarioAvatarMenu: {
    width: '74px',
    height: '74px',
    borderRadius: '50%',
    backgroundColor: '#7d87d4',
    border: 'none',
    color: '#ffffff',
    fontSize: '24px',
    fontWeight: 700,
  },
  usuarioAvatarImagem: {
    width: '100%',
    height: '100%',
    objectFit: 'cover',
    borderRadius: '50%',
  },
  fotoPerfilLinha: {
    display: 'flex',
    alignItems: 'center',
    gap: '14px',
    padding: '12px',
    marginBottom: '16px',
    backgroundColor: designTokens.colorNeutralLight,
    borderRadius: '6px',
  },
  fotoPerfilGrande: {
    width: '64px',
    height: '64px',
    borderRadius: '50%',
    flexShrink: 0,
    backgroundColor: '#7d87d4',
    color: '#ffffff',
    display: 'grid',
    placeItems: 'center',
    fontSize: '22px',
    fontWeight: 800,
    overflow: 'hidden',
  },
  fotoPerfilTexto: {
    display: 'grid',
    gap: '4px',
    minWidth: 0,
  },
  fotoPerfilAjuda: {
    color: designTokens.colorNeutralMedium,
    fontSize: '12px',
    lineHeight: '16px',
  },
  usuarioArea: {
    position: 'relative',
  },
  menuPerfil: {
    position: 'absolute',
    top: '42px',
    right: 0,
    width: '260px',
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '6px',
    boxShadow: '0 14px 30px rgba(0, 0, 0, 0.20)',
    zIndex: 20,
    overflow: 'hidden',
  },
  menuPerfilTopo: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    padding: '18px 16px 14px',
    gap: '6px',
    textAlign: 'center',
  },
  menuNome: {
    fontSize: '15px',
    lineHeight: '20px',
    fontWeight: 700,
    color: designTokens.colorNeutralDark,
    maxWidth: '100%',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  menuEmail: {
    fontSize: '12px',
    lineHeight: '16px',
    color: designTokens.colorNeutralMedium,
    maxWidth: '100%',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  menuPerfilTipo: {
    fontSize: '12px',
    lineHeight: '16px',
    color: designTokens.colorNeutralMedium,
  },
  menuVersao: {
    marginTop: '4px',
    fontSize: '10px',
    lineHeight: '14px',
    color: designTokens.colorNeutralMedium,
  },
  menuItem: {
    width: '100%',
    height: '44px',
    border: 'none',
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: 'transparent',
    color: designTokens.colorNeutralDark,
    font: 'inherit',
    cursor: 'pointer',
    textAlign: 'center',
    ':hover': {
      backgroundColor: designTokens.colorNeutralLight,
    },
  },
  menuItemSair: {
    color: '#d70000',
  },
  header: {
    gridRow: '1',
    gridColumn: '2',
    backgroundColor: designTokens.colorSurface,
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '0 20px',
  },
  content: {
    gridRow: '2',
    gridColumn: '2',
    backgroundColor: designTokens.colorPageBackground,
    overflowY: 'auto',
    padding: '20px',
  },
  cardGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: '16px',
  },
  perfilSurface: {
    width: 'min(1080px, calc(100vw - 48px))',
    maxWidth: '1080px',
  },
  perfilGrid: {
    display: 'grid',
    gridTemplateColumns: 'minmax(360px, 1fr) minmax(320px, 1fr)',
    gap: '20px',
    marginTop: '16px',
  },
  perfilCard: {
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '6px',
    overflow: 'hidden',
  },
  perfilCardTitulo: {
    padding: '14px 16px',
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
    color: designTokens.colorPrimary,
    fontWeight: 800,
  },
  perfilCardCorpo: {
    padding: '16px',
  },
  perfilCampos: {
    display: 'grid',
    gap: '14px',
  },
  perfilRotulo: {
    display: 'grid',
    gap: '6px',
    fontSize: '12px',
    fontWeight: 700,
    color: designTokens.colorNeutralDark,
  },
  obrigatorio: {
    color: designTokens.colorPrimary,
  },
  biometriaTexto: {
    color: designTokens.colorNeutralMedium,
    marginBottom: '14px',
  },
  biometriaGrade: {
    display: 'grid',
    gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    gap: '12px',
  },
  biometriaMetodo: {
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: '6px',
    padding: '12px',
    display: 'grid',
    gap: '8px',
    backgroundColor: designTokens.colorNeutralLight,
  },
  biometriaMetodoTopo: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    fontWeight: 800,
    color: designTokens.colorNeutralDark,
  },
  biometriaStatus: {
    fontSize: '12px',
    lineHeight: '16px',
    color: designTokens.colorNeutralMedium,
  },
  empresasLista: {
    margin: 0,
    padding: 0,
    listStyle: 'none',
    display: 'grid',
    gap: '8px',
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
  const emailUsuario = contexto?.user?.userPrincipalName ?? contexto?.user?.loginHint ?? '';
  const identidadeUsuario = emailUsuario || nomeUsuario;
  const chaveFotoUsuario = `sst.fotoPerfil.${identidadeUsuario}`;
  const chaveNomeUsuario = `sst.nomePerfil.${identidadeUsuario}`;
  const [fotoPerfil, setFotoPerfil] = useState<string | null>(() => localStorage.getItem(chaveFotoUsuario));
  const [nomePerfil, setNomePerfil] = useState(() => localStorage.getItem(chaveNomeUsuario) ?? nomeUsuario);
  const [menuPerfilAberto, setMenuPerfilAberto] = useState(false);
  const [perfilAberto, setPerfilAberto] = useState(false);
  const [alertasAbertos, setAlertasAbertos] = useState<number | null>(null);
  const [railExpandido, setRailExpandido] = useState<boolean>(
    () => localStorage.getItem(CHAVE_RAIL_EXPANDIDO) === '1',
  );

  useEffect(() => {
    localStorage.setItem(CHAVE_RAIL_EXPANDIDO, railExpandido ? '1' : '0');
  }, [railExpandido]);

  useEffect(() => {
    setFotoPerfil(localStorage.getItem(chaveFotoUsuario));
    setNomePerfil(localStorage.getItem(chaveNomeUsuario) ?? nomeUsuario);
  }, [chaveFotoUsuario, chaveNomeUsuario, nomeUsuario]);

  const iniciaisPerfil = obterIniciais(nomePerfil);
  const { dispatchToast } = useToastController(ID_TOASTER_GLOBAL);

  function avisarErroFoto(mensagem: string) {
    dispatchToast(
      <Toast>
        <ToastTitle>{mensagem}</ToastTitle>
      </Toast>,
      { intent: 'error', timeout: 5000 },
    );
  }

  function selecionarFotoPerfil(evento: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = evento.target.files?.[0];
    evento.target.value = '';
    if (!arquivo || !arquivo.type.startsWith('image/')) return;
    if (arquivo.size > TAMANHO_MAXIMO_FOTO_PERFIL_BYTES) {
      avisarErroFoto('Imagem muito grande (máximo 8 MB). Escolha outra foto.');
      return;
    }
    const leitor = new FileReader();
    leitor.onload = () => {
      const fotoOriginal = typeof leitor.result === 'string' ? leitor.result : null;
      if (!fotoOriginal) return;
      redimensionarFotoPerfil(fotoOriginal, DIMENSAO_MAXIMA_FOTO_PERFIL)
        .then((foto) => {
          try {
            localStorage.setItem(chaveFotoUsuario, foto);
            setFotoPerfil(foto);
          } catch {
            avisarErroFoto('Não foi possível salvar a foto neste dispositivo (armazenamento cheio).');
          }
        })
        .catch(() => avisarErroFoto('Não foi possível processar essa imagem.'));
    };
    leitor.onerror = () => avisarErroFoto('Não foi possível ler o arquivo selecionado.');
    leitor.readAsDataURL(arquivo);
  }

  function abrirMeuPerfil() {
    setMenuPerfilAberto(false);
    setPerfilAberto(true);
  }

  function salvarMeuPerfil() {
    localStorage.setItem(chaveNomeUsuario, nomePerfil.trim() || nomeUsuario);
    setNomePerfil(nomePerfil.trim() || nomeUsuario);
    setPerfilAberto(false);
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
          <div className={estilos.usuarioArea}>
            <button
              className={estilos.usuarioChip}
              title="Abrir menu do usuário"
              aria-label={`Abrir menu do usuário de ${nomePerfil}`}
              aria-expanded={menuPerfilAberto}
              onClick={() => setMenuPerfilAberto((aberto) => !aberto)}
            >
              <div className={estilos.usuarioResumo}>
                <Text className={estilos.usuarioNome}>{nomePerfil}</Text>
                {emailUsuario && <span className={estilos.usuarioEmailTopo}>{emailUsuario}</span>}
              </div>
              <div className={estilos.usuarioAvatar}>
                {fotoPerfil ? (
                  <img src={fotoPerfil} alt="" className={estilos.usuarioAvatarImagem} />
                ) : (
                  <span>{iniciaisPerfil}</span>
                )}
              </div>
              <ChevronDown16Regular />
            </button>
            {menuPerfilAberto && (
              <div className={estilos.menuPerfil} role="menu">
                <div className={estilos.menuPerfilTopo}>
                  <div className={mergeClasses(estilos.usuarioAvatar, estilos.usuarioAvatarMenu)}>
                    {fotoPerfil ? (
                      <img src={fotoPerfil} alt="" className={estilos.usuarioAvatarImagem} />
                    ) : (
                      <span>{iniciaisPerfil}</span>
                    )}
                  </div>
                  <div className={estilos.menuNome}>{nomePerfil}</div>
                  <div className={estilos.menuEmail}>{emailUsuario || 'Conta Microsoft'}</div>
                  <div className={estilos.menuPerfilTipo}>Personalizado</div>
                  <div className={estilos.menuVersao}>VERSÃO: {VERSAO_APP}</div>
                </div>
                <button className={estilos.menuItem} type="button" role="menuitem" onClick={abrirMeuPerfil}>
                  Meu perfil
                </button>
                <button className={estilos.menuItem} type="button" role="menuitem" onClick={() => setMenuPerfilAberto(false)}>
                  Atualizações
                </button>
                <button className={estilos.menuItem} type="button" role="menuitem" onClick={() => setMenuPerfilAberto(false)}>
                  Suporte técnico
                </button>
                <button className={estilos.menuItem} type="button" role="menuitem" onClick={() => setMenuPerfilAberto(false)}>
                  Termos de uso
                </button>
                <button className={mergeClasses(estilos.menuItem, estilos.menuItemSair)} type="button" role="menuitem">
                  Sair do sistema
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      <Dialog open={perfilAberto} onOpenChange={(_, data) => setPerfilAberto(data.open)}>
        <DialogSurface className={estilos.perfilSurface}>
          <DialogBody>
            <DialogTitle>Meu perfil</DialogTitle>
            <div className={estilos.perfilGrid}>
              <section className={estilos.perfilCard}>
                <div className={estilos.perfilCardTitulo}>Informações do usuário</div>
                <div className={estilos.perfilCardCorpo}>
                  <div className={estilos.fotoPerfilLinha}>
                    <div className={estilos.fotoPerfilGrande}>
                      {fotoPerfil ? (
                        <img src={fotoPerfil} alt="Foto do perfil" className={estilos.usuarioAvatarImagem} />
                      ) : (
                        <span>{iniciaisPerfil}</span>
                      )}
                    </div>
                    <div className={estilos.fotoPerfilTexto}>
                      <Text weight="semibold">Foto do perfil</Text>
                      <span className={estilos.fotoPerfilAjuda}>Usada no menu do usuário deste navegador.</span>
                      <Button
                        appearance="secondary"
                        icon={<Camera20Regular />}
                        onClick={() => document.getElementById('upload-foto-perfil')?.click()}
                      >
                        Alterar foto
                      </Button>
                    </div>
                  </div>
                  <div className={estilos.perfilCampos}>
                    <label className={estilos.perfilRotulo}>
                      <span>Nome <span className={estilos.obrigatorio}>*</span></span>
                      <Input value={nomePerfil} onChange={(_, data) => setNomePerfil(data.value)} />
                    </label>
                    <label className={estilos.perfilRotulo}>
                      <span>E-mail de acesso <span className={estilos.obrigatorio}>*</span></span>
                      <Input value={emailUsuario || 'Conta Microsoft'} disabled />
                    </label>
                  </div>
                  <input
                    id="upload-foto-perfil"
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    hidden
                    onChange={selecionarFotoPerfil}
                  />
                </div>
              </section>

              <div style={{ display: 'grid', gap: 20, alignContent: 'start' }}>
                <section className={estilos.perfilCard}>
                  <div className={estilos.perfilCardTitulo}>Minha assinatura biométrica</div>
                  <div className={estilos.perfilCardCorpo}>
                    <Text size={200} className={estilos.biometriaTexto}>
                      Cada usuário assina documentos com a própria biometria cadastrada: reconhecimento facial
                      ou digital do polegar.
                    </Text>
                    <div className={estilos.biometriaGrade}>
                      <div className={estilos.biometriaMetodo}>
                        <div className={estilos.biometriaMetodoTopo}>
                          <PersonAvailable24Regular />
                          <span>Facial</span>
                        </div>
                        <div className={estilos.biometriaStatus}>Vinculada ao usuário logado.</div>
                        <Button appearance="secondary" disabled>Gerenciar face</Button>
                      </div>
                      <div className={estilos.biometriaMetodo}>
                        <div className={estilos.biometriaMetodoTopo}>
                          <Fingerprint24Regular />
                          <span>Polegar</span>
                        </div>
                        <div className={estilos.biometriaStatus}>Vinculada ao usuário logado.</div>
                        <Button appearance="secondary" disabled>Gerenciar digital</Button>
                      </div>
                    </div>
                  </div>
                </section>

                <section className={estilos.perfilCard}>
                  <div className={estilos.perfilCardTitulo}>Obras que tenho acesso</div>
                  <div className={estilos.perfilCardCorpo}>
                    <ul className={estilos.empresasLista}>
                      <li>Ponte Rio Cuiá</li>
                      <li>Obra principal SST</li>
                    </ul>
                  </div>
                </section>
              </div>
            </div>
          </DialogBody>
          <DialogActions>
            <Button appearance="secondary" onClick={() => setPerfilAberto(false)}>Cancelar</Button>
            <Button appearance="primary" icon={<Checkmark20Regular />} onClick={salvarMeuPerfil}>Salvar</Button>
          </DialogActions>
        </DialogSurface>
      </Dialog>

      <main className={estilos.content}>{children}</main>
      <Toaster toasterId={ID_TOASTER_GLOBAL} />
    </div>
  );
}

export function CardGrid({ children }: { children: ReactNode }) {
  const estilos = useStyles();
  return <div className={estilos.cardGrid}>{children}</div>;
}

function obterIniciais(nome: string) {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return 'US';
  const primeira = partes[0]?.[0] ?? '';
  const segunda = partes.length > 1 ? partes[partes.length - 1]?.[0] ?? '' : partes[0]?.[1] ?? '';
  return `${primeira}${segunda}`.toUpperCase();
}
