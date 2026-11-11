// Regras puras da tela "Realizar integração" (11/11/2026): rótulos das etapas, o que falta para
// avançar e o texto do botão. Sem React e sem importar valores de lib/api (só tipos), para rodar em
// `node --test tests-unit/`. A trava real é do servidor (409); aqui a tela só explica o que falta.
import type { EtapaIntegracao, IntegracaoAtendimentoDetalhe, TipoEtapaIntegracao } from '../../../lib/api';

export const TOTAL_ETAPAS = 5;
export const TOTAL_FOTOS_ETAPA = 3;

// Ordem fixa: a posição na trilha é sempre a mesma, pulada ou não.
export const TIPOS_EM_ORDEM: TipoEtapaIntegracao[] = ['nr06', 'nr12', 'nr18', 'nr35', 'ordemServico'];

const ROTULOS: Record<TipoEtapaIntegracao, { codigo: string; curto: string; nome: string }> = {
  nr06: { codigo: 'NR-06', curto: 'NR-06', nome: 'EPI' },
  nr12: { codigo: 'NR-12', curto: 'NR-12', nome: 'Máquinas e equipamentos' },
  nr18: { codigo: 'NR-18', curto: 'NR-18', nome: 'Condições e meio ambiente na construção' },
  nr35: { codigo: 'NR-35', curto: 'NR-35', nome: 'Trabalho em altura' },
  ordemServico: { codigo: 'NR-01', curto: 'OS (NR-01)', nome: 'Ordem de Serviço (NR-01)' },
};

export function rotuloEtapa(tipo: TipoEtapaIntegracao | string): { codigo: string; curto: string; nome: string } {
  return ROTULOS[tipo as TipoEtapaIntegracao] ?? { codigo: String(tipo), curto: String(tipo), nome: String(tipo) };
}

/** A ordem 1 a 5 é fixa por tipo, então o pop-up sabe o nome da etapa só pela ordem. */
export function tipoDaOrdem(ordem: number): TipoEtapaIntegracao | null {
  return TIPOS_EM_ORDEM[ordem - 1] ?? null;
}

/** Participantes que ainda contam na etapa: quem foi marcado "Não assinou" fica de fora do certificado e das pendências. */
export function participantesAtivos<T extends { naoAssinou?: boolean }>(etapa: { participantes: T[] }): T[] {
  return etapa.participantes.filter((p) => !p.naoAssinou);
}

/** dd/MM/aaaa HH:mm, horário de Brasília: data da assinatura de cada pessoa. */
export function dataHoraBrasil(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Sao_Paulo' }).replace(', ', ' ');
}

export function ehEtapaOs(etapa: Pick<EtapaIntegracao, 'tipo'>): boolean {
  return etapa.tipo === 'ordemServico';
}

export function formatarCarga(minutos: number): string {
  const m = Math.max(0, Math.round(minutos));
  return `${Math.floor(m / 60)} h ${String(m % 60).padStart(2, '0')}`;
}

export function primeiroNome(nome: string): string {
  return nome.trim().split(/\s+/)[0] ?? nome;
}

/** Primeiro nome em listas; se repetir, usa os dois primeiros nomes ("Ana Oliveira"); se ainda repetir, o nome completo. */
export function nomesCurtos(nomes: string[]): string[] {
  const conta = (lista: string[]) => lista.reduce((m, x) => m.set(x, (m.get(x) ?? 0) + 1), new Map<string, number>());
  const primeiros = nomes.map(primeiroNome);
  const doisNomes = nomes.map((n) => n.trim().split(/\s+/).slice(0, 2).join(' '));
  const c1 = conta(primeiros);
  const c2 = conta(doisNomes);
  return nomes.map((n, i) => ((c1.get(primeiros[i]) ?? 0) < 2 ? primeiros[i] : (c2.get(doisNomes[i]) ?? 0) < 2 ? doisNomes[i] : n.trim()));
}

export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '?';
  return ((partes[0]?.[0] ?? '') + (partes.length > 1 ? partes[partes.length - 1]?.[0] ?? '' : '')).toUpperCase();
}

export function etapaAtual(integracao: Pick<IntegracaoAtendimentoDetalhe, 'etapas' | 'etapaAtualOrdem'>): EtapaIntegracao | null {
  const ordenadas = [...integracao.etapas].sort((a, b) => a.ordem - b.ordem);
  if (integracao.etapaAtualOrdem != null) {
    const porOrdem = ordenadas.find((e) => e.ordem === integracao.etapaAtualOrdem);
    if (porOrdem) return porOrdem;
  }
  return ordenadas.find((e) => e.status === 'emAndamento') ?? null;
}

export function proximaEtapa(integracao: Pick<IntegracaoAtendimentoDetalhe, 'etapas'>, atual: Pick<EtapaIntegracao, 'ordem'>): EtapaIntegracao | null {
  return [...integracao.etapas]
    .sort((a, b) => a.ordem - b.ordem)
    .find((e) => e.ordem > atual.ordem && e.status !== 'pulada') ?? null;
}

/** "etapa 3 de 5 (NR-18)". */
export function descreverEtapa(ordem: number): string {
  const tipo = tipoDaOrdem(ordem);
  return `etapa ${ordem} de ${TOTAL_ETAPAS}${tipo ? ` (${rotuloEtapa(tipo).codigo})` : ''}`;
}

export interface BotaoIntegracao {
  situacao: 'iniciar' | 'continuar';
  rotulo: string;
  /** Texto do passo no pop-up. */
  detalhe: string;
}

/**
 * Botão do pop-up Funcionário novo (decisão do usuário, 11/11): sem agendamento de turma. Sem
 * atendimento aberto na obra: "Iniciar integração". Com atendimento aberto: "Continuar integração —
 * etapa X de 5 (NR-xx)", que leva direto à etapa em que parou.
 */
export function botaoIntegracao(emAndamento: { status: string; etapaAtualOrdem?: number | null } | null | undefined): BotaoIntegracao {
  if (emAndamento && emAndamento.status === 'emAndamento' && emAndamento.etapaAtualOrdem != null) {
    const texto = descreverEtapa(emAndamento.etapaAtualOrdem);
    return {
      situacao: 'continuar',
      rotulo: `Continuar integração — ${texto}`,
      detalhe: `Em andamento: ${texto}.`,
    };
  }
  return {
    situacao: 'iniciar',
    rotulo: 'Iniciar integração',
    detalhe: '5 etapas em ordem: NR-06, NR-12, NR-18, NR-35 e Ordem de Serviço (NR-01).',
  };
}

/**
 * O que falta para liberar o "Avançar" de uma etapa de curso, em frases curtas (mockup v4): carga, 3 fotos,
 * instrutor e assinatura biométrica de todos os participantes. Etapa sem participantes (pulada) não tem
 * pendência. Se a regra do servidor apontar algo que esta conta não viu, o texto do servidor entra no lugar.
 */
export function faltasDaEtapa(etapa: EtapaIntegracao, instrutorTrabalhadorId?: string | null): string[] {
  const ativos = participantesAtivos(etapa);
  if (ativos.length === 0 || etapa.status === 'pulada') return [];
  const faltas: string[] = [];
  // Toda etapa de curso precisa de ao menos 1 período lançado, inclusive NR-06 com mínimo 0.
  const minimo = Math.round(etapa.cargaMinimaHoras * 60);
  if ((etapa.periodos?.length ?? 0) === 0) faltas.push('Lançar a carga horária');
  else if (minimo > 0 && etapa.cargaLancadaMinutos < minimo) faltas.push(`carga ${formatarCarga(minimo - etapa.cargaLancadaMinutos)}`);
  const necessarias = etapa.fotosNecessarias > 0 ? etapa.fotosNecessarias : TOTAL_FOTOS_ETAPA;
  if (etapa.fotosRegistradas < necessarias) {
    const n = necessarias - etapa.fotosRegistradas;
    faltas.push(`${n} foto${n > 1 ? 's' : ''} da etapa`);
  }
  if (!instrutorTrabalhadorId) faltas.push('instrutor da etapa');
  const pendentes = nomesCurtos(ativos.filter((p) => !p.presencaConfirmada).map((p) => p.nome));
  if (pendentes.length > 0) faltas.push(`assinatura${pendentes.length > 1 ? 's' : ''} de ${pendentes.join(', ')}`);
  if (faltas.length === 0 && (etapa.faltas?.length ?? 0) > 0) return [...etapa.faltas];
  return faltas;
}

/** Texto do botão de avanço: "Avançar para NR-12", "Avançar para a Ordem de Serviço" ou "Concluir integração". */
export function rotuloAvancar(proxima: Pick<EtapaIntegracao, 'tipo'> | null): string {
  if (!proxima) return 'Concluir integração';
  if (proxima.tipo === 'ordemServico') return 'Avançar para a Ordem de Serviço';
  return `Avançar para ${rotuloEtapa(proxima.tipo).codigo}`;
}

/** Mensagem verde depois de avançar, como no mockup. */
export function mensagemEtapaConcluida(etapa: { tipo: EtapaIntegracao['tipo']; participantes: { naoAssinou?: boolean }[] }): string {
  const cod = rotuloEtapa(etapa.tipo).codigo;
  const n = participantesAtivos(etapa).length;
  const semCert = etapa.participantes.length - n;
  const nota = semCert > 0 ? ` · ${semCert} não assinou, sem certificado` : '';
  if (etapa.participantes.length === 0) return `${cod} pulada — ninguém precisava desta etapa`;
  if (n === 0) return `${cod} concluída — nenhum certificado gerado${nota}`;
  return `${cod} concluída — ${n} certificado${n > 1 ? 's' : ''} gerado${n > 1 ? 's' : ''}, cada um com a assinatura do trabalhador${nota}`;
}

export function dataBrasil(iso?: string | null): string {
  return iso ? iso.slice(0, 10).split('-').reverse().join('/') : '—';
}
