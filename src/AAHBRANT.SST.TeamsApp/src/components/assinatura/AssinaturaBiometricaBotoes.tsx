import { useState } from 'react';
import { FeedbackInline } from '@ui';
import { capturarDigitalLocal, type CapturaLocal, type DispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { tocarBipeAssinaturaAceita } from '../../lib/bipeAssinatura';
import { classificarErroAssinatura } from '../../lib/erroAssinatura';
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import { BotaoBiometriaDigital } from './BotaoBiometriaDigital';
import { ErroFacialDialog } from './ErroFacialDialog';

interface Props {
  /** Chamado com a leitura do leitor. Lança o erro do servidor quando a pessoa lida não é quem deveria assinar. */
  aoDigital: (captura: CapturaLocal, leitor: DispositivoLocal) => Promise<void>;
  /** Chamado com a foto do rosto (câmera ao vivo). Lança o erro do servidor (pessoa errada, rosto rejeitado). */
  aoFacial: (foto: File) => Promise<void>;
  agenteDisponivel: boolean | null;
  dispositivoLocal: DispositivoLocal | null;
  /** Quem assina (para o texto do "sem acesso"): integração, turma ou OS. */
  quem?: 'integracao' | 'turma' | 'os';
  /** Cadastro do assinante, quando se sabe: false desabilita o método e explica. */
  temDigital?: boolean;
  temFacial?: boolean;
  desabilitado?: boolean;
  /** O servidor respondeu 409 "já assinou": a tela só recarrega. */
  aoJaAssinou?: () => void;
}

// Par de botões "Assinar com digital" e "Assinar com facial" (16/11/2026) para as assinaturas do instrutor
// (etapa da integração e turma comum) e do técnico da Ordem de Serviço. Mesmos botões e mesma captura do
// quiosque (AssinaturaQuiosque): digital vinho, facial verde, 52 px de altura. A decisão de quem pode
// assinar é do servidor; aqui só se mostra a recusa com o texto dele.
export function AssinaturaBiometricaBotoes({
  aoDigital,
  aoFacial,
  agenteDisponivel,
  dispositivoLocal,
  quem = 'integracao',
  temDigital,
  temFacial,
  desabilitado,
  aoJaAssinou,
}: Props) {
  const [lendo, setLendo] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [mensagem, setMensagem] = useState<{ tom: 'erro' | 'info' | 'sucesso'; texto: string } | null>(null);
  const [erroFacial, setErroFacial] = useState<string | null>(null);

  const ocupado = lendo || enviando || !!desabilitado;
  const semLeitor = agenteDisponivel === false || !dispositivoLocal;

  function tratar(err: unknown, facial: boolean) {
    const { tipo, texto } = classificarErroAssinatura(err, quem);
    if (tipo === 'ja_assinou') {
      setMensagem({ tom: 'info', texto });
      aoJaAssinou?.();
      return;
    }
    if (facial) setErroFacial(texto);
    else setMensagem({ tom: 'erro', texto });
  }

  async function digital() {
    if (!dispositivoLocal) return;
    try {
      setLendo(true);
      setMensagem({ tom: 'info', texto: 'Encoste o dedo no leitor.' });
      setErroFacial(null);
      const captura = await capturarDigitalLocal(dispositivoLocal.dispositivoId);
      setLendo(false);
      setEnviando(true);
      setMensagem({ tom: 'info', texto: 'Conferindo a digital…' });
      await aoDigital(captura, dispositivoLocal);
      tocarBipeAssinaturaAceita();
      setMensagem(null);
    } catch (e) {
      tratar(e, false);
    } finally {
      setLendo(false);
      setEnviando(false);
    }
  }

  async function facial(foto: File) {
    try {
      setEnviando(true);
      setMensagem(null);
      await aoFacial(foto);
      tocarBipeAssinaturaAceita();
    } catch (e) {
      tratar(e, true);
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div>
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'stretch' }}>
        <BotaoBiometriaDigital onClick={() => void digital()} disabled={ocupado || semLeitor || temDigital === false}>
          Assinar com digital
        </BotaoBiometriaDigital>
        <SeletorFotoCamera
          aoSelecionarArquivo={facial}
          aoErroValidacao={setErroFacial}
          rotulo="Assinar com facial"
          desabilitado={ocupado || temFacial === false}
          tamanho="medium"
          variante="facialAzure"
          modoCamera="user"
          modoCaptura="assinatura"
          exigirCamera
        />
      </div>
      {temDigital === false && <div style={{ fontSize: 12, marginTop: 6 }}>Sem digital cadastrada: use a facial ou cadastre a digital.</div>}
      {temFacial === false && <div style={{ fontSize: 12, marginTop: 6 }}>Sem facial cadastrada: use a digital ou cadastre o rosto.</div>}
      {semLeitor && temDigital !== false && (
        <div style={{ fontSize: 12, marginTop: 6 }}>
          Leitor Futronic não encontrado nesta máquina. Verifique o leitor e o Agente Biométrico, ou use a facial.
        </div>
      )}
      {mensagem && (
        <div style={{ marginTop: 10 }} role="status" aria-live="polite">
          <FeedbackInline tom={mensagem.tom}>{mensagem.texto}</FeedbackInline>
        </div>
      )}
      <ErroFacialDialog mensagem={erroFacial} aoFechar={() => setErroFacial(null)} />
    </div>
  );
}
