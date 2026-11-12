import { useMemo, useState } from 'react';
import { Campo, FeedbackInline, Field, FormGrid, Select, StatusChip, useTipografia } from '@ui';
import {
  Camera24Regular,
  Desktop20Regular,
  Key20Regular,
  Server24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StreamCamera,
  type AgenteCamera,
  type CameraResumo,
  type EquipamentoCamera,
} from '../../lib/api';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';
import { FormularioEquipamento } from './FormularioEquipamento';
import { MarcaEstado, OpcaoCartao } from './PecasCameraIp';
import { PainelSenhaEquipamento } from './PainelSenhaEquipamento';
import { TesteConexaoCamera } from './TesteConexaoCamera';
import { situacaoDoAgente } from './rotulos';

export type OrigemImagem = 'tablet' | 'ip';

const NOVO_EQUIPAMENTO = '__novo__';
const LENTES = [1, 2] as const;

export interface OrigemImagemCameraProps {
  obraId: string;
  // Câmera em edição (nula no cadastro): a própria lente dela não conta como "em uso" e o teste usa a câmera já salva.
  camera?: CameraResumo;
  origem: OrigemImagem;
  aoMudarOrigem: (origem: OrigemImagem) => void;
  equipamentoId: string;
  aoMudarEquipamento: (id: string) => void;
  canal: number | null;
  aoMudarCanal: (canal: number | null) => void;
  stream: number;
  aoMudarStream: (stream: number) => void;
  equipamentos: EquipamentoCamera[];
  agentes: AgenteCamera[];
  todasCameras: CameraResumo[];
}

// Etapa 2 do cadastro da câmera: de onde vem a imagem. "Tablet ou navegador" é o jeito de sempre; "Câmera IP" é lida
// pelo agente instalado no PC da obra (equipamento + lente + qualidade, com teste de conexão).
export function OrigemImagemCamera({
  obraId,
  camera,
  origem,
  aoMudarOrigem,
  equipamentoId,
  aoMudarEquipamento,
  canal,
  aoMudarCanal,
  stream,
  aoMudarStream,
  equipamentos,
  agentes,
  todasCameras,
}: OrigemImagemCameraProps) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const [criandoEquipamento, setCriandoEquipamento] = useState(false);
  const [definindoSenha, setDefinindoSenha] = useState(false);

  const equipamento = equipamentos.find((q) => q.id === equipamentoId);
  const agente = equipamento ? agentes.find((a) => a.id === equipamento.agenteCameraId) : undefined;
  const agentesDaObra = agentes.filter((a) => a.ativo && a.obraId === obraId);

  // Lente (canal) já usada por outra câmera do equipamento escolhido: aparece bloqueada com o nome dela.
  const lentesEmUso = useMemo(() => {
    const mapa = new Map<number, string>();
    if (!equipamentoId) return mapa;
    for (const c of todasCameras) {
      if (c.equipamentoCameraId === equipamentoId && c.canal && c.id !== camera?.id) mapa.set(c.canal, c.nome);
    }
    return mapa;
  }, [todasCameras, equipamentoId, camera?.id]);

  function primeiraLenteLivre(id: string): number | null {
    const usadas = new Set(
      todasCameras.filter((c) => c.equipamentoCameraId === id && c.canal && c.id !== camera?.id).map((c) => c.canal as number),
    );
    return LENTES.find((n) => !usadas.has(n)) ?? null;
  }

  function escolherEquipamento(valor: string) {
    setDefinindoSenha(false);
    if (valor === NOVO_EQUIPAMENTO) {
      setCriandoEquipamento(true);
      return;
    }
    setCriandoEquipamento(false);
    aoMudarEquipamento(valor);
    aoMudarCanal(valor ? primeiraLenteLivre(valor) : null);
  }

  const semObra = !obraId;
  const semAgente = !semObra && agentesDaObra.length === 0 && equipamentos.length === 0;
  const lenteLivreAgora = canal !== null && !lentesEmUso.has(canal);
  const semSenha = !!equipamento && !equipamento.senhaDefinida;
  const podeTestar = !!equipamento && lenteLivreAgora && !criandoEquipamento;

  // Câmera já salva e sem alteração na origem: testa pela câmera. Qualquer mudança no formulário testa pelo equipamento,
  // para o teste refletir o que está na tela (e não o que foi salvo antes).
  const igualAoSalvo =
    !!camera &&
    camera.equipamentoCameraId === equipamentoId &&
    camera.canal === canal &&
    (camera.stream ?? StreamCamera.Secundaria) === stream;
  const solicitarTeste = () =>
    igualAoSalvo && camera
      ? api.cameras.testarConexaoDaCamera(camera.id)
      : api.cameras.testarConexaoDoEquipamento(equipamentoId, canal ?? 1, stream);

  return (
    <>
      <div className={e.opcoes} role="radiogroup" aria-label="Origem da imagem" style={{ marginBottom: 16 }}>
        <OpcaoCartao
          ativa={origem === 'tablet'}
          aoEscolher={() => aoMudarOrigem('tablet')}
          titulo={
            <>
              <Camera24Regular aria-hidden="true" /> Tablet ou navegador
            </>
          }
          descricao="A imagem vem do aparelho que está monitorando, na tela Monitorar. É como funciona hoje."
        />
        <OpcaoCartao
          ativa={origem === 'ip'}
          aoEscolher={() => aoMudarOrigem('ip')}
          titulo={
            <>
              <Server24Regular aria-hidden="true" /> Câmera IP
            </>
          }
          descricao="Câmera fixa da obra, lida pelo agente instalado no PC da obra. Não precisa de tablet nem de navegador aberto."
        />
      </div>

      {origem === 'tablet' && (
        <FeedbackInline tom="info">
          Nada muda para câmeras de tablet. Depois de cadastrar, abra a tela <strong>Monitorar</strong> no aparelho que vai
          filmar.
        </FeedbackInline>
      )}

      {origem === 'ip' && semObra && (
        <FeedbackInline tom="info">Escolha a obra na Etapa 1 para ver os equipamentos e agentes dela.</FeedbackInline>
      )}

      {origem === 'ip' && semAgente && (
        <FeedbackInline tom="info">
          Esta obra ainda não tem agente cadastrado. Cadastre o agente do PC da obra na aba "Equipamentos e agentes" e
          depois volte para escolher a câmera.
        </FeedbackInline>
      )}

      {origem === 'ip' && !semObra && !semAgente && (
        <div>
          <FormGrid>
            <Campo span={12}>
              <Field
                label="Equipamento"
                required
                dica="A câmera física da obra. Uma câmera de duas lentes é um equipamento com duas câmeras."
              >
                <Select
                  value={criandoEquipamento ? NOVO_EQUIPAMENTO : equipamentoId}
                  onChange={(_, d) => escolherEquipamento(d.value)}
                >
                  <option value="">Selecione</option>
                  {equipamentos.map((q) => (
                    <option key={q.id} value={q.id}>
                      {q.nome} · {q.host}
                      {q.senhaDefinida ? '' : ' (sem senha)'}
                    </option>
                  ))}
                  {agentesDaObra.length > 0 && <option value={NOVO_EQUIPAMENTO}>+ Cadastrar novo equipamento…</option>}
                </Select>
              </Field>
              {equipamento && !criandoEquipamento && (
                <div className={e.resumo}>
                  <span className={e.resumoItem}>
                    <Desktop20Regular aria-hidden="true" /> Agente: {equipamento.agenteNome}
                  </span>
                  {agente && (() => {
                    const s = situacaoDoAgente(agente);
                    return <MarcaEstado tom={s.tom}>{s.rotulo}</MarcaEstado>;
                  })()}
                  {equipamento.senhaDefinida ? (
                    <span className={e.resumoItem}>
                      <Key20Regular aria-hidden="true" /> Senha definida
                    </span>
                  ) : (
                    <span className={mergeClasses(e.resumoItem, e.dicaAtencao)}>
                      <Key20Regular aria-hidden="true" /> Senha ainda não definida
                    </span>
                  )}
                </div>
              )}
            </Campo>
          </FormGrid>

          {criandoEquipamento && (
            <FormularioEquipamento
              agentes={agentes}
              obraId={obraId}
              aoSalvar={(id) => {
                setCriandoEquipamento(false);
                aoMudarEquipamento(id);
                aoMudarCanal(primeiraLenteLivre(id));
              }}
              aoCancelar={() => setCriandoEquipamento(false)}
            />
          )}

          {equipamento && !criandoEquipamento && (
            <>
              <div className={e.grupoCampo}>
                <span className={mergeClasses(tipo.corpo, e.rotuloCampo)} id="rotulo-lente" style={{ marginBottom: 0 }}>
                  Lente da câmera <span aria-hidden="true">*</span>
                </span>
                <div className={e.opcoes} role="radiogroup" aria-labelledby="rotulo-lente">
                  {LENTES.map((n) => {
                    const emUso = lentesEmUso.get(n);
                    return (
                      <OpcaoCartao
                        key={n}
                        ativa={canal === n && !emUso}
                        bloqueada={!!emUso}
                        aoEscolher={() => aoMudarCanal(n)}
                        titulo={
                          <>
                            Lente {n}
                            <StatusChip tom={emUso ? 'neutro' : 'info'}>{emUso ? 'Em uso' : 'Livre'}</StatusChip>
                          </>
                        }
                        descricao={
                          emUso
                            ? `Já é a câmera "${emUso}".`
                            : n === 1
                              ? 'Primeira lente (ou a única) do equipamento.'
                              : 'Segunda lente, para câmeras de duas lentes: cada lente é uma câmera.'
                        }
                      />
                    );
                  })}
                </div>
                {lentesEmUso.size === LENTES.length && (
                  <FeedbackInline tom="aviso">
                    As duas lentes deste equipamento já têm câmera. Escolha outro equipamento ou cadastre um novo.
                  </FeedbackInline>
                )}
              </div>

              <div className={e.grupoCampo}>
                <span className={mergeClasses(tipo.corpo, e.rotuloCampo)} id="rotulo-qualidade" style={{ marginBottom: 0 }}>
                  Qualidade da imagem
                </span>
                <div className={e.opcoes} role="radiogroup" aria-labelledby="rotulo-qualidade">
                  <OpcaoCartao
                    ativa={stream === StreamCamera.Secundaria}
                    aoEscolher={() => aoMudarStream(StreamCamera.Secundaria)}
                    titulo={
                      <>
                        Secundária <StatusChip tom="ok">Recomendada</StatusChip>
                      </>
                    }
                    descricao="Mais leve. Usa menos a internet da obra e serve para a maioria das câmeras."
                  />
                  <OpcaoCartao
                    ativa={stream === StreamCamera.Principal}
                    aoEscolher={() => aoMudarStream(StreamCamera.Principal)}
                    titulo="Principal"
                    descricao="Mais detalhe, mas usa mais internet da obra. A imagem é reduzida antes do envio."
                  />
                </div>
              </div>

              {semSenha && (
                <>
                  <FeedbackInline
                    tom="aviso"
                    acao={definindoSenha ? undefined : { rotulo: 'Definir senha do equipamento', aoClicar: () => setDefinindoSenha(true) }}
                  >
                    <strong>Falta a senha deste equipamento.</strong> Você pode salvar a câmera agora; ela fica desligada até a
                    senha ser definida.
                  </FeedbackInline>
                  {definindoSenha && (
                    <PainelSenhaEquipamento
                      equipamento={equipamento}
                      aoSalvar={() => setDefinindoSenha(false)}
                      aoCancelar={() => setDefinindoSenha(false)}
                    />
                  )}
                </>
              )}

              <TesteConexaoCamera
                podeTestar={podeTestar}
                dicaFalta={
                  lenteLivreAgora
                    ? undefined
                    : 'Escolha uma lente livre para testar a conexão.'
                }
                lente={canal}
                stream={stream}
                chaveResultado={`${equipamentoId}|${canal ?? ''}|${stream}`}
                solicitar={solicitarTeste}
              />
            </>
          )}
        </div>
      )}
    </>
  );
}
