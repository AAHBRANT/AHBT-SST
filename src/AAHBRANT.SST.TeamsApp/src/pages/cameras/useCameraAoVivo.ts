import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type ResultadoQuadroAoVivo } from '../../lib/api';
import {
  desfechoDaRecusa,
  desfechoDoEncerramento,
  MENSAGEM_AGENTE_NAO_RESPONDEU,
  MENSAGEM_SEM_REDE,
} from './aoVivoMensagens';
import type { MensagemFalhaTeste } from './mensagensTesteConexao';
import { paraData } from './rotulos';

// Fase do ciclo de vida da transmissão. "ao vivo" e "com atraso" são o mesmo ciclo (transmitindo): o atraso é calculado
// pelo tempo desde a última imagem que chegou.
export type FaseAoVivo = 'conectando' | 'transmitindo' | 'pausado' | 'tempo' | 'inatividade' | 'encerrada' | 'falha' | 'limite';
export type EstadoAoVivo = Exclude<FaseAoVivo, 'transmitindo'> | 'ao-vivo' | 'atraso';

const LIMITE_CONECTANDO_MS = 50_000;
const ATRASO_MS = 5_000;
// Esperas do laço: um pedido por vez, nunca empilhado.
const ESPERA_APOS_QUADRO_MS = 80;
const ESPERA_SEM_NOVO_MS = 300;
const ESPERA_AGUARDANDO_MS = 1_000;
const FALHAS_SEGUIDAS_MAXIMAS = 15;
// O relógio do PC da obra pode estar errado: se a hora do quadro destoa mais que isto da hora local, usa a hora local.
const DESVIO_RELOGIO_MS = 60_000;

const esperar = (ms: number) => new Promise<void>((resolve) => window.setTimeout(resolve, ms));

export interface UltimaImagem {
  // Hora mostrada: a do quadro (X-Capturado-Em) ou, sem ela (ou com relógio fora do esperado), a de chegada aqui.
  hora: Date;
  chegouEm: number;
}

// "Ver ao vivo" de uma câmera IP: abre a sessão, busca o último quadro em laço (um pedido por vez, com ?apos=) e o expõe
// como URL de blob. Nada vai para disco nem para localStorage. Pausa com a tela em segundo plano e, ao sair, encerra.
export function useCameraAoVivo(cameraId: string) {
  const [fase, setFaseEstado] = useState<FaseAoVivo>('conectando');
  const faseRef = useRef<FaseAoVivo>('conectando');
  const setFase = useCallback((f: FaseAoVivo) => {
    faseRef.current = f;
    setFaseEstado(f);
  }, []);
  const [mensagem, setMensagem] = useState<MensagemFalhaTeste | null>(null);
  const [urlQuadro, setUrlQuadro] = useState<string | null>(null);
  const [ultima, setUltima] = useState<UltimaImagem | null>(null);
  const [agora, setAgora] = useState(() => Date.now());
  const [restante, setRestante] = useState<number | null>(null);

  const geracao = useRef(0);
  const abortRef = useRef<AbortController | null>(null);
  const sessaoRef = useRef<string | null>(null);
  const fimRef = useRef<number | null>(null);
  const urlRef = useRef<string | null>(null);
  // Últimos cabeçalhos de autenticação usados: o encerramento ao sair precisa deles na hora, sem await. Só em memória.
  const cabecalhosRef = useRef<Record<string, string> | null>(null);
  const guardarCabecalhos = useCallback((h: Record<string, string>) => {
    cabecalhosRef.current = h;
  }, []);
  // O 410 "encerrada" com a tela aberta e visível reabre sozinho UMA vez; só um clique do usuário zera isto.
  const reabriuSozinha = useRef(false);
  const abrirRef = useRef<() => Promise<void>>(async () => {});

  // Interrompe o laço atual (se houver) sem avisar o servidor.
  const interromper = useCallback(() => {
    geracao.current += 1;
    abortRef.current?.abort();
    abortRef.current = null;
  }, []);

  const encerrarNoServidor = useCallback(() => {
    const id = sessaoRef.current;
    sessaoRef.current = null;
    fimRef.current = null;
    if (id) void api.cameras.aoVivo.encerrar(id, cabecalhosRef.current ?? undefined);
  }, []);

  const trocarUrl = useCallback((nova: string | null) => {
    const anterior = urlRef.current;
    urlRef.current = nova;
    setUrlQuadro(nova);
    // Revoga o anterior só depois que o <img> já passou para o novo.
    if (anterior) window.setTimeout(() => URL.revokeObjectURL(anterior), 1_500);
  }, []);

  const abrir: () => Promise<void> = useCallback(async () => {
    interromper();
    const minha = ++geracao.current;
    const ativa = () => geracao.current === minha;
    const controlador = new AbortController();
    abortRef.current = controlador;

    setFase('conectando');
    setMensagem(null);
    setUltima(null);
    setRestante(null);
    trocarUrl(null);

    const inicio = Date.now();
    let sessaoId: string;
    try {
      const r = await api.cameras.aoVivo.iniciar(cameraId, controlador.signal, guardarCabecalhos);
      if (!ativa()) return;
      if (!r.ok) {
        const d = desfechoDaRecusa(r.status, r.codigo);
        setMensagem(d.mensagem);
        setFase(d.fase);
        return;
      }
      sessaoId = r.sessao.sessaoId;
      sessaoRef.current = sessaoId;
      fimRef.current = performance.now() + r.sessao.encerraEmSegundos * 1_000;
      setRestante(r.sessao.encerraEmSegundos);
    } catch {
      if (!ativa()) return;
      setMensagem(MENSAGEM_SEM_REDE);
      setFase('falha');
      return;
    }

    const falhar = (m: MensagemFalhaTeste) => {
      encerrarNoServidor();
      setMensagem(m);
      setFase('falha');
    };
    const publicar = (r: Extract<ResultadoQuadroAoVivo, { tipo: 'quadro' }>) => {
      const chegouEm = Date.now();
      let hora = new Date(chegouEm);
      if (r.capturadoEm) {
        const capturada = paraData(r.capturadoEm);
        if (!Number.isNaN(capturada.getTime()) && Math.abs(capturada.getTime() - chegouEm) <= DESVIO_RELOGIO_MS) hora = capturada;
      }
      trocarUrl(URL.createObjectURL(r.imagem));
      setUltima({ hora, chegouEm });
      setFase('transmitindo');
    };

    let sequencia: number | null = null;
    let recebeu = false;
    let falhasSeguidas = 0;
    while (ativa()) {
      if (!recebeu && Date.now() - inicio > LIMITE_CONECTANDO_MS) return falhar(MENSAGEM_AGENTE_NAO_RESPONDEU);

      let r: ResultadoQuadroAoVivo;
      try {
        r = await api.cameras.aoVivo.quadro(sessaoId, sequencia, controlador.signal, guardarCabecalhos);
      } catch {
        return; // interrompido (pausa, cancelar ou saída da tela)
      }
      if (!ativa()) return;

      switch (r.tipo) {
        case 'quadro':
          falhasSeguidas = 0;
          recebeu = true;
          if (r.sequencia !== null) sequencia = r.sequencia;
          publicar(r);
          await esperar(ESPERA_APOS_QUADRO_MS);
          break;
        case 'semNovo':
          falhasSeguidas = 0;
          await esperar(ESPERA_SEM_NOVO_MS);
          break;
        case 'aguardando':
          falhasSeguidas = 0;
          await esperar(ESPERA_AGUARDANDO_MS);
          break;
        case 'encerrada': {
          sessaoRef.current = null;
          fimRef.current = null;
          if (r.motivo === 'encerrada' && !reabriuSozinha.current && document.visibilityState === 'visible') {
            reabriuSozinha.current = true;
            void abrirRef.current();
            return;
          }
          const d = desfechoDoEncerramento(r.motivo);
          setMensagem(d.mensagem);
          setFase(d.fase);
          return;
        }
        case 'erro':
          falhasSeguidas += 1;
          if (falhasSeguidas >= FALHAS_SEGUIDAS_MAXIMAS) return falhar(MENSAGEM_SEM_REDE);
          await esperar(Math.min(4_000, 500 * falhasSeguidas));
          break;
      }
    }
  }, [cameraId, interromper, encerrarNoServidor, trocarUrl, setFase, guardarCabecalhos]);

  useEffect(() => {
    abrirRef.current = abrir;
  }, [abrir]);

  // Clique do usuário (Tentar de novo, Continuar assistindo, Retomar): zera a trava da reabertura automática.
  const iniciar = useCallback(() => {
    reabriuSozinha.current = false;
    void abrir();
  }, [abrir]);

  const cancelar = useCallback(() => {
    interromper();
    encerrarNoServidor();
  }, [interromper, encerrarNoServidor]);

  // Abre ao montar. No Strict Mode (dev) o efeito roda duas vezes: a primeira rodada é interrompida pela limpeza.
  useEffect(() => {
    void abrir();
    return () => {
      interromper();
      encerrarNoServidor();
      const url = urlRef.current;
      urlRef.current = null;
      if (url) URL.revokeObjectURL(url);
    };
  }, [abrir, interromper, encerrarNoServidor]);

  // Tela em segundo plano: para de buscar imagens (sem encerrar a sessão; o servidor encerra sozinho sem espectador).
  useEffect(() => {
    const aoMudar = () => {
      if (document.visibilityState !== 'hidden') return;
      if (faseRef.current !== 'conectando' && faseRef.current !== 'transmitindo') return;
      interromper();
      setFase('pausado');
    };
    document.addEventListener('visibilitychange', aoMudar);
    return () => document.removeEventListener('visibilitychange', aoMudar);
  }, [interromper, setFase]);

  // Página fechando ou recarregando: encerra com keepalive.
  useEffect(() => {
    const aoSair = () => encerrarNoServidor();
    window.addEventListener('pagehide', aoSair);
    return () => window.removeEventListener('pagehide', aoSair);
  }, [encerrarNoServidor]);

  // Relógio de 1 s: atraso, "há N s" e contagem regressiva (contada em relógio monotônico, não no do aparelho).
  useEffect(() => {
    const id = window.setInterval(() => {
      setAgora(Date.now());
      if (fimRef.current !== null) setRestante(Math.max(0, Math.ceil((fimRef.current - performance.now()) / 1_000)));
    }, 1_000);
    return () => window.clearInterval(id);
  }, []);

  const atrasado = fase === 'transmitindo' && !!ultima && agora - ultima.chegouEm > ATRASO_MS;
  const estado: EstadoAoVivo = fase === 'transmitindo' ? (atrasado ? 'atraso' : 'ao-vivo') : fase;
  const haSegundos = ultima ? Math.max(0, Math.round((agora - ultima.chegouEm) / 1_000)) : null;

  return { estado, mensagem, urlQuadro, ultima, haSegundos, restante, iniciar, cancelar };
}
