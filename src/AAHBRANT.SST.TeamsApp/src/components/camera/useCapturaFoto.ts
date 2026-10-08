import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react';
import { comprimirImagem } from '../../lib/imagem';
import { leituraParaLocalizacao, pendenciasFoto, vincularDadosFoto, type ContextoFoto, type DadosFoto, type LocalizacaoFoto } from '../../lib/dadosFoto';
import { criarRastreadorFoto } from '../../lib/fontesLocalizacao';
import type { EstadoRastreio, Rastreador } from '../../lib/rastreadorLocalizacao';

// Quanto o clique em "capturar" espera pela posição quando ela ainda não está boa. O quadro já foi
// congelado no clique; a posição que chegar nesse intervalo fica dentro da janela de 60 s.
const ESPERA_LOCALIZACAO_NO_CLIQUE_MS = 15_000;

export interface UseCapturaFotoOptions {
  contextoFoto?: ContextoFoto;
  aoSelecionarArquivo: (arquivo: File) => void | Promise<void>;
  // Sem isso, o usuário só descobria que o arquivo era grande demais depois do upload ir e voltar
  // do servidor com erro — o limite de negócio (5 MB pra foto, mais pra PDF/certificado) já existe
  // no backend, mas nunca era checado antes de gastar a requisição inteira.
  aoErroValidacao?: (mensagem: string) => void;
  tamanhoMaximoMb?: number;
  // Câmera frontal ("user", ex.: reconhecimento facial — a pessoa fotografa o próprio rosto) ou
  // traseira ("environment", padrão — fotos de evidência/EPI/documento, apontando pra outra coisa).
  modoCamera?: 'user' | 'environment';
  // false para pontos de anexo que nunca são foto (ex.: documento PDF) — pula o diálogo de câmera
  // e vai direto pro seletor de arquivos nativo, já que a câmera só pode produzir JPEG.
  permitirCamera?: boolean;
  // Pedido do usuário (22/09): cadastro de reconhecimento facial não pode aceitar foto do
  // álbum/arquivos — uma foto antiga ou de outra pessoa escolhida da galeria quebra a garantia de
  // "captura ao vivo" que a biometria facial depende. Com isto true, se a câmera falhar (sem
  // permissão, sem hardware, navegador sem suporte) o fluxo termina em erro em vez de cair no
  // seletor de arquivos nativo — vale pra qualquer dispositivo (PC/notebook/celular/tablet).
  exigirCamera?: boolean;
}

// Lógica de captura/seleção de foto extraída de SeletorFotoCamera (14/09) para ser reaproveitada
// também pelo visual de slot em quadro (SlotFoto/GradeFotosEvidencia) — mesmo diálogo de câmera ao
// vivo (getUserMedia) nos dois lugares, em vez de um <input capture> "burro" que o Chrome/Edge de
// desktop costuma ignorar (só funciona de verdade em navegador mobile).
// Câmera frontal (reconhecimento facial) pede 1280x720 como ideal: o padrão de muitas webcams é
// 640x480, onde o rosto dificilmente chega aos 200 px de lado que o servidor exige na foto de cadastro.
// É só preferência (ideal): se o aparelho não tiver, o navegador entrega o que houver.
function resolucaoIdeal(modo: 'user' | 'environment'): MediaTrackConstraints {
  return modo === 'user' ? { width: { ideal: 1280 }, height: { ideal: 720 } } : {};
}

// Chave única (não por modoCamera): um notebook com webcam USB externa plugada é o mesmo
// equipamento físico independente de a tela pedir câmera "user" (facial) ou "environment"
// (evidência) — o usuário só quer escolher uma vez qual câmera o computador deve usar.
const CHAVE_CAMERA_PREFERIDA = 'sst.camera.dispositivoPreferido';

function lerCameraPreferida(): string | null {
  try {
    return localStorage.getItem(CHAVE_CAMERA_PREFERIDA);
  } catch {
    return null;
  }
}

function salvarCameraPreferida(deviceId: string) {
  try {
    localStorage.setItem(CHAVE_CAMERA_PREFERIDA, deviceId);
  } catch {
    // Sem localStorage (modo privado, storage bloqueado) — só não persiste a escolha entre sessões.
  }
}

export function useCapturaFoto({
  aoSelecionarArquivo,
  aoErroValidacao,
  tamanhoMaximoMb = 5,
  modoCamera = 'environment',
  permitirCamera = true,
  exigirCamera = false,
  contextoFoto,
}: UseCapturaFotoOptions) {
  const [localFoto, setLocalFoto] = useState(contextoFoto?.local ?? '');
  // Rastreio contínuo (08/10): liga ao abrir a câmera e só desliga ao fechar — a posição fica sempre
  // fresca e o clique não precisa esperar um GPS "do zero". Ver lib/rastreadorLocalizacao.ts.
  const rastreador = useRef<Rastreador | null>(null);
  const [estadoLocalizacao, setEstadoLocalizacao] = useState<EstadoRastreio | null>(null);
  const [aguardandoLocalizacao, setAguardandoLocalizacao] = useState(false);
  const localizando = estadoLocalizacao?.status === 'buscando';
  const localizacao: LocalizacaoFoto = estadoLocalizacao?.leitura ? leituraParaLocalizacao(estadoLocalizacao.leitura)
    : estadoLocalizacao?.motivo ? { motivoLocalizacao: estadoLocalizacao.motivo } : {};

  useEffect(() => {
    setLocalFoto(contextoFoto?.local ?? '');
  }, [contextoFoto?.local]);
  const [fotoPendente, setFotoPendente] = useState<{ arquivo: File; dados: DadosFoto } | null>(null);
  function pararLocalizacao() {
    rastreador.current?.parar();
    rastreador.current = null;
  }
  useEffect(() => () => pararLocalizacao(), []);
  // Reavalia a idade da posição mesmo sem leitura nova (ela "vence" após 30 s).
  useEffect(() => {
    if (!estadoLocalizacao) return;
    const t = setInterval(() => { if (rastreador.current) setEstadoLocalizacao(rastreador.current.estado()); }, 5_000);
    return () => clearInterval(t);
  }, [estadoLocalizacao !== null]); // eslint-disable-line react-hooks/exhaustive-deps
  function tentarLocalizacao() {
    if (!contextoFoto) return;
    pararLocalizacao();
    const r = criarRastreadorFoto();
    rastreador.current = r;
    r.assinar(e => { if (rastreador.current === r) setEstadoLocalizacao(e); });
  }
  function montarDados(origem: 'camera' | 'arquivo', loc: LocalizacaoFoto = localizacao, capturadaEm = new Date()): DadosFoto {
    return { ...(origem === 'camera' ? loc : {}), origem,
      capturadaEm: origem === 'camera' ? capturadaEm.toISOString() : null,
      fusoMinutos: new Date().getTimezoneOffset(),
      obraId: contextoFoto?.obraId, obraNome: contextoFoto?.obraNome, local: localFoto.trim() };
  }
  const inputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const [processando, setProcessando] = useState(false);
  const [stream, setStream] = useState<MediaStream | null>(null);
  // Pedido do usuário (22/09): notebook com mais de uma câmera (webcam interna + USB externa, ex.:
  // quiosque de assinatura facial) precisa deixar escolher qual usar — antes disso o navegador
  // escolhia sozinho via `facingMode`, sem opção de troca. Lista só vem populada com rótulos depois
  // da primeira permissão concedida (getUserMedia bem-sucedido); ver abrirCamera/atualizarDispositivos.
  const [dispositivosVideo, setDispositivosVideo] = useState<MediaDeviceInfo[]>([]);
  const [dispositivoAtualId, setDispositivoAtualId] = useState<string | null>(null);

  async function atualizarDispositivos() {
    if (!navigator.mediaDevices?.enumerateDevices) return;
    try {
      const todos = await navigator.mediaDevices.enumerateDevices();
      setDispositivosVideo(todos.filter((d) => d.kind === 'videoinput'));
    } catch {
      // Falha ao listar não impede o uso da câmera já aberta — só não mostra o seletor.
    }
  }

  // O <video> vive dentro do diálogo do Fluent, que monta o conteúdo um instante depois do stream
  // chegar. Se o stream fosse ligado só no efeito abaixo, o elemento podia ainda não existir: a luz da
  // câmera acendia e a tela ficava em branco (visto no Teams em 07/10). Por isso o vídeo é ligado de
  // dois jeitos: aqui, quando o elemento aparece (ref por função), e no efeito, quando o stream muda.
  function ligarVideo(elemento: HTMLVideoElement, fluxo: MediaStream) {
    if (elemento.srcObject !== fluxo) elemento.srcObject = fluxo;
    void elemento.play().catch(() => {
      // O autoPlay já cobre o caso comum; se o navegador recusar o play() explícito, não há o que fazer.
    });
  }

  const aoMontarVideo = useCallback(
    (elemento: HTMLVideoElement | null) => {
      videoRef.current = elemento;
      if (elemento && stream) ligarVideo(elemento, stream);
    },
    [stream],
  );

  // Para as tracks da câmera sempre que o stream muda ou o componente desmonta — sem isso a luz da
  // webcam ficava acesa mesmo depois de fechar o diálogo.
  useEffect(() => {
    if (!stream) return;
    if (videoRef.current) ligarVideo(videoRef.current, stream);
    return () => {
      stream.getTracks().forEach((track) => track.stop());
    };
  }, [stream]);

  async function tratarArquivo(arquivo: File | undefined, dados = montarDados('arquivo')) {
    if (!arquivo) return;

    let arquivoFinal = arquivo;
    if (arquivo.type.startsWith('image/')) {
      try {
        arquivoFinal = await comprimirImagem(arquivo);
      } catch {
        arquivoFinal = arquivo;
      }
    }

    const tamanhoMb = arquivoFinal.size / (1024 * 1024);
    if (tamanhoMb > tamanhoMaximoMb) {
      const sugestao = arquivoFinal.type.startsWith('image/') ? ' Tente uma foto com qualidade menor.' : '';
      aoErroValidacao?.(
        `O arquivo tem ${tamanhoMb.toFixed(1)} MB — o máximo permitido é ${tamanhoMaximoMb} MB.${sugestao}`,
      );
      return;
    }

    if (contextoFoto) {
      vincularDadosFoto(arquivoFinal, dados);
      if (pendenciasFoto(dados).length) {
        setFotoPendente({ arquivo: arquivoFinal, dados });
        return;
      }
    }
    try {
      setProcessando(true);
      await aoSelecionarArquivo(arquivoFinal);
    } finally {
      setProcessando(false);
    }
  }

  async function salvarFotoPendente() {
    if (!fotoPendente) return;
    try {
      setProcessando(true);
      await aoSelecionarArquivo(fotoPendente.arquivo);
      setFotoPendente(null);
    } catch (erro) {
      aoErroValidacao?.(erro instanceof Error ? erro.message : 'Falha ao salvar a foto. Tente novamente.');
    } finally { setProcessando(false); }
  }

  function falharAbertura() {
    if (exigirCamera) {
      aoErroValidacao?.(
        'Não foi possível acessar a câmera. Verifique se o navegador/app tem permissão de câmera liberada e tente novamente — este cadastro exige captura ao vivo, não aceita foto do álbum/arquivos.',
      );
      return;
    }
    // Sem câmera, permissão negada, ou navegador sem suporte — cai no seletor de arquivos.
    inputRef.current?.click();
  }

  async function abrirCamera() {
    if (contextoFoto?.local) setLocalFoto(contextoFoto.local);
    tentarLocalizacao();
    if (!permitirCamera || !navigator.mediaDevices?.getUserMedia) {
      falharAbertura();
      return;
    }
    // Se o usuário já escolheu uma câmera específica antes neste computador (ex.: webcam USB do
    // quiosque de assinatura facial), tenta abrir ela direto — sem isso o navegador decidiria
    // sozinho e podia cair na webcam interna do notebook em vez da externa. Se o dispositivo salvo
    // não existir mais (foi desconectado), cai para a escolha automática por facingMode abaixo.
    const preferidaId = lerCameraPreferida();
    if (preferidaId) {
      try {
        const novoStream = await navigator.mediaDevices.getUserMedia({ video: { deviceId: { exact: preferidaId }, ...resolucaoIdeal(modoCamera) } });
        setStream(novoStream);
        setDispositivoAtualId(preferidaId);
        await atualizarDispositivos();
        return;
      } catch {
        // Câmera salva indisponível — segue para a tentativa padrão abaixo.
      }
    }
    try {
      const novoStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: modoCamera, ...resolucaoIdeal(modoCamera) } });
      setStream(novoStream);
      setDispositivoAtualId(novoStream.getVideoTracks()[0]?.getSettings().deviceId ?? null);
      await atualizarDispositivos();
    } catch {
      falharAbertura();
    }
  }

  // Troca de câmera com o diálogo aberto (seletor no DialogoCameraFoto) — para o stream atual e
  // abre o dispositivo escolhido, sem fechar o diálogo. Lembra a escolha pras próximas vezes neste
  // mesmo computador (pedido do usuário, 22/09).
  async function trocarDispositivo(deviceId: string) {
    if (!navigator.mediaDevices?.getUserMedia) return;
    try {
      const novoStream = await navigator.mediaDevices.getUserMedia({ video: { deviceId: { exact: deviceId }, ...resolucaoIdeal(modoCamera) } });
      setStream(novoStream);
      setDispositivoAtualId(deviceId);
      salvarCameraPreferida(deviceId);
    } catch {
      aoErroValidacao?.('Não foi possível trocar para essa câmera. Verifique se ela ainda está conectada.');
    }
  }

  function fecharCamera() {
    pararLocalizacao();
    setEstadoLocalizacao(null);
    setStream(null);
  }

  // Anexo da galeria no lugar da câmera (sem geolocalização): fecha o diálogo e abre o seletor
  // de arquivos; o local digitado no diálogo segue em localFoto.
  function anexarDaGaleria() {
    fecharCamera();
    inputRef.current?.click();
  }

  async function capturarFoto() {
    const video = videoRef.current;
    if (!video || !video.videoWidth || processando) return;
    const capturadaEm = new Date();
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext('2d')?.drawImage(video, 0, 0);
    // O rastreio roda desde a abertura, então a posição normalmente já está pronta. Se ainda não
    // estiver (GPS frio, precisão ruim), espera um pouco — o quadro já foi congelado acima, então a
    // hora da captura continua sendo a do clique.
    let loc: LocalizacaoFoto = {};
    const r = rastreador.current;
    if (contextoFoto && r) {
      let e = r.estado();
      if (e.status === 'buscando' || e.status === 'imprecisa') {
        setProcessando(true);
        setAguardandoLocalizacao(true);
        try { await r.aguardarPronta(ESPERA_LOCALIZACAO_NO_CLIQUE_MS); } finally { setAguardandoLocalizacao(false); setProcessando(false); }
        e = r.estado();
      }
      loc = e.leitura ? leituraParaLocalizacao(e.leitura)
        : { motivoLocalizacao: e.motivo ?? 'A localização não chegou a tempo. Tente novamente no local.' };
    }
    const dados = montarDados('camera', loc, capturadaEm);
    fecharCamera();
    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        void tratarArquivo(new File([blob], `captura-${Date.now()}.jpg`, { type: 'image/jpeg' }), dados).catch(erro => aoErroValidacao?.(erro instanceof Error ? erro.message : 'Falha ao salvar foto.'));
      },
      'image/jpeg',
      0.92,
    );
  }

  function onInputChange(evento: ChangeEvent<HTMLInputElement>) {
    const arquivo = evento.target.files?.[0];
    evento.target.value = '';
    void tratarArquivo(arquivo).catch(erro => aoErroValidacao?.(erro instanceof Error ? erro.message : 'Falha ao salvar foto.'));
  }

  return {
    contextoFoto, localFoto, setLocalFoto, localizacao, localizando, estadoLocalizacao, aguardandoLocalizacao, tentarLocalizacao,
    fotoPendente, salvarFotoPendente, cancelarFotoPendente: () => setFotoPendente(null),
    inputRef,
    videoRef,
    aoMontarVideo,
    processando,
    stream,
    modoCamera,
    permitirCamera,
    exigirCamera,
    dispositivosVideo,
    dispositivoAtualId,
    abrirCamera,
    fecharCamera,
    capturarFoto,
    anexarDaGaleria,
    trocarDispositivo,
    onInputChange,
  };
}

export type UseCapturaFoto = ReturnType<typeof useCapturaFoto>;
