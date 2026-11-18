import { useState, type KeyboardEvent } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { Dismiss24Regular, Info24Regular, Print24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  BotaoAcao,
  Button,
  Dialog,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Spinner,
  designTokens,
  tokensUi,
} from '@ui';
import { api, type AdesivoInspecaoInfo, type FormatoAdesivo } from '../../lib/api';
import {
  ADESIVO_GRADIENTE,
  ADESIVO_MARCA,
  ADESIVO_SUBTITULO,
  ADESIVO_TITULO,
  formatarDataAdesivo,
} from './adesivoVisual';
import { IconeSituacaoAdesivo } from './IconeSituacaoAdesivo';

// Diálogo "Imprimir adesivo" (mockup aprovado: docs/mockups/adesivo-inspecao-2026-11-17.html, seção 3).
// Escolhe o formato (etiqueta avulsa 99,1 x 67,7 mm ou folha A4 com 8 posições) e, na folha A4, em qual das 8
// posições (grade 2 colunas x 4 linhas) o adesivo vai — para reaproveitar uma folha já usada em parte.
// Ao confirmar, gera o PDF no servidor e entrega o arquivo a quem abriu o diálogo (visualizador da tela).

const MOBILE = '@media (max-width: 600px)';

const useStylesBotao = makeStyles({
  // Celular (mockup): alvo de toque de 48 px e botão em largura total.
  gatilho: { [MOBILE]: { width: '100%', minHeight: '48px' } },
});

// Botão do cabeçalho do detalhe da inspeção (estilo "ver", ao lado de Visualizar PDF e Baixar PDF).
export function BotaoImprimirAdesivo({ onClick }: { onClick: () => void }) {
  const e = useStylesBotao();
  return (
    <BotaoAcao tom="ver" icon={<Print24Regular />} onClick={onClick} aria-label="Imprimir adesivo" className={e.gatilho}>
      Imprimir adesivo
    </BotaoAcao>
  );
}

const useStyles = makeStyles({
  superficie: {
    width: '640px',
    maxWidth: 'calc(100vw - 24px)',
    padding: '24px',
    borderRadius: tokensUi.raio.lg,
    [MOBILE]: { padding: '18px' },
  },
  titulo: { fontSize: '20px', lineHeight: '26px', fontWeight: 700 },
  lead: { margin: '0 0 16px', color: designTokens.colorNeutralMedium, fontSize: '13px', lineHeight: '18px', fontWeight: 500 },
  opcoes: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: '12px',
    marginBottom: '16px',
    [MOBILE]: { gridTemplateColumns: '1fr' },
  },
  opcao: {
    display: 'flex',
    gap: '12px',
    alignItems: 'flex-start',
    textAlign: 'left',
    minHeight: '72px',
    padding: '14px',
    borderRadius: tokensUi.raio.md,
    border: `1.5px solid ${designTokens.colorCardBorder}`,
    backgroundColor: 'transparent',
    color: 'inherit',
    font: 'inherit',
    cursor: 'pointer',
    [MOBILE]: { minHeight: '48px' },
  },
  opcaoSel: {
    border: '1.5px solid var(--sst-acao-marca-tinta)',
    backgroundColor: 'var(--sst-acao-marca-wash)',
  },
  radio: {
    position: 'relative',
    width: '20px',
    height: '20px',
    borderRadius: '50%',
    border: `2px solid ${designTokens.colorNeutralMedium}`,
    flex: 'none',
    marginTop: '1px',
  },
  radioSel: {
    border: '2px solid var(--sst-acao-marca-tinta)',
    '::after': {
      content: '""',
      position: 'absolute',
      inset: '3px',
      borderRadius: '50%',
      backgroundColor: 'var(--sst-acao-marca-tinta)',
    },
  },
  opcaoTitulo: { display: 'block', fontSize: '14px', lineHeight: '20px', fontWeight: 700 },
  opcaoDetalhe: { display: 'block', fontSize: '12px', lineHeight: '16px', fontWeight: 500, color: designTokens.colorNeutralMedium },
  posicaoRotulo: { margin: '0 0 2px', fontSize: '12px', lineHeight: '16px', fontWeight: 700 },
  posicaoAjuda: { margin: '0 0 10px', fontSize: '12px', lineHeight: '16px', fontWeight: 500, color: designTokens.colorNeutralMedium, maxWidth: '236px' },
  // Folha A4 em miniatura: 2 colunas x 4 linhas, cada célula na proporção do adesivo (99,1 x 67,7 mm).
  folha: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: '6px',
    width: '236px',
    boxSizing: 'border-box',
    padding: '8px',
    borderRadius: tokensUi.raio.sm,
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorNeutralLight,
    [MOBILE]: { width: '100%', maxWidth: '260px' },
  },
  celula: {
    aspectRatio: '99.1 / 67.7',
    minHeight: '48px',
    borderRadius: '6px',
    border: `1.5px dashed ${designTokens.colorNeutralMedium}`,
    backgroundColor: designTokens.colorSurface,
    color: designTokens.colorNeutralDark,
    fontFamily: 'inherit',
    fontSize: '16px',
    fontWeight: 700,
    cursor: 'pointer',
    padding: 0,
  },
  celulaSel: {
    border: '2px solid var(--sst-acao-marca-tinta)',
    backgroundColor: 'var(--sst-acao-marca-wash)',
    color: 'var(--sst-acao-marca-tinta)',
  },
  area: { display: 'flex', gap: '16px', flexWrap: 'wrap', alignItems: 'flex-start', [MOBILE]: { flexDirection: 'column', alignItems: 'stretch' } },
  blocoFolha: { flex: 'none', [MOBILE]: { width: '100%' } },
  previa: {
    display: 'flex',
    justifyContent: 'center',
    flex: '1 1 260px',
    minWidth: 0,
    boxSizing: 'border-box',
    padding: '14px',
    overflow: 'hidden',
    borderRadius: tokensUi.raio.md,
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorNeutralLight,
  },
  aviso: {
    display: 'flex',
    gap: '10px',
    alignItems: 'flex-start',
    marginTop: '14px',
    padding: '12px 14px',
    borderRadius: tokensUi.raio.md,
    fontSize: '13px',
    lineHeight: '18px',
    fontWeight: 600,
  },
  avisoIcone: { width: '20px', height: '20px', flex: 'none', marginTop: '1px', display: 'flex' },
  avisoInfo: { backgroundColor: tokensUi.status.info.fundo, color: tokensUi.status.info.tinta },
  avisoAlerta: { backgroundColor: tokensUi.status.alerta.fundo, color: tokensUi.status.alerta.tinta },
  avisoTexto: { color: designTokens.colorNeutralDark },
  acoes: {
    display: 'flex',
    justifyContent: 'flex-end',
    gap: '8px',
    marginTop: '20px',
    paddingTop: '16px',
    borderTop: `1px solid ${tokensUi.bordaSuave}`,
    [MOBILE]: { flexDirection: 'column-reverse' },
  },
  botaoLargo: { [MOBILE]: { width: '100%', minHeight: '48px' } },
  // ---- Miniatura do adesivo (mm reais, reduzida com zoom) ----
  esc: { zoom: 0.5, [MOBILE]: { zoom: 0.4 } },
  adesivo: {
    position: 'relative',
    display: 'flex',
    flexDirection: 'column',
    flex: 'none',
    width: '99.1mm',
    height: '67.7mm',
    overflow: 'hidden',
    borderRadius: '2.2mm',
    backgroundColor: '#fff',
    color: '#111',
    boxShadow: '0 0 0 .25mm #bbb, 0 1.2mm 3mm rgba(0,0,0,.18)',
  },
  adesivoBloq: {
    '::after': {
      content: '""',
      position: 'absolute',
      inset: 0,
      border: '1.3mm solid #dc2626',
      borderRadius: '2.2mm',
      pointerEvents: 'none',
    },
  },
  adCab: {
    flex: 'none',
    height: '10.5mm',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'flex-end',
    padding: '0 3.5mm',
    backgroundColor: '#fff',
    color: ADESIVO_MARCA,
    borderBottom: `.7mm solid ${ADESIVO_MARCA}`,
    fontSize: '2.5mm',
    lineHeight: 1,
    fontWeight: 700,
    letterSpacing: '.2mm',
    textTransform: 'uppercase',
  },
  adSt: { flex: 'none', height: '15.5mm', display: 'flex', alignItems: 'center', gap: '3mm', padding: '0 3.5mm', color: '#fff' },
  adStTitulo: { display: 'block', fontSize: '7.6mm', lineHeight: 1, fontWeight: 900, letterSpacing: '.2mm' },
  adStSub: { display: 'block', marginTop: '1mm', fontSize: '2.6mm', lineHeight: 1.1, fontWeight: 700, letterSpacing: '.35mm', textTransform: 'uppercase', opacity: 0.95 },
  adCorpo: { flex: 1, minHeight: 0, display: 'flex', gap: '2.5mm', padding: '2.2mm 3.5mm 0' },
  adInfo: { flex: 1, minWidth: 0 },
  adObj: { fontSize: '5.2mm', lineHeight: 1, fontWeight: 900, letterSpacing: '.15mm', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' },
  adDl: { display: 'grid', gridTemplateColumns: 'auto 1fr', columnGap: '2mm', rowGap: '.75mm', alignItems: 'baseline', margin: '1.6mm 0 0' },
  adDt: { fontSize: '2.1mm', lineHeight: 1.2, fontWeight: 700, color: '#777', textTransform: 'uppercase', letterSpacing: '.15mm', whiteSpace: 'nowrap' },
  adDd: { margin: 0, minWidth: 0, fontSize: '2.8mm', lineHeight: 1.2, fontWeight: 600, color: '#111', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  adDdDest: { fontWeight: 900 },
  adQr: { flex: 'none', width: '24mm', textAlign: 'center' },
  adQrCaixa: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: '22mm',
    height: '22mm',
    margin: '0 auto',
    boxSizing: 'border-box',
    border: '.4mm solid #111',
    backgroundColor: '#fff',
    color: '#111',
    fontSize: '4mm',
    fontWeight: 900,
  },
  adQrLegenda: { display: 'block', marginTop: '.8mm', fontSize: '2.2mm', lineHeight: 1.15, fontWeight: 700 },
  adRod: {
    flex: 'none',
    height: '6mm',
    display: 'flex',
    alignItems: 'center',
    padding: '0 3.5mm',
    backgroundColor: '#f1f0f5',
    borderTop: '.3mm solid #d8d6e0',
    fontSize: '2.1mm',
    lineHeight: 1.15,
    fontWeight: 600,
    color: '#444',
  },
});

export interface ImprimirAdesivoDialogProps {
  aberto: boolean;
  onFechar: () => void;
  inspecaoId: string;
  adesivo: AdesivoInspecaoInfo;
  /** Protocolo da inspeção (numeroDocumento), mostrado no cabeçalho do diálogo e na miniatura. */
  protocolo?: string | null;
  dataInspecao?: string | null;
  inspetorNome?: string | null;
  /** Recebe o PDF gerado (a tela abre no visualizador, porque o Teams bloqueia window.open). */
  onGerado: (pdf: Blob, formato: FormatoAdesivo) => void;
}

const POSICOES = [1, 2, 3, 4, 5, 6, 7, 8];

// Setas navegam entre os radios do grupo (padrão WAI-ARIA): move o foco e seleciona o vizinho (com volta ao início/fim).
function navegarComSetas(e: KeyboardEvent<HTMLElement>) {
  const delta = e.key === 'ArrowRight' || e.key === 'ArrowDown' ? 1 : e.key === 'ArrowLeft' || e.key === 'ArrowUp' ? -1 : 0;
  if (!delta) return;
  const radios = Array.from(e.currentTarget.parentElement?.querySelectorAll<HTMLElement>('[role="radio"]') ?? []);
  const atual = radios.indexOf(e.currentTarget);
  if (atual < 0) return;
  e.preventDefault();
  const proximo = radios[(atual + delta + radios.length) % radios.length];
  proximo.focus();
  proximo.click();
}

export function ImprimirAdesivoDialog({
  aberto, onFechar, inspecaoId, adesivo, protocolo, dataInspecao, inspetorNome, onGerado,
}: ImprimirAdesivoDialogProps) {
  const s = useStyles();
  const [formato, setFormato] = useState<FormatoAdesivo>('Etiqueta');
  const [posicao, setPosicao] = useState(1);
  const [gerando, setGerando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const bloqueio = adesivo.situacao === 'NaoLiberado';

  async function imprimir() {
    setGerando(true);
    setErro(null);
    try {
      const pdf = await api.inspecoes.gerarAdesivo(inspecaoId, formato === 'A4' ? { formato, posicao } : { formato });
      onGerado(pdf, formato);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível gerar o adesivo.');
    } finally {
      setGerando(false);
    }
  }

  const proxima = adesivo.proximaInspecaoEm
    ? formatarDataAdesivo(adesivo.proximaInspecaoEm)
    : bloqueio
      ? 'Reinspeção antes do uso'
      : null;

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => { if (!d.open && !gerando) onFechar(); }}>
      <DialogSurface className={s.superficie} aria-describedby={undefined}>
        <DialogBody style={{ display: 'block' }}>
          <DialogTitle
            className={s.titulo}
            action={
              <Button appearance="subtle" aria-label="Fechar" icon={<Dismiss24Regular />} onClick={onFechar} disabled={gerando} />
            }
          >
            Imprimir adesivo
          </DialogTitle>
          <DialogContent>
            <p className={s.lead}>
              {[protocolo, adesivo.objetoIdentificacao, dataInspecao ? `concluída em ${formatarDataAdesivo(dataInspecao)}` : null]
                .filter(Boolean)
                .join(' · ')}
            </p>

            <div className={s.opcoes} role="radiogroup" aria-label="Formato do adesivo">
              <button
                type="button"
                role="radio"
                aria-checked={formato === 'Etiqueta'}
                tabIndex={formato === 'Etiqueta' ? 0 : -1}
                onKeyDown={navegarComSetas}
                className={mergeClasses(s.opcao, formato === 'Etiqueta' && s.opcaoSel)}
                onClick={() => setFormato('Etiqueta')}
              >
                <i aria-hidden="true" className={mergeClasses(s.radio, formato === 'Etiqueta' && s.radioSel)} />
                <span>
                  <b className={s.opcaoTitulo}>Etiqueta avulsa</b>
                  <span className={s.opcaoDetalhe}>99,1 × 67,7 mm · 1 adesivo</span>
                </span>
              </button>
              <button
                type="button"
                role="radio"
                aria-checked={formato === 'A4'}
                tabIndex={formato === 'A4' ? 0 : -1}
                onKeyDown={navegarComSetas}
                className={mergeClasses(s.opcao, formato === 'A4' && s.opcaoSel)}
                onClick={() => setFormato('A4')}
              >
                <i aria-hidden="true" className={mergeClasses(s.radio, formato === 'A4' && s.radioSel)} />
                <span>
                  <b className={s.opcaoTitulo}>Folha A4</b>
                  <span className={s.opcaoDetalhe}>8 por folha · escolha a posição inicial</span>
                </span>
              </button>
            </div>

            <div className={s.area}>
              {formato === 'A4' && (
                <div className={s.blocoFolha}>
                  <p className={s.posicaoRotulo}>Posição na folha</p>
                  <p className={s.posicaoAjuda}>Escolha o espaço livre para reaproveitar uma folha já usada.</p>
                  <div className={s.folha} role="radiogroup" aria-label="Posição do adesivo na folha A4">
                    {POSICOES.map((n) => (
                      <button
                        key={n}
                        type="button"
                        role="radio"
                        aria-checked={posicao === n}
                        tabIndex={posicao === n ? 0 : -1}
                        onKeyDown={navegarComSetas}
                        aria-label={`Posição ${n}, linha ${Math.ceil(n / 2)}, coluna ${n % 2 === 1 ? 1 : 2}`}
                        className={mergeClasses(s.celula, posicao === n && s.celulaSel)}
                        onClick={() => setPosicao(n)}
                      >
                        {n}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              <div className={s.previa}>
                <div className={s.esc}>
                  <div className={mergeClasses(s.adesivo, bloqueio && s.adesivoBloq)} role="img" aria-label={`Pré-visualização do adesivo: ${ADESIVO_TITULO[adesivo.situacao]}, ${adesivo.objetoIdentificacao}`}>
                    <div className={s.adCab}>{adesivo.tema}</div>
                    <div className={s.adSt} style={{ backgroundImage: ADESIVO_GRADIENTE[adesivo.situacao] }}>
                      <IconeSituacaoAdesivo situacao={adesivo.situacao} tamanho="9.5mm" />
                      <div>
                        <b className={s.adStTitulo}>{ADESIVO_TITULO[adesivo.situacao]}</b>
                        <span className={s.adStSub}>{ADESIVO_SUBTITULO[adesivo.situacao]}</span>
                      </div>
                    </div>
                    <div className={s.adCorpo}>
                      <div className={s.adInfo}>
                        <div className={s.adObj}>{adesivo.objetoIdentificacao}</div>
                        <dl className={s.adDl}>
                          <dt className={s.adDt}>Obra</dt>
                          <dd className={s.adDd}>{adesivo.obraNome}</dd>
                          {dataInspecao && (
                            <>
                              <dt className={s.adDt}>Inspeção</dt>
                              <dd className={s.adDd}>{formatarDataAdesivo(dataInspecao)}</dd>
                            </>
                          )}
                          {proxima && (
                            <>
                              <dt className={s.adDt}>{adesivo.proximaInspecaoEm ? 'Próxima até' : 'Próxima'}</dt>
                              <dd className={mergeClasses(s.adDd, s.adDdDest)}>{proxima}</dd>
                            </>
                          )}
                          {inspetorNome && (
                            <>
                              <dt className={s.adDt}>Inspetor</dt>
                              <dd className={s.adDd}>{inspetorNome}</dd>
                            </>
                          )}
                          {protocolo && (
                            <>
                              <dt className={s.adDt}>Protocolo</dt>
                              <dd className={s.adDd}>{protocolo}</dd>
                            </>
                          )}
                        </dl>
                      </div>
                      <div className={s.adQr}>
                        <div className={s.adQrCaixa} aria-hidden="true">QR</div>
                        <span className={s.adQrLegenda}>Confira a situação atual</span>
                      </div>
                    </div>
                    <div className={s.adRod}>
                      <span>
                        <b style={{ color: ADESIVO_MARCA }}>Inspeção interna de SST.</b>{' '}
                        {/extintor/i.test(adesivo.tema) ? 'Não substitui etiqueta de manutenção de extintor.' : 'Leia o QR para conferir a situação atual.'}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {bloqueio ? (
              <div className={mergeClasses(s.aviso, s.avisoAlerta)} role="alert">
                <span className={s.avisoIcone}><Warning24Regular /></span>
                <span className={s.avisoTexto}>
                  <b style={{ color: tokensUi.status.alerta.tinta }}>Este adesivo bloqueia o uso do objeto.</b> Cole-o no local visível,
                  retire o objeto de operação e avise o responsável da obra. Ao reinspecionar e liberar, imprima um novo adesivo.
                </span>
              </div>
            ) : (
              <div className={mergeClasses(s.aviso, s.avisoInfo)}>
                <span className={s.avisoIcone}><Info24Regular /></span>
                <span className={s.avisoTexto}>
                  Adesivo de inspeção interna de SST. O QR sempre mostra a situação atual do objeto.
                </span>
              </div>
            )}

            {erro && (
              <div style={{ marginTop: 14 }}>
                <FeedbackInline tom="erro">{erro}</FeedbackInline>
              </div>
            )}

            <div className={s.acoes}>
              <Button appearance="secondary" className={s.botaoLargo} onClick={onFechar} disabled={gerando}>
                Cancelar
              </Button>
              <BotaoAcao
                tom="baixar"
                aria-label={bloqueio ? 'Imprimir adesivo de bloqueio' : 'Imprimir'}
                icon={gerando ? <Spinner size="tiny" /> : <Print24Regular />}
                className={s.botaoLargo}
                onClick={() => void imprimir()}
                disabled={gerando}
              >
                {bloqueio ? 'Imprimir adesivo de bloqueio' : 'Imprimir'}
              </BotaoAcao>
            </div>
          </DialogContent>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
