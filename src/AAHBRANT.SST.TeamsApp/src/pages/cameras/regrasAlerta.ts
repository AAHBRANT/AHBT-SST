// Regras de alerta da câmera (Etapa 3 do cadastro) e textos de confirmação/modo sombra da aba Alertas.
// Sem imports: testável com node --test.

export const PERSISTENCIA_MIN = 1;
export const PERSISTENCIA_MAX = 30;
export const PERSISTENCIA_PADRAO = 3;
export const NAO_REPETIR_MIN = 0;
export const NAO_REPETIR_MAX = 3600;
export const NAO_REPETIR_PADRAO = 300;

export type ItemRegra = 'capacete' | 'colete' | 'oculos';

export interface LimiteRegra {
  min: number;
  max: number;
}

export const LIMITE_PERSISTENCIA: LimiteRegra = { min: PERSISTENCIA_MIN, max: PERSISTENCIA_MAX };
export const LIMITE_NAO_REPETIR: LimiteRegra = { min: NAO_REPETIR_MIN, max: NAO_REPETIR_MAX };

export function segundos(n: number): string {
  return `${n} ${n === 1 ? 'segundo' : 'segundos'}`;
}

// Lê o texto digitado: só inteiro dentro do limite vale; vazio, decimal ou fora do limite = null.
export function lerSegundos(texto: string, limite: LimiteRegra): number | null {
  const t = texto.trim();
  if (!/^\d+$/.test(t)) return null;
  const n = Number(t);
  return n >= limite.min && n <= limite.max ? n : null;
}

export function mensagemLimite(limite: LimiteRegra): string {
  return `Informe um número inteiro de ${limite.min} a ${limite.max} segundos.`;
}

const NOME_ITEM: Record<ItemRegra, string> = { capacete: 'capacete', colete: 'colete', oculos: 'óculos' };

// "A pessoa precisa aparecer sem capacete por pelo menos 3 segundos para gerar alerta."
export function ajudaPersistencia(item: ItemRegra, n: number): string {
  return `A pessoa precisa aparecer sem ${NOME_ITEM[item]} por pelo menos ${segundos(n)} para gerar alerta.`;
}

// 300 -> "5 minutos"; 90 -> "1,5 minutos"; 60 -> "1 minuto".
export function equivalenteEmMinutos(n: number): string {
  const m = n / 60;
  if (Number.isInteger(m)) return `${m} ${m === 1 ? 'minuto' : 'minutos'}`;
  return `${m.toFixed(1).replace('.', ',')} minutos`;
}

export function ajudaNaoRepetir(n: number): string {
  const identificado = 'Para trabalhador identificado vale sempre 1 alerta por hora.';
  if (n === 0) {
    return `Desligado: quem não foi identificado pode gerar alerta a cada análise, respeitando o intervalo entre alertas da câmera. ${identificado}`;
  }
  return `Quem não foi identificado não gera outro alerta por ${segundos(n)} (${equivalenteEmMinutos(n)}) depois de um alerta. ${identificado}`;
}

export type EstadoAviso = 'avisa' | 'sombra' | 'mudo';

// Modo sombra manda sobre o Telegram enquanto estiver ligado; a escolha do Telegram é guardada.
export function estadoDoAviso(modoSombra: boolean, avisarTelegram: boolean): EstadoAviso {
  if (modoSombra) return 'sombra';
  return avisarTelegram ? 'avisa' : 'mudo';
}

export interface TemposRegra {
  capacete: number;
  colete: number;
  oculos: number;
}

// Frase de resultado do fim da Etapa 3.
//  - tempos iguais: "depois de 3 segundos sem capacete, sem colete ou sem óculos"
//  - tempos diferentes: "depois de 3 segundos sem capacete, 5 segundos sem colete ou 4 segundos sem óculos"
export function fraseResultado(estado: EstadoAviso, tempos: TemposRegra): string {
  if (estado !== 'avisa') {
    return 'Esta câmera vai registrar as ocorrências no sistema, sem enviar aviso no Telegram.';
  }
  const { capacete, colete, oculos } = tempos;
  const itens: [number, string][] = [
    [capacete, 'sem capacete'],
    [colete, 'sem colete'],
    [oculos, 'sem óculos'],
  ];
  const iguais = capacete === colete && colete === oculos;
  const partes = iguais ? itens.map(([, nome]) => nome) : itens.map(([n, nome]) => `${segundos(n)} ${nome}`);
  const lista = `${partes.slice(0, -1).join(', ')} ou ${partes[partes.length - 1]}`;
  return iguais
    ? `Esta câmera vai avisar no Telegram depois de ${segundos(capacete)} ${lista}.`
    : `Esta câmera vai avisar no Telegram depois de ${lista}.`;
}

// ---- Aba Alertas ----

export interface OcorrenciaConfirmacao {
  semCapacete: number;
  semColete: number;
  semOculos?: number;
  avisoCorCapacete?: boolean;
  confirmada?: boolean;
  sombra?: boolean;
  segundosObservados?: number | null;
}

export interface PersistenciaCamera {
  persistenciaCapaceteSegundos?: number;
  persistenciaColeteSegundos?: number;
  persistenciaOculosSegundos?: number;
}

// Mínimo exigido para a ocorrência: o maior tempo entre os itens que faltaram. Câmera desconhecida = null.
export function minimoDaOcorrencia(o: OcorrenciaConfirmacao, camera: PersistenciaCamera | undefined): number | null {
  if (!camera) return null;
  const tempos: number[] = [];
  if (o.semCapacete > 0) tempos.push(camera.persistenciaCapaceteSegundos ?? PERSISTENCIA_PADRAO);
  if (o.semColete > 0) tempos.push(camera.persistenciaColeteSegundos ?? PERSISTENCIA_PADRAO);
  if ((o.semOculos ?? 0) > 0) tempos.push(camera.persistenciaOculosSegundos ?? PERSISTENCIA_PADRAO);
  return tempos.length > 0 ? Math.max(...tempos) : null;
}

export interface LinhaConfirmacao {
  confirmada: boolean;
  rotulo: string;
  // "sem capacete por 5 s (mínimo 3 s)"; vazio quando o servidor não informou o tempo observado.
  detalhe: string;
}

// Ocorrência antiga (sem o campo) vale confirmada.
export function linhaConfirmacao(
  o: OcorrenciaConfirmacao,
  camera: PersistenciaCamera | undefined,
  itensTexto: string,
): LinhaConfirmacao {
  const confirmada = !ehNaoConfirmada(o);
  const rotulo = confirmada ? 'Confirmada' : 'Não confirmada';
  if (o.segundosObservados == null) return { confirmada, rotulo, detalhe: '' };
  const minimo = minimoDaOcorrencia(o, camera);
  const base = `${itensTexto} por ${o.segundosObservados} s`;
  return { confirmada, rotulo, detalhe: minimo == null ? base : `${base} (mínimo ${minimo} s)` };
}

// Não confirmada = expirou antes do tempo mínimo. O aviso de cor do capacete (sem falta de EPI) vem sempre confirmado.
export function ehNaoConfirmada(o: OcorrenciaConfirmacao): boolean {
  const soCor = o.avisoCorCapacete === true && o.semCapacete + o.semColete + (o.semOculos ?? 0) === 0;
  return o.confirmada === false && !soCor;
}

// Ocorrência de câmera com aviso que não se confirmou: não é "alerta não enviado".
export function ehNaoConfirmadaSemSombra(o: OcorrenciaConfirmacao): boolean {
  return o.sombra !== true && ehNaoConfirmada(o);
}

export function resumoSombra(total: number): string {
  return `${total} em modo sombra`;
}
