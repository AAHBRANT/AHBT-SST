import type { Tom } from '../../tokens/tokens';

// Regra de vencimento em 3 níveis (PR-SST-003): vermelho = vencido, amarelo = vence em até 30 dias,
// verde = válido. Copiada de BadgeVencimento.tsx, que passa a ser substituído por StatusChip.
export const LIMIAR_ALERTA_DIAS = 30;

export type NivelVencimento = 'vencido' | 'alerta' | 'valido';

export function nivelVencimento(dataValidade?: string | null): NivelVencimento | null {
  if (!dataValidade) return null;
  const hoje = new Date(new Date().toDateString());
  const validade = new Date(dataValidade);
  const diasRestantes = Math.ceil((validade.getTime() - hoje.getTime()) / 86_400_000);
  if (diasRestantes < 0) return 'vencido';
  if (diasRestantes <= LIMIAR_ALERTA_DIAS) return 'alerta';
  return 'valido';
}

export function tomDeVencimento(nivel: NivelVencimento): Tom {
  return nivel === 'vencido' ? 'alerta' : nivel === 'alerta' ? 'atencao' : 'ok';
}

export function rotuloDeVencimento(nivel: NivelVencimento): string {
  return nivel === 'vencido' ? 'Vencido' : nivel === 'alerta' ? 'Vence em breve' : 'Válido';
}
