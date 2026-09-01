import { Fragment, useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import {
  api,
  nivelRiscoAprCor,
  nivelRiscoAprLabel,
  type AprEtapa,
  type AprEtapaRisco,
  type NovaAprEtapa,
  type NovoAprEtapaRisco,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

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

// Uma "etapa" (Ordem/Descrição) pode se repetir em várias linhas de risco na planilha original —
// aqui isso vira Etapa → N AprEtapaRisco, cada um com risco inicial e residual (P×S calculados
// pelo backend conforme a Matriz de Risco da APR REV.02).
function BadgeNivelRisco({ nivel }: { nivel: number }) {
  return (
    <Badge appearance="tint" style={{ backgroundColor: nivelRiscoAprCor[nivel], color: nivel === 4 ? '#fff' : '#000' }}>
      {nivelRiscoAprLabel[nivel]}
    </Badge>
  );
}

export function AprEtapasTab({ aprId }: { aprId: string }) {
  const estilos = usePageStyles();
  const [etapas, setEtapas] = useState<AprEtapa[]>([]);
  const [novaEtapa, setNovaEtapa] = useState<NovaAprEtapa>(() => etapaVazia(aprId, 1));
  const [expandidaId, setExpandidaId] = useState<string | null>(null);
  const [novoRisco, setNovoRisco] = useState<NovoAprEtapaRisco | null>(null);
  const [riscoEditandoId, setRiscoEditandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Etapas da atividade</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Ordem">
          <Input
            type="number"
            min={1}
            value={String(novaEtapa.ordem)}
            onChange={(_, d) => setNovaEtapa({ ...novaEtapa, ordem: Math.max(1, Number(d.value) || 1) })}
          />
        </Field>
        <Field label="Descrição da etapa">
          <Input value={novaEtapa.descricao} onChange={(_, d) => setNovaEtapa({ ...novaEtapa, descricao: d.value })} />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criarEtapa} disabled={carregando}>
          Adicionar etapa
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : etapas.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma etapa cadastrada ainda." />
      ) : (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Ordem</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell>Riscos cadastrados</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {etapas.map((etapa) => (
            <Fragment key={etapa.id}>
              <TableRow onClick={() => expandir(etapa)} style={{ cursor: 'pointer' }}>
                <TableCell>{etapa.ordem}</TableCell>
                <TableCell>{etapa.descricao}</TableCell>
                <TableCell>{etapa.riscos.length}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    aria-label="Excluir etapa"
                    onClick={(e) => {
                      e.stopPropagation();
                      excluirEtapa(etapa.id);
                    }}
                  />
                </TableCell>
              </TableRow>
              {expandidaId === etapa.id && (
                <TableRow>
                  <TableCell colSpan={4}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '8px 0' }}>
                      <Text weight="semibold">Perigos / riscos — {etapa.descricao}</Text>

                      {etapa.riscos.length > 0 && (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHeaderCell>Perigo / evento perigoso</TableHeaderCell>
                              <TableHeaderCell>P</TableHeaderCell>
                              <TableHeaderCell>S</TableHeaderCell>
                              <TableHeaderCell>Risco inicial</TableHeaderCell>
                              <TableHeaderCell>Responsável</TableHeaderCell>
                              <TableHeaderCell>P res.</TableHeaderCell>
                              <TableHeaderCell>S res.</TableHeaderCell>
                              <TableHeaderCell>Risco residual</TableHeaderCell>
                              <TableHeaderCell></TableHeaderCell>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {etapa.riscos.map((risco) => (
                              <TableRow key={risco.id}>
                                <TableCell>{risco.perigoEventoPerigoso}</TableCell>
                                <TableCell>{risco.probabilidadeInicial}</TableCell>
                                <TableCell>{risco.severidadeInicial}</TableCell>
                                <TableCell>
                                  <BadgeNivelRisco nivel={risco.nivelRiscoInicial} />
                                </TableCell>
                                <TableCell>{risco.responsavel ?? '-'}</TableCell>
                                <TableCell>{risco.probabilidadeResidual}</TableCell>
                                <TableCell>{risco.severidadeResidual}</TableCell>
                                <TableCell>
                                  <BadgeNivelRisco nivel={risco.nivelRiscoResidual} />
                                </TableCell>
                                <TableCell>
                                  <div style={{ display: 'flex', gap: 4 }}>
                                    <Button
                                      appearance="subtle"
                                      size="small"
                                      icon={<Edit24Regular />}
                                      aria-label="Editar"
                                      onClick={() => editarRisco(risco)}
                                    />
                                    <Button
                                      appearance="subtle"
                                      size="small"
                                      icon={<Delete24Regular />}
                                      aria-label="Excluir"
                                      onClick={() => excluirRisco(risco.id, etapa.id)}
                                    />
                                  </div>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      )}

                      {novoRisco && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                          <Text weight="semibold" size={200}>
                            {riscoEditandoId ? 'Editar risco' : 'Novo perigo / risco'}
                          </Text>
                          <div className={estilos.form}>
                            <Field label="Perigo / evento perigoso" required>
                              <Input
                                value={novoRisco.perigoEventoPerigoso}
                                onChange={(_, d) => setNovoRisco({ ...novoRisco, perigoEventoPerigoso: d.value })}
                              />
                            </Field>
                            <Field label="Fonte / circunstância">
                              <Input
                                value={novoRisco.fonteCircunstancia ?? ''}
                                onChange={(_, d) => setNovoRisco({ ...novoRisco, fonteCircunstancia: d.value })}
                              />
                            </Field>
                            <Field label="Possíveis lesões / agravos / danos">
                              <Input
                                value={novoRisco.possiveisLesoes ?? ''}
                                onChange={(_, d) => setNovoRisco({ ...novoRisco, possiveisLesoes: d.value })}
                              />
                            </Field>
                            <Field label="Trabalhadores expostos">
                              <Input
                                value={novoRisco.trabalhadoresExpostos ?? ''}
                                onChange={(_, d) => setNovoRisco({ ...novoRisco, trabalhadoresExpostos: d.value })}
                              />
                            </Field>
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
                            <Field label="Responsável">
                              <Input
                                value={novoRisco.responsavel ?? ''}
                                onChange={(_, d) => setNovoRisco({ ...novoRisco, responsavel: d.value })}
                                placeholder="ex.: Encarregado / Operador"
                              />
                            </Field>
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
                            <Field label="Medidas de prevenção / controle" style={{ gridColumn: '1 / -1' }}>
                              <Textarea
                                value={novoRisco.medidasPrevencao ?? ''}
                                onChange={(_, d) => setNovoRisco({ ...novoRisco, medidasPrevencao: d.value })}
                              />
                            </Field>
                          </div>
                          <div className={estilos.formActions}>
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
                          </div>
                        </div>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </Fragment>
          ))}
        </TableBody>
      </Table>
      )}
    </div>
  );
}
