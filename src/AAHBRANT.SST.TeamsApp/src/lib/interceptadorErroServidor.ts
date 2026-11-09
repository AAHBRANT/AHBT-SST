import { API_BASE_URL } from './apiBase';
import { mostrarAvisoErro } from './avisoErro';

// Mostra no pop-up central qualquer recusa do servidor a uma ação do usuário (salvar, enviar,
// excluir...), em todas as telas, sem precisar mexer em cada uma. Só olha métodos que alteram
// dados: falha ao carregar uma lista continua sendo tratada pela própria tela.
//
// Fica de fora o que já tem tratamento próprio e esperado:
//  - 401: sessão expirada, a tela de login cuida;
//  - 409: conflito da fila offline, o syncEngine resolve sozinho;
//  - tentativas de reconhecimento facial/digital, que recusam várias vezes até o rosto/dedo bater.
const STATUS_IGNORADOS = new Set([401, 409]);
const ROTAS_IGNORADAS = [
  /\/autenticacao\/(facial|biometria-local)/i,
  /\/presenca\/facial/i,
  /\/assinatura\/facial\/(fotos|refazer)/i,
];

function ehMutacao(metodo: string): boolean {
  return metodo !== 'GET' && metodo !== 'HEAD' && metodo !== 'OPTIONS';
}

function mensagemDoCorpo(corpo: string, status: number): string {
  if (corpo) {
    try {
      const json = JSON.parse(corpo) as { erro?: string; title?: string; detail?: string };
      if (json.erro) return json.erro;
      if (json.detail) return json.detail;
      if (json.title) return json.title;
    } catch {
      if (corpo.length < 300 && !corpo.trimStart().startsWith('<')) return corpo;
    }
  }
  if (status === 403) return 'Seu perfil não tem permissão para esta ação.';
  if (status === 404) return 'O registro não foi encontrado. Ele pode ter sido excluído por outra pessoa.';
  if (status >= 500) return 'O servidor teve um problema ao processar. Tente de novo em instantes.';
  return 'O servidor recusou a operação.';
}

let instalado = false;

export function instalarInterceptadorErroServidor(): void {
  if (instalado) return;
  instalado = true;
  const fetchOriginal = window.fetch.bind(window);

  window.fetch = async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const resposta = await fetchOriginal(entrada, init);
    try {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = (init?.method ?? (entrada instanceof Request ? entrada.method : 'GET')).toUpperCase();
      // Só a API do SST (o agente de biometria local e serviços externos têm tratamento próprio).
      const base = API_BASE_URL || window.location.origin;
      const ehApi = url.includes('/api/') && (url.startsWith(base) || url.startsWith('/api/'));
      if (
        !resposta.ok &&
        ehApi &&
        ehMutacao(metodo) &&
        !STATUS_IGNORADOS.has(resposta.status) &&
        !ROTAS_IGNORADAS.some((rota) => rota.test(url))
      ) {
        const corpo = await resposta.clone().text().catch(() => '');
        mostrarAvisoErro({
          tipo: 'erro',
          titulo: 'Não foi possível salvar',
          descricao: 'O que você preencheu continua na tela.',
          itens: [{ nome: mensagemDoCorpo(corpo, resposta.status), dica: `Código ${resposta.status}` }],
        });
      }
    } catch {
      // Nunca deixar o aviso atrapalhar a requisição original.
    }
    return resposta;
  };
}
