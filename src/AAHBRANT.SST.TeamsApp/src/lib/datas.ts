// Datas padrão pra formulários de criação (pedido do usuário, 03/09): em vez de nascer em branco
// obrigando escolher a data toda vez, esses campos já vêm preenchidos pensando em facilidade —
// continuam editáveis normalmente. Usa data local (não toISOString(), que é UTC e erraria o dia
// pra quem preenche à noite no fuso do Brasil).

export function hojeIso(): string {
  const agora = new Date();
  return `${agora.getFullYear()}-${String(agora.getMonth() + 1).padStart(2, '0')}-${String(agora.getDate()).padStart(2, '0')}`;
}

// Segunda-feira da semana atual, em ISO — usado por formulários cujo campo é "início da semana"
// (ex.: DDS Semanal, sempre segunda a sexta), pra já sugerir a segunda-feira certa em vez de
// obrigar contar de cabeça qual é.
export function segundaFeiraAtualIso(): string {
  const agora = new Date();
  const diferenca = (agora.getDay() + 6) % 7; // 0=domingo..6=sábado -> distância até a segunda anterior
  const segunda = new Date(agora.getFullYear(), agora.getMonth(), agora.getDate() - diferenca);
  return `${segunda.getFullYear()}-${String(segunda.getMonth() + 1).padStart(2, '0')}-${String(segunda.getDate()).padStart(2, '0')}`;
}

// Instantes gravados pelo backend (AssinadoEm, CreatedAtUtc, PresencaConfirmadaEm...) são UTC, mas
// chegam da API sem o "Z" (ex.: 2026-09-29T15:22:16). new Date() lê texto sem fuso como hora LOCAL
// do aparelho, e a tela mostrava o horário 3 h adiantado no Brasil (ou errado em outro fuso).
// Ponto único: todo instante vindo da API deve passar por aqui antes de ser exibido.
export function lerInstanteUtc(valor: string): Date {
  return new Date(/([zZ]|[+-]\d{2}:?\d{2})$/.test(valor) ? valor : `${valor}Z`);
}

// Sempre no horário de Brasília, independente do fuso configurado no aparelho.
const FUSO_BRASILIA = 'America/Sao_Paulo';

export function formatarDataHoraBrasilia(valor: string): string {
  return lerInstanteUtc(valor).toLocaleString('pt-BR', { timeZone: FUSO_BRASILIA });
}

export function formatarHoraBrasilia(valor: string): string {
  return lerInstanteUtc(valor).toLocaleTimeString('pt-BR', { timeZone: FUSO_BRASILIA, hour: '2-digit', minute: '2-digit' });
}
