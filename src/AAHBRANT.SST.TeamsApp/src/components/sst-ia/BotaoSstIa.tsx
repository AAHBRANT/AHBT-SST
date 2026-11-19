import { useCallback, useEffect, useRef, useState, type PointerEvent as EventoPonteiro } from 'react';
import { useLocation } from 'react-router-dom';
// makeStyles ainda não tem reexport em @ui (mesma exceção pontual da SuporteIaTab).
// oxlint-disable-next-line no-restricted-imports
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { api, type SuporteIaSolicitacao } from '../../lib/api';
import { useEstilosEsmeralda } from '@ui';
import { IconeSstIa } from './IconeSstIa';
import { PainelSstIa, type AbaSstIa } from './PainelSstIa';
import { STATUS_COM_NOVIDADE } from './statusSstIa';
import { useConversaSstIa, type MensagemSstIa } from './useConversaSstIa';
import { useLeituraVoz } from './useLeituraVoz';

// Botão flutuante do SST IA (comportamento aprovado no mockup interativo de 18/11/2026,
// docs/mockups/sst-ia-comportamento-2026-11-18.html):
//  - entra como pílula "SST IA" e vira círculo após 4 s; o nome volta ao passar o mouse;
//  - ao rolar a tela fica menor e translúcido;
//  - o AppShell reserva 88 px no fim de toda tela e telas com barra fixa sobem o botão;
//  - some com janela modal, tela cheia, teclado aberto no celular e menu lateral aberto no celular;
//  - "Esconder por hoje" e o lado (direita/esquerda) ficam guardados neste aparelho;
//  - nunca abre sozinho; o contador vinho mostra chamados que pedem ação de quem abriu.

const CHAVE_ESCONDIDO = 'sst.sstIa.escondidoEm';
const CHAVE_LADO = 'sst.sstIa.lado';
const CHAVE_VOZ = 'sst.sstIa.voz';
const VINHO = '#670000';

const useStyles = makeStyles({
  // Posição fica no invólucro: o vidro tem overflow hidden e cortaria o contador.
  involucro: {
    position: 'fixed',
    right: '20px',
    bottom: 'calc(20px + var(--sst-ia-rodape, 0px))',
    zIndex: 20,
    transitionProperty: 'bottom, opacity, transform',
    transitionDuration: '250ms',
    '@media (max-width: 680px)': { right: '16px', bottom: 'calc(16px + var(--sst-ia-rodape, 0px))' },
    '@media (prefers-reduced-motion: reduce)': { transitionDuration: '0ms' },
  },
  botao: {
    height: '48px',
    minWidth: '48px',
    padding: '0 12px',
    borderRadius: '24px',
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    cursor: 'pointer',
    fontFamily: 'inherit',
    fontSize: '14px',
    fontWeight: 600,
    textShadow: '0 1px 2px rgba(6,78,59,0.35)',
    touchAction: 'none',
    userSelect: 'none',
    transitionProperty: 'padding',
    transitionDuration: '250ms',
    ':focus-visible': { outline: '3px solid #ebe9ad', outlineOffset: '2px' },
    '@media (prefers-reduced-motion: reduce)': { transitionDuration: '0ms' },
  },
  esquerda: { right: 'auto', left: '76px', '@media (max-width: 680px)': { left: '16px', right: 'auto' } },
  aberto: { paddingLeft: '13px', paddingRight: '16px' },
  // Só o capacete: círculo perfeito de 48 px (sem o espaço do nome escondido).
  circulo: { width: '48px', paddingLeft: 0, paddingRight: 0, justifyContent: 'center', gap: 0 },
  rolando: { transform: 'scale(0.86)', opacity: 0.55 },
  oculto: { opacity: 0, transform: 'scale(0.6)', pointerEvents: 'none', visibility: 'hidden' },
  icone: { fontSize: '24px', flexShrink: 0, color: '#ffffff' },
  nome: {
    maxWidth: 0,
    overflow: 'hidden',
    whiteSpace: 'nowrap',
    transitionProperty: 'max-width',
    transitionDuration: '300ms',
  },
  nomeVisivel: { maxWidth: '100px' },
  contador: {
    position: 'absolute',
    top: '-3px',
    right: '-3px',
    minWidth: '18px',
    padding: '1px 6px',
    borderRadius: '10px',
    backgroundColor: VINHO,
    color: '#ffffff',
    fontSize: '10.5px',
    fontWeight: 700,
    textShadow: 'none',
    border: '1.5px solid #ffffff',
    pointerEvents: 'none',
    lineHeight: '14px',
    textAlign: 'center',
  },
});

const hoje = () => new Date().toLocaleDateString('sv-SE');

function ler(chave: string): string | null {
  try {
    return localStorage.getItem(chave);
  } catch {
    return null;
  }
}
function gravar(chave: string, valor: string) {
  try {
    localStorage.setItem(chave, valor);
  } catch {
    /* modo privado: vale só nesta sessão */
  }
}

/** Janela modal aberta (Fluent Dialog, confirmações, pendências) ou tela cheia (câmera, assinatura). */
function haJanelaNaFrente(): boolean {
  if (document.fullscreenElement) return true;
  return !!document.querySelector(
    '[role="dialog"][aria-modal="true"]:not([data-sst-ia-painel]), [role="alertdialog"], [data-sst-tela-cheia]',
  );
}

export function BotaoSstIa({ menuMobileAberto }: { menuMobileAberto: boolean }) {
  const estilos = useStyles();
  const esm = useEstilosEsmeralda();
  const location = useLocation();

  const [painelAberto, setPainelAberto] = useState(false);
  const [aba, setAba] = useState<AbaSstIa>('perguntar');
  const [pilula, setPilula] = useState(true);
  const [sobMouse, setSobMouse] = useState(false);
  const [rolando, setRolando] = useState(false);
  const [janela, setJanela] = useState(false);
  const [teclado, setTeclado] = useState(false);
  const [escondidoHoje, setEscondidoHoje] = useState(() => ler(CHAVE_ESCONDIDO) === hoje());
  const [esquerda, setEsquerda] = useState(() => ler(CHAVE_LADO) === 'esquerda');
  const [vozLigada, setVozLigadaEstado] = useState(() => ler(CHAVE_VOZ) !== 'desligada');
  const [chamados, setChamados] = useState<SuporteIaSolicitacao[] | null>(null);

  const leitura = useLeituraVoz();
  const vozRef = useRef(vozLigada);
  useEffect(() => {
    vozRef.current = vozLigada;
  }, [vozLigada]);
  const aoResponder = useCallback(
    (m: MensagemSstIa, falada: boolean) => {
      // Pergunta feita falando → resposta lida em voz alta (se a voz estiver ligada).
      if (falada && vozRef.current) leitura.ler(m.id, m.texto);
    },
    [leitura],
  );
  const conversa = useConversaSstIa(aoResponder);

  const setVozLigada = (v: boolean) => {
    setVozLigadaEstado(v);
    gravar(CHAVE_VOZ, v ? 'ligada' : 'desligada');
  };

  // Pílula com o nome por 4 s a cada troca de tela.
  useEffect(() => {
    setPilula(true);
    const t = window.setTimeout(() => setPilula(false), 4000);
    return () => window.clearTimeout(t);
  }, [location.pathname]);

  // Rolagem de qualquer área (o conteúdo rola no <main> do AppShell, não na janela).
  useEffect(() => {
    let t: number | undefined;
    const aoRolar = (e: Event) => {
      if (e.target instanceof Element && e.target.closest('[data-sst-ia-painel]')) return;
      setRolando(true);
      window.clearTimeout(t);
      t = window.setTimeout(() => setRolando(false), 600);
    };
    document.addEventListener('scroll', aoRolar, { capture: true, passive: true });
    return () => {
      document.removeEventListener('scroll', aoRolar, { capture: true });
      window.clearTimeout(t);
    };
  }, []);

  // Janelas modais e tela cheia: observa o DOM (os diálogos do Fluent abrem em portal no body).
  useEffect(() => {
    const avaliar = () => setJanela(haJanelaNaFrente());
    avaliar();
    const obs = new MutationObserver(avaliar);
    obs.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['role', 'aria-modal', 'data-sst-tela-cheia'] });
    document.addEventListener('fullscreenchange', avaliar);
    return () => {
      obs.disconnect();
      document.removeEventListener('fullscreenchange', avaliar);
    };
  }, []);

  // Teclado virtual no celular: a área visível encolhe bastante.
  useEffect(() => {
    const vv = window.visualViewport;
    if (!vv) return;
    const avaliar = () => setTeclado(window.innerWidth <= 680 && vv.height < window.innerHeight * 0.75);
    vv.addEventListener('resize', avaliar);
    return () => vv.removeEventListener('resize', avaliar);
  }, []);

  const carregarChamados = useCallback(async () => {
    try {
      setChamados(await api.suporteIa.listar());
    } catch {
      setChamados([]);
    }
  }, []);

  // Contador: busca ao abrir o app e a cada 5 min.
  useEffect(() => {
    void carregarChamados();
    const t = window.setInterval(() => void carregarChamados(), 5 * 60_000);
    return () => window.clearInterval(t);
  }, [carregarChamados]);

  const novidades = (chamados ?? []).filter((c) => c.souSolicitante !== false && STATUS_COM_NOVIDADE.includes(c.status)).length;

  // Clique fora fecha o painel (Esc fecha pelo próprio painel).
  useEffect(() => {
    if (!painelAberto) return;
    const aoClicar = (e: MouseEvent) => {
      const alvo = e.target as Element | null;
      if (!alvo || alvo.closest('[data-sst-ia-painel], [data-sst-ia-botao]')) return;
      // Menus suspensos do Fluent (Select nativo não gera evento aqui) abrem em portal: não fechar.
      if (alvo.closest('[role="listbox"], [role="menu"], [role="dialog"]')) return;
      setPainelAberto(false);
    };
    document.addEventListener('mousedown', aoClicar);
    return () => document.removeEventListener('mousedown', aoClicar);
  }, [painelAberto]);

  // Arrastar para o outro lado da tela (sem arrastar = clique abre o painel).
  const arraste = useRef<{ x: number; moveu: boolean } | null>(null);
  // O click chega depois do pointerup: guarda se aquele gesto foi arraste para não abrir o painel.
  const foiArraste = useRef(false);
  const aoApertar = (e: EventoPonteiro<HTMLButtonElement>) => {
    arraste.current = { x: e.clientX, moveu: false };
    e.currentTarget.setPointerCapture(e.pointerId);
  };
  const aoMover = (e: EventoPonteiro<HTMLButtonElement>) => {
    if (arraste.current && Math.abs(e.clientX - arraste.current.x) > 30) arraste.current.moveu = true;
  };
  const aoSoltar = (e: EventoPonteiro<HTMLButtonElement>) => {
    const a = arraste.current;
    arraste.current = null;
    foiArraste.current = !!a?.moveu;
    if (!a?.moveu) return;
    const lado = e.clientX < window.innerWidth / 2;
    setEsquerda(lado);
    gravar(CHAVE_LADO, lado ? 'esquerda' : 'direita');
  };

  const abrir = () => {
    setPainelAberto(true);
    if (novidades > 0) setAba('chamados');
    void carregarChamados();
  };

  const esconderHoje = () => {
    leitura.parar();
    setPainelAberto(false);
    setEscondidoHoje(true);
    gravar(CHAVE_ESCONDIDO, hoje());
  };

  // O item "SST IA" do menu lateral abre a tela completa; se o botão estava escondido, volta amanhã.
  if (escondidoHoje) return null;

  // Abaixo de 680 px o menu lateral aberto é uma gaveta sobre a tela inteira.
  const oculto = janela || teclado || (menuMobileAberto && window.innerWidth <= 680);

  return (
    <>
      {painelAberto && (
        <PainelSstIa
          oculto={janela}
          aba={aba}
          setAba={setAba}
          conversa={conversa}
          leitura={leitura}
          vozLigada={vozLigada}
          setVozLigada={setVozLigada}
          aoFechar={() => setPainelAberto(false)}
          aoEsconderHoje={esconderHoje}
          aoMudarChamados={() => void carregarChamados()}
          chamados={chamados}
          esquerda={esquerda}
        />
      )}
      {!painelAberto && (
        <div
          className={mergeClasses(estilos.involucro, esquerda && estilos.esquerda, rolando && estilos.rolando, oculto && estilos.oculto)}
          aria-hidden={oculto || undefined}
        >
          <button
            type="button"
            data-sst-ia-botao=""
            className={mergeClasses(esm.vidro, estilos.botao, pilula || sobMouse ? estilos.aberto : estilos.circulo)}
            aria-label={novidades > 0 ? `SST IA, ${novidades} chamado(s) com novidade` : 'SST IA, tire dúvidas do G-SST'}
            title="SST IA"
            tabIndex={oculto ? -1 : 0}
            onPointerEnter={(e) => e.pointerType === 'mouse' && setSobMouse(true)}
            onPointerLeave={() => setSobMouse(false)}
            onPointerDown={aoApertar}
            onPointerMove={aoMover}
            onPointerUp={aoSoltar}
            onClick={() => {
              if (foiArraste.current) {
                foiArraste.current = false;
                return;
              }
              abrir();
            }}
          >
            <IconeSstIa className={estilos.icone} />
            <span className={mergeClasses(estilos.nome, (pilula || sobMouse) && estilos.nomeVisivel)}>SST IA</span>
          </button>
          {novidades > 0 && <span className={estilos.contador}>{novidades > 9 ? '9+' : novidades}</span>}
        </div>
      )}
    </>
  );
}
