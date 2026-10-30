/** Datas civis do calendário não passam por UTC (YYYY-MM-DD seria interpretado como meia-noite UTC). */
export function chaveDia(data: Date): string {
  return `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, '0')}-${String(data.getDate()).padStart(2, '0')}`;
}

export function dataDoDia(chave: string): Date {
  const [ano, mes, dia] = chave.split('-').map(Number);
  return new Date(ano, mes - 1, dia);
}

export function diasDoMesNaGrade(mes: Date, primeiroDia: 0 | 1 = 0): Date[] {
  const inicio = new Date(mes.getFullYear(), mes.getMonth(), 1);
  inicio.setDate(inicio.getDate() - (inicio.getDay() - primeiroDia + 7) % 7);
  const fim = new Date(mes.getFullYear(), mes.getMonth() + 1, 0);
  fim.setDate(fim.getDate() + (primeiroDia + 6 - fim.getDay() + 7) % 7);
  const dias: Date[] = [];
  for (const dia = new Date(inicio); dia <= fim; dia.setDate(dia.getDate() + 1)) dias.push(new Date(dia));
  return dias;
}
