import { useCallback, useMemo, useState } from 'react';
import { Button, Field, Legenda, Spinner, Textarea, designTokens, tokensUi } from '@ui';
import { Mic24Regular, RecordStop24Filled, Sparkle24Regular } from '@fluentui/react-icons';
import {
  api,
  type PerguntaRelatoOcorrencia,
  type RespostaPerguntaRelato,
  type RelatoOcorrenciaResposta,
  type RelatoOcorrenciaSugestao,
} from '../../lib/api';
import { IndicadorGravacao } from '../../components/voz/IndicadorGravacao';
import { useGravacaoVoz } from '../../components/voz/useGravacaoVoz';

interface Props {
  obraId: string;
  desabilitado?: boolean;
  /**
   * Nova sugestão da IA. `primeiraRodada` = veio de um relato novo (substitui o formulário todo);
   * caso contrário veio das respostas às perguntas (o pai preserva o que o técnico editou à mão).
   */
  aoSugerir: (sugestao: RelatoOcorrenciaSugestao, primeiraRodada: boolean) => void;
  /** A classificação falhou: só a transcrição voltou, vai para a descrição. */
  aoSoTranscrever: (transcricao: string) => void;
  aoErro: (mensagem: string) => void;
  /**
   * Campos que o técnico preencheu à mão no formulário, no momento do envio — vão junto das respostas
   * para a IA não perguntar o que já está na tela.
   */
  camposPreenchidos?: () => RespostaPerguntaRelato[];
}

const bloco = {
  display: 'grid',
  justifyItems: 'start',
  gap: tokensUi.espaco.sm,
  padding: tokensUi.espaco.md,
  borderRadius: tokensUi.raio.sm,
  border: `1px dashed ${designTokens.colorPrimary}`,
  backgroundColor: designTokens.colorPageBackground,
} as const;

const quadroPerguntas = {
  display: 'grid',
  gap: tokensUi.espaco.md,
  padding: tokensUi.espaco.md,
  borderRadius: tokensUi.raio.sm,
  borderLeft: `3px solid ${tokensUi.status.atencao.tinta}`,
  backgroundColor: tokensUi.status.atencao.fundo,
} as const;

const linhaAcoes = { display: 'flex', gap: tokensUi.espaco.sm, flexWrap: 'wrap', alignItems: 'center' } as const;

const botaoParar = { backgroundColor: tokensUi.status.alerta.tinta, color: designTokens.colorWhite } as const;

// "Relatar por voz" / "Escrever relato" do registro de ocorrência: a IA preenche o formulário e faz
// até 4 perguntas sobre o que faltou; as respostas (escritas ou faladas) reclassificam o relato.
export function RelatoOcorrenciaIa({ obraId, desabilitado, aoSugerir, aoSoTranscrever, aoErro, camposPreenchidos }: Props) {
  const [modoTexto, setModoTexto] = useState(false);
  const [textoRelato, setTextoRelato] = useState('');
  const [relatoBase, setRelatoBase] = useState<string | null>(null);
  const [perguntas, setPerguntas] = useState<PerguntaRelatoOcorrencia[]>([]);
  const [respostas, setRespostas] = useState<Record<number, string>>({});
  // Respostas de todas as rodadas desde o relato. Antes cada rodada mandava só as respostas da rodada
  // atual, e a IA voltava a fazer as perguntas das rodadas anteriores (visto ao vivo com o técnico).
  const [respondidas, setRespondidas] = useState<RespostaPerguntaRelato[]>([]);
  const [processando, setProcessando] = useState(false);
  const [completando, setCompletando] = useState(false);

  const receber = useCallback(
    (resultado: RelatoOcorrenciaResposta, primeiraRodada: boolean) => {
      if (primeiraRodada) {
        setRelatoBase(resultado.transcricao);
        setRespondidas([]);
      }
      if (!resultado.sugestao) {
        aoSoTranscrever(resultado.transcricao);
        setPerguntas([]);
        return;
      }
      aoSugerir(resultado.sugestao, primeiraRodada);
      setPerguntas(resultado.sugestao.perguntas);
      setRespostas({});
    },
    [aoSugerir, aoSoTranscrever],
  );

  const enviarRelatoFalado = useMemo(() => api.acidentes.relatoVoz(obraId), [obraId]);
  const relatoVoz = useGravacaoVoz(enviarRelatoFalado, (r) => receber(r, true), aoErro);

  // Responder por voz: a fala vira a resposta de todas as perguntas abertas de uma vez.
  const respostaVoz = useGravacaoVoz(
    api.acidentes.transcrever,
    // 500 = limite da pergunta no ClassificarRelatoOcorrenciaCommandValidator (4 perguntas juntas passavam dele).
    (r) => void completar([{ pergunta: perguntas.map((p) => p.pergunta).join(' ').slice(0, 500), resposta: r.transcricao }]),
    aoErro,
  );

  function novoRelato() {
    setPerguntas([]);
    setRespostas({});
    setRespondidas([]);
    setRelatoBase(null);
  }

  async function enviarTexto() {
    if (!textoRelato.trim()) return;
    try {
      setProcessando(true);
      receber(await api.acidentes.relatoTexto(obraId, textoRelato.trim()), true);
      setModoTexto(false);
    } catch (e) {
      aoErro(e instanceof Error ? e.message : 'A IA não conseguiu analisar o relato agora.');
    } finally {
      setProcessando(false);
    }
  }

  async function completar(complementos = perguntas.map((p, i) => ({ pergunta: p.pergunta, resposta: respostas[i] ?? '' }))) {
    if (!relatoBase || complementos.every((c) => !c.resposta.trim())) return;
    const novas = complementos.filter((c) => c.resposta.trim());
    const acumuladas = [...respondidas, ...novas];
    try {
      setProcessando(true);
      setCompletando(true);
      const resultado = await api.acidentes.relatoTexto(obraId, relatoBase, [...acumuladas, ...(camposPreenchidos?.() ?? [])]);
      setRespondidas(acumuladas);
      receber(resultado, false);
    } catch (e) {
      aoErro(e instanceof Error ? e.message : 'A IA não conseguiu completar o registro agora.');
    } finally {
      setProcessando(false);
      setCompletando(false);
    }
  }

  const ocupado =
    processando || relatoVoz.estado !== 'parado' || respostaVoz.estado !== 'parado' || Boolean(desabilitado);
  const analisando = processando || relatoVoz.estado === 'transcrevendo' || respostaVoz.estado === 'transcrevendo';
  // Respondendo às perguntas (falando ou com a IA processando a resposta): as perguntas continuam à
  // vista, numeradas, para o técnico responder em sequência sem precisar decorá-las.
  const respondendo = respostaVoz.estado !== 'parado' || completando;

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: tokensUi.espaco.md }}>
      <div style={bloco}>
        {relatoVoz.estado === 'gravando' ? (
          <>
            <IndicadorGravacao segundos={relatoVoz.segundos} niveis={relatoVoz.niveis} />
            <Button icon={<RecordStop24Filled />} style={botaoParar} onClick={relatoVoz.parar}>
              Parar e preencher
            </Button>
            <Legenda>O áudio não é salvo. Só o texto volta para o formulário.</Legenda>
          </>
        ) : analisando ? (
          <Legenda>
            <Spinner size="extra-tiny" style={{ display: 'inline-flex', verticalAlign: 'middle', marginRight: 6 }} />
            A IA está analisando o relato e preenchendo o registro…
          </Legenda>
        ) : modoTexto ? (
          <div style={{ display: 'grid', gap: tokensUi.espaco.sm, width: '100%' }}>
            <Field label="Relato da ocorrência" dica="Escreva como contaria a alguém: a IA organiza o texto e preenche os campos do registro.">
              <Textarea
                value={textoRelato}
                onChange={(_, d) => setTextoRelato(d.value)}
                rows={6}
                resize="vertical"
                placeholder="O que aconteceu, onde, quando, quem estava envolvido, se houve lesão e qual atendimento foi prestado."
              />
            </Field>
            <div style={linhaAcoes}>
              <Button appearance="primary" icon={<Sparkle24Regular />} onClick={enviarTexto} disabled={!textoRelato.trim()}>
                Preencher com IA
              </Button>
              <Button onClick={() => setModoTexto(false)}>Cancelar</Button>
            </div>
          </div>
        ) : perguntas.length > 0 ? (
          // Com perguntas abertas, o microfone daqui começaria um relato do zero e perderia o original —
          // quem clica nele quer responder. Por isso a resposta (voz ou texto) fica só no quadro abaixo.
          <>
            <strong style={{ fontSize: 14 }}>Responda às perguntas abaixo</strong>
            <Legenda>Por voz ou por escrito. A IA completa o registro com as suas respostas.</Legenda>
            <div style={linhaAcoes}>
              <Button size="small" onClick={novoRelato} disabled={ocupado}>
                Começar novo relato
              </Button>
            </div>
          </>
        ) : (
          <>
            <strong style={{ fontSize: 14 }}>Relate a ocorrência</strong>
            <Legenda>
              {obraId
                ? 'Conte o que aconteceu: onde, quando, quem estava envolvido, se houve lesão e qual atendimento foi prestado. A IA preenche o registro para você revisar.'
                : 'Selecione a obra acima para relatar a ocorrência.'}
            </Legenda>
            <div style={linhaAcoes}>
              <Button appearance="primary" icon={<Mic24Regular />} onClick={relatoVoz.iniciar} disabled={!obraId || ocupado}>
                {relatoBase ? 'Relatar de novo' : 'Relatar por voz'}
              </Button>
              <Button onClick={() => setModoTexto(true)} disabled={!obraId || ocupado}>
                Escrever relato
              </Button>
            </div>
          </>
        )}
      </div>

      {perguntas.length > 0 && relatoVoz.estado === 'parado' && (!analisando || respondendo) && (
        <div style={quadroPerguntas}>
          <strong style={{ fontSize: 13, color: tokensUi.status.atencao.tinta }}>
            {perguntas.length === 1 ? 'A IA precisa de mais 1 informação' : `A IA precisa de mais ${perguntas.length} informações`}
          </strong>
          {respondendo ? (
            <>
              <ol style={{ margin: 0, paddingLeft: 20, display: 'grid', gap: tokensUi.espaco.sm, fontSize: 14, lineHeight: '20px' }}>
                {perguntas.map((p, i) => (
                  <li key={`${i}-${p.pergunta}`}>{p.pergunta}</li>
                ))}
              </ol>
              {respostaVoz.estado === 'gravando' ? (
                <>
                  <IndicadorGravacao segundos={respostaVoz.segundos} niveis={respostaVoz.niveis} />
                  <div style={linhaAcoes}>
                    <Button icon={<RecordStop24Filled />} style={botaoParar} onClick={respostaVoz.parar}>
                      Parar e completar
                    </Button>
                    <Legenda>Responda na ordem. Pode pular o que não souber.</Legenda>
                  </div>
                </>
              ) : (
                <Legenda>
                  <Spinner size="extra-tiny" style={{ display: 'inline-flex', verticalAlign: 'middle', marginRight: 6 }} />
                  Completando o registro com as suas respostas…
                </Legenda>
              )}
            </>
          ) : (
            <>
              {perguntas.map((p, i) => (
                <Field key={`${i}-${p.pergunta}`} label={p.pergunta} dica="Responda com o que souber. A IA usa a resposta para completar o registro.">
                  <Textarea
                    value={respostas[i] ?? ''}
                    onChange={(_, d) => setRespostas((atual) => ({ ...atual, [i]: d.value }))}
                    rows={1}
                    resize="vertical"
                    placeholder="Sua resposta"
                  />
                </Field>
              ))}
              <div style={linhaAcoes}>
                <Button
                  appearance="primary"
                  size="small"
                  onClick={() => void completar()}
                  disabled={ocupado || Object.values(respostas).every((r) => !r.trim())}
                >
                  Completar com as respostas
                </Button>
                <Button size="small" icon={<Mic24Regular />} onClick={respostaVoz.iniciar} disabled={ocupado}>
                  Responder por voz
                </Button>
                <Legenda>Pode deixar em branco o que não souber.</Legenda>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}
