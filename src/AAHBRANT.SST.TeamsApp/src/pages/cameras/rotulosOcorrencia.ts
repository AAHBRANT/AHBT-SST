// Textos das ocorrências de EPI (selo, resumo da cena, não conferido). Sem imports: testável com node --test.

// "Sem capacete", "Sem capacete e sem óculos", "Sem capacete, sem colete e sem óculos".
export function descreverIrregularidade(semCapacete: number, semColete: number, semOculos = 0): string {
  const itens: string[] = [];
  if (semCapacete > 0) itens.push('sem capacete');
  if (semColete > 0) itens.push('sem colete');
  if (semOculos > 0) itens.push('sem óculos');
  if (itens.length === 0) return 'Cena regular';
  const texto = itens.length === 1 ? itens[0] : `${itens.slice(0, -1).join(', ')} e ${itens[itens.length - 1]}`;
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

export function resumoCena(
  pessoas: number,
  semCapacete: number,
  semColete: number,
  capaceteNaoAvaliavel = 0,
  coleteNaoAvaliavel = 0,
  semOculos = 0,
  oculosNaoAvaliavel = 0,
): string {
  const partes = [`${pessoas} ${pessoas === 1 ? 'pessoa' : 'pessoas'} na cena`];
  if (semCapacete > 0) partes.push(`${semCapacete} sem capacete`);
  if (semColete > 0) partes.push(`${semColete} sem colete`);
  if (semOculos > 0) partes.push(`${semOculos} sem óculos`);
  if (capaceteNaoAvaliavel > 0)
    partes.push(`${capaceteNaoAvaliavel} ${capaceteNaoAvaliavel === 1 ? 'capacete não conferido' : 'capacetes não conferidos'}`);
  if (coleteNaoAvaliavel > 0)
    partes.push(`${coleteNaoAvaliavel} ${coleteNaoAvaliavel === 1 ? 'colete não conferido' : 'coletes não conferidos'}`);
  if (oculosNaoAvaliavel > 0)
    partes.push(`${oculosNaoAvaliavel} ${oculosNaoAvaliavel === 1 ? 'óculos não conferido' : 'óculos não conferidos'}`);
  return partes.join(', ');
}

// Linhas do cartão de alerta para quem não pôde ser conferido (nunca vira "sem ...").
// "Capacete não conferido em 1 pessoa", "Óculos não conferido em 2 pessoas (longe ou de costas)".
export function linhasNaoConferido(capacete = 0, colete = 0, oculos = 0): string[] {
  const pessoas = (n: number) => `${n} ${n === 1 ? 'pessoa' : 'pessoas'}`;
  const linhas: string[] = [];
  if (capacete > 0) linhas.push(`Capacete não conferido em ${pessoas(capacete)}`);
  if (colete > 0) linhas.push(`Colete não conferido em ${pessoas(colete)}`);
  if (oculos > 0) linhas.push(`Óculos não conferido em ${pessoas(oculos)} (longe ou de costas)`);
  return linhas;
}

// Resultado da análise que é só aviso de padrão (cor do capacete): ocorrência sem falta de EPI.
// Tem gravidade menor, não vai ao Telegram e não conta como irregularidade.
export function ehSoAvisoDeCor(r: {
  situacao: string;
  semCapacete: number;
  semColete: number;
  semOculos?: number;
  avisoCorCapacete?: boolean;
}): boolean {
  return (
    r.situacao === 'ocorrencia' &&
    r.avisoCorCapacete === true &&
    r.semCapacete + r.semColete + (r.semOculos ?? 0) === 0
  );
}
