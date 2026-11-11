import { useState } from 'react';
import { Button, Card, FeedbackInline, StatusChip } from '@ui';
import { Checkmark16Regular, Fingerprint24Regular, PersonRegular } from '@fluentui/react-icons';
import { api, type EtapaIntegracao, type IntegracaoAtendimentoDetalhe, type SessaoTreinamentoDetalhe } from '../../../lib/api';
import type { DispositivoLocal } from '../../../lib/agenteBiometricoLocal';
import { PresencaBiometricaTurma } from '../../../components/treinamentos/PresencaBiometricaTurma';
import { FotosTurma } from '../../../components/treinamentos/FotosTurma';
import { BlocoInstrutorTurma } from '../../treinamentos/BlocoInstrutorTurma';
import { CargaEtapa } from './CargaEtapa';
import { CadastroBiometriaDialog } from './CadastroBiometriaDialog';
import { NaoAssinouDialog } from './NaoAssinouDialog';
import { AvatarIniciais, TituloSecao } from './pecas';
import { mergeClasses, useEstilosIntegracao } from './estilos';
import { TOTAL_FOTOS_ETAPA, dataHoraBrasil, nomesCurtos, participantesAtivos, rotuloEtapa } from './helpers';

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
}

const rotuloMetodo = (metodo?: string | null) => (metodo === 'reconhecimentoFacial' ? 'facial' : metodo === 'biometria' ? 'digital' : null);

// Etapa de curso (mockup v4): carga x mínimo, 3 fotos exclusivas da etapa e uma assinatura biométrica
// por pessoa, que gera o certificado do curso. A turma da etapa é uma SessaoTreinamento comum, então
// presença e fotos reaproveitam os mesmos componentes e endpoints da turma.
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
}: Props) {
  const e = useEstilosIntegracao();
  const rotulo = rotuloEtapa(etapa.tipo);
  // O título do servidor já começa com o código ("NR-18 — Construção civil"); tira o prefixo para não repetir.
  const tituloSemCodigo = etapa.titulo?.replace(new RegExp(`^${rotulo.codigo}\\s*[—·:-]\\s*`), '').trim();
  const nomeCurso = tituloSemCodigo && tituloSemCodigo !== rotulo.codigo ? tituloSemCodigo : rotulo.nome;
  const [coletando, setColetando] = useState(false);
  const [marcandoNaoAssinou, setMarcandoNaoAssinou] = useState<{ trabalhadorId: string; nome: string } | null>(null);
  const [desfazendoId, setDesfazendoId] = useState<string | null>(null);
  const [cadastrando, setCadastrando] = useState<{ trabalhadorId: string; nome: string } | null>(null);

  const participantes = etapa.participantes;
  const funcaoPorTrabalhador = new Map(integracao.participantes.map((p) => [p.trabalhadorId, p.funcao]));
  const ativos = participantesAtivos(etapa);
  const nAssinaram = ativos.filter((p) => p.presencaConfirmada).length;
  const todosAssinaram = ativos.length > 0 && nAssinaram === ativos.length;
  const nFotos = etapa.fotosRegistradas;
  const necessarias = etapa.fotosNecessarias > 0 ? etapa.fotosNecessarias : TOTAL_FOTOS_ETAPA;
  const total = integracao.participantes.length;
  const nomesParticipantes = ativos.length === total ? 'todos os admitidos' : nomesCurtos(ativos.map((p) => p.nome)).join(', ');

  async function desfazer(trabalhadorId: string) {
    try {
      setDesfazendoId(trabalhadorId);
      aoErro(null);
      aoAtualizarIntegracao(await api.integracoesAtendimento.desfazerNaoAssinou(integracao.id, etapa.id, trabalhadorId));
      void recarregar();
    } catch (err) {
      aoErro(err instanceof Error ? err.message : 'Não foi possível desfazer.');
    } finally {
      setDesfazendoId(null);
    }
  }

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
        <CargaEtapa integracaoId={integracao.id} etapa={etapa} somenteLeitura={somenteLeitura} aoAtualizar={aoAtualizarIntegracao} aoErro={aoErro} />
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
        </dl>
      </Card>

      {sessao && !sessao.instrutor && !somenteLeitura && (
        <BlocoInstrutorTurma
          detalhe={sessao}
          agenteDisponivel={agenteDisponivel}
          dispositivoLocal={dispositivoLocal}
          aoAtualizar={recarregar}
        />
      )}

      {sessao && (
        <Card>
          {nFotos < necessarias && !somenteLeitura && (
            <FeedbackInline tom="aviso">
              <b>Tire as {necessarias} fotos desta etapa, em horário próprio.</b> A etapa só avança com as {necessarias} fotos.
            </FeedbackInline>
          )}
          <FotosTurma
            detalhe={sessao}
            somenteLeitura={somenteLeitura}
            aoAtualizar={recarregar}
            exigirCamera
            exigirTokenCaptura
            localFoto={`${rotulo.codigo} · Foto exclusiva desta etapa`}
            titulo="Fotos da etapa"
            subtitulo={`Evidência do ${rotulo.codigo}. Só câmera ao vivo, sem galeria nem envio de arquivo. As fotos nunca valem para outro curso.`}
          />
          <div className={e.fotosNota}>
            Cada foto leva carimbo de data/hora, geolocalização e curso. Mesmo no mesmo dia, cada curso tem o seu bloco de horário (ex.: NR-06 de manhã, NR-12 à tarde).
          </div>
        </Card>
      )}

      <Card
        titulo={
          <TituloSecao numero="·" estado={todosAssinaram ? 'feito' : 'neutro'}>
            Assinaturas do {rotulo.codigo} ({nAssinaram}/{ativos.length})
          </TituloSecao>
        }
        subtitulo="Uma assinatura biométrica por pessoa neste curso, por digital ou facial. Cada uma gera o certificado da pessoa."
      >
        {!somenteLeitura && sessao && (
          <div style={{ marginBottom: 12 }}>
            <Button
              appearance="primary"
              size="large"
              className={mergeClasses(e.btnGrande, e.btnCheio)}
              icon={<Fingerprint24Regular />}
              disabled={todosAssinaram}
              aria-expanded={coletando}
              onClick={() => setColetando((v) => !v)}
            >
              {coletando ? 'Fechar a coleta de assinaturas' : 'Coletar assinaturas'}
            </Button>
            <div className={e.mudo} style={{ marginTop: 6 }}>
              {todosAssinaram
                ? 'Todas as assinaturas deste curso foram coletadas.'
                : 'Abre a fila de leitura por digital ou facial. Quem estiver sem biometria cadastra na linha da própria pessoa.'}
            </div>
          </div>
        )}

        {coletando && !somenteLeitura && !todosAssinaram && sessao && (
          <div style={{ marginBottom: 12 }}>
            <PresencaBiometricaTurma
              detalhe={sessao}
              somenteLeitura={somenteLeitura}
              agenteDisponivel={agenteDisponivel}
              dispositivoLocal={dispositivoLocal}
              aoAtualizar={recarregar}
              aoErro={aoErro}
              textoAjuda={`Abra a fila: cada pessoa assina o ${rotulo.codigo} pela digital ou pelo rosto, uma após a outra. Esta leitura vale só para este curso.`}
            />
          </div>
        )}

        {participantes.map((p) => {
          const metodo = rotuloMetodo(p.metodo);
          const semBiometria = !p.presencaConfirmada && !p.naoAssinou && !p.temDigital && !p.temFacial;
          const podeMarcar = !p.presencaConfirmada && !p.naoAssinou && !somenteLeitura;
          return (
            <div key={p.trabalhadorId} className={e.linhaParticipante}>
              <AvatarIniciais nome={p.nome} pequeno />
              <div>
                <b>{p.nome}</b> <span className={e.funcaoPessoa}>{funcaoPorTrabalhador.get(p.trabalhadorId) ?? ''}</span>
                <div style={{ marginTop: 4 }}>
                  {p.presencaConfirmada ? (
                    <StatusChip tom="ok" icone={<Checkmark16Regular />}>
                      {metodo ? `Assinado por ${metodo}` : 'Assinado'}
                      {p.presencaEmUtc ? ` em ${dataHoraBrasil(p.presencaEmUtc)}` : ''}
                    </StatusChip>
                  ) : p.naoAssinou ? (
                    <StatusChip tom="neutro">Não assinou — sem certificado</StatusChip>
                  ) : semBiometria ? (
                    <StatusChip tom="alerta">Sem biometria</StatusChip>
                  ) : (
                    <StatusChip tom="atencao">Aguardando</StatusChip>
                  )}
                </div>
                {p.naoAssinou && p.motivoNaoAssinou && <div className={e.mudo} style={{ marginTop: 4 }}>Motivo: {p.motivoNaoAssinou}</div>}
              </div>
              <div className={e.acoesLinha}>
                {semBiometria && !somenteLeitura && (
                  <Button
                    appearance="primary"
                    className={e.btnMedio}
                    icon={<Fingerprint24Regular />}
                    onClick={() => setCadastrando({ trabalhadorId: p.trabalhadorId, nome: p.nome })}
                  >
                    Cadastrar biometria agora
                  </Button>
                )}
                {podeMarcar && (
                  <Button appearance="secondary" className={e.btnMedio} onClick={() => setMarcandoNaoAssinou({ trabalhadorId: p.trabalhadorId, nome: p.nome })}>
                    Não assinou
                  </Button>
                )}
                {p.naoAssinou && !somenteLeitura && (
                  <Button className={e.btnMedio} disabled={desfazendoId === p.trabalhadorId} onClick={() => void desfazer(p.trabalhadorId)}>
                    Desfazer
                  </Button>
                )}
              </div>
            </div>
          );
        })}
      </Card>

      {marcandoNaoAssinou && (
        <NaoAssinouDialog
          nome={marcandoNaoAssinou.nome}
          codigoCurso={rotulo.codigo}
          aoFechar={() => setMarcandoNaoAssinou(null)}
          aoConfirmar={async (motivo) => {
            aoAtualizarIntegracao(await api.integracoesAtendimento.marcarNaoAssinou(integracao.id, etapa.id, marcandoNaoAssinou.trabalhadorId, motivo));
            setMarcandoNaoAssinou(null);
            void recarregar();
          }}
        />
      )}

      {cadastrando && (
        <CadastroBiometriaDialog
          trabalhadorId={cadastrando.trabalhadorId}
          trabalhadorNome={cadastrando.nome}
          aoFechar={() => setCadastrando(null)}
          aoAlterar={() => void recarregar()}
        />
      )}
    </>
  );
}
