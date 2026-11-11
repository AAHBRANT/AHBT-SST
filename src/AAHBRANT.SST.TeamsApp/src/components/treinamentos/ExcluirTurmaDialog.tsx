import { useEffect, useState } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { Delete24Regular, LockClosed24Regular } from '@fluentui/react-icons';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Input,
  Carregando,
  designTokens,
  tokensUi,
} from '@ui';
import { api, type PreviaExclusaoSessaoTreinamento, type ResultadoExclusaoTurma, type SessaoTreinamento } from '../../lib/api';

const useStyles = makeStyles({
  // Mockup turma-excluir-2026-11-10: "Excluir turma" é secundário (contorno, texto vermelho); só o botão de
  // confirmar do diálogo é vermelho sólido.
  botaoContorno: {
    backgroundColor: 'transparent',
    color: tokensUi.status.alerta.tinta,
    border: `1px solid color-mix(in srgb, ${tokensUi.status.alerta.tinta} 55%, transparent)`,
    ':hover': { backgroundColor: tokensUi.status.alerta.fundo, color: tokensUi.status.alerta.tinta },
  },
  confirmar: {
    backgroundColor: tokensUi.status.alerta.tinta,
    color: designTokens.colorWhite,
    ':hover': { backgroundColor: tokensUi.status.alerta.tinta, color: designTokens.colorWhite, filter: 'brightness(0.92)' },
  },
  cabecalho: { display: 'flex', alignItems: 'center', gap: '12px' },
  icone: {
    width: '40px',
    height: '40px',
    borderRadius: '50%',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  iconeErro: { backgroundColor: tokensUi.status.alerta.fundo, color: tokensUi.status.alerta.tinta },
  iconeAtencao: { backgroundColor: tokensUi.status.atencao.fundo, color: tokensUi.status.atencao.tinta },
  corpo: { display: 'flex', flexDirection: 'column', gap: '12px' },
  caixa: {
    border: `1px solid ${tokensUi.bordaSuave}`,
    borderRadius: tokensUi.raio.sm,
    overflow: 'hidden',
  },
  caixaTitulo: {
    padding: '8px 12px',
    fontSize: '11px',
    fontWeight: 700,
    letterSpacing: '0.06em',
    textTransform: 'uppercase',
    color: designTokens.colorNeutralMedium,
    borderBottom: `1px solid ${tokensUi.bordaSuave}`,
  },
  caixaLinha: {
    display: 'flex',
    justifyContent: 'space-between',
    gap: '10px',
    padding: '8px 12px',
    borderBottom: `1px solid ${tokensUi.bordaSuave}`,
    ':last-child': { borderBottom: 'none' },
  },
  zerado: { color: designTokens.colorNeutralMedium },
  numero: { fontVariantNumeric: 'tabular-nums', fontWeight: 700 },
});

// Botão "Excluir turma" do cabeçalho do detalhe (e reaproveitado pela lista): contorno vermelho, secundário.
export function BotaoExcluirTurma({ onClick, children = 'Excluir turma', disabled }: { onClick: () => void; children?: string; disabled?: boolean }) {
  const e = useStyles();
  return (
    <Button appearance="secondary" className={e.botaoContorno} icon={<Delete24Regular />} onClick={onClick} disabled={disabled}>
      {children}
    </Button>
  );
}

export type AlvoExclusaoTurma =
  | { tipo: 'unica'; turmaId: string }
  | { tipo: 'lote'; turmas: SessaoTreinamento[] };

interface Props {
  alvo: AlvoExclusaoTurma | null;
  aoFechar: () => void;
  // Chamado depois que ao menos uma turma foi realmente excluída (a tela recarrega ou volta para a lista).
  aoExcluir: (resultados: ResultadoExclusaoTurma[]) => void;
}

function plural(n: number, singular: string, pluralTxt: string) {
  return `${n} ${n === 1 ? singular : pluralTxt}`;
}

function dataBr(iso: string) {
  return iso.slice(0, 10).split('-').reverse().join('/');
}

// Confirmação da exclusão definitiva (mockup turma-excluir-2026-11-10, aprovado): mostra o que vai junto, pede para
// digitar EXCLUIR (ou "EXCLUIR N" no lote) e, quando a turma não pode ser excluída, mostra o motivo no lugar da confirmação.
export function ExcluirTurmaDialog({ alvo, aoFechar, aoExcluir }: Props) {
  const e = useStyles();
  const [previa, setPrevia] = useState<PreviaExclusaoSessaoTreinamento | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [digitado, setDigitado] = useState('');
  const [excluindo, setExcluindo] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultadoLote, setResultadoLote] = useState<ResultadoExclusaoTurma[] | null>(null);

  const turmaId = alvo?.tipo === 'unica' ? alvo.turmaId : null;
  const turmasLote = alvo?.tipo === 'lote' ? alvo.turmas : null;

  useEffect(() => {
    setDigitado('');
    setErro(null);
    setPrevia(null);
    setResultadoLote(null);
    if (!turmaId) return;
    let ativo = true;
    setCarregando(true);
    api.sessoesTreinamento
      .previaExclusao(turmaId)
      .then((p) => ativo && setPrevia(p))
      .catch((ex) => ativo && setErro(ex instanceof Error ? ex.message : 'Não foi possível consultar a turma.'))
      .finally(() => ativo && setCarregando(false));
    return () => {
      ativo = false;
    };
  }, [turmaId, turmasLote]);

  if (!alvo) return null;

  const lote = alvo.tipo === 'lote';
  const palavra = lote ? `EXCLUIR ${alvo.turmas.length}` : 'EXCLUIR';
  const bloqueada = !lote && previa !== null && !previa.podeExcluir;
  const somaLote = lote
    ? {
        inscricoes: alvo.turmas.reduce((a, t) => a + t.totalParticipantes, 0),
        presencas: alvo.turmas.reduce((a, t) => a + t.totalPresencasConfirmadas, 0),
        fotos: alvo.turmas.reduce((a, t) => a + t.totalFotosEvidencia, 0),
      }
    : null;
  const contagens = lote
    ? somaLote!
    : { inscricoes: previa?.inscricoes ?? 0, presencas: previa?.presencas ?? 0, fotos: previa?.fotosEvidencia ?? 0 };

  async function excluir() {
    if (!alvo) return;
    try {
      setExcluindo(true);
      setErro(null);
      if (alvo.tipo === 'unica') {
        await api.sessoesTreinamento.excluir(alvo.turmaId);
        aoExcluir([{ id: alvo.turmaId, resultado: 'excluida' }]);
        return;
      }
      const resultados = await api.sessoesTreinamento.excluirLote(alvo.turmas.map((t) => t.id));
      if (resultados.every((r) => r.resultado === 'excluida')) {
        aoExcluir(resultados);
        return;
      }
      // Houve turma que não pôde sair: mostra o motivo de cada uma antes de fechar (as outras já foram excluídas).
      setResultadoLote(resultados);
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível excluir. Nada foi apagado.');
    } finally {
      setExcluindo(false);
    }
  }

  function fecharResultadoLote() {
    const resultados = resultadoLote ?? [];
    setResultadoLote(null);
    if (resultados.some((r) => r.resultado === 'excluida')) aoExcluir(resultados);
    else aoFechar();
  }

  function rotuloTurma(id: string) {
    const t = alvo?.tipo === 'lote' ? alvo.turmas.find((x) => x.id === id) : undefined;
    return t ? `${t.numeroCertificado ?? ''} · ${t.cursoTreinamentoNome}`.replace(/^ · /, '') : id;
  }

  const titulo = resultadoLote
    ? 'Resultado da exclusão'
    : bloqueada
      ? 'Esta turma não pode ser excluída'
      : lote
        ? `Excluir ${plural(alvo.turmas.length, 'turma', 'turmas')} definitivamente?`
        : 'Excluir esta turma definitivamente?';

  return (
    <Dialog open onOpenChange={(_, d) => { if (d.open || excluindo) return;
      if (resultadoLote) fecharResultadoLote();
      else aoFechar(); }}>
      <DialogSurface aria-label={titulo}>
        <DialogBody>
          <DialogTitle>
            <span className={e.cabecalho}>
              <span className={`${e.icone} ${bloqueada || resultadoLote ? e.iconeAtencao : e.iconeErro}`} aria-hidden="true">
                {bloqueada || resultadoLote ? <LockClosed24Regular /> : <Delete24Regular />}
              </span>
              {titulo}
            </span>
          </DialogTitle>
          <DialogContent>
            <div className={e.corpo}>
              {carregando && <Carregando variante="lista" linhas={3} />}
              {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

              {resultadoLote && (
                <>
                  <p style={{ margin: 0 }}>
                    {plural(resultadoLote.filter((r) => r.resultado === 'excluida').length, 'turma excluída', 'turmas excluídas')}.
                    {' '}As demais não puderam ser excluídas e continuam como estavam:
                  </p>
                  <div className={e.caixa}>
                    <div className={e.caixaTitulo}>Não excluídas</div>
                    {resultadoLote.filter((r) => r.resultado !== 'excluida').map((r) => (
                      <div key={r.id} className={e.caixaLinha} style={{ flexDirection: 'column', gap: 2 }}>
                        <b>{rotuloTurma(r.id)}</b>
                        <span className={e.zerado}>{r.motivo}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}

              {bloqueada && previa && (
                <>
                  <p style={{ margin: 0 }}>{previa.motivo}</p>
                  <div className={e.caixa}>
                    <div className={e.caixaTitulo}>Como seguir</div>
                    {previa.certificadosEmitidos > 0 ? (
                      <>
                        <div className={e.caixaLinha}>
                          <span>Se os certificados foram emitidos por engano, procure a SST: eles precisam ser tratados antes.</span>
                        </div>
                        <div className={e.caixaLinha}>
                          <span>Sem nenhum certificado emitido, a turma pode ser excluída.</span>
                        </div>
                      </>
                    ) : (
                      <div className={e.caixaLinha}>
                        <span>Procure a SST: o documento assinado precisa ser tratado antes de a turma ser excluída.</span>
                      </div>
                    )}
                  </div>
                </>
              )}

              {!resultadoLote && !bloqueada && !carregando && (lote || previa) && (
                <>
                  {lote ? (
                    <>
                      <p style={{ margin: 0 }}>
                        As turmas abaixo serão <b>apagadas do banco</b>. Não há como recuperar nem refazer a lista de presença.
                      </p>
                      <div className={e.caixa}>
                        <div className={e.caixaTitulo}>Turmas</div>
                        {alvo.turmas.map((t) => (
                          <div key={t.id} className={e.caixaLinha}>
                            <span>{t.numeroCertificado} · {t.cursoTreinamentoNome}</span>
                          </div>
                        ))}
                      </div>
                    </>
                  ) : (
                    previa && (
                      <p style={{ margin: 0 }}>
                        <b>{previa.numeroCertificado} · {previa.cursoNome}</b>, {previa.obraNome}, {dataBr(previa.dataRealizacao)}. A turma será{' '}
                        <b>apagada do banco</b>. Não há como recuperar.
                      </p>
                    )
                  )}
                  <div className={e.caixa}>
                    <div className={e.caixaTitulo}>O que vai junto</div>
                    <div className={e.caixaLinha}><span>Inscrições</span><span className={e.numero}>{contagens.inscricoes}</span></div>
                    <div className={e.caixaLinha}>
                      <span className={contagens.presencas === 0 ? e.zerado : undefined}>Presenças</span>
                      <span className={`${e.numero} ${contagens.presencas === 0 ? e.zerado : ''}`}>{contagens.presencas}</span>
                    </div>
                    <div className={e.caixaLinha}>
                      <span className={contagens.fotos === 0 ? e.zerado : undefined}>Fotos de evidência</span>
                      <span className={`${e.numero} ${contagens.fotos === 0 ? e.zerado : ''}`}>{contagens.fotos}</span>
                    </div>
                  </div>
                  <FeedbackInline tom="erro">
                    Ação definitiva e irreversível.{lote ? '' : ' Se a turma ainda vai acontecer, prefira não excluir.'}
                  </FeedbackInline>
                  <div>
                    <label htmlFor="excluir-turma-confirmacao" style={{ display: 'block', fontSize: 13, marginBottom: 6 }}>
                      Para confirmar, digite <code style={{ fontWeight: 700 }}>{palavra}</code>
                    </label>
                    <Input
                      id="excluir-turma-confirmacao"
                      style={{ width: '100%' }}
                      size="large"
                      autoComplete="off"
                      placeholder={palavra}
                      value={digitado}
                      onChange={(_, d) => setDigitado(d.value)}
                      disabled={excluindo}
                    />
                  </div>
                </>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            {resultadoLote || bloqueada ? (
              <Button appearance="primary" onClick={resultadoLote ? fecharResultadoLote : aoFechar}>
                Entendi
              </Button>
            ) : (
              <>
                <Button appearance="secondary" onClick={aoFechar} disabled={excluindo}>Cancelar</Button>
                <Button
                  appearance="secondary"
                  className={e.confirmar}
                  icon={<Delete24Regular />}
                  onClick={excluir}
                  disabled={excluindo || carregando || digitado !== palavra || (!lote && !previa)}
                >
                  {lote ? `Excluir ${plural(alvo.turmas.length, 'turma', 'turmas')}` : 'Excluir turma'}
                </Button>
              </>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
