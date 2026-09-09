import { useEffect, useState } from 'react';
import {
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
import { CampoData } from '../../components/CampoData';
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
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

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
export function InstalacoesTab() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [epcs, setEpcs] = useState<CatalogoEpc[]>([]);
  const [instalacoes, setInstalacoes] = useState<InstalacaoEpc[]>([]);
  const [novaInstalacao, setNovaInstalacao] = useState<NovaInstalacaoEpc>(instalacaoVazia(''));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(false);
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Instalações de EPC</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

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
      </div>

      {obraId && (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Nova instalação</Text>
            </div>
            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Instalação</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col4}>
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
              </div>
              <div className={estilos.col4}>
                <Field label="Local de instalação">
                  <Input
                    placeholder="Ex.: Torre 2, pavimento 8"
                    value={novaInstalacao.localInstalacao ?? ''}
                    onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, localInstalacao: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Quantidade">
                  <Input
                    type="number"
                    value={String(novaInstalacao.quantidade)}
                    onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, quantidade: Number(d.value) })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Data de instalação">
                  <CampoData
                    value={novaInstalacao.dataInstalacao}
                    onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, dataInstalacao: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Validade (se houver)">
                  <CampoData
                    value={novaInstalacao.dataValidade ?? ''}
                    onChange={(_, d) => setNovaInstalacao({ ...novaInstalacao, dataValidade: d.value })}
                  />
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button
                appearance="primary"
                icon={<Add24Regular />}
                onClick={criar}
                disabled={carregando || !novaInstalacao.catalogoEpcId || novaInstalacao.quantidade <= 0}
              >
                Registrar instalação
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Instalações registradas</Text>
            </div>
            {carregandoLista ? (
              <ListaCarregando />
            ) : instalacoes.length === 0 ? (
              <EstadoVazio mensagem="Nenhuma instalação de EPC registrada nesta obra ainda." />
            ) : (
              <Table noNativeElements>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>EPC</TableHeaderCell>
                    <TableHeaderCell>Local</TableHeaderCell>
                    <TableHeaderCell>Qtd.</TableHeaderCell>
                    <TableHeaderCell>Instalação</TableHeaderCell>
                    <TableHeaderCell>Validade</TableHeaderCell>
                    <TableHeaderCell>Última inspeção</TableHeaderCell>
                    <TableHeaderCell>Situação</TableHeaderCell>
                    <TableHeaderCell></TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {instalacoes.map((inst) => (
                    <>
                      <TableRow key={inst.id}>
                        <TableCell>{nomeEpc(inst.catalogoEpcId)}</TableCell>
                        <TableCell>{inst.localInstalacao}</TableCell>
                        <TableCell>{inst.quantidade}</TableCell>
                        <TableCell>{inst.dataInstalacao.slice(0, 10)}</TableCell>
                        <TableCell>{inst.dataValidade?.slice(0, 10)}</TableCell>
                        <TableCell>
                          {inst.dataUltimaInspecao
                            ? `${inst.dataUltimaInspecao.slice(0, 10)} — ${statusInspecaoEpcLabel[inst.statusUltimaInspecao ?? 0] ?? ''}`
                            : 'Sem inspeção'}
                        </TableCell>
                        <TableCell>
                          {inst.dataRemocao ? (
                            <Text size={200}>Removida em {inst.dataRemocao.slice(0, 10)}</Text>
                          ) : (
                            <Text size={200}>Instalada</Text>
                          )}
                        </TableCell>
                        <TableCell>
                          <div style={{ display: 'flex', gap: 4 }}>
                            {!inst.dataRemocao && (
                              <>
                                <Button
                                  appearance="subtle"
                                  icon={<SearchInfo24Regular />}
                                  onClick={() => abrirInspecao(inst)}
                                  aria-label="Registrar inspeção"
                                />
                                <Button
                                  appearance="subtle"
                                  icon={<ArrowExit24Regular />}
                                  onClick={() => abrirRemocao(inst)}
                                  aria-label="Registrar remoção"
                                />
                              </>
                            )}
                            <Button
                              appearance="subtle"
                              icon={<Delete24Regular />}
                              onClick={() => excluir(inst.id)}
                              aria-label="Excluir"
                            />
                          </div>
                        </TableCell>
                      </TableRow>
                      {acaoAberta?.id === inst.id && acaoAberta.tipo === 'inspecao' && (
                        <TableRow key={`${inst.id}-inspecao`}>
                          <TableCell colSpan={8}>
                            <div className={estilos.formGrid} style={{ padding: '8px 0' }}>
                              <div className={estilos.col3}>
                                <Field label="Data da inspeção">
                                  <CampoData value={inspecaoData} onChange={(_, d) => setInspecaoData(d.value)} />
                                </Field>
                              </div>
                              <div className={estilos.col3}>
                                <Field label="Status">
                                  <Select
                                    value={String(inspecaoStatus)}
                                    onChange={(_, d) => setInspecaoStatus(Number(d.value))}
                                  >
                                    <option value={String(StatusInspecaoEpc.Conforme)}>Conforme</option>
                                    <option value={String(StatusInspecaoEpc.NaoConforme)}>Não conforme</option>
                                  </Select>
                                </Field>
                              </div>
                              <div className={estilos.col6}>
                                <Field label="Observações">
                                  <Textarea value={inspecaoObs} onChange={(_, d) => setInspecaoObs(d.value)} />
                                </Field>
                              </div>
                              <div className={estilos.col12} style={{ display: 'flex', gap: 8 }}>
                                <Button appearance="primary" onClick={() => salvarInspecao(inst.id)} disabled={carregando}>
                                  Salvar inspeção
                                </Button>
                                <Button appearance="secondary" onClick={() => setAcaoAberta(null)}>
                                  Cancelar
                                </Button>
                              </div>
                            </div>
                          </TableCell>
                        </TableRow>
                      )}
                      {acaoAberta?.id === inst.id && acaoAberta.tipo === 'remocao' && (
                        <TableRow key={`${inst.id}-remocao`}>
                          <TableCell colSpan={8}>
                            <div className={estilos.formGrid} style={{ padding: '8px 0' }}>
                              <div className={estilos.col3}>
                                <Field label="Data da remoção">
                                  <CampoData value={remocaoData} onChange={(_, d) => setRemocaoData(d.value)} />
                                </Field>
                              </div>
                              <div className={estilos.col6}>
                                <Field label="Observações">
                                  <Textarea value={remocaoObs} onChange={(_, d) => setRemocaoObs(d.value)} />
                                </Field>
                              </div>
                              <div className={estilos.col12} style={{ display: 'flex', gap: 8 }}>
                                <Button appearance="primary" onClick={() => salvarRemocao(inst.id)} disabled={carregando}>
                                  Confirmar remoção (repõe estoque)
                                </Button>
                                <Button appearance="secondary" onClick={() => setAcaoAberta(null)}>
                                  Cancelar
                                </Button>
                              </div>
                            </div>
                          </TableCell>
                        </TableRow>
                      )}
                    </>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
