import { useState, type ReactNode } from 'react';
import { Button, Card, FeedbackInline, StatusChip } from '@ui';
import { Checkmark16Regular, Checkmark24Regular, Fingerprint24Regular } from '@fluentui/react-icons';
import { api, type EtapaIntegracao, type IntegracaoAtendimentoDetalhe, type SessaoTreinamentoDetalhe } from '../../../lib/api';
import { credencialLeitura, type DispositivoLocal } from '../../../lib/agenteBiometricoLocal';
import { AssinaturaBiometricaBotoes } from '../../../components/assinatura/AssinaturaBiometricaBotoes';
import { PresencaBiometricaTurma } from '../../../components/treinamentos/PresencaBiometricaTurma';
import { CadastroBiometriaDialog } from './CadastroBiometriaDialog';
import { NaoAssinouDialog } from './NaoAssinouDialog';
import { AvatarIniciais, TituloSecao } from './pecas';
import { useEstilosIntegracao } from './estilos';
import { dataHoraBrasil } from './helpers';
import { montarFilaAssinaturas, resumoFila, vezDaFila, type LinhaFila } from './helpersInstrutor';

interface Props {
  integracao: IntegracaoAtendimentoDetalhe;
  etapa: EtapaIntegracao;
  codigoCurso: string;
  /** Turma da etapa: a leitura em fila dos colaboradores usa a presença dela. Carrega depois da integração. */
  sessao: SessaoTreinamentoDetalhe | null;
  funcaoPorTrabalhador: Map<string, string>;
  somenteLeitura: boolean;
  agenteDisponivel: boolean | null;
  dispositivoLocal: DispositivoLocal | null;
  recarregar: () => Promise<void>;
  aoAtualizarIntegracao: (detalhe: IntegracaoAtendimentoDetalhe) => void;
  aoErro: (mensagem: string | null) => void;
}

// Fila única de assinaturas da etapa de curso (16/11/2026, mockup v3 + decisão 4 do usuário): o instrutor e
// os colaboradores no mesmo bloco, o instrutor primeiro. Cada um assina uma vez por digital ou facial e a "vez"
// passa sozinha ao próximo. Substitui o card separado "Assinaturas do NR-xx" e o botão "Coletar assinaturas";
// continuam aqui a leitura em fila dos colaboradores (PresencaBiometricaTurma), o cadastro de biometria, o
// "Não assinou" com motivo e o "Desfazer". O instrutor assina pelas rotas próprias da etapa (o servidor
// recusa quem não é o instrutor desta integração); o colaborador, pela presença da turma da etapa.
export function FilaAssinaturasEtapa({
  integracao,
  etapa,
  codigoCurso,
  sessao,
  funcaoPorTrabalhador,
  somenteLeitura,
  agenteDisponivel,
  dispositivoLocal,
  recarregar,
  aoAtualizarIntegracao,
  aoErro,
}: Props) {
  const e = useEstilosIntegracao();
  const [marcandoNaoAssinou, setMarcandoNaoAssinou] = useState<{ trabalhadorId: string; nome: string } | null>(null);
  const [desfazendoId, setDesfazendoId] = useState<string | null>(null);
  const [cadastrando, setCadastrando] = useState<{ trabalhadorId: string; nome: string } | null>(null);

  const instrutor = integracao.instrutorTrabalhadorId
    ? { trabalhadorId: integracao.instrutorTrabalhadorId, nome: integracao.instrutorNome ?? 'Instrutor' }
    : null;
  const linhas = montarFilaAssinaturas(etapa, instrutor);
  const { feitos, total } = resumoFila(linhas);
  const vez = vezDaFila(linhas);
  const nColaboradores = linhas.filter((l) => l.tipo === 'colaborador' && !l.naoAssinou).length;
  const linhaInstrutor = linhas.find((l) => l.tipo === 'instrutor');
  const todosAssinaram = total > 0 && feitos === total;

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

  const noVez = (l: LinhaFila) => vez.tipo !== 'todos' && vez.linha.trabalhadorId === l.trabalhadorId && vez.linha.tipo === l.tipo;

  function chip(l: LinhaFila) {
    if (l.assinou) {
      return (
        <StatusChip tom="ok" icone={<Checkmark16Regular />}>
          {l.metodo ? `Assinado por ${l.metodo}` : 'Assinado'}
          {l.em ? ` em ${dataHoraBrasil(l.em)}` : ''}
        </StatusChip>
      );
    }
    if (l.naoAssinou) return <StatusChip tom="neutro">Não assinou — sem certificado</StatusChip>;
    if (l.semBiometria) return <StatusChip tom="alerta">Sem biometria</StatusChip>;
    if (!somenteLeitura && noVez(l)) return <StatusChip tom="atencao">Na vez</StatusChip>;
    return <StatusChip tom="neutro">Aguardando</StatusChip>;
  }

  // Painel da vez (mockup): quem assina agora e os botões. O instrutor tem botões próprios; os colaboradores,
  // a leitura em fila (1:N, quem encostar o dedo ou olhar para a câmera é identificado).
  let painel: ReactNode = null;
  if (!somenteLeitura) {
    if (vez.tipo === 'todos') {
      painel = (
        <div className={e.okCaixa} role="status">
          <span className={e.okCaixaIcone}>
            <Checkmark24Regular />
          </span>
          <div>
            <b style={{ display: 'block', fontSize: 15 }}>Todos assinaram</b>
            <span style={{ fontSize: 12 }}>Instrutor e colaboradores. Avance a etapa para gerar os certificados.</span>
          </div>
        </div>
      );
    } else if (vez.tipo === 'instrutor') {
      painel = (
        <div className={e.vez}>
          <div className={e.vezTopo}>
            <AvatarIniciais nome={vez.linha.nome} />
            <div>
              <div className={e.mudo}>Vez de assinar</div>
              <div className={e.vezQuem}>{vez.linha.nome} (instrutor)</div>
            </div>
          </div>
          {vez.linha.semBiometria ? (
            <Button
              appearance="primary"
              size="large"
              className={e.btnGrande}
              icon={<Fingerprint24Regular />}
              onClick={() => setCadastrando({ trabalhadorId: vez.linha.trabalhadorId, nome: vez.linha.nome })}
            >
              Cadastrar biometria do instrutor
            </Button>
          ) : (
            <>
              <AssinaturaBiometricaBotoes
                quem="integracao"
                agenteDisponivel={agenteDisponivel}
                dispositivoLocal={dispositivoLocal}
                temDigital={vez.linha.temDigital}
                temFacial={vez.linha.temFacial}
                aoJaAssinou={() => void recarregar()}
                aoDigital={async (captura, leitor) => {
                  aoAtualizarIntegracao(
                    await api.integracoesAtendimento.assinarInstrutorDigital(
                      integracao.id,
                      etapa.id,
                      credencialLeitura(leitor, captura.leituras[0]),
                      { trabalhadorId: captura.trabalhadorId, score: captura.score, imagemDigital: captura.imagemPng },
                    ),
                  );
                  void recarregar();
                }}
                aoFacial={async (foto) => {
                  aoAtualizarIntegracao(await api.integracoesAtendimento.assinarInstrutorFacial(integracao.id, etapa.id, foto));
                  void recarregar();
                }}
              />
              <div className={e.mudo} style={{ marginTop: 8 }}>A fila passa sozinha para o próximo assim que a assinatura for aceita.</div>
            </>
          )}
        </div>
      );
    } else {
      painel = (
        <div className={e.vez}>
          <div className={e.vezTopo}>
            <AvatarIniciais nome={vez.linha.nome} />
            <div>
              <div className={e.mudo}>Vez de assinar</div>
              <div className={e.vezQuem}>{vez.linha.nome}</div>
            </div>
          </div>
          {sessao ? (
            <PresencaBiometricaTurma
              detalhe={sessao}
              somenteLeitura={somenteLeitura}
              agenteDisponivel={agenteDisponivel}
              dispositivoLocal={dispositivoLocal}
              aoAtualizar={recarregar}
              aoErro={aoErro}
              textoAjuda={`Cada pessoa assina o ${codigoCurso} pela digital ou pelo rosto, uma após a outra; quem encostar o dedo ou olhar para a câmera é reconhecido e a fila passa sozinha para o próximo.`}
            />
          ) : (
            <div className={e.mudo}>Carregando a turma da etapa…</div>
          )}
        </div>
      );
    }
  }

  return (
    <Card
      titulo={
        <TituloSecao numero="·" estado={todosAssinaram ? 'feito' : 'atual'}>
          Fila de assinaturas ({feitos}/{total})
        </TituloSecao>
      }
      acoes={<span className={e.mudo}>{instrutor ? `Instrutor + ${nColaboradores} ${nColaboradores === 1 ? 'colaborador' : 'colaboradores'}` : `${nColaboradores} colaboradores`}</span>}
      subtitulo="Cada pessoa assina uma vez nesta etapa, por digital ou facial. Quem assina sai da vez e a fila chama o próximo."
    >
      {!somenteLeitura && linhaInstrutor?.semBiometria && (
        <div style={{ marginBottom: 12 }}>
          <FeedbackInline tom="erro">
            <b>O instrutor ainda não tem biometria cadastrada.</b> A etapa só avança com a assinatura biométrica dele. Cadastre agora; a fila dos colaboradores segue normalmente.
          </FeedbackInline>
        </div>
      )}

      {painel}

      {linhas.map((l) => (
        <div key={`${l.tipo}-${l.trabalhadorId}`} className={e.linhaParticipante}>
          <AvatarIniciais nome={l.nome} pequeno />
          <div>
            <b>{l.nome}</b>{' '}
            <span className={e.funcaoPessoa}>{l.tipo === 'instrutor' ? 'Instrutor' : funcaoPorTrabalhador.get(l.trabalhadorId) ?? ''}</span>
            <div style={{ marginTop: 4 }}>{chip(l)}</div>
            {l.naoAssinou && l.motivoNaoAssinou && <div className={e.mudo} style={{ marginTop: 4 }}>Motivo: {l.motivoNaoAssinou}</div>}
          </div>
          <div className={e.acoesLinha}>
            {l.semBiometria && !somenteLeitura && !(l.tipo === 'instrutor' && vez.tipo === 'instrutor') && (
              <Button
                appearance="primary"
                className={e.btnMedio}
                icon={<Fingerprint24Regular />}
                onClick={() => setCadastrando({ trabalhadorId: l.trabalhadorId, nome: l.nome })}
              >
                {l.tipo === 'instrutor' ? 'Cadastrar biometria do instrutor' : 'Cadastrar biometria agora'}
              </Button>
            )}
            {l.tipo === 'colaborador' && !l.assinou && !l.naoAssinou && !somenteLeitura && (
              <Button appearance="secondary" className={e.btnMedio} onClick={() => setMarcandoNaoAssinou({ trabalhadorId: l.trabalhadorId, nome: l.nome })}>
                Não assinou
              </Button>
            )}
            {l.naoAssinou && !somenteLeitura && (
              <Button className={e.btnMedio} disabled={desfazendoId === l.trabalhadorId} onClick={() => void desfazer(l.trabalhadorId)}>
                Desfazer
              </Button>
            )}
          </div>
        </div>
      ))}
      {!somenteLeitura && (
        <div className={e.mudo} style={{ marginTop: 8 }}>"Não assinou" pede o motivo e a pessoa fica sem certificado desta etapa.</div>
      )}

      {marcandoNaoAssinou && (
        <NaoAssinouDialog
          nome={marcandoNaoAssinou.nome}
          codigoCurso={codigoCurso}
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
    </Card>
  );
}
