// Núcleo da localização das fotos, sem dependência do Teams nem do navegador (testável em Node).
//
// Por que existe (08/10): o fluxo antigo pedia UMA posição ao abrir a câmera, tentando o SDK do
// Teams e só depois o navegador, com tetos que não fechavam (8 s + 15 s > 20 s do teto geral), e
// cada pedido ligava o GPS do zero (maximumAge 0). No iPhone e no PC isso estourava o tempo. Agora
// as fontes rodam EM PARALELO e CONTINUAMENTE enquanto a câmera está aberta; o rastreador guarda a
// melhor leitura recente e, na hora do clique, a posição normalmente já está pronta.

export interface LeituraLocalizacao {
  latitude: number;
  longitude: number;
  precisaoMetros: number | null;
  // Epoch em ms de quando a posição foi medida (normalizado — ver normalizarMomento).
  obtidaEm: number;
  fonte: string;
}

// Uma fonte entrega leituras (quantas quiser) e/ou uma falha definitiva. Retorna a função de parar.
export interface FonteLocalizacao {
  nome: string;
  iniciar(aoLer: (l: LeituraLocalizacao) => void, aoFalhar: (motivo: string) => void): () => void;
}

export type StatusRastreio = 'buscando' | 'imprecisa' | 'pronta' | 'falhou';
export interface EstadoRastreio {
  status: StatusRastreio;
  leitura: LeituraLocalizacao | null;
  motivo: string | null;
}

// Mesmos limites da validação (front pendenciasFoto e backend DadosCapturaFoto.cs): 100 m e 60 s.
// A idade usada aqui é a metade, para sobrar margem entre "leitura escolhida" e "clique gravado".
export const PRECISAO_MAX_M = 100;
export const IDADE_MAX_LEITURA_MS = 30_000;

// O SDK do Teams não documenta a unidade do timestamp e o navegador pode devolver posição em cache.
// Só confia no momento informado se ele for plausível (até 2 min no passado, até 5 s no futuro);
// aceita também em segundos. Caso contrário usa o momento em que a leitura chegou.
export function normalizarMomento(informado: number | null | undefined, agora: number): number {
  if (informado == null || !Number.isFinite(informado) || informado <= 0) return agora;
  const plausivel = (ms: number) => ms <= agora + 5_000 && ms >= agora - 120_000;
  if (plausivel(informado)) return Math.min(informado, agora);
  if (plausivel(informado * 1000)) return Math.min(informado * 1000, agora);
  return agora;
}

export function leituraAceitavel(l: LeituraLocalizacao | null, agora: number): boolean {
  return !!l && agora - l.obtidaEm <= IDADE_MAX_LEITURA_MS
    && (l.precisaoMetros == null || (l.precisaoMetros >= 0 && l.precisaoMetros <= PRECISAO_MAX_M));
}

// Melhor = aceitável mais recente; entre leituras de idade parecida (até 5 s), a mais precisa.
function melhor(a: LeituraLocalizacao | null, b: LeituraLocalizacao, agora: number): LeituraLocalizacao {
  if (!a) return b;
  const aOk = leituraAceitavel(a, agora), bOk = leituraAceitavel(b, agora);
  if (aOk !== bOk) return bOk ? b : a;
  if (Math.abs(a.obtidaEm - b.obtidaEm) <= 5_000) {
    const pa = a.precisaoMetros ?? Infinity, pb = b.precisaoMetros ?? Infinity;
    return pb <= pa ? b : a;
  }
  return b.obtidaEm > a.obtidaEm ? b : a;
}

export interface OpcoesRastreador {
  fontes: FonteLocalizacao[];
  agora?: () => number;
}

export function criarRastreador({ fontes, agora = Date.now }: OpcoesRastreador) {
  let leitura: LeituraLocalizacao | null = null;
  const falhas = new Map<string, string>();
  const ouvintes = new Set<(e: EstadoRastreio) => void>();
  const paradas: (() => void)[] = [];
  let ativo = true;

  function estado(): EstadoRastreio {
    const t = agora();
    if (leitura && leituraAceitavel(leitura, t)) return { status: 'pronta', leitura, motivo: null };
    if (leitura && t - leitura.obtidaEm <= IDADE_MAX_LEITURA_MS) return { status: 'imprecisa', leitura, motivo: null };
    if (falhas.size >= fontes.length) {
      // Prioriza o motivo mais acionável (permissão) sobre os genéricos.
      const motivos = [...falhas.values()];
      return { status: 'falhou', leitura: null, motivo: motivos.find(m => /permiss/i.test(m)) ?? motivos[0] ?? 'Localização indisponível.' };
    }
    return { status: 'buscando', leitura: null, motivo: null };
  }
  const avisar = () => { const e = estado(); ouvintes.forEach(o => o(e)); };
  function assinar(fn: (e: EstadoRastreio) => void) { ouvintes.add(fn); fn(estado()); return () => { ouvintes.delete(fn); }; }

  for (const fonte of fontes) {
    try {
      paradas.push(fonte.iniciar(
        l => { if (!ativo) return; falhas.delete(fonte.nome); leitura = melhor(leitura, l, agora()); avisar(); },
        m => { if (!ativo) return; falhas.set(fonte.nome, m); avisar(); },
      ));
    } catch (e) {
      falhas.set(fonte.nome, e instanceof Error ? e.message : 'Falha ao iniciar a localização.');
    }
  }

  return {
    estado,
    assinar,
    // Espera até ter leitura aceitável (ou até o teto). Devolve a leitura aceitável, ou null.
    aguardarPronta(tetoMs: number): Promise<LeituraLocalizacao | null> {
      const e = estado();
      if (e.status === 'pronta') return Promise.resolve(e.leitura);
      if (e.status === 'falhou') return Promise.resolve(null);
      return new Promise(resolve => {
        let sair = () => {};
        const timer = setTimeout(() => { sair(); resolve(null); }, tetoMs);
        sair = assinar(s => {
          if (s.status === 'pronta' || s.status === 'falhou') {
            clearTimeout(timer); queueMicrotask(() => sair()); resolve(s.status === 'pronta' ? s.leitura : null);
          }
        });
      });
    },
    parar() { ativo = false; paradas.forEach(p => { try { p(); } catch { /* ignora */ } }); ouvintes.clear(); },
  };
}

export type Rastreador = ReturnType<typeof criarRastreador>;
