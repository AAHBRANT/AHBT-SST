import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
} from '@fluentui/react-components';
import { Add24Regular, ArrowDownload24Regular, Signature24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  MotivoEntregaEpi,
  type AtualizarEntregaEpi,
  type CatalogoEpi,
  type EntregaEpi,
  type NovaEntregaEpi,
  type Trabalhador,
} from '../../lib/api';
import { AssinaturaEntregaEpiDialog } from '../../components/assinatura/AssinaturaEntregaEpiDialog';
import { AssinaturaDevolucaoEpiDialog } from '../../components/assinatura/AssinaturaDevolucaoEpiDialog';
import { usePageStyles } from '../pageStyles';

function entregaVazia(): NovaEntregaEpi {
  return {
    trabalhadorId: '',
    catalogoEpiId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    dataDevolucao: '',
    dataValidade: '',
    quantidade: 1,
    vistoConsorcioResponsavel: '',
    motivo: '',
    observacoes: '',
    motivoTipo: MotivoEntregaEpi.Inicial,
    numeroListaPresencaNr6: '',
    dataTreinamentoNr6: '',
  };
}

// Entregas de EPI do módulo dedicado /epi — registro, devolução (repõe estoque no backend),
// ficha em PDF e atalho para a assinatura eletrônica (AssinarEntregaEpiPage). O bloqueio de
// estoque insuficiente / CA vencido acontece no backend (CriarEntregaEpiCommand); o erro retornado
// é exibido como veio, mesmo padrão já usado em todo o resto do frontend (ver api.ts request()).
interface EntregasTabProps {
  aoNavegarParaMatriz: () => void;
}

export function EntregasTab({ aoNavegarParaMatriz }: EntregasTabProps) {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [episPermitidos, setEpisPermitidos] = useState<CatalogoEpi[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaEpi>(entregaVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [devolucaoId, setDevolucaoId] = useState<string | null>(null);
  const [devolucaoData, setDevolucaoData] = useState('');
  const [devolucaoQtd, setDevolucaoQtd] = useState('');
  const [entregaParaAssinar, setEntregaParaAssinar] = useState<EntregaEpi | null>(null);
  const [devolucaoParaAssinar, setDevolucaoParaAssinar] = useState<EntregaEpi | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaEpis, listaTrabalhadores] = await Promise.all([
        api.entregasEpi.listar(),
        api.catalogosEpi.listar(),
        api.trabalhadores.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPI.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  useEffect(() => {
    let cancelado = false;
    async function carregarEpisPermitidos() {
      const trabalhador = trabalhadores.find((t) => t.id === novaEntrega.trabalhadorId);
      if (!trabalhador) {
        setEpisPermitidos([]);
        return;
      }
      try {
        const lista = await api.funcoes.listarEpis(trabalhador.funcaoId);
        if (!cancelado) setEpisPermitidos(lista);
      } catch {
        if (!cancelado) setEpisPermitidos([]);
      }
    }
    carregarEpisPermitidos();
    return () => {
      cancelado = true;
    };
  }, [novaEntrega.trabalhadorId, trabalhadores]);

  function nomeEpi(id: string) {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  function vencido(dataValidade?: string | null) {
    if (!dataValidade) return false;
    return new Date(dataValidade) < new Date(new Date().toDateString());
  }

  async function criar() {
    if (!novaEntrega.trabalhadorId || !novaEntrega.catalogoEpiId || !novaEntrega.dataEntrega || novaEntrega.quantidade < 1) {
      setErro('Preencha trabalhador, EPI, data de entrega e quantidade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const payload: NovaEntregaEpi = {
        ...novaEntrega,
        dataDevolucao: novaEntrega.dataDevolucao || null,
        dataValidade: novaEntrega.dataValidade || null,
        numeroListaPresencaNr6: novaEntrega.numeroListaPresencaNr6 || null,
        dataTreinamentoNr6: novaEntrega.dataTreinamentoNr6 || null,
      };
      const { id } = await api.entregasEpi.criar(payload);
      setEntregaParaAssinar({ ...payload, id });
      setNovaEntrega(entregaVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar entrega de EPI.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarDevolucao(entrega: EntregaEpi) {
    setDevolucaoId(entrega.id);
    setDevolucaoData(new Date().toISOString().slice(0, 10));
    setDevolucaoQtd(String(entrega.quantidade));
  }

  async function confirmarDevolucao(entrega: EntregaEpi) {
    try {
      setCarregando(true);
      setErro(null);
      const atualizada: AtualizarEntregaEpi = {
        ...entrega,
        // Entregas registradas antes da Fase 2 podem não ter motivoTipo (campo era opcional);
        // o backend exige o enum em toda atualização, então assume Inicial como padrão seguro.
        motivoTipo: entrega.motivoTipo ?? MotivoEntregaEpi.Inicial,
        dataDevolucao: devolucaoData,
        quantidadeDevolucao: Number(devolucaoQtd) || entrega.quantidade,
      };
      await api.entregasEpi.atualizar(atualizada);
      setDevolucaoId(null);
      setDevolucaoParaAssinar(atualizada);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar devolução.');
    } finally {
      setCarregando(false);
    }
  }

  async function baixarFicha(trabalhadorId: string) {
    try {
      setBaixandoId(trabalhadorId);
      const blob = await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ficha-epi-${trabalhadorId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova entrega de EPI</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Trabalhador">
            <Select
              value={novaEntrega.trabalhadorId}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, trabalhadorId: d.value, catalogoEpiId: '' })}
            >
              <option value="">Selecione</option>
              {trabalhadores.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome} ({t.matricula})
                </option>
              ))}
            </Select>
          </Field>
          <Field label="EPI">
            <Select
              value={novaEntrega.catalogoEpiId}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpiId: d.value })}
              disabled={!novaEntrega.trabalhadorId || episPermitidos.length === 0}
            >
              <option value="">Selecione</option>
              {episPermitidos.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.nome} (estoque total: {e.saldoTotal})
                </option>
              ))}
            </Select>
            {novaEntrega.trabalhadorId && episPermitidos.length === 0 && (
              <Text size={200}>
                Esta função não tem EPIs cadastrados na matriz.{' '}
                <Button appearance="transparent" size="small" onClick={aoNavegarParaMatriz}>
                  Cadastrar em Matriz de EPI por Função
                </Button>
              </Text>
            )}
          </Field>
          <Field label="Quantidade">
            <Input
              type="number"
              value={String(novaEntrega.quantidade)}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, quantidade: Number(d.value) })}
            />
          </Field>
          <Field label="Data de entrega">
            <Input
              type="date"
              value={novaEntrega.dataEntrega}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })}
            />
          </Field>
          <Field label="Validade">
            <Input
              type="date"
              value={novaEntrega.dataValidade ?? ''}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataValidade: d.value })}
            />
          </Field>
          <Field label="Motivo">
            <Select
              value={novaEntrega.motivoTipo}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivoTipo: Number(d.value) })}
            >
              {Object.entries(motivoEntregaEpiLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Nº lista de presença (NR-6)">
            <Input
              value={novaEntrega.numeroListaPresencaNr6 ?? ''}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, numeroListaPresencaNr6: d.value })}
            />
          </Field>
          <Field label="Data do treinamento (NR-6)">
            <Input
              type="date"
              value={novaEntrega.dataTreinamentoNr6 ?? ''}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataTreinamentoNr6: d.value })}
            />
          </Field>
          <Field label="Visto do consórcio/responsável">
            <Input
              value={novaEntrega.vistoConsorcioResponsavel ?? ''}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, vistoConsorcioResponsavel: d.value })}
            />
          </Field>
          <Field label="Observação do motivo (opcional)">
            <Input
              value={novaEntrega.motivo ?? ''}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivo: d.value })}
            />
          </Field>
          <Field label="Observações">
            <Input
              value={novaEntrega.observacoes ?? ''}
              onChange={(_, d) => setNovaEntrega({ ...novaEntrega, observacoes: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar entrega
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Entregas registradas</Text>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Trabalhador</TableHeaderCell>
              <TableHeaderCell>EPI</TableHeaderCell>
              <TableHeaderCell>Qtd.</TableHeaderCell>
              <TableHeaderCell>Entrega</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Devolução</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entregas.map((entrega) => (
              <TableRow key={entrega.id}>
                <TableCell>{nomeTrabalhador(entrega.trabalhadorId)}</TableCell>
                <TableCell>{nomeEpi(entrega.catalogoEpiId)}</TableCell>
                <TableCell>{entrega.quantidade}</TableCell>
                <TableCell>{entrega.dataEntrega?.slice(0, 10)}</TableCell>
                <TableCell>
                  {entrega.dataValidade?.slice(0, 10)}
                  {vencido(entrega.dataValidade) && !entrega.dataDevolucao && (
                    <Badge color="danger" appearance="tint" style={{ marginLeft: 8 }}>
                      Vencido
                    </Badge>
                  )}
                </TableCell>
                <TableCell>
                  {devolucaoId === entrega.id ? (
                    <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                      <Input
                        type="date"
                        value={devolucaoData}
                        onChange={(_, d) => setDevolucaoData(d.value)}
                        style={{ width: 130 }}
                      />
                      <Input
                        type="number"
                        value={devolucaoQtd}
                        onChange={(_, d) => setDevolucaoQtd(d.value)}
                        style={{ width: 60 }}
                      />
                      <Button size="small" appearance="primary" onClick={() => confirmarDevolucao(entrega)} disabled={carregando}>
                        Confirmar
                      </Button>
                    </div>
                  ) : entrega.dataDevolucao ? (
                    entrega.dataDevolucao.slice(0, 10)
                  ) : (
                    <Button size="small" appearance="subtle" onClick={() => iniciarDevolucao(entrega)}>
                      Registrar devolução
                    </Button>
                  )}
                </TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button
                      appearance="subtle"
                      icon={<Signature24Regular />}
                      onClick={() => navigate(`/epi/${entrega.id}/assinar`)}
                      aria-label="Assinar ficha"
                      title="Assinar ficha"
                    />
                    <Button
                      appearance="subtle"
                      icon={<ArrowDownload24Regular />}
                      onClick={() => baixarFicha(entrega.trabalhadorId)}
                      disabled={baixandoId === entrega.trabalhadorId}
                      aria-label="Baixar ficha do trabalhador"
                      title="Baixar ficha de EPI do trabalhador em PDF"
                    />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {entregaParaAssinar && (
        <AssinaturaEntregaEpiDialog
          open={!!entregaParaAssinar}
          onClose={() => setEntregaParaAssinar(null)}
          entregaId={entregaParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(entregaParaAssinar.trabalhadorId)}
          epiNome={nomeEpi(entregaParaAssinar.catalogoEpiId)}
          quantidade={entregaParaAssinar.quantidade}
          dataEntrega={entregaParaAssinar.dataEntrega}
          numeroListaPresencaNr6={entregaParaAssinar.numeroListaPresencaNr6}
          dataTreinamentoNr6={entregaParaAssinar.dataTreinamentoNr6}
        />
      )}

      {devolucaoParaAssinar && (
        <AssinaturaDevolucaoEpiDialog
          open={!!devolucaoParaAssinar}
          onClose={() => setDevolucaoParaAssinar(null)}
          entregaId={devolucaoParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(devolucaoParaAssinar.trabalhadorId)}
          epiNome={nomeEpi(devolucaoParaAssinar.catalogoEpiId)}
          quantidadeDevolucao={devolucaoParaAssinar.quantidadeDevolucao ?? devolucaoParaAssinar.quantidade}
          dataDevolucao={devolucaoParaAssinar.dataDevolucao ?? ''}
        />
      )}
    </div>
  );
}
