// Textos e desfechos da página "Ver ao vivo" (puro, sem React nem API: testável em tests-unit/).
// Recusa e encerramento são escolhidos SÓ pelo código estável do servidor, nunca pelo texto. Textos do mockup aprovado em
// docs/mockups/2026-11-13-cameras-ao-vivo-v1.html.
import { mensagemDaFalhaAoVivo, type MensagemFalhaTeste } from './mensagensTesteConexao.ts';

export type FaseDesfecho = 'tempo' | 'inatividade' | 'encerrada' | 'falha' | 'limite';

export interface Desfecho {
  fase: FaseDesfecho;
  // Só nas fases com texto livre (falha, limite e encerrada); tempo e inatividade têm cartão fixo na página.
  mensagem: MensagemFalhaTeste | null;
}

const AGENTE_NAO_RESPONDEU: MensagemFalhaTeste = {
  titulo: 'O agente da obra não respondeu.',
  texto: 'Veja se o PC da obra está ligado e com internet. Depois, tente de novo.',
};

export const MENSAGEM_SEM_REDE: MensagemFalhaTeste = {
  titulo: 'Não foi possível abrir a imagem.',
  texto: 'Confira a internet deste aparelho e tente de novo.',
};

export const MENSAGEM_AGENTE_NAO_RESPONDEU = AGENTE_NAO_RESPONDEU;

const LIMITE: MensagemFalhaTeste = {
  titulo: 'O máximo de câmeras ao vivo ao mesmo tempo foi atingido.',
  texto: 'Feche uma transmissão e tente de novo.',
};

export const MENSAGEM_LIMITE = LIMITE;
export const TEXTO_TEMPO_MAXIMO = 'A transmissão chegou ao tempo máximo e parou sozinha para poupar a internet da obra.';

const RECUSAS: Record<string, MensagemFalhaTeste> = {
  nao_e_camera_ip: {
    titulo: 'Esta câmera não é uma câmera IP.',
    texto: 'O Ver ao vivo só funciona em câmeras IP. Câmera de tablet usa o Monitorar.',
  },
  camera_desabilitada: {
    titulo: 'O monitoramento desta câmera está desligado.',
    texto: 'Ligue o monitoramento no cadastro da câmera e tente de novo.',
  },
  agente_revogado: {
    titulo: 'O acesso do agente desta obra foi revogado.',
    texto: 'Confira o agente na aba Equipamentos e agentes.',
  },
  agente_sem_contato: AGENTE_NAO_RESPONDEU,
  agente_desatualizado: {
    titulo: 'O serviço da obra precisa ser atualizado.',
    texto: 'O serviço da obra precisa ser atualizado para mostrar a imagem ao vivo.',
  },
};

const NAO_ENCONTRADA: MensagemFalhaTeste = {
  titulo: 'Não encontramos esta câmera.',
  texto: 'Ela pode ter sido excluída ou pertencer a outra obra.',
};

const ENCERRADA_GENERICA: MensagemFalhaTeste = {
  titulo: 'Transmissão encerrada',
  texto: 'A transmissão terminou. Nada foi gravado.',
};

// Resposta do POST de abertura quando não veio sessão (404, 409 ou 429).
export function desfechoDaRecusa(status: number, codigo: string | null): Desfecho {
  if (status === 429 || codigo === 'limite_sessoes') return { fase: 'limite', mensagem: LIMITE };
  if (status === 404) return { fase: 'falha', mensagem: NAO_ENCONTRADA };
  const mensagem = (codigo && RECUSAS[codigo]) || {
    titulo: 'Não foi possível abrir a imagem.',
    texto: 'Tente de novo em instantes.',
  };
  return { fase: 'falha', mensagem };
}

// Motivo do 410 do quadro: limite_sessoes, inatividade, tempo_maximo, encerrada, agente_sem_resposta ou falha:<codigo>.
export function desfechoDoEncerramento(motivo: string | null): Desfecho {
  if (motivo === 'limite_sessoes') return { fase: 'limite', mensagem: LIMITE };
  if (motivo === 'tempo_maximo') return { fase: 'tempo', mensagem: null };
  if (motivo === 'inatividade') return { fase: 'inatividade', mensagem: null };
  if (motivo === 'agente_sem_resposta') {
    return {
      fase: 'falha',
      mensagem: {
        titulo: 'O computador da obra parou de enviar a imagem.',
        texto: 'Veja se o PC da obra está ligado e com internet. Depois, tente de novo.',
      },
    };
  }
  if (motivo?.startsWith('falha:')) return { fase: 'falha', mensagem: mensagemDaFalhaAoVivo(motivo.slice('falha:'.length)) };
  return { fase: 'encerrada', mensagem: ENCERRADA_GENERICA };
}
