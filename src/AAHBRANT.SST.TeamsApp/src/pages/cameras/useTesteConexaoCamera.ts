import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type TesteConexaoCamera } from '../../lib/api';
import { mensagemDaFalha, type MensagemFalhaTeste } from './mensagensTesteConexao';

// Consulta o resultado a cada 2 s. O servidor marca o pedido como Expirado depois de 2 min sem resposta do agente;
// o limite abaixo só protege a tela caso essa resposta nunca chegue.
const INTERVALO_CONSULTA_MS = 2000;
const LIMITE_TOTAL_MS = 150_000;
const FALHAS_SEGUIDAS_MAXIMAS = 3;

export type { MensagemFalhaTeste };
export type FaseTeste = 'parado' | 'enviando' | 'aguardando' | 'ok' | 'falha';

const esperar = (ms: number) => new Promise<void>((resolve) => window.setTimeout(resolve, ms));

// Teste de conexão da câmera IP: pede ao servidor, consulta até concluir ou expirar. A miniatura só vem uma vez, na
// consulta que traz a conclusão; fica apenas em memória deste hook (nunca em localStorage) e some ao reiniciar ou sair da tela.
export function useTesteConexaoCamera() {
  const [fase, setFase] = useState<FaseTeste>('parado');
  const [miniaturaBase64, setMiniaturaBase64] = useState<string | null>(null);
  const [falha, setFalha] = useState<MensagemFalhaTeste | null>(null);
  const rodada = useRef(0);

  // Sair da tela cancela a consulta em andamento.
  useEffect(
    () => () => {
      rodada.current += 1;
    },
    [],
  );

  const reiniciar = useCallback(() => {
    rodada.current += 1;
    setFase('parado');
    setMiniaturaBase64(null);
    setFalha(null);
  }, []);

  const iniciar = useCallback(async (solicitar: () => Promise<{ id: string }>) => {
    const minha = ++rodada.current;
    const ativa = () => rodada.current === minha;
    setMiniaturaBase64(null);
    setFalha(null);
    setFase('enviando');

    let id: string;
    try {
      id = (await solicitar()).id;
    } catch (e) {
      if (!ativa()) return;
      setFalha({
        titulo: 'Não foi possível pedir o teste.',
        texto: e instanceof Error ? e.message : 'Tente de novo em instantes.',
      });
      setFase('falha');
      return;
    }
    if (!ativa()) return;
    setFase('aguardando');

    const inicio = Date.now();
    let falhasSeguidas = 0;
    while (ativa()) {
      await esperar(INTERVALO_CONSULTA_MS);
      if (!ativa()) return;
      if (Date.now() - inicio > LIMITE_TOTAL_MS) {
        setFalha({ titulo: 'O teste demorou mais que o esperado.', texto: 'Tente de novo em instantes.' });
        setFase('falha');
        return;
      }
      let teste: TesteConexaoCamera;
      try {
        teste = await api.cameras.obterTesteConexao(id);
        falhasSeguidas = 0;
      } catch (e) {
        falhasSeguidas += 1;
        if (falhasSeguidas >= FALHAS_SEGUIDAS_MAXIMAS && ativa()) {
          setFalha({
            titulo: 'Não foi possível consultar o resultado do teste.',
            texto: e instanceof Error ? e.message : 'Confira a conexão e tente de novo.',
          });
          setFase('falha');
          return;
        }
        continue;
      }
      if (!ativa()) return;
      if (teste.situacao === 'Pendente') continue;
      if (teste.situacao === 'Concluido' && teste.sucesso) {
        setMiniaturaBase64(teste.miniaturaBase64);
        setFase('ok');
        return;
      }
      setFalha(mensagemDaFalha(teste));
      setFase('falha');
      return;
    }
  }, []);

  return { fase, miniaturaBase64, falha, iniciar, reiniciar };
}
