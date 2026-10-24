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
