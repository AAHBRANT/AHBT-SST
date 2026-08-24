// Utilitários de exibição de CPF (LGPD). O backend sempre recebe/retorna o CPF como string de
// dígitos crus (ver Trabalhador.cpf em api.ts) — a máscara/formatação é só de UI, nunca do payload.

export function formatarCpf(valor: string): string {
  const digitos = valor.replace(/\D/g, '').slice(0, 11);
  let resultado = digitos.slice(0, 3);
  if (digitos.length > 3) resultado += `.${digitos.slice(3, 6)}`;
  if (digitos.length > 6) resultado += `.${digitos.slice(6, 9)}`;
  if (digitos.length > 9) resultado += `-${digitos.slice(9, 11)}`;
  return resultado;
}

// Mostra só os 2 dígitos verificadores finais — usado como exibição padrão no perfil do
// trabalhador, com um botão de revelar para mostrar o CPF completo sob demanda.
export function mascararCpf(valor: string): string {
  const digitos = valor.replace(/\D/g, '');
  if (digitos.length < 11) return formatarCpf(digitos);
  return `***.***.***-${digitos.slice(-2)}`;
}
