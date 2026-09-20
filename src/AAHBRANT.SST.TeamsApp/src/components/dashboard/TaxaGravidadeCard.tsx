import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { Input, Text, makeStyles, mergeClasses } from '@fluentui/react-components';
import type { Acidente, RegistroHhtMensal } from '../../lib/api';
import { SstTooltip, StatusChip, designTokens, tokensUi, useTipografia, escalonado } from '@ui';

// Mesma casca visual do KpiCard (@ui): este card vive na grade de indicadores do Dashboard e
// precisa ler como irmão dos outros seis — mesmo padding (lg) e mesma borda de topo vinho, senão o
// 7º card da fileira destoa dos outros 6 (achado ao revisar o polimento visual do Dashboard, 19/09).
const useStyles = makeStyles({
  root: {
    backgroundColor: designTokens.colorSurface,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderTop: `3px solid ${designTokens.colorPrimary}`,
    borderRadius: tokensUi.raio.lg,
    boxShadow: designTokens.cardShadow,
    padding: '20px 16px',
    width: '100%',
    minHeight: '100%',
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'space-between',
    gap: '6px',
    boxSizing: 'border-box',
  },
  rotulo: { color: designTokens.colorNeutralMedium },
});

const CHAVE_META_LOCALSTORAGE = 'sst.tg.metaTaxaGravidade';

interface TaxaGravidadeCardProps {
  acidentes: Acidente[];
  registrosHht: RegistroHhtMensal[];
  /** Posição na grade de KPIs, para a entrada escalonada acompanhar os irmãos. */
  indice?: number;
}

// TG = (Dias Perdidos + Dias Debitados) × 1.000.000 / HHT — NBR 14280. Cálculo client-side,
// consistente com todos os outros KPIs do app (nenhum endpoint de agregação dedicado).
// A meta de comparação é um valor de negócio que este sistema não pode inventar — fica salva em
// localStorage, definida pelo próprio usuário no card (decisão de 2026-08-26).
export function TaxaGravidadeCard({ acidentes, registrosHht, indice = 0 }: TaxaGravidadeCardProps) {
  const casca = useStyles();
  const tipo = useTipografia();
  const [meta, setMeta] = useState<number | null>(null);
  const [editandoMeta, setEditandoMeta] = useState(false);
  const [rascunhoMeta, setRascunhoMeta] = useState('');

  useEffect(() => {
    try {
      const salvo = window.localStorage.getItem(CHAVE_META_LOCALSTORAGE);
      if (salvo) setMeta(Number(salvo));
    } catch {
      // localStorage indisponível (ex.: modo privado) — segue sem meta salva.
    }
  }, []);

  function salvarMeta() {
    const valor = Number(rascunhoMeta);
    if (!rascunhoMeta || Number.isNaN(valor) || valor <= 0) {
      setEditandoMeta(false);
      return;
    }
    setMeta(valor);
    try {
      window.localStorage.setItem(CHAVE_META_LOCALSTORAGE, String(valor));
    } catch {
      // Segue apenas em memória se localStorage indisponível.
    }
    setEditandoMeta(false);
  }

  const { diasPerdidos, diasDebitados, hht, taxaGravidade } = useMemo(() => {
    const perdidos = acidentes.reduce((soma, a) => soma + (a.diasAfastamento ?? 0), 0);
    const debitados = acidentes.reduce((soma, a) => soma + (a.diasDebitados ?? 0), 0);
    const horas = registrosHht.reduce((soma, r) => soma + r.horasHomemTrabalhadas, 0);
    const tg = horas > 0 ? ((perdidos + debitados) * 1_000_000) / horas : null;
    return { diasPerdidos: perdidos, diasDebitados: debitados, hht: horas, taxaGravidade: tg };
  }, [acidentes, registrosHht]);

  const dentroDaMeta = meta !== null && taxaGravidade !== null ? taxaGravidade <= meta : null;

  return (
    <motion.div variants={escalonado(indice)} initial="inicial" animate="visivel" className={casca.root}>
      <SstTooltip
        titulo="Taxa de Gravidade"
        conteudo={hht > 0 ? `HHT: ${hht.toLocaleString('pt-BR')} h` : 'Sem lançamento de HHT.'}
        detalhes={
          hht > 0
            ? [`Dias perdidos: ${diasPerdidos}`, `Dias debitados: ${diasDebitados}`, 'Base: NBR 14280']
            : ['Não é possível calcular sem horas-homem trabalhadas.']
        }
        relacao="description"
      >
        <div>
          <div className={tipo.display} style={{ color: designTokens.colorPrimary }}>
            {taxaGravidade !== null ? taxaGravidade.toFixed(2) : '—'}
          </div>
          <div className={mergeClasses(tipo.legenda, casca.rotulo)}>Taxa de Gravidade (NBR 14280)</div>
        </div>
      </SstTooltip>

      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        {renderBadgeMeta(dentroDaMeta)}

        {editandoMeta ? (
          <Input
            size="small"
            type="number"
            min={1}
            autoFocus
            value={rascunhoMeta}
            onChange={(_, d) => setRascunhoMeta(d.value)}
            onClick={(e) => e.stopPropagation()}
            onBlur={salvarMeta}
            onKeyDown={(e) => e.key === 'Enter' && salvarMeta()}
            style={{ width: 90 }}
          />
        ) : (
          <Text
            size={200}
            style={{ color: designTokens.colorNeutralMedium, cursor: 'pointer', textDecoration: 'underline' }}
            onClick={(e) => {
              e.stopPropagation();
              setRascunhoMeta(meta !== null ? String(meta) : '');
              setEditandoMeta(true);
            }}
          >
            {meta !== null ? `Meta: ${meta}` : 'Definir meta'}
          </Text>
        )}
      </div>
    </motion.div>
  );
}

function renderBadgeMeta(dentroDaMeta: boolean | null) {
  if (dentroDaMeta === null) return null;
  return dentroDaMeta ? (
    <StatusChip tom="ok">Dentro da meta</StatusChip>
  ) : (
    <StatusChip tom="alerta">Acima da meta</StatusChip>
  );
}
