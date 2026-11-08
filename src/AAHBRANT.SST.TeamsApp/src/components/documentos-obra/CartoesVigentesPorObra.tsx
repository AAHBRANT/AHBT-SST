import type { ReactNode } from 'react';
import { makeStyles, tokens } from '@fluentui/react-components';
import { Button, Card, StatusChip, type Tom } from '@ui';

// Grade "documento vigente por obra" (06/11/2026, mockup pgr-pcmso-geral_2026-11-06_v1): mesmo
// desenho para PGR e PCMSO. Verde = obra com documento vigente; laranja = obra em andamento sem
// documento vigente, com o próximo passo.
const useStyles = makeStyles({
  grade: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: '14px' },
  cartao: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderLeft: '4px solid #16a34a',
    borderRadius: '8px',
    padding: '14px 16px',
    backgroundColor: tokens.colorNeutralBackground1,
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
  },
  pendente: { borderLeft: '4px solid #d97706' },
  titulo: { fontWeight: 700, fontSize: '16px' },
  vigTexto: { fontSize: '12px', marginTop: '6px' },
  barra: { height: '6px', backgroundColor: tokens.colorNeutralStroke2, borderRadius: '4px', overflow: 'hidden' },
  vigDatas: { display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: tokens.colorNeutralForeground3 },
  dica: { fontSize: '11px', color: tokens.colorNeutralForeground3, backgroundColor: tokens.colorNeutralBackground2, borderRadius: '6px', padding: '6px 8px' },
  numeros: { display: 'flex', gap: '16px', fontSize: '12px' },
  obra: { color: tokens.colorNeutralForeground3, fontSize: '12px' },
  tags: { display: 'flex', gap: '6px', margin: '4px 0', flexWrap: 'wrap' },
  linha: {
    display: 'flex',
    justifyContent: 'space-between',
    gap: '12px',
    fontSize: '12px',
    paddingTop: '4px',
    borderTop: `1px solid ${tokens.colorNeutralStroke3}`,
  },
  valor: { fontWeight: 600, textAlign: 'right' },
  acoes: { display: 'flex', gap: '8px', marginTop: 'auto', paddingTop: '8px', alignItems: 'center', whiteSpace: 'nowrap' },
  vazio: { color: tokens.colorNeutralForeground3 },
});

export interface CartaoDocumentoObra {
  chave: string;
  titulo: string;
  obra: string;
  pendente?: boolean;
  tags: { rotulo: string; tom: Tom }[];
  linhas: { rotulo: string; valor: ReactNode }[];
  acao?: { rotulo: string; aoClicar: () => void; secundaria?: boolean };
  acaoExtra?: { rotulo: string; aoClicar: () => void };
  // Card de documento vigente (07/11/2026, mockup card-pgr-vigente_2026-11-07_v1): barra de vigência,
  // dica discreta e números rápidos. Com 60 dias ou menos para a revisão o card fica laranja.
  vigencia?: { inicio?: string | null; fim?: string | null };
  dica?: string;
  numeros?: { rotulo: string; valor: number }[];
}

const DIA = 86400000;
function diaLocal(iso: string) {
  return new Date(iso.slice(0, 10) + 'T00:00:00').getTime();
}
function infoVigencia(v: { inicio?: string | null; fim?: string | null }) {
  if (!v.fim) return null;
  const hoje = diaLocal(new Date().toISOString());
  const fim = diaLocal(v.fim);
  const inicio = v.inicio ? diaLocal(v.inicio) : fim - 365 * DIA;
  const dias = Math.round((fim - hoje) / DIA);
  const total = Math.max(fim - inicio, DIA);
  const pct = Math.min(100, Math.max(2, ((hoje - inicio) / total) * 100));
  return { dias, pct, perto: dias <= 60 };
}

export function dataBr(d?: string | null) {
  return d ? new Date(d.slice(0, 10) + 'T00:00:00').toLocaleDateString('pt-BR') : '—';
}

export function CartoesVigentesPorObra({
  titulo,
  subtitulo,
  cartoes,
  carregando,
  vazio,
}: {
  titulo: string;
  subtitulo?: string;
  cartoes: CartaoDocumentoObra[];
  carregando: boolean;
  vazio: string;
}) {
  const s = useStyles();
  return (
    <Card titulo={titulo} subtitulo={subtitulo}>
      {carregando ? (
        <div className={s.vazio}>Carregando…</div>
      ) : cartoes.length === 0 ? (
        <div className={s.vazio}>{vazio}</div>
      ) : (
        <div className={s.grade}>
          {cartoes.map((c) => {
            const vig = c.vigencia ? infoVigencia(c.vigencia) : null;
            const cor = vig?.perto ? '#d97706' : '#16a34a';
            return (
            <div key={c.chave} className={`${s.cartao} ${c.pendente || vig?.perto ? s.pendente : ''}`}>
              <div className={s.titulo}>{c.titulo}</div>
              <div className={s.obra}>{c.obra}</div>
              <div className={s.tags}>
                {c.tags.map((t) => (
                  <StatusChip key={t.rotulo} tom={t.tom}>
                    {t.rotulo}
                  </StatusChip>
                ))}
                {vig?.perto && (
                  <StatusChip tom="atencao">{vig.dias < 0 ? 'Revisão atrasada' : `Revisar em ${vig.dias} dias`}</StatusChip>
                )}
              </div>
              {vig && (
                <>
                  <div className={s.vigTexto}>
                    <b>Vigência</b> · {vig.dias < 0 ? `revisão passou há ${-vig.dias} dias` : `faltam ${vig.dias} dias`}
                  </div>
                  <div className={s.barra}>
                    <div style={{ width: `${vig.pct}%`, height: '100%', backgroundColor: cor }} />
                  </div>
                  <div className={s.vigDatas}>
                    <span>{dataBr(c.vigencia?.inicio)}</span>
                    <span>revisão {dataBr(c.vigencia?.fim)}</span>
                  </div>
                </>
              )}
              {c.dica && <div className={s.dica}>{c.dica}</div>}
              {c.numeros && c.numeros.length > 0 && (
                <div className={s.numeros}>
                  {c.numeros.map((n) => (
                    <span key={n.rotulo}>
                      <b>{n.valor}</b> {n.rotulo}
                    </span>
                  ))}
                </div>
              )}
              {c.linhas.map((l) => (
                <div key={l.rotulo} className={s.linha}>
                  <span>{l.rotulo}</span>
                  <span className={s.valor}>{l.valor}</span>
                </div>
              ))}
              {(c.acao || c.acaoExtra) && (
                <div className={s.acoes}>
                  {c.acao && (
                    <Button appearance={c.acao.secundaria ? 'secondary' : 'primary'} onClick={c.acao.aoClicar}>
                      {c.acao.rotulo}
                    </Button>
                  )}
                  {c.acaoExtra && (
                    <Button appearance="transparent" onClick={c.acaoExtra.aoClicar} style={{ color: '#670000', fontWeight: 600 }}>
                      {c.acaoExtra.rotulo}
                    </Button>
                  )}
                </div>
              )}
            </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}
