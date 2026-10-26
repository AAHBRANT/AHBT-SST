import { ResultadoAso, type Aso } from './api';

// Regra única do ASO vigente (decisão do usuário, 26/10/2026), a mesma do backend (Application/Asos/AsoVigente.cs):
//  1. O vigente de uma pessoa é o ASO do EXAME MAIS RECENTE; os demais são "superados" e nunca aparecem como vencidos
//     ou pendentes nem bloqueiam.
//  2. Empate de data de exame: vale o que tem parecer sobre o Pendente; entre os que têm parecer, o Inapto; depois a
//     maior validade; por último o id, só para o resultado nunca depender da ordem em que a API devolveu.
//  3. O vigente decide a liberação: Apto ou Apto com restrição dentro da validade. Pendente nunca libera.
// A API já marca `vigente` em cada ASO; a eleição local só entra se a marca não vier.

export type SituacaoAso = 'SemAso' | 'Liberado' | 'Vencido' | 'SemParecer' | 'Inapto';

const dia = (iso: string) => iso.slice(0, 10);

function comparar(a: Aso, b: Aso): number {
  const exame = dia(b.dataExame).localeCompare(dia(a.dataExame));
  if (exame !== 0) return exame;
  const parecer = Number(b.resultadoStatus !== ResultadoAso.Pendente) - Number(a.resultadoStatus !== ResultadoAso.Pendente);
  if (parecer !== 0) return parecer;
  const inapto = Number(b.resultadoStatus === ResultadoAso.Inapto) - Number(a.resultadoStatus === ResultadoAso.Inapto);
  if (inapto !== 0) return inapto;
  const validade = dia(b.dataValidade).localeCompare(dia(a.dataValidade));
  if (validade !== 0) return validade;
  return b.id.localeCompare(a.id);
}

// Ordena do vigente ao mais antigo, sem empate.
export function ordenarPorVigencia(asos: Aso[]): Aso[] {
  return [...asos].sort(comparar);
}

export function elegerAsoVigente(asosDaPessoa: Aso[]): Aso | undefined {
  return asosDaPessoa.find((a) => a.vigente) ?? ordenarPorVigencia(asosDaPessoa)[0];
}

export function asoVigentePorTrabalhador(asos: Aso[]): Map<string, Aso> {
  const porTrabalhador = new Map<string, Aso[]>();
  for (const aso of asos) {
    const lista = porTrabalhador.get(aso.trabalhadorId);
    if (lista) lista.push(aso);
    else porTrabalhador.set(aso.trabalhadorId, [aso]);
  }
  const vigentes = new Map<string, Aso>();
  for (const [trabalhadorId, lista] of porTrabalhador) vigentes.set(trabalhadorId, elegerAsoVigente(lista)!);
  return vigentes;
}

export function hojeISO(): string {
  const agora = new Date();
  const mes = String(agora.getMonth() + 1).padStart(2, '0');
  const diaDoMes = String(agora.getDate()).padStart(2, '0');
  return `${agora.getFullYear()}-${mes}-${diaDoMes}`;
}

// Situação do ASO vigente. Vencimento vence qualquer parecer; o parecer só decide quando o exame ainda vale.
export function situacaoDoVigente(vigente: Aso | undefined, hoje: string = hojeISO()): SituacaoAso {
  if (!vigente) return 'SemAso';
  if (dia(vigente.dataValidade) < hoje) return 'Vencido';
  if (vigente.resultadoStatus === ResultadoAso.Inapto) return 'Inapto';
  if (vigente.resultadoStatus === ResultadoAso.Pendente) return 'SemParecer';
  return 'Liberado';
}

export function asoLiberado(vigente: Aso | undefined, hoje: string = hojeISO()): boolean {
  return situacaoDoVigente(vigente, hoje) === 'Liberado';
}
