const CHAVE_CONTA = 'sst.contaOffline';

export function identificarConta(headers?: Record<string, string>): string | undefined {
  const payload = headers?.Authorization?.replace(/^Bearer\s+/i, '').split('.')[1];
  if (!payload) return undefined;
  try {
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    return json.oid ? `${json.tid ?? ''}:${json.oid}` : undefined;
  } catch { return undefined; }
}

// Apenas a identidade, nunca senha, segredo ou cópia adicional do token.
export function lembrarConta(headers: Record<string, string>) {
  const conta = identificarConta(headers);
  if (conta) localStorage.setItem(CHAVE_CONTA, conta);
}

export function contaOffline(): string {
  const conta = localStorage.getItem(CHAVE_CONTA);
  if (conta) return conta;
  if (import.meta.env.DEV) return 'desenvolvimento';
  throw new Error('Entre com internet neste dispositivo antes de usar as inspeções offline.');
}
