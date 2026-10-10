import type { Tom } from '@ui';
import type { ExameFuncaoObra } from '../../lib/api';

// Mesmo mapeamento nível → tom de InventarioTab.tsx/RiscosTab.tsx.
export const tomPorNivel: Record<number, Tom> = {
  1: 'ok',
  2: 'info',
  3: 'atencao',
  4: 'alerta',
  5: 'alerta',
};

// A importação grava os controles como "EPC: …\nEPI: …" (ImportarEstruturaSstObraCommand.Juntar).
export function extrairControle(texto: string | null, rotulo: string): string {
  const linha = (texto ?? '').split('\n').find((l) => l.startsWith(`${rotulo}: `));
  return linha ? linha.slice(rotulo.length + 2) : '—';
}

export function rotuloPeriodicidade(meses: number | null): string {
  if (meses == null) return '—';
  if (meses === 12) return 'Anual';
  if (meses === 24) return 'Bienal';
  if (meses === 6) return 'Semestral';
  return `${meses} meses`;
}

// O PCMSO escreve os exames em caixa alta e sem acento ("AUDIOMETRIA TONAL"). Os exames comuns
// ganham a grafia correta para exibição; os demais ficam só com a primeira letra maiúscula.
const NOME_EXIBICAO: Record<string, string> = {
  'EXAME CLINICO': 'Exame clínico',
  'AUDIOMETRIA TONAL': 'Audiometria tonal',
  'ACUIDADE VISUAL': 'Acuidade visual',
  'AVALIACAO PSICOSSOCIAL': 'Avaliação psicossocial',
  ELETROCARDIOGRAMA: 'Eletrocardiograma (ECG)',
  ELETROENCEFALOGRAMA: 'Eletroencefalograma (EEG)',
  ESPIROMETRIA: 'Espirometria',
  'GLICEMIA EM JEJUM': 'Glicemia em jejum',
  'HEMOGRAMA COMPLETO': 'Hemograma completo',
  'RAIO-X DE TORAX PADRAO OIT': 'Raio-X de tórax padrão OIT',
  'ACIDO HIPURICO': 'Ácido hipúrico',
  'ACIDO METIL HIPURICO': 'Ácido metil-hipúrico',
};

export function nomeExame(exame: string): string {
  const conhecido = NOME_EXIBICAO[exame.trim().toUpperCase()];
  if (conhecido) return conhecido;
  const minusculo = exame.toLowerCase();
  return minusculo.charAt(0).toUpperCase() + minusculo.slice(1);
}

export function asosDoExame(e: ExameFuncaoObra): string {
  return [
    e.admissional && 'Admissional',
    e.periodico && 'Periódico',
    e.retornoTrabalho && 'Retorno',
    e.mudancaRisco && 'Mudança de risco',
    e.demissional && 'Demissional',
  ]
    .filter(Boolean)
    .join(', ');
}

// Categoria (TipoExameComplementar) que o registro de exame complementar recebe quando o técnico
// escolhe o exame do PCMSO: 1 Audiometria, 2 Acuidade visual, 3 Espirometria, 4 Laboratoriais,
// 5 Avaliação clínica, 6 Exame específico.
const CATEGORIA_POR_CODIGO: Record<string, number> = { '0281': 1, '0296': 2, '1057': 3, '0295': 5 };
const EXAMES_LABORATORIAIS = ['GLICEMIA', 'HEMOGRAMA', 'ACIDO'];

export function categoriaDoExame(e: Pick<ExameFuncaoObra, 'codigoExame' | 'exame'>): number {
  if (e.codigoExame && CATEGORIA_POR_CODIGO[e.codigoExame]) return CATEGORIA_POR_CODIGO[e.codigoExame];
  const nome = e.exame.toUpperCase();
  if (nome.startsWith('EXAME CLINICO')) return 5;
  return EXAMES_LABORATORIAIS.some((p) => nome.startsWith(p)) ? 4 : 6;
}

// Soma meses a uma data "AAAA-MM-DD" sem passar por fuso horário.
export function somarMeses(data: string, meses: number): string {
  const [ano, mes, dia] = data.slice(0, 10).split('-').map(Number);
  const alvo = new Date(Date.UTC(ano, mes - 1 + meses, 1));
  const ultimoDia = new Date(Date.UTC(alvo.getUTCFullYear(), alvo.getUTCMonth() + 1, 0)).getUTCDate();
  alvo.setUTCDate(Math.min(dia, ultimoDia));
  return alvo.toISOString().slice(0, 10);
}
