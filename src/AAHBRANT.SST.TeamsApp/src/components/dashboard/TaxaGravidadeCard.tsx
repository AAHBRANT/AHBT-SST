import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { Badge, Input, Text, Tooltip } from '@fluentui/react-components';
import type { Acidente, RegistroHhtMensal } from '../../lib/api';
import { usePageStyles } from '../../pages/pageStyles';
import { useDashboardStyles } from './dashboardStyles';
import { designTokens } from '../../theme';

const CHAVE_META_LOCALSTORAGE = 'sst.tg.metaTaxaGravidade';

interface TaxaGravidadeCardProps {
  acidentes: Acidente[];
  registrosHht: RegistroHhtMensal[];
}

// TG = (Dias Perdidos + Dias Debitados) × 1.000.000 / HHT — NBR 14280. Cálculo client-side,
// consistente com todos os outros KPIs do app (nenhum endpoint de agregação dedicado).
// A meta de comparação é um valor de negócio que este sistema não pode inventar — fica salva em
// localStorage, definida pelo próprio usuário no card (decisão de 2026-08-26).
export function TaxaGravidadeCard({ acidentes, registrosHht }: TaxaGravidadeCardProps) {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();
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
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className={estilosPagina.card}
    >
      <Tooltip
        content={
          hht > 0
            ? `HHT: ${hht.toLocaleString('pt-BR')} h · Dias perdidos: ${diasPerdidos} · Dias debitados: ${diasDebitados}`
            : 'Sem lançamento de HHT — não é possível calcular a Taxa de Gravidade.'
        }
        relationship="description"
      >
        <div>
          <div className={estilos.kpiValor} style={{ color: designTokens.colorPrimary }}>
            {taxaGravidade !== null ? taxaGravidade.toFixed(2) : '—'}
          </div>
          <div className={estilos.kpiRotulo}>Taxa de Gravidade (NBR 14280)</div>
        </div>
      </Tooltip>

      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
        {renderBadgeMeta(dentroDaMeta)}

        {editandoMeta ? (
          <Input
            size="small"
            type="number"
            min={1}
            autoFocus
            value={rascunhoMeta}
            onChange={(_, d) => setRascunhoMeta(d.value)}
            onBlur={salvarMeta}
            onKeyDown={(e) => e.key === 'Enter' && salvarMeta()}
            style={{ width: 90 }}
          />
        ) : (
          <Text
            size={200}
            style={{ color: designTokens.colorNeutralMedium, cursor: 'pointer', textDecoration: 'underline' }}
            onClick={() => {
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
    <Badge appearance="filled" color="success">
      Dentro da meta
    </Badge>
  ) : (
    <Badge appearance="filled" color="danger">
      Acima da meta
    </Badge>
  );
}
