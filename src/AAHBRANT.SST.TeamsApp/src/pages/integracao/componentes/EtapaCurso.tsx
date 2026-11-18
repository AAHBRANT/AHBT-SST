import { Card, FeedbackInline, StatusChip } from '@ui';
import { PersonRegular } from '@fluentui/react-icons';
import type { EtapaIntegracao, FotosAutomaticasIntegracao, IntegracaoAtendimentoDetalhe, SessaoTreinamentoDetalhe } from '../../../lib/api';
import type { DispositivoLocal } from '../../../lib/agenteBiometricoLocal';
import { lerInstanteUtc } from '../../../components/assinatura/assinaturaDigital';
import { FotosTurma } from '../../../components/treinamentos/FotosTurma';
import { BlocoInstrutorTurma } from '../../treinamentos/BlocoInstrutorTurma';
import { CargaEtapa } from './CargaEtapa';
import { FilaAssinaturasEtapa } from './FilaAssinaturasEtapa';
import { FotosAutomaticasEtapa } from './FotosAutomaticasEtapa';
import { TituloSecao } from './pecas';
import { useEstilosIntegracao } from './estilos';
import { TOTAL_FOTOS_ETAPA, nomesCurtos, participantesAtivos, rotuloEtapa } from './helpers';
import { formatarHoraBrasil } from './helpersInstrutor';

interface Props {
  integracao: IntegracaoAtendimentoDetalhe;
  etapa: EtapaIntegracao;
  /** Turma da etapa (presença, fotos e instrutor). Carrega depois da integração. */
  sessao: SessaoTreinamentoDetalhe | null;
  /** Recarrega a turma e a integração (carga, fotos e assinaturas mudam o que falta para avançar). */
  recarregar: () => Promise<void>;
  aoAtualizarIntegracao: (detalhe: IntegracaoAtendimentoDetalhe) => void;
  aoErro: (mensagem: string | null) => void;
  agenteDisponivel: boolean | null;
  dispositivoLocal: DispositivoLocal | null;
  /** Etapa já concluída (ou integração concluída): nada mais se altera. */
  somenteLeitura: boolean;
  /** Relógio da tela (ms), para a carga automática e a espera do fim. */
  agoraMs: number;
  /** Intervalos das fotos automáticas (servidor). */
  fotosAutomaticas?: FotosAutomaticasIntegracao | null;
}

// Etapa de curso (mockup v3, 16/11/2026): cartão da etapa, carga horária AUTOMÁTICA, fotos (automáticas, com
// "Tirar foto agora" como plano B) e a fila única de assinaturas (instrutor + colaboradores). A turma da etapa
// é uma SessaoTreinamento comum, então presença e fotos reaproveitam os mesmos componentes e endpoints da
// turma. Cada seção tem `data-alvo`: as linhas "Falta" da página rolam até ela e a destacam.
export function EtapaCurso({
  integracao,
  etapa,
  sessao,
  recarregar,
  aoAtualizarIntegracao,
  aoErro,
  agenteDisponivel,
  dispositivoLocal,
  somenteLeitura,
  agoraMs,
  fotosAutomaticas,
}: Props) {
  const e = useEstilosIntegracao();
  const rotulo = rotuloEtapa(etapa.tipo);
  // O título do servidor já começa com o código ("NR-18 — Construção civil"); tira o prefixo para não repetir.
  const tituloSemCodigo = etapa.titulo?.replace(new RegExp(`^${rotulo.codigo}\\s*[—·:-]\\s*`), '').trim();
  const nomeCurso = tituloSemCodigo && tituloSemCodigo !== rotulo.codigo ? tituloSemCodigo : rotulo.nome;

  const participantes = etapa.participantes;
  const funcaoPorTrabalhador = new Map(integracao.participantes.map((p) => [p.trabalhadorId, p.funcao]));
  const ativos = participantesAtivos(etapa);
  const nFotos = etapa.fotosRegistradas;
  const necessarias = etapa.fotosNecessarias > 0 ? etapa.fotosNecessarias : TOTAL_FOTOS_ETAPA;
  const total = integracao.participantes.length;
  const nomesParticipantes = ativos.length === total ? 'todos os admitidos' : nomesCurtos(ativos.map((p) => p.nome)).join(', ');
  const abertaEm = etapa.iniciadaEmUtc ? lerInstanteUtc(etapa.iniciadaEmUtc).getTime() : null;

  if (etapa.status === 'pulada' || participantes.length === 0) {
    return (
      <Card
        titulo={<TituloSecao numero="–">{rotulo.codigo} · {nomeCurso}</TituloSecao>}
        acoes={<StatusChip tom="neutro">Etapa pulada</StatusChip>}
      >
        <FeedbackInline tom="info">
          <b>Ninguém precisa.</b> Nenhuma pessoa desta turma tem {rotulo.codigo} a fazer nesta integração. A etapa é pulada, sem fotos nem assinaturas.
        </FeedbackInline>
      </Card>
    );
  }

  return (
    <>
      <Card
        titulo={<TituloSecao numero={etapa.ordem} estado={somenteLeitura ? 'feito' : 'atual'}>{rotulo.codigo} · {nomeCurso}</TituloSecao>}
        acoes={<StatusChip tom={somenteLeitura ? 'ok' : 'atencao'}>{somenteLeitura ? 'Concluída' : 'Em andamento'}</StatusChip>}
      >
        {etapa.aplicabilidade && (
          <div className={e.restr}>
            <PersonRegular /> {etapa.aplicabilidade}
          </div>
        )}
        <dl className={e.kv}>
          {etapa.conteudo && (
            <>
              <dt className={e.kvRotulo}>Conteúdo</dt>
              <dd className={e.kvValor}>{etapa.conteudo}</dd>
            </>
          )}
          <dt className={e.kvRotulo}>Instrutor</dt>
          <dd className={e.kvValor}>{integracao.instrutorNome ?? sessao?.instrutor?.nome ?? '—'}</dd>
          <dt className={e.kvRotulo}>Participantes</dt>
          <dd className={e.kvValor}>
            <b>{ativos.length}</b>{ativos.length > 0 ? ` (${nomesParticipantes})` : ' (ninguém para assinar nesta etapa)'}
          </dd>
          {abertaEm != null && Number.isFinite(abertaEm) && (
            <>
              <dt className={e.kvRotulo}>Aberta às</dt>
              <dd className={e.kvValor}>{formatarHoraBrasil(abertaEm)}</dd>
            </>
          )}
        </dl>
      </Card>

      {sessao && !sessao.instrutor && !somenteLeitura && (
        <div data-alvo="instrutor" tabIndex={-1} className={e.secaoAlvo}>
          <BlocoInstrutorTurma detalhe={sessao} agenteDisponivel={agenteDisponivel} dispositivoLocal={dispositivoLocal} aoAtualizar={recarregar} />
        </div>
      )}

      <div data-alvo="carga" tabIndex={-1} className={e.secaoAlvo}>
        <CargaEtapa
          integracaoId={integracao.id}
          etapa={etapa}
          codigoCurso={rotulo.codigo}
          somenteLeitura={somenteLeitura}
          agoraMs={agoraMs}
          aoAtualizar={aoAtualizarIntegracao}
          aoErro={aoErro}
        />
      </div>

      {sessao && (
        <div data-alvo="fotos" tabIndex={-1} className={e.secaoAlvo}>
          <Card
            titulo={<TituloSecao numero="·" estado={nFotos >= necessarias ? 'feito' : 'atual'}>Fotos da etapa ({nFotos}/{necessarias})</TituloSecao>}
            acoes={nFotos >= necessarias ? <StatusChip tom="ok">Completas</StatusChip> : <StatusChip tom="atencao">Faltam {necessarias - nFotos}</StatusChip>}
            subtitulo={somenteLeitura ? undefined : 'Deixe o tablet virado para a turma: o sistema tira as fotos sozinho, com a câmera ao vivo.'}
          >
            <FotosAutomaticasEtapa
              sessao={sessao}
              codigoEtapa={rotulo.codigo}
              configuracao={fotosAutomaticas}
              somenteLeitura={somenteLeitura}
              aoAtualizar={recarregar}
              aoErro={aoErro}
            />
            <div style={{ height: 14 }} />
            <FotosTurma
              detalhe={sessao}
              somenteLeitura={somenteLeitura}
              aoAtualizar={recarregar}
              exigirCamera
              exigirTokenCaptura
              localFoto={`${rotulo.codigo} · Foto exclusiva desta etapa`}
              titulo="Fotos registradas"
              subtitulo={`Evidência do ${rotulo.codigo}. Só câmera ao vivo, sem galeria nem envio de arquivo. As fotos nunca valem para outro curso. Toque em um quadro vazio para tirar uma foto agora.`}
            />
            <div className={e.fotosNota}>
              Cada foto leva carimbo de data/hora, geolocalização e curso. Mesmo no mesmo dia, cada curso tem o seu bloco de horário (ex.: NR-06 de manhã, NR-12 à tarde).
            </div>
          </Card>
        </div>
      )}

      <div data-alvo="fila" tabIndex={-1} className={e.secaoAlvo}>
        <FilaAssinaturasEtapa
          integracao={integracao}
          etapa={etapa}
          codigoCurso={rotulo.codigo}
          sessao={sessao}
          funcaoPorTrabalhador={funcaoPorTrabalhador}
          somenteLeitura={somenteLeitura}
          agenteDisponivel={agenteDisponivel}
          dispositivoLocal={dispositivoLocal}
          recarregar={recarregar}
          aoAtualizarIntegracao={aoAtualizarIntegracao}
          aoErro={aoErro}
        />
      </div>
    </>
  );
}
