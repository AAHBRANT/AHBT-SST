// Mensagens do teste de conexão da câmera IP (puro, sem React nem API: testável em tests-unit/).
// A mensagem é escolhida SÓ pelo código estável que o agente envia e o servidor valida; o texto livre (motivo/detalhe) nunca
// decide a causa, só aparece em letra pequena para o suporte. Textos do mockup aprovado em docs/mockups/2026-11-12-cameras-ip-configuracao-v1.html.
export interface TesteConexaoResumo {
  situacao: string;
  codigo?: string | null;
  motivo: string | null;
  detalhe: string | null;
}

export interface MensagemFalhaTeste {
  titulo: string;
  texto: string;
  // Texto técnico devolvido pelo agente (já sem senha), mostrado em letra pequena.
  detalhe?: string;
}

export const CODIGOS_TESTE_CONEXAO = [
  'senha_recusada',
  'acesso_negado',
  'camera_nao_encontrada',
  'canal_ou_stream_invalido',
  'conexao_recusada',
  'conexao_encerrada',
  'sem_resposta',
  'ffmpeg_ausente',
  'sem_imagem',
  'imagem_ilegivel',
  'erro_desconhecido',
] as const;

export type CodigoTesteConexao = (typeof CODIGOS_TESTE_CONEXAO)[number];

const MENSAGENS: Record<CodigoTesteConexao, Omit<MensagemFalhaTeste, 'detalhe'>> = {
  senha_recusada: {
    titulo: 'A câmera não aceitou o usuário ou a senha.',
    texto: 'Confira os dados do equipamento e tente de novo.',
  },
  acesso_negado: {
    titulo: 'A câmera negou o acesso deste usuário.',
    texto: 'Confira se o usuário informado no equipamento tem permissão para ver a imagem da câmera.',
  },
  camera_nao_encontrada: {
    titulo: 'Não encontramos a câmera nesse endereço.',
    texto: 'Confira o IP e a porta. A câmera precisa estar ligada e na mesma rede do PC da obra.',
  },
  canal_ou_stream_invalido: {
    titulo: 'A câmera não tem essa lente ou qualidade.',
    texto: 'Confira a lente (canal) e a qualidade escolhidas e tente de novo.',
  },
  conexao_recusada: {
    titulo: 'A câmera recusou a conexão.',
    texto: 'Confira a porta de vídeo do equipamento e se a câmera está ligada.',
  },
  conexao_encerrada: {
    titulo: 'A câmera encerrou a conexão.',
    texto: 'Confira o usuário, a senha, a lente e a qualidade, e se a câmera está ligada. Depois, tente de novo.',
  },
  sem_resposta: {
    titulo: 'A câmera não respondeu a tempo.',
    texto: 'Veja se ela está ligada e na mesma rede do PC da obra. Depois, tente de novo.',
  },
  ffmpeg_ausente: {
    titulo: 'O agente não conseguiu ler imagens neste PC.',
    texto: 'Peça para conferir a instalação do agente no PC da obra: o componente de leitura de vídeo não foi encontrado.',
  },
  sem_imagem: {
    titulo: 'A câmera respondeu, mas não entregou imagem.',
    texto: 'Confira a lente (canal) ou tente a qualidade Secundária.',
  },
  imagem_ilegivel: {
    titulo: 'Não conseguimos abrir o formato da imagem.',
    texto: 'Troque para a qualidade Secundária. Se continuar, chame o suporte.',
  },
  erro_desconhecido: {
    titulo: 'Não foi possível ler a imagem da câmera.',
    texto: 'Confira os dados do equipamento, a lente e a qualidade, e tente de novo.',
  },
};

const CONFERIR_AGENTE: MensagemFalhaTeste = {
  titulo: 'O agente da obra não respondeu.',
  texto: 'Veja se o PC da obra está ligado e com internet. Depois, tente de novo.',
};

function ehCodigoConhecido(codigo: string | null | undefined): codigo is CodigoTesteConexao {
  return !!codigo && (CODIGOS_TESTE_CONEXAO as readonly string[]).includes(codigo);
}

export function mensagemDaFalha(teste: TesteConexaoResumo): MensagemFalhaTeste {
  if (teste.situacao === 'Expirado') return CONFERIR_AGENTE;
  // Código ausente ou fora da lista (servidor mais novo que a tela) = mensagem neutra, sem inventar causa.
  const codigo: CodigoTesteConexao = ehCodigoConhecido(teste.codigo) ? teste.codigo : 'erro_desconhecido';
  const detalhe = teste.detalhe?.trim() || teste.motivo?.trim() || undefined;
  return { ...MENSAGENS[codigo], detalhe };
}
