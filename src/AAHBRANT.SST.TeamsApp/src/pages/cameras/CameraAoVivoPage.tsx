import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  ArrowClockwise20Regular,
  Clock20Regular,
  ErrorCircle24Regular,
  FullScreenMaximize20Regular,
  FullScreenMinimize20Regular,
  People24Regular,
  Pause24Regular,
  Play20Regular,
  ShieldCheckmark24Regular,
  Stop24Regular,
  Timer24Regular,
} from '@fluentui/react-icons';
import { Button, Card, Carregando, EstadoVazio, FeedbackInline, PageHeader, Spinner, useTipografia } from '@ui';
import type { CameraResumo } from '../../lib/api';
import { useCameras } from '../../lib/consultas';
import { MENSAGEM_LIMITE, TEXTO_TEMPO_MAXIMO } from './aoVivoMensagens';
import { formatarContagem } from './aoVivoRegras';
import { mergeClasses, useEstilosAoVivo } from './estilosAoVivo';
import { ehCameraIp, formatarHoraComSegundos } from './rotulos';
import { useCameraAoVivo, type EstadoAoVivo } from './useCameraAoVivo';

// Proporção da última imagem de cada câmera nesta sessão do app: na próxima abertura o quadro já reserva o tamanho certo.
const PROPORCAO_PADRAO = 16 / 9;
const proporcoesVistas = new Map<string, number>();

const ESTADOS_COM_IMAGEM: EstadoAoVivo[] = ['ao-vivo', 'atraso', 'pausado', 'tempo', 'inatividade', 'encerrada'];
const ESTADOS_AO_VIVO: EstadoAoVivo[] = ['ao-vivo', 'atraso'];

// Tela "Ver ao vivo" de uma câmera IP: imagem quase ao vivo (JPEG em sequência), que não é gravada e para sozinha.
export function CameraAoVivoPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const consulta = useCameras();
  if (consulta.isLoading) return <Carregando variante="detalhe" linhas={6} />;
  if (consulta.isError) {
    return (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void consulta.refetch() }}>
        Não foi possível consultar a câmera.
      </FeedbackInline>
    );
  }
  const camera = consulta.data?.find((c) => c.id === id);
  if (!camera) {
    return (
      <EstadoVazio
        variante="sem-resultado"
        titulo="Câmera não encontrada"
        descricao="Ela pode ter sido excluída ou pertencer a outra obra."
        acao={{ rotulo: 'Voltar para Câmeras', aoClicar: () => navigate('/cameras') }}
      />
    );
  }
  if (!ehCameraIp(camera)) {
    return (
      <EstadoVazio
        variante="sem-resultado"
        titulo="Esta câmera não é uma câmera IP"
        descricao="O Ver ao vivo só funciona em câmeras IP. Câmera de tablet usa o Monitorar."
        acao={{ rotulo: 'Voltar para Câmeras', aoClicar: () => navigate('/cameras') }}
      />
    );
  }
  return <AoVivo key={camera.id} camera={camera} />;
}

function AoVivo({ camera }: { camera: CameraResumo }) {
  const estilos = useEstilosAoVivo();
  const tipo = useTipografia();
  const navigate = useNavigate();
  const v = useCameraAoVivo(camera.id);
  const palcoRef = useRef<HTMLDivElement>(null);
  const [telaCheia, setTelaCheia] = useState(false);
  const [proporcao, setProporcao] = useState(() => proporcoesVistas.get(camera.id) ?? PROPORCAO_PADRAO);

  useEffect(() => {
    const aoMudar = () => setTelaCheia(document.fullscreenElement === palcoRef.current);
    document.addEventListener('fullscreenchange', aoMudar);
    return () => document.removeEventListener('fullscreenchange', aoMudar);
  }, []);

  async function alternarTelaCheia() {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
      else await palcoRef.current?.requestFullscreen();
    } catch {
      // O navegador pode recusar (ex.: dentro do Teams): a tela segue normal.
    }
  }

  const { estado } = v;
  const aoVivo = ESTADOS_AO_VIVO.includes(estado);
  const mostrarImagem = !!v.urlQuadro && ESTADOS_COM_IMAGEM.includes(estado);
  const hora = v.ultima ? formatarHoraComSegundos(v.ultima.hora) : null;
  const sufixoAtraso = v.haSegundos !== null ? ` · há ${v.haSegundos} s` : '';

  const textoDoEstado: Record<EstadoAoVivo, string> = {
    conectando: 'Conectando à câmera.',
    'ao-vivo': 'Ao vivo.',
    atraso: 'A imagem está chegando com atraso.',
    pausado: 'Transmissão pausada.',
    tempo: 'Transmissão encerrada: tempo máximo atingido.',
    inatividade: 'Transmissão encerrada por inatividade.',
    encerrada: v.mensagem?.titulo ?? 'Transmissão encerrada.',
    falha: v.mensagem?.titulo ?? 'Não foi possível abrir a imagem.',
    limite: v.mensagem?.titulo ?? MENSAGEM_LIMITE.titulo,
  };

  const botaoContinuar = (rotulo: string) => (
    <Button appearance="primary" className={estilos.botaoGrande} icon={<Play20Regular />} onClick={v.iniciar}>
      {rotulo}
    </Button>
  );
  const botaoTentar = (
    <Button appearance="primary" className={estilos.botaoGrande} icon={<ArrowClockwise20Regular />} onClick={v.iniciar}>
      Tentar de novo
    </Button>
  );
  const cartao = (icone: ReactNode, titulo: string, texto: string, botao: ReactNode) => (
    <div className={estilos.sobre}>
      <div className={estilos.cartao}>
        <span className={estilos.cartaoIcone} aria-hidden="true">
          {icone}
        </span>
        <div className={tipo.subtitulo}>{titulo}</div>
        <p className={mergeClasses(tipo.corpo, estilos.cartaoTexto)}>{texto}</p>
        {botao}
      </div>
    </div>
  );
  const bloco = (icone: ReactNode, titulo: string, texto: string) => (
    <div className={estilos.espera}>
      <span className={mergeClasses(estilos.icone, estilos.iconeAtencao)} aria-hidden="true">
        {icone}
      </span>
      <div>
        <div className={mergeClasses(tipo.subtitulo, estilos.esperaTitulo)}>{titulo}</div>
        <p className={mergeClasses(tipo.corpo, estilos.esperaTexto)} style={{ marginTop: 4 }}>
          {texto}
        </p>
      </div>
      {botaoTentar}
    </div>
  );

  return (
    <div>
      <PageHeader
        voltarPara="/cameras"
        rotuloVoltar="Voltar para Câmeras"
        titulo={`Ao vivo — ${camera.nome}`}
        subtitulo={`${camera.obraNome} · ${camera.areaNome ?? 'Sem área definida'} · Câmera IP · lente ${camera.canal ?? '—'}`}
      />

      {/* Troca de estado anunciada de forma discreta: não muda a cada imagem, só quando o estado muda. */}
      <div className={estilos.soLeitor} role="status" aria-live="polite">
        {textoDoEstado[estado]}
      </div>

      <div className={estilos.vivo}>
        <div className={estilos.colunaImagem}>
          <div
            ref={palcoRef}
            role="group"
            aria-label={`Imagem da câmera ${camera.nome}`}
            className={mergeClasses(estilos.palco, telaCheia && estilos.palcoTelaCheia)}
            style={telaCheia ? undefined : { aspectRatio: String(proporcao) }}
          >
            {mostrarImagem && (
              <img
                className={mergeClasses(estilos.quadro, !aoVivo && estilos.quadroApagado)}
                src={v.urlQuadro ?? undefined}
                alt={`Imagem da câmera ${camera.nome}`}
                onLoad={(ev) => {
                  const { naturalWidth: l, naturalHeight: a } = ev.currentTarget;
                  if (!l || !a) return;
                  const p = l / a;
                  proporcoesVistas.set(camera.id, p);
                  setProporcao((atual) => (Math.abs(atual - p) > 0.01 ? p : atual));
                }}
              />
            )}

            {estado === 'conectando' && (
              <div className={estilos.espera}>
                <Spinner size="large" aria-hidden="true" />
                <div className={mergeClasses(tipo.subtitulo, estilos.esperaTitulo)}>Conectando à câmera…</div>
                <p className={mergeClasses(tipo.corpo, estilos.esperaTexto)}>Pode levar até 1 minuto.</p>
                <Button
                  size="small"
                  onClick={() => {
                    v.cancelar();
                    navigate('/cameras');
                  }}
                >
                  Cancelar
                </Button>
              </div>
            )}

            {aoVivo && (
              <>
                <span className={mergeClasses(estilos.selo, estilos.seloTopo, tipo.legenda)}>
                  <span
                    aria-hidden="true"
                    className={estado === 'atraso' ? estilos.quadradoAtraso : estilos.ponto}
                  />
                  {estado === 'atraso' ? 'Com atraso' : 'Ao vivo'}
                </span>
                {hora && (
                  <span
                    className={mergeClasses(
                      estilos.selo,
                      estilos.seloBase,
                      tipo.legenda,
                      estado === 'atraso' && estilos.seloAtencao,
                    )}
                  >
                    Última imagem às {hora}
                    {estado === 'atraso' ? sufixoAtraso : ''}
                  </span>
                )}
              </>
            )}

            {estado === 'pausado' &&
              cartao(
                <Pause24Regular />,
                'Transmissão pausada',
                'Pausamos porque esta tela ficou em segundo plano.',
                botaoContinuar('Retomar'),
              )}
            {estado === 'tempo' &&
              cartao(
                <Timer24Regular />,
                'Transmissão encerrada',
                TEXTO_TEMPO_MAXIMO,
                botaoContinuar('Continuar assistindo'),
              )}
            {estado === 'inatividade' &&
              cartao(
                <Stop24Regular />,
                'Encerrada por inatividade',
                'Paramos porque a tela ficou parada por um tempo. Nada foi gravado.',
                botaoContinuar('Continuar assistindo'),
              )}
            {estado === 'encerrada' &&
              cartao(
                <Stop24Regular />,
                v.mensagem?.titulo ?? 'Transmissão encerrada',
                v.mensagem?.texto ?? 'A transmissão terminou. Nada foi gravado.',
                botaoContinuar('Continuar assistindo'),
              )}
            {estado === 'falha' &&
              bloco(
                <ErrorCircle24Regular />,
                v.mensagem?.titulo ?? 'Não foi possível abrir a imagem.',
                v.mensagem?.texto ?? 'Tente de novo em instantes.',
              )}
            {estado === 'limite' &&
              bloco(
                <People24Regular />,
                v.mensagem?.titulo ?? MENSAGEM_LIMITE.titulo,
                v.mensagem?.texto ?? MENSAGEM_LIMITE.texto,
              )}

            {telaCheia && (
              <Button
                className={estilos.sairTelaCheia}
                icon={<FullScreenMinimize20Regular />}
                onClick={() => void alternarTelaCheia()}
              >
                Sair da tela cheia
              </Button>
            )}
          </div>

          {estado === 'atraso' && (
            <div className={mergeClasses(estilos.avisoAtraso, tipo.corpo)} role="status">
              <Clock20Regular aria-hidden="true" />
              <div>
                <strong>A imagem está chegando com atraso.</strong> Seguimos tentando; não precisa fazer nada.
              </div>
            </div>
          )}
        </div>

        <Card>
          <aside className={estilos.painel} aria-label="Dados da transmissão">
            <div className={estilos.kvs}>
              <div>
                <div className={mergeClasses(tipo.micro, estilos.rotulo)}>Última imagem</div>
                <div
                  className={mergeClasses(
                    tipo.subtitulo,
                    estilos.valor,
                    estado === 'atraso' && estilos.valorAtencao,
                    !hora && estilos.valorApoio,
                  )}
                >
                  {hora ?? 'Ainda sem imagem'}
                  {hora && aoVivo && (
                    <small className={mergeClasses(tipo.legenda, estado === 'atraso' ? estilos.valorAtencao : estilos.valorApoio)}>
                      {estado === 'atraso' && v.haSegundos !== null ? `há ${v.haSegundos} s` : 'agora'}
                    </small>
                  )}
                </div>
              </div>
              {aoVivo && v.restante !== null && (
                <div>
                  <div className={mergeClasses(tipo.micro, estilos.rotulo)}>Para sozinha em</div>
                  <div className={mergeClasses(tipo.subtitulo, estilos.valor)} role="timer">
                    <Clock20Regular aria-hidden="true" />
                    {formatarContagem(v.restante)}
                  </div>
                </div>
              )}
            </div>

            <div className={estilos.naoGrava}>
              <ShieldCheckmark24Regular className={estilos.naoGravaIcone} aria-hidden="true" />
              <div className={tipo.corpo}>
                <strong>Não gravamos vídeo.</strong>
                <span className={mergeClasses(tipo.legenda, estilos.naoGravaDetalhe)}>
                  A imagem chega com 1 a 2 segundos de atraso e a última é apagada quando a transmissão termina.
                </span>
              </div>
            </div>

            {aoVivo && document.fullscreenEnabled && (
              <Button
                className={estilos.botaoPainel}
                icon={telaCheia ? <FullScreenMinimize20Regular /> : <FullScreenMaximize20Regular />}
                onClick={() => void alternarTelaCheia()}
              >
                {telaCheia ? 'Sair da tela cheia' : 'Tela cheia'}
              </Button>
            )}
          </aside>
        </Card>
      </div>
    </div>
  );
}
