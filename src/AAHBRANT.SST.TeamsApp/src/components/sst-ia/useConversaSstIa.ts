import { useCallback, useRef, useState } from 'react';
import { api, ErroAgenteIa } from '../../lib/api';

export interface MensagemSstIa {
  id: string;
  autor: 'usuario' | 'ia';
  texto: string;
  /** Pergunta feita por voz (a resposta é lida em voz alta se a voz estiver ligada). */
  falada?: boolean;
  /** Resposta ainda chegando. */
  gerando?: boolean;
  erro?: boolean;
}

let sequencia = 0;
const novoId = (prefixo: string) => `${prefixo}-${Date.now()}-${++sequencia}`;

/**
 * Conversa com o agente de suporte. Uma sessão por conversa, criada na primeira pergunta e guardada
 * enquanto o app estiver aberto (o botão fica montado no AppShell, então trocar de tela não perde nada).
 * `aoResponder` recebe a resposta final (para a leitura em voz alta).
 */
export function useConversaSstIa(aoResponder: (mensagem: MensagemSstIa, perguntaFalada: boolean) => void) {
  const [mensagens, setMensagens] = useState<MensagemSstIa[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [respondendo, setRespondendo] = useState(false);
  const sessaoRef = useRef<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const atualizar = useCallback((id: string, mudar: (m: MensagemSstIa) => MensagemSstIa) => {
    setMensagens((lista) => lista.map((m) => (m.id === id ? mudar(m) : m)));
  }, []);

  const enviar = useCallback(
    async (texto: string, tela: string, falada = false) => {
      const pergunta = texto.trim();
      if (!pergunta || respondendo) return;
      const idPergunta = novoId('u');
      const idResposta = novoId('ia');
      setMensagens((lista) => [
        ...lista,
        { id: idPergunta, autor: 'usuario', texto: pergunta, falada },
        { id: idResposta, autor: 'ia', texto: '', gerando: true },
      ]);
      setRespondendo(true);
      setStatus('Pensando…');
      const controle = new AbortController();
      abortRef.current = controle;

      // Uma mensagem do agente pode vir em vários blocos (id próprio cada); juntamos tudo na mesma bolha.
      const blocos = new Map<string, string>();
      const textoAtual = () => [...blocos.values()].filter(Boolean).join('\n\n');
      let falhou: string | null = null;

      const executar = async (podeRecriar: boolean): Promise<void> => {
        try {
          if (!sessaoRef.current) sessaoRef.current = (await api.agenteIa.criarSessao()).sessaoId;
          for await (const ev of api.agenteIa.enviar(sessaoRef.current, pergunta, tela, controle.signal)) {
            if (ev.tipo === 'delta' && ev.id) {
              blocos.set(ev.id, (blocos.get(ev.id) ?? '') + (ev.texto ?? ''));
              setStatus(null);
              atualizar(idResposta, (m) => ({ ...m, texto: textoAtual() }));
            } else if (ev.tipo === 'mensagem' && ev.id) {
              blocos.set(ev.id, ev.texto ?? '');
              setStatus(null);
              atualizar(idResposta, (m) => ({ ...m, texto: textoAtual() }));
            } else if (ev.tipo === 'status') {
              if (!textoAtual()) setStatus(ev.texto ?? 'Pensando…');
            } else if (ev.tipo === 'erro') {
              falhou = ev.texto ?? 'O SST IA não conseguiu responder agora.';
            } else if (ev.tipo === 'fim' && ev.texto === 'encerrada') {
              sessaoRef.current = null;
            }
          }
        } catch (e) {
          if (controle.signal.aborted) return;
          // Conversa expirada (ou de outro usuário): abre uma nova uma vez e repete a pergunta.
          if (podeRecriar && e instanceof ErroAgenteIa && e.status === 404) {
            sessaoRef.current = null;
            return executar(false);
          }
          // fetch sem rede lança TypeError ("Failed to fetch"): mensagem em português, sem o texto do navegador.
          falhou =
            e instanceof ErroAgenteIa && e.status === 503
              ? 'O SST IA ainda não está ligado neste ambiente.'
              : e instanceof ErroAgenteIa && e.message
                ? e.message
                : e instanceof TypeError || !navigator.onLine
                  ? 'Sem conexão com o servidor. Confira a internet e tente de novo.'
                  : 'Não foi possível falar com o SST IA agora. Tente de novo em instantes.';
        }
      };

      await executar(true);
      abortRef.current = null;
      setRespondendo(false);
      setStatus(null);
      const final = textoAtual();
      if (controle.signal.aborted) {
        atualizar(idResposta, (m) => ({ ...m, gerando: false, texto: final || 'Resposta interrompida.' }));
        return;
      }
      if (falhou && !final) {
        atualizar(idResposta, (m) => ({ ...m, gerando: false, erro: true, texto: falhou! }));
        return;
      }
      const resposta: MensagemSstIa = { id: idResposta, autor: 'ia', texto: final || 'Sem resposta.', gerando: false };
      atualizar(idResposta, () => resposta);
      aoResponder(resposta, falada);
    },
    [atualizar, aoResponder, respondendo],
  );

  const interromper = useCallback(() => abortRef.current?.abort(), []);

  const novaConversa = useCallback(() => {
    abortRef.current?.abort();
    sessaoRef.current = null;
    setMensagens([]);
    setStatus(null);
    setRespondendo(false);
  }, []);

  return { mensagens, status, respondendo, enviar, interromper, novaConversa };
}
