// Regras puras do botão "Ver ao vivo" e do contador (sem React nem API: testável em tests-unit/).

// O ao vivo existe a partir do serviço da obra 1.1.0 (mesma regra do servidor).
const VERSAO_MINIMA = [1, 1, 0] as const;

// true/false quando a versão é legível; null quando o agente ainda não informou versão (o servidor decide pelo 409).
export function versaoAtendeAoVivo(versao: string | null | undefined): boolean | null {
  const m = versao ? /^\s*v?(\d+)\.(\d+)(?:\.(\d+))?/.exec(versao) : null;
  if (!m) return null;
  const partes = [Number(m[1]), Number(m[2]), Number(m[3] ?? 0)];
  for (let i = 0; i < 3; i += 1) {
    if (partes[i] !== VERSAO_MINIMA[i]) return partes[i] > VERSAO_MINIMA[i];
  }
  return true;
}

export type BloqueioAoVivo = 'senha' | 'agente' | 'versao';

export interface DadosBloqueioAoVivo {
  equipamentoSemSenha: boolean;
  agenteSemContato: boolean;
  versaoDoAgente: string | null | undefined;
}

// Primeiro motivo que impede de abrir; nulo = pode abrir. Sem a lista de agentes ou equipamentos carregada, não bloqueia:
// o servidor responde pelos códigos e a página explica.
export function bloqueioAoVivo(d: DadosBloqueioAoVivo): BloqueioAoVivo | null {
  if (d.equipamentoSemSenha) return 'senha';
  if (d.agenteSemContato) return 'agente';
  if (versaoAtendeAoVivo(d.versaoDoAgente) === false) return 'versao';
  return null;
}

export const TEXTOS_BLOQUEIO_AO_VIVO: Record<BloqueioAoVivo, { nota: string; balao: string }> = {
  agente: {
    nota: 'Agente da obra sem contato. Veja se o PC da obra está ligado e com internet.',
    balao: 'Agente da obra sem contato. Veja se o PC da obra está ligado.',
  },
  versao: {
    nota: 'O serviço da obra precisa ser atualizado para mostrar a imagem ao vivo.',
    balao: 'O serviço da obra precisa ser atualizado.',
  },
  senha: { nota: 'Falta a senha do equipamento.', balao: 'Falta a senha do equipamento.' },
};

// "6:40" (minutos sem zero à esquerda, segundos com dois dígitos).
export function formatarContagem(segundos: number): string {
  const s = Math.max(0, Math.floor(segundos));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

// Texto do aviso no Telegram de uma ocorrência. Câmera com o aviso desligado nunca envia: isso não é falha, então o
// texto é neutro (tom "neutro") e não "Alerta não enviado". Sem o dado da câmera, vale o comportamento de antes.
export function rotuloAlertaTelegram(
  alertaEnviado: boolean,
  avisarTelegramDaCamera: boolean | undefined,
): { texto: string; tom: 'ok' | 'aviso' | 'neutro' } {
  if (alertaEnviado) return { texto: 'Alerta enviado ao Telegram', tom: 'ok' };
  if (avisarTelegramDaCamera === false) return { texto: 'Sem aviso no Telegram', tom: 'neutro' };
  return { texto: 'Alerta não enviado', tom: 'aviso' };
}
