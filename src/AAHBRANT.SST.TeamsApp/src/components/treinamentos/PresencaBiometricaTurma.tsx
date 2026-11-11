import { useEffect, useRef, useState } from 'react';
import { Button, FeedbackInline, Legenda } from '@ui';
import { Camera24Regular, Stop24Regular } from '@fluentui/react-icons';
import { api, type SessaoTreinamentoDetalhe } from '../../lib/api';
import { capturarDigitalLocal, credencialLeitura, type DispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { FilaFacialDds, type ResultadoFilaFacial } from '../dds/FilaFacialDds';
import { tocarBipeAssinaturaAceita } from '../../lib/bipeAssinatura';
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import { MutacaoEnfileiradaOfflineError } from '../../lib/offline/syncEngine';
import { ErroFacialDialog } from '../assinatura/ErroFacialDialog';
import { BotaoBiometriaDigital } from '../assinatura/BotaoBiometriaDigital';

interface Props {
  detalhe: SessaoTreinamentoDetalhe;
  /** Turma encerrada (ou etapa fora de andamento): some o bloco de leitura. */
  somenteLeitura: boolean;
  agenteDisponivel: boolean | null;
  dispositivoLocal: DispositivoLocal | null;
  /** Recarrega o detalhe da turma depois de cada presença confirmada. */
  aoAtualizar: () => Promise<void> | void;
  aoErro?: (mensagem: string | null) => void;
  /** Texto de apoio da fila. Padrão: o da turma comum. */
  textoAjuda?: string;
}

interface ContextoFila {
  detalhe: SessaoTreinamentoDetalhe;
  dispositivo: DispositivoLocal | null;
  aoAtualizar: Props['aoAtualizar'];
}

// Presença biométrica da turma (digital e facial), extraída de SessaoTreinamentoDetalhePage em
// 11/11/2026 sem mudança visual, para a turma comum e para cada etapa de curso da integração: lá a
// presença confirmada é a assinatura biométrica de cada participante no certificado do curso.
// Fila contínua (19/10, igual ao DDS): aberta, cada participante encosta o dedo ou olha para a câmera
// e a presença é confirmada sozinha, um após o outro, até alguém fechar a fila.
export function PresencaBiometricaTurma({ detalhe, somenteLeitura, agenteDisponivel, dispositivoLocal, aoAtualizar, aoErro, textoAjuda }: Props) {
  const sessaoId = detalhe.sessao.id;
  const [lendoFacial, setLendoFacial] = useState(false);
  const [mensagemPresenca, setMensagemPresenca] = useState<{ tipo: 'success' | 'info' | 'erro'; texto: string } | null>(null);
  const [erroFacial, setErroFacial] = useState<string | null>(null);
  const [filaAberta, setFilaAberta] = useState(false);
  const [filaFacialAberta, setFilaFacialAberta] = useState(false);
  const filaAtiva = useRef(false);
  const contextoFila = useRef<ContextoFila>({ detalhe, dispositivo: dispositivoLocal, aoAtualizar });
  contextoFila.current = { detalhe, dispositivo: dispositivoLocal, aoAtualizar };

  // Fluxo em fila (pedido do usuário, 04/09; contínua desde 19/10): um único leitor compartilhado —
  // cada participante encosta o dedo, sem precisar ser selecionado antes. O agente local faz o match
  // 1:N e devolve QUEM reconheceu; aqui se confere a inscrição na turma e se registra.
  async function processarLeituraFila(trabalhadorId: string, leitor: DispositivoLocal, captura: Awaited<ReturnType<typeof capturarDigitalLocal>>) {
    const atual = contextoFila.current.detalhe;
    const participante = atual.participantes.find((p) => p.trabalhadorId === trabalhadorId);
    if (!participante) {
      setMensagemPresenca({ tipo: 'erro', texto: 'Esta pessoa não está inscrita nesta turma.' });
      return;
    }
    if (participante.presencaConfirmadaEm) {
      setMensagemPresenca({ tipo: 'info', texto: `${participante.trabalhadorNome} já teve a presença confirmada.` });
      return;
    }
    await api.sessoesTreinamento.registrarPresenca(sessaoId, trabalhadorId, credencialLeitura(leitor, captura.leituras[0]), captura.score, captura.imagemPng);
    tocarBipeAssinaturaAceita();
    setMensagemPresenca({ tipo: 'success', texto: `Presença de ${participante.trabalhadorNome} confirmada.` });
    await contextoFila.current.aoAtualizar();
  }

  async function lacoFila() {
    while (filaAtiva.current) {
      try {
        const leitor = contextoFila.current.dispositivo;
        if (!leitor) break;
        const captura = await capturarDigitalLocal(leitor.dispositivoId);
        if (!filaAtiva.current) break;
        await processarLeituraFila(captura.trabalhadorId, leitor, captura);
        await new Promise((r) => setTimeout(r, 1200));
      } catch (e) {
        if (!filaAtiva.current) break;
        const texto = e instanceof Error ? e.message : '';
        if (/Nenhum dedo detectado|dentro do tempo limite/i.test(texto)) continue;
        if (e instanceof TypeError) {
          setMensagemPresenca({ tipo: 'erro', texto: 'Perdi a conexão com o leitor. Verifique o Agente Biométrico e abra a fila de novo.' });
          fecharFila();
          break;
        }
        setMensagemPresenca({ tipo: 'erro', texto: `Digital não reconhecida: ${texto || 'tente de novo.'}` });
        await new Promise((r) => setTimeout(r, 1500));
      }
    }
  }

  function abrirFila() {
    if (!dispositivoLocal || filaAtiva.current) return;
    filaAtiva.current = true;
    setFilaAberta(true);
    aoErro?.(null);
    setMensagemPresenca({ tipo: 'info', texto: 'Fila aberta. Próximo participante: encoste o dedo no leitor.' });
    void lacoFila();
  }

  function fecharFila() {
    filaAtiva.current = false;
    setFilaAberta(false);
  }

  useEffect(() => () => { filaAtiva.current = false; }, []);

  // Envio da fila facial em tela cheia: o servidor identifica a pessoa na obra e confere a inscrição.
  async function enviarFotoFila(foto: File): Promise<ResultadoFilaFacial> {
    const atual = contextoFila.current.detalhe;
    try {
      const r = await api.sessoesTreinamento.registrarPresencaFacial(sessaoId, atual.sessao.obraId, foto);
      const nome = atual.participantes.find((p) => p.trabalhadorId === r.trabalhadorId)?.trabalhadorNome ?? 'Participante';
      return { trabalhadorNome: nome, jaConfirmado: false };
    } catch (e) {
      // Já confirmado não é erro na fila: mostra como aviso e segue para o próximo.
      if (e instanceof Error && /já confirmada/i.test(e.message)) return { trabalhadorNome: 'Este participante', jaConfirmado: true };
      throw e;
    }
  }

  async function confirmarPresencaFacial(arquivo: File) {
    try {
      setLendoFacial(true);
      aoErro?.(null);
      setErroFacial(null);
      setMensagemPresenca(null);

      const resultado = await api.sessoesTreinamento.registrarPresencaFacial(sessaoId, detalhe.sessao.obraId, arquivo);
      const participante = detalhe.participantes.find((p) => p.trabalhadorId === resultado.trabalhadorId);
      setMensagemPresenca({
        tipo: 'success',
        texto: `Presença de ${participante?.trabalhadorNome ?? 'participante'} confirmada por reconhecimento facial.`,
      });
      tocarBipeAssinaturaAceita();
      await aoAtualizar();
    } catch (e) {
      if (e instanceof MutacaoEnfileiradaOfflineError) {
        setMensagemPresenca({
          tipo: 'info',
          texto: 'Sem internet — a foto foi salva neste dispositivo e será verificada assim que a conexão voltar.',
        });
        return;
      }
      setErroFacial(e instanceof Error ? e.message : 'Falha na validação facial.');
    } finally {
      setLendoFacial(false);
    }
  }

  return (
    <>
      {!somenteLeitura && agenteDisponivel === false && (
        <FeedbackInline tom="aviso">
          Leitor Futronic não encontrado nesta máquina. Verifique se o leitor está conectado e se o Agente
          Biométrico está em execução, depois recarregue esta página.
        </FeedbackInline>
      )}

      {!somenteLeitura && (
        <div style={{ marginBottom: 16, display: 'flex', flexDirection: 'column', gap: 8 }}>
          <Legenda>
            {filaAberta
              ? 'Leitor aberto: cada participante encosta o dedo e a presença é confirmada sozinha.'
              : textoAjuda ?? 'Abra a fila para o pessoal registrar a presença pela digital ou pelo rosto, um após o outro — não precisa selecionar ninguém antes.'}
          </Legenda>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
            {filaAberta ? (
              <Button appearance="primary" size="large" icon={<Stop24Regular />} onClick={fecharFila}>
                Fechar fila
              </Button>
            ) : (
              <BotaoBiometriaDigital onClick={abrirFila} disabled={!agenteDisponivel || !dispositivoLocal || lendoFacial}>
                Abrir fila
              </BotaoBiometriaDigital>
            )}
            <Button
              appearance="primary"
              size="large"
              icon={<Camera24Regular />}
              disabled={filaAberta || lendoFacial}
              onClick={() => setFilaFacialAberta(true)}
            >
              Abrir fila facial
            </Button>
            <SeletorFotoCamera
              aoSelecionarArquivo={confirmarPresencaFacial}
              aoErroValidacao={setErroFacial}
              rotulo="Facial Azure"
              desabilitado={filaAberta || lendoFacial}
              tamanho="medium"
              variante="facialAzure"
              modoCamera="user"
              modoCaptura="assinatura"
              exigirCamera
              permitirCapturaAutomatica
            />
          </div>
          {mensagemPresenca && (
            <FeedbackInline tom={mensagemPresenca.tipo === 'success' ? 'sucesso' : mensagemPresenca.tipo === 'erro' ? 'erro' : 'info'}>
              {mensagemPresenca.texto}
            </FeedbackInline>
          )}
        </div>
      )}

      <FilaFacialDds
        enviarFoto={enviarFotoFila}
        titulo="Fila facial"
        subtitulo={`${detalhe.sessao.cursoTreinamentoNome} · ${detalhe.sessao.obraNome}`}
        totalInscritos={detalhe.participantes.filter((p) => !p.presencaConfirmadaEm).length}
        aberto={filaFacialAberta}
        aoFechar={() => setFilaFacialAberta(false)}
        aoPresencaConfirmada={() => { void aoAtualizar(); }}
      />
      <ErroFacialDialog mensagem={erroFacial} aoFechar={() => setErroFacial(null)} />
    </>
  );
}
