import { Link, useNavigate, useParams } from 'react-router-dom';
import { Play20Regular, Stop20Regular, Video24Regular } from '@fluentui/react-icons';
import {
  BotaoAcao,
  Card,
  Carregando,
  EstadoVazio,
  FeedbackInline,
  KpiCard,
  PageHeader,
  StatusChip,
  useTipografia,
  type Tom,
} from '@ui';
import { tipoCameraLabel } from '../../lib/api';
import { useCameras } from '../../lib/consultas';
import { useMonitoramentoCamera, type EstadoMonitoramento } from './useMonitoramentoCamera';
import { mergeClasses, useStyles } from './estilos';
import { descreverIrregularidade, formatarHora, resumoCena } from './rotulos';

const chipPorEstado: Record<EstadoMonitoramento, { tom: Tom; rotulo: string }> = {
  monitorando: { tom: 'ok', rotulo: 'Monitorando' },
  iniciando: { tom: 'info', rotulo: 'Abrindo a câmera…' },
  parado: { tom: 'neutro', rotulo: 'Parado' },
  erro: { tom: 'alerta', rotulo: 'Câmera indisponível' },
};

// Tela de monitoramento de uma câmera: usa a câmera do próprio dispositivo (tablet na obra), detecta
// movimento no navegador e envia quadros para análise de EPI (capacete e colete).
export function MonitorarCameraPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const estilos = useStyles();
  const tipo = useTipografia();
  const { data: cameras, isLoading } = useCameras();
  const camera = cameras?.find((c) => c.id === id);
  const { videoRef, ...m } = useMonitoramentoCamera(id);

  if (isLoading) return <Carregando variante="detalhe" linhas={6} />;
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

  const monitorando = m.estado === 'monitorando';
  const abrindo = m.estado === 'iniciando';
  const chip = chipPorEstado[m.estado];
  const analise = m.ultimaAnalise;
  const ocorrencia = analise?.situacao === 'ocorrencia';

  let situacaoRotulo = '';
  let situacaoTom: Tom = 'ok';
  if (analise) {
    if (analise.situacao === 'falha') {
      situacaoRotulo = 'Análise não concluída';
      situacaoTom = 'atencao';
    } else {
      situacaoRotulo = descreverIrregularidade(analise.semCapacete, analise.semColete);
      situacaoTom = ocorrencia ? 'alerta' : 'ok';
    }
  }

  return (
    <div>
      <PageHeader
        voltarPara="/cameras"
        rotuloVoltar="Voltar para Câmeras"
        titulo={`Monitorar — ${camera.nome}`}
        subtitulo={`${camera.obraNome} · ${camera.areaNome ?? 'Sem área definida'} · ${tipoCameraLabel[camera.tipo] ?? 'Sem tipo'}`}
        acoes={<StatusChip tom={chip.tom}>{chip.rotulo}</StatusChip>}
      />

      {m.erro && <FeedbackInline tom="erro">{m.erro}</FeedbackInline>}
      {!camera.habilitada && (
        <FeedbackInline tom="aviso">O monitoramento desta câmera está desligado no cadastro.</FeedbackInline>
      )}

      <div className={estilos.colunas}>
        <div className={estilos.colunaVideo}>
          <div className={estilos.caixaVideo}>
            <video
              ref={videoRef}
              className={estilos.video}
              playsInline
              muted
              aria-label={`Imagem ao vivo da câmera do dispositivo para ${camera.nome}`}
            />
            {!monitorando && (
              <div className={estilos.espera}>
                <Video24Regular aria-hidden="true" />
                <div className={tipo.subtitulo}>
                  {abrindo ? 'Abrindo a câmera…' : 'A imagem aparece aqui quando o monitoramento começa'}
                </div>
              </div>
            )}
            {monitorando && (
              <>
                <div className={mergeClasses(estilos.selo, estilos.seloTopo, tipo.legenda)}>
                  {m.movimento ? 'Movimento detectado · analisando' : 'Cena parada'}
                </div>
                {m.segundosParaProxima > 0 && (
                  <div className={mergeClasses(estilos.selo, estilos.seloBase, tipo.legenda)}>
                    Próxima análise liberada em {m.segundosParaProxima} s
                  </div>
                )}
              </>
            )}
          </div>
          <FeedbackInline tom="aviso">
            Mantenha esta tela aberta e o tablet ligado na tomada. Se a tela bloquear ou a página fechar, o
            monitoramento para. A foto só é enviada para análise quando há movimento, no máximo uma a cada 30
            segundos.
          </FeedbackInline>
        </div>

        <div className={estilos.colunaPainel}>
          <Card densidade="compacta">
            <div className={estilos.cardInterno}>
              <div className={mergeClasses(tipo.legenda, estilos.rotuloMedio)}>
                {analise ? `Última análise · ${formatarHora(analise.em)}` : 'Última análise'}
              </div>
              {analise ? (
                <>
                  <div className={estilos.linha}>
                    <StatusChip tom={situacaoTom}>{situacaoRotulo}</StatusChip>
                    {analise.situacao !== 'falha' && (
                      <span className={tipo.corpo}>
                        {resumoCena(analise.pessoas, analise.semCapacete, analise.semColete)}
                      </span>
                    )}
                  </div>
                  {ocorrencia && (
                    <div className={tipo.corpo}>
                      {analise.alertaEnviado ? 'Alerta enviado ao Telegram' : 'Alerta não enviado'}
                    </div>
                  )}
                </>
              ) : (
                <div className={mergeClasses(tipo.corpo, estilos.rotuloMedio)}>
                  {monitorando
                    ? 'Aguardando o primeiro movimento para analisar.'
                    : 'Nenhuma análise nesta sessão ainda.'}
                </div>
              )}
            </div>
          </Card>

          <div className={estilos.indicadores}>
            <KpiCard rotulo="Análises" valor={m.contadores.analises} tom="neutro" />
            <KpiCard
              rotulo="Ocorrências"
              valor={m.contadores.ocorrencias}
              tom={m.contadores.ocorrencias > 0 ? 'alerta' : 'neutro'}
            />
            <KpiCard rotulo="Alertas enviados" valor={m.contadores.alertas} tom="neutro" />
          </div>

          <Card densidade="compacta">
            <div className={estilos.cardInterno}>
              <div className={estilos.topoOcorrencias}>
                <div className={tipo.subtitulo}>Ocorrências desta sessão</div>
                <Link to="/cameras?aba=alertas" className={mergeClasses(estilos.verTodas, tipo.corpo)}>
                  Ver todas
                </Link>
              </div>
              {m.ocorrenciasSessao.length === 0 ? (
                <div className={mergeClasses(tipo.corpo, estilos.rotuloMedio)}>
                  Nenhuma ocorrência nesta sessão. Tudo regular até agora.
                </div>
              ) : (
                <ul className={estilos.lista}>
                  {m.ocorrenciasSessao.map((o) => (
                    <li key={o.ocorrenciaId ?? o.em.getTime()} className={estilos.itemOcorrencia}>
                      <div className={tipo.corpo}>
                        {formatarHora(o.em)} · {descreverIrregularidade(o.semCapacete, o.semColete)}
                      </div>
                      <div className={mergeClasses(tipo.legenda, estilos.rotuloMedio)}>
                        {resumoCena(o.pessoas, o.semCapacete, o.semColete)}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </Card>

          {monitorando ? (
            <BotaoAcao
              tom="ver"
              aria-label="Parar monitoramento"
              icon={<Stop20Regular />}
              className={estilos.botao}
              onClick={m.parar}
            >
              Parar monitoramento
            </BotaoAcao>
          ) : (
            <BotaoAcao
              tom="baixar"
              aria-label="Iniciar monitoramento"
              icon={<Play20Regular />}
              className={estilos.botao}
              disabled={!camera.habilitada || abrindo}
              onClick={() => void m.iniciar()}
            >
              {abrindo ? 'Abrindo a câmera…' : 'Iniciar monitoramento'}
            </BotaoAcao>
          )}
        </div>
      </div>
    </div>
  );
}
