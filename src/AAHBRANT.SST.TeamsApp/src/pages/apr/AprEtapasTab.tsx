import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  StatusChip,
  Text,
  Textarea,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import {
  api,
  nivelRiscoAprLabel,
  type AprEtapa,
  type AprEtapaRisco,
  type NovaAprEtapa,
  type NovoAprEtapaRisco,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function etapaVazia(aprId: string, proximaOrdem: number): NovaAprEtapa {
  return { aprId, ordem: proximaOrdem, descricao: '' };
}

function riscoVazio(aprEtapaId: string): NovoAprEtapaRisco {
  return {
    aprEtapaId,
    perigoEventoPerigoso: '',
    fonteCircunstancia: '',
    possiveisLesoes: '',
    trabalhadoresExpostos: '',
    probabilidadeInicial: 1,
    severidadeInicial: 1,
    medidasPrevencao: '',
    responsavel: '',
    probabilidadeResidual: 1,
    severidadeResidual: 1,
  };
}

// Fórmula fixa da Matriz de Risco da APR REV.02 (1-4 Baixo, 5-9 Moderado, 10-15 Alto, 16-25 Crítico).
const tomPorNivelRisco: Record<number, Tom> = { 1: 'ok', 2: 'atencao', 3: 'alerta', 4: 'alerta' };

function ChipNivelRisco({ nivel }: { nivel: number }) {
  return <StatusChip tom={tomPorNivelRisco[nivel]}>{nivelRiscoAprLabel[nivel]}</StatusChip>;
}

const colunasRiscos: Coluna<AprEtapaRisco>[] = [
  { chave: 'perigo', rotulo: 'Perigo / evento perigoso', render: (r) => r.perigoEventoPerigoso },
  { chave: 'p', rotulo: 'P', render: (r) => r.probabilidadeInicial },
  { chave: 's', rotulo: 'S', render: (r) => r.severidadeInicial },
  { chave: 'riscoInicial', rotulo: 'Risco inicial', render: (r) => <ChipNivelRisco nivel={r.nivelRiscoInicial} /> },
  { chave: 'responsavel', rotulo: 'Responsável', render: (r) => r.responsavel ?? '-' },
  { chave: 'pRes', rotulo: 'P res.', render: (r) => r.probabilidadeResidual },
  { chave: 'sRes', rotulo: 'S res.', render: (r) => r.severidadeResidual },
  { chave: 'riscoResidual', rotulo: 'Risco residual', render: (r) => <ChipNivelRisco nivel={r.nivelRiscoResidual} /> },
];

// Onda 2 Task 11 (camada ui/): uma "etapa" (Ordem/Descrição) pode se repetir em várias linhas de
// risco na planilha original — aqui isso vira Etapa → N AprEtapaRisco, cada um com risco inicial e
// residual (P×S calculados pelo backend). DataTable expansivel (mesmo padrão de MatrizEpiTab) com
// uma segunda DataTable aninhada para os riscos da etapa aberta.
export function AprEtapasTab({ aprId }: { aprId: string }) {
  const [etapas, setEtapas] = useState<AprEtapa[]>([]);
  const [novaEtapa, setNovaEtapa] = useState<NovaAprEtapa>(() => etapaVazia(aprId, 1));
  const [expandidaId, setExpandidaId] = useState<string | null>(null);
  const [novoRisco, setNovoRisco] = useState<NovoAprEtapaRisco | null>(null);
  const [riscoEditandoId, setRiscoEditandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const etps = await api.aprEtapas.listar(aprId);
      setEtapas(etps);
      setNovaEtapa(etapaVazia(aprId, etps.length + 1));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar etapas.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aprId]);

  async function criarEtapa() {
    if (!novaEtapa.descricao.trim()) {
      setErro('Informe a descrição da etapa.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.aprEtapas.criar(novaEtapa);
      await carregar();
      sucessoToast('Etapa criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar etapa.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluirEtapa(id: string) {
    if (!(await confirmar('Excluir esta etapa? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.aprEtapas.excluir(id);
      await carregar();
      sucessoToast('Etapa excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir etapa.');
    }
  }

  function expandir(etapa: AprEtapa) {
    if (expandidaId === etapa.id) {
      setExpandidaId(null);
      setNovoRisco(null);
      setRiscoEditandoId(null);
      return;
    }
    setExpandidaId(etapa.id);
    setNovoRisco(riscoVazio(etapa.id));
    setRiscoEditandoId(null);
  }

  function editarRisco(risco: AprEtapaRisco) {
    setRiscoEditandoId(risco.id);
    setNovoRisco({
      aprEtapaId: risco.aprEtapaId,
      perigoEventoPerigoso: risco.perigoEventoPerigoso,
      fonteCircunstancia: risco.fonteCircunstancia ?? '',
      possiveisLesoes: risco.possiveisLesoes ?? '',
      trabalhadoresExpostos: risco.trabalhadoresExpostos ?? '',
      probabilidadeInicial: risco.probabilidadeInicial,
      severidadeInicial: risco.severidadeInicial,
      medidasPrevencao: risco.medidasPrevencao ?? '',
      responsavel: risco.responsavel ?? '',
      probabilidadeResidual: risco.probabilidadeResidual,
      severidadeResidual: risco.severidadeResidual,
    });
  }

  async function salvarRisco() {
    if (!novoRisco) return;
    if (!novoRisco.perigoEventoPerigoso.trim()) {
      setErro('Informe o perigo/evento perigoso.');
      return;
    }
    const eraEdicao = !!riscoEditandoId;
    try {
      setCarregando(true);
      setErro(null);
      if (riscoEditandoId) {
        const { aprEtapaId: _aprEtapaId, ...resto } = novoRisco;
        await api.aprEtapas.atualizarRisco(riscoEditandoId, resto);
      } else {
        await api.aprEtapas.criarRisco(novoRisco);
      }
      setNovoRisco(riscoVazio(novoRisco.aprEtapaId));
      setRiscoEditandoId(null);
      await carregar();
      sucessoToast(eraEdicao ? 'Risco atualizado com sucesso.' : 'Risco adicionado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar risco.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluirRisco(id: string, aprEtapaId: string) {
    if (!(await confirmar('Excluir este risco? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.aprEtapas.excluirRisco(id);
      if (riscoEditandoId === id) {
        setRiscoEditandoId(null);
        setNovoRisco(riscoVazio(aprEtapaId));
      }
      await carregar();
      sucessoToast('Risco excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir risco.');
    }
  }

  const colunasEtapas: Coluna<AprEtapa>[] = [
    { chave: 'ordem', rotulo: 'Ordem' },
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'riscos', rotulo: 'Riscos cadastrados', render: (e) => e.riscos.length },
  ];

  return (
    <>
      <Card titulo="Etapas da atividade">
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}

        <FormSection titulo="Nova etapa" numero={1} primeira>
          <FormGrid>
            <Campo span={2}>
              <Field label="Ordem">
                <Input
                  type="number"
                  min={1}
                  value={String(novaEtapa.ordem)}
                  onChange={(_, d) => setNovaEtapa({ ...novaEtapa, ordem: Math.max(1, Number(d.value) || 1) })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Descrição da etapa">
                <Input value={novaEtapa.descricao} onChange={(_, d) => setNovaEtapa({ ...novaEtapa, descricao: d.value })} />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criarEtapa} disabled={carregando}>
              Adicionar etapa
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Etapas da atividade"
          colunas={colunasEtapas}
          linhas={etapas}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhuma etapa cadastrada ainda.' }}
          aoClicarLinha={expandir}
          acoesLinha={(e) => (
            <Button
              appearance="subtle"
              icon={<Delete24Regular />}
              aria-label="Excluir etapa"
              onClick={() => excluirEtapa(e.id)}
            />
          )}
          expansivel={{
            aberta: (e) => expandidaId === e.id,
            render: (etapa) => (
              <>
                <Text weight="semibold">Perigos / riscos — {etapa.descricao}</Text>

                {etapa.riscos.length > 0 && (
                  <DataTable
                    aria-label={`Riscos da etapa ${etapa.descricao}`}
                    densidade="compacta"
                    colunas={colunasRiscos}
                    linhas={etapa.riscos}
                    chaveLinha={(r) => r.id}
                    acoesLinha={(r) => (
                      <>
                        <Button
                          appearance="subtle"
                          size="small"
                          icon={<Edit24Regular />}
                          aria-label="Editar"
                          onClick={() => editarRisco(r)}
                        />
                        <Button
                          appearance="subtle"
                          size="small"
                          icon={<Delete24Regular />}
                          aria-label="Excluir"
                          onClick={() => excluirRisco(r.id, etapa.id)}
                        />
                      </>
                    )}
                  />
                )}

                {novoRisco && (
                  <FormSection titulo={riscoEditandoId ? 'Editar risco' : 'Novo perigo / risco'}>
                    <FormGrid>
                      <Campo span={6}>
                        <Field label="Perigo / evento perigoso" required>
                          <Input
                            value={novoRisco.perigoEventoPerigoso}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, perigoEventoPerigoso: d.value })}
                          />
                        </Field>
                      </Campo>
                      <Campo span={6}>
                        <Field label="Fonte / circunstância">
                          <Input
                            value={novoRisco.fonteCircunstancia ?? ''}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, fonteCircunstancia: d.value })}
                          />
                        </Field>
                      </Campo>
                      <Campo span={6}>
                        <Field label="Possíveis lesões / agravos / danos">
                          <Input
                            value={novoRisco.possiveisLesoes ?? ''}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, possiveisLesoes: d.value })}
                          />
                        </Field>
                      </Campo>
                      <Campo span={6}>
                        <Field label="Funcionários expostos">
                          <Input
                            value={novoRisco.trabalhadoresExpostos ?? ''}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, trabalhadoresExpostos: d.value })}
                          />
                        </Field>
                      </Campo>
                      <Campo span={2}>
                        <Field label="P (probabilidade inicial)">
                          <Select
                            value={String(novoRisco.probabilidadeInicial)}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, probabilidadeInicial: Number(d.value) })}
                          >
                            {[1, 2, 3, 4, 5].map((v) => (
                              <option key={v} value={v}>
                                {v}
                              </option>
                            ))}
                          </Select>
                        </Field>
                      </Campo>
                      <Campo span={2}>
                        <Field label="S (severidade inicial)">
                          <Select
                            value={String(novoRisco.severidadeInicial)}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, severidadeInicial: Number(d.value) })}
                          >
                            {[1, 2, 3, 4, 5].map((v) => (
                              <option key={v} value={v}>
                                {v}
                              </option>
                            ))}
                          </Select>
                        </Field>
                      </Campo>
                      <Campo span={4}>
                        <Field label="Responsável">
                          <Input
                            value={novoRisco.responsavel ?? ''}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, responsavel: d.value })}
                            placeholder="ex.: Encarregado / Operador"
                          />
                        </Field>
                      </Campo>
                      <Campo span={2}>
                        <Field label="P res. (probabilidade residual)">
                          <Select
                            value={String(novoRisco.probabilidadeResidual)}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, probabilidadeResidual: Number(d.value) })}
                          >
                            {[1, 2, 3, 4, 5].map((v) => (
                              <option key={v} value={v}>
                                {v}
                              </option>
                            ))}
                          </Select>
                        </Field>
                      </Campo>
                      <Campo span={2}>
                        <Field label="S res. (severidade residual)">
                          <Select
                            value={String(novoRisco.severidadeResidual)}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, severidadeResidual: Number(d.value) })}
                          >
                            {[1, 2, 3, 4, 5].map((v) => (
                              <option key={v} value={v}>
                                {v}
                              </option>
                            ))}
                          </Select>
                        </Field>
                      </Campo>
                      <Campo span={12}>
                        <Field label="Medidas de prevenção / controle">
                          <Textarea
                            value={novoRisco.medidasPrevencao ?? ''}
                            onChange={(_, d) => setNovoRisco({ ...novoRisco, medidasPrevencao: d.value })}
                          />
                        </Field>
                      </Campo>
                    </FormGrid>
                    <FormRodape>
                      <Button appearance="primary" onClick={salvarRisco} disabled={carregando}>
                        {riscoEditandoId ? 'Salvar risco' : 'Adicionar risco'}
                      </Button>
                      {riscoEditandoId && (
                        <Button
                          appearance="secondary"
                          onClick={() => {
                            setRiscoEditandoId(null);
                            setNovoRisco(riscoVazio(etapa.id));
                          }}
                        >
                          Cancelar edição
                        </Button>
                      )}
                    </FormRodape>
                  </FormSection>
                )}
              </>
            ),
          }}
        />
      </Card>
      {dialogElement}
    </>
  );
}
