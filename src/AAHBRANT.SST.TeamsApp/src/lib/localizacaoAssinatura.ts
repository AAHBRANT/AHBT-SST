import { obterLocalizacaoFoto } from './dadosFoto';

// Geolocalização declarada pelo aparelho no momento da assinatura, para o log de assinaturas da
// Ficha de EPI. Espelha StatusLocalizacaoAssinatura do backend. Nunca bloqueia a assinatura: se o
// usuário negar ou o aparelho não tiver posição, segue sem coordenadas e o log mostra o motivo.
export const StatusLocalizacaoAssinatura = {
  NaoInformada: 0,
  Capturada: 1,
  NaoAutorizada: 2,
  Indisponivel: 3,
} as const;

export interface LocalizacaoAssinatura {
  status: number;
  latitude?: number | null;
  longitude?: number | null;
  precisaoMetros?: number | null;
}

// Posição recente é reaproveitada: a assinatura em lote dispara várias chamadas seguidas e não faz
// sentido pedir o GPS a cada documento. Se o usuário negou a permissão, não insiste por um tempo.
const IDADE_MAX_POSICAO_MS = 30_000;
const IDADE_MAX_NEGADA_MS = 10 * 60_000;
// A assinatura não pode ficar presa esperando o GPS.
const TETO_ESPERA_MS = 8_000;

let ultima: { em: number; valor: LocalizacaoAssinatura } | null = null;

export async function obterLocalizacaoAssinatura(): Promise<LocalizacaoAssinatura> {
  const agora = Date.now();
  if (ultima) {
    const idade = agora - ultima.em;
    const limite = ultima.valor.status === StatusLocalizacaoAssinatura.NaoAutorizada ? IDADE_MAX_NEGADA_MS : IDADE_MAX_POSICAO_MS;
    if (ultima.valor.status !== StatusLocalizacaoAssinatura.Indisponivel && idade <= limite) return ultima.valor;
  }

  let timer: ReturnType<typeof setTimeout> | undefined;
  let valor: LocalizacaoAssinatura;
  try {
    const bruta = await Promise.race([
      obterLocalizacaoFoto(),
      new Promise<null>((resolve) => { timer = setTimeout(() => resolve(null), TETO_ESPERA_MS); }),
    ]);
    if (bruta && bruta.latitude != null && bruta.longitude != null) {
      valor = {
        status: StatusLocalizacaoAssinatura.Capturada,
        latitude: bruta.latitude,
        longitude: bruta.longitude,
        precisaoMetros: bruta.precisaoMetros ?? null,
      };
    } else {
      const negada = !!bruta?.motivoLocalizacao && /bloqueada|negada/i.test(bruta.motivoLocalizacao);
      valor = { status: negada ? StatusLocalizacaoAssinatura.NaoAutorizada : StatusLocalizacaoAssinatura.Indisponivel };
    }
  } catch {
    valor = { status: StatusLocalizacaoAssinatura.Indisponivel };
  } finally {
    clearTimeout(timer);
  }
  ultima = { em: Date.now(), valor };
  return valor;
}

// Formato do corpo JSON esperado pelo backend (LocalizacaoAssinaturaBody).
export function localizacaoParaCorpo(l: LocalizacaoAssinatura) {
  return { status: l.status, latitude: l.latitude ?? null, longitude: l.longitude ?? null, precisaoMetros: l.precisaoMetros ?? null };
}
