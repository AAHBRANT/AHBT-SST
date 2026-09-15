import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  Select,
  StatusChip,
  Text,
  Textarea,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, SearchInfo24Regular, ArrowExit24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusInspecaoEpc,
  statusInspecaoEpcLabel,
  type CatalogoEpc,
  type InstalacaoEpc,
  type NovaInstalacaoEpc,
  type Obra,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const hoje = () => new Date().toISOString().slice(0, 10);

const instalacaoVazia = (obraId: string): NovaInstalacaoEpc => ({
  catalogoEpcId: '',
  obraId,
  localInstalacao: '',
  quantidade: 1,
  dataInstalacao: hoje(),
  dataValidade: '',
});

// Instalação/Inspeção de EPC por Obra (decisão confirmada com o usuário, 04/09) — equivalente à
// aba Entregas do EPI, mas sem trabalhador nem assinatura: o EPC fica instalado numa Obra, com
// inspeções periódicas registradas direto na linha (sem tela de agenda separada) e remoção que
// repõe o estoque.
// Onda 3 Task 22.5 (camada ui/): sem equivalente direto em EPI — mesmas conversões do Guia (1, 2, 4,
// 6) aplicadas à lógica própria de instalação/inspeção/remoção. As duas ações por linha (inspeção OU
// remoção) que a versão antiga simulava com uma TableRow extra viram o `expansivel` do DataTable
// (mesmo mecanismo do histórico de movimentações em EstoqueTab.tsx/EstoqueEpcTab.tsx), só que o
// conteúdo expandido é um formulário em vez de uma tabela aninhada.
export function InstalacoesTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [epcs, setEpcs] = useState<CatalogoEpc[]>([]);
  const [instalacoes, setInstalacoes] = useState<InstalacaoEpc[]>([]);
  const [novaInstalacao, setNovaInstalacao] = useState<NovaInstalacaoEpc>(instalacaoVazia(''));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  const [acaoAberta, setAcaoAberta] = useState<{ id: string; tipo: 'inspecao' | 'remocao' } | null>(null);
  const [inspecaoData, setInspecaoData] = useState(hoje());
  const [inspecaoStatus, setInspecaoStatus] = useState<number>(StatusInspecaoEpc.Conforme);
  const [inspecaoObs, setInspecaoObs] = useState('');
  const [remocaoData, setRemocaoData] = useState(hoje());
  const [remocaoObs, setRemocaoObs] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const [listaObras, listaEpcs] = await Promise.all([api.obras.listar(), api.catalogosEpc.listar()]);
        setObras(listaObras);
        setEpcs(listaEpcs);
        if (listaObras.length > 0) setObraId(listaObras[0].id);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e catálogo de EPC.');
      }
    })();
  }, []);

  async function carregar() {
    if (!obraId) return;
    try {
      setCarregandoLista(true);
      setErro(null);
      setInstalacoes(await api.instalacoesEpc.listar(obraId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar instalações de EPC.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    setNovaInstalacao(instalacaoVazia(obraId));
    setAcaoAberta(null);
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  function nomeEpc(catalogoEpcId: string) {
    return epcs.find((e) => e.id === catalogoEpcId)?.nome ?? '—';
  }

  async function criar() {
    if (!obraId || !novaInstalacao.catalogoEpcId) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.instalacoesEpc.criar({
        ...novaInstalacao,
        dataValidade: novaInstalacao.dataValidade || null,
      });
      setNovaInstalacao(instalacaoVazia(obraId));
      await carregar();
      sucessoToast('Instalação de EPC registrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar instalação de EPC.');
    } finally {
      setCarregando(false);
    }
  }

  function abrirInspecao(instalacao: InstalacaoEpc) {
    setAcaoAberta({ id: instalacao.id, tipo: 'inspecao' });
    setInspecaoData(hoje());
    setInspecaoStatus(StatusInspecaoEpc.Conforme);
    setInspecaoObs('');
  }

  function abrirRemocao(instalacao: InstalacaoEpc) {
    setAcaoAberta({ id: instalacao.id, tipo: 'remocao' });
    setRemocaoData(hoje());
    setRemocaoObs('');
  }

  async function salvarInspecao(id: string) {
    try {
      setCarregando(true);
      setErro(null);
      await api.instalacoesEpc.registrarInspecao(id, {
        dataInspecao: inspecaoData,
        status: inspecaoStatus,
        observacoes: inspecaoObs || null,
      });
      setAcaoAberta(null);
      await carregar();
      sucessoToast('Inspeção registrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar inspeção.');
    } finally {
      setCarregando(false);
    }
  }

  async function salvarRemocao(id: string) {
    try {
      setCarregando(true);
      setErro(null);
      await api.instalacoesEpc.registrarRemocao(id, {
        dataRemocao: remocaoData,
        observacoes: remocaoObs || null,
      });
      setAcaoAberta(null);
      await carregar();
      sucessoToast('Remoção registrada com sucesso — estoque reposto.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar remoção.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este registro de instalação de EPC? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.instalacoesEpc.excluir(id);
      await carregar();
      sucessoToast('Registro de instalação excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir registro de instalação.');
    }
  }

  const colunas: Coluna<InstalacaoEpc>[] = [
    { chave: 'epc', rotulo: 'EPC', render: (inst) => nomeEpc(inst.catalogoEpcId) },
    { chave: 'local', rotulo: 'Local', render: (inst) => inst.localInstalacao },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita', render: (inst) => inst.quantidade },
    { chave: 'instalacao', rotulo: 'Instalação', render: (inst) => inst.dataInstalacao.slice(0, 10) },
    { chave: 'validade', rotulo: 'Validade', render: (inst) => inst.dataValidade?.slice(0, 10) },
    {
      chave: 'ultimaInspecao',
      rotulo: 'Última inspeção',
      render: (inst) =>
        inst.dataUltimaInspecao
          ? `${inst.dataUltimaInspecao.slice(0, 10)} — ${statusInspecaoEpcLabel[inst.statusUltimaInspecao ?? 0] ?? ''}`
          : 'Sem inspeção',
    },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (inst) =>
        inst.dataRemocao ? (
          <StatusChip tom="neutro">Removida em {inst.dataRemocao.slice(0, 10)}</StatusChip>
        ) : (
          <StatusChip tom="ok">Instalada</StatusChip>
        ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader titulo="Instalações de EPC" />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card densidade="compacta">
        <FormSection titulo="Obra" numero={1} primeira>
          <FormGrid>
            <Campo span={6}>
              <Field label="Obra">
                <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                  <option value="">Selecione</option>
                  {obras.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </Card>

      {obraId && (
        <>
          <Card titulo="Nova instalação" densidade="compacta">
            <FormSection titulo="Dados da instalação" numero={1} primeira>
              <FormGrid>
                <Campo span={4}>
                  <Field label="EPC">
                    <Select
                      value={novaInstalacao.catalogoEpcId}
                      onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, catalogoEpcId: d.value })}
                    >
                      <option value="">Selecione</option>
                      {epcs.map((e) => (
                        <option key={e.id} value={e.id}>
                          {e.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Local de instalação">
                    <Input
                      placeholder="Ex.: Torre 2, pavimento 8"
                      value={novaInstalacao.localInstalacao ?? ''}
                      onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, localInstalacao: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Quantidade">
                    <Input
                      type="number"
                      value={String(novaInstalacao.quantidade)}
                      onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, quantidade: Number(d.value) })}
                    />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Data de instalação">
                    <CampoData
                      value={novaInstalacao.dataInstalacao}
                      onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, dataInstalacao: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Validade (se houver)">
                    <CampoData
                      value={novaInstalacao.dataValidade ?? ''}
                      onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, dataValidade: d.value })}
                    />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button
                  appearance="primary"
                  icon={<Add24Regular />}
                  onClick={criar}
                  disabled={carregando || !novaInstalacao.catalogoEpcId || novaInstalacao.quantidade <= 0}
                >
                  Registrar instalação
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Instalações registradas" densidade="compacta">
            <DataTable
              aria-label="Instalações de EPC registradas"
              colunas={colunas}
              linhas={instalacoes}
              chaveLinha={(inst) => inst.id}
              carregando={carregandoLista}
              vazio={{ titulo: 'Nenhuma instalação de EPC registrada nesta obra ainda.' }}
              acoesLinha={(inst) => (
                <div style={{ display: 'flex', gap: 4 }}>
                  {!inst.dataRemocao && (
                    <>
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<SearchInfo24Regular />}
                        onClick={() => abrirInspecao(inst)}
                        aria-label="Registrar inspeção"
                      />
                      <Button
                        appearance="subtle"
                        size="small"
                        icon={<ArrowExit24Regular />}
                        onClick={() => abrirRemocao(inst)}
                        aria-label="Registrar remoção"
                      />
                    </>
                  )}
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<Delete24Regular />}
                    onClick={() => excluir(inst.id)}
                    aria-label="Excluir"
                  />
                </div>
              )}
              expansivel={{
                aberta: (inst) => acaoAberta?.id === inst.id,
                render: (inst) =>
                  acaoAberta?.tipo === 'inspecao' ? (
                    <FormGrid>
                      <Campo span={3}>
                        <Field label="Data da inspeção">
                          <CampoData value={inspecaoData} onChange={(_, d) => setInspecaoData(d.value)} />
                        </Field>
                      </Campo>
                      <Campo span={3}>
                        <Field label="Status">
                          <Select value={String(inspecaoStatus)} onChange={(_, d) => setInspecaoStatus(Number(d.value))}>
                            <option value={String(StatusInspecaoEpc.Conforme)}>Conforme</option>
                            <option value={String(StatusInspecaoEpc.NaoConforme)}>Não conforme</option>
                          </Select>
                        </Field>
                      </Campo>
                      <Campo span={6}>
                        <Field label="Observações">
                          <Textarea value={inspecaoObs} onChange={(_, d) => setInspecaoObs(d.value)} />
                        </Field>
                      </Campo>
                      <Campo span={12}>
                        <div style={{ display: 'flex', gap: 8 }}>
                          <Button appearance="primary" onClick={() => salvarInspecao(inst.id)} disabled={carregando}>
                            Salvar inspeção
                          </Button>
                          <Button appearance="secondary" onClick={() => setAcaoAberta(null)}>
                            Cancelar
                          </Button>
                        </div>
                      </Campo>
                    </FormGrid>
                  ) : acaoAberta?.tipo === 'remocao' ? (
                    <FormGrid>
                      <Campo span={3}>
                        <Field label="Data da remoção">
                          <CampoData value={remocaoData} onChange={(_, d) => setRemocaoData(d.value)} />
                        </Field>
                      </Campo>
                      <Campo span={6}>
                        <Field label="Observações">
                          <Textarea value={remocaoObs} onChange={(_, d) => setRemocaoObs(d.value)} />
                        </Field>
                      </Campo>
                      <Campo span={12}>
                        <div style={{ display: 'flex', gap: 8 }}>
                          <Button appearance="primary" onClick={() => salvarRemocao(inst.id)} disabled={carregando}>
                            Confirmar remoção (repõe estoque)
                          </Button>
                          <Button appearance="secondary" onClick={() => setAcaoAberta(null)}>
                            Cancelar
                          </Button>
                        </div>
                      </Campo>
                    </FormGrid>
                  ) : (
                    <Text>—</Text>
                  ),
              }}
            />
          </Card>
        </>
      )}
    </div>
  );
}
