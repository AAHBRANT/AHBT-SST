// Extraído de AssinaturaEntregaEpiDialog.tsx (04/09) ao ganhar um segundo consumidor
// (AssinaturaLoteEntregaEpiDialog.tsx) — o texto é citação literal do modelo institucional oficial
// (AHBT-FIC-SSO-XXX-00_FichaEntregaEPI, seção 2) e não pode divergir entre os dois fluxos de
// assinatura (item único vs. lote).
export function formatarDataBr(data?: string | null): string {
  if (!data) return '___/___/______';
  return data.slice(0, 10).split('-').reverse().join('/');
}

export function clausulasTermoCompromisso(numeroListaPresencaNr6?: string | null, dataTreinamentoNr6?: string | null): string[] {
  return [
    'Declaro ter recebido do Consórcio Ponte Rio Cuiá os Equipamentos de Proteção Individual (EPIs) relacionados nesta ficha, nas datas e quantidades ali indicadas, todos em perfeitas condições de uso e com Certificado de Aprovação (CA) válido.',
    `Declaro ter recebido orientação e treinamento sobre o uso correto, a guarda, a conservação, a higienização e os critérios de substituição de cada EPI relacionado, conforme registrado na Lista de Presença de Treinamento (NR-6) nº ${numeroListaPresencaNr6 || '__________'}, realizada em ${formatarDataBr(dataTreinamentoNr6)}.`,
    'Comprometo-me a utilizar os EPIs exclusivamente para a finalidade a que se destinam, durante toda a execução das minhas atividades laborais, zelando por sua guarda, conservação e higienização adequadas, e a comunicar imediatamente ao Setor de Segurança do Trabalho qualquer dano, extravio ou alteração que os torne impróprios para uso.',
    'Comprometo-me a devolver os EPIs sempre que solicitado, inclusive nos casos de substituição, troca de função, mudança de atividade ou rescisão do meu contrato de trabalho.',
    'Estou ciente de que o descumprimento das obrigações aqui assumidas constitui falta funcional, passível de sanções disciplinares que poderão variar, a critério do empregador, de advertência por escrito até a rescisão contratual por justa causa, sem prejuízo de demais medidas legais cabíveis, conforme disposto no Art. 158 da CLT e na Norma Regulamentadora nº 6 (NR-6).',
  ];
}
