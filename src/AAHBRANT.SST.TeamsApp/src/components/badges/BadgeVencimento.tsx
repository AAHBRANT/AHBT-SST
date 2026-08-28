import { Badge } from '@fluentui/react-components';

// Painel visual de 3 níveis (PR-SST-003 — Saúde Ocupacional): vermelho = vencido, amarelo = vence em
// até 30 dias, verde = válido. Limiar de 30 dias é uma convenção própria (não veio explícito no
// PR-SST-003) — ajustar aqui centralizadamente se o usuário definir outro prazo.
const LIMIAR_ALERTA_DIAS = 30;

export type NivelVencimento = 'vencido' | 'alerta' | 'valido';

export function nivelVencimento(dataValidade?: string | null): NivelVencimento | null {
  if (!dataValidade) return null;
  const hoje = new Date(new Date().toDateString());
  const validade = new Date(dataValidade);
  const diasRestantes = Math.ceil((validade.getTime() - hoje.getTime()) / (1000 * 60 * 60 * 24));
  if (diasRestantes < 0) return 'vencido';
  if (diasRestantes <= LIMIAR_ALERTA_DIAS) return 'alerta';
  return 'valido';
}

const corPorNivel: Record<NivelVencimento, 'danger' | 'warning' | 'success'> = {
  vencido: 'danger',
  alerta: 'warning',
  valido: 'success',
};

const rotuloPorNivel: Record<NivelVencimento, string> = {
  vencido: 'Vencido',
  alerta: 'Vence em breve',
  valido: 'Válido',
};

export function BadgeVencimento({ dataValidade }: { dataValidade?: string | null }) {
  const nivel = nivelVencimento(dataValidade);
  if (!nivel) return null;
  return (
    <Badge color={corPorNivel[nivel]} appearance="tint" style={{ marginLeft: 8 }}>
      {rotuloPorNivel[nivel]}
    </Badge>
  );
}
