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
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpcLabel,
  MotivoEntregaEpc,
  type CatalogoEpc,
  type EntregaEpc,
  type NovaEntregaEpc,
  type Trabalhador,
} from '../../lib/api';
import { AssinaturaEntregaEpcDialog } from '../../components/assinatura/AssinaturaEntregaEpcDialog';
import { usePageStyles } from '../pageStyles';
import { FotoCatalogoEpc } from './FotoCatalogoEpc';

function entregaVazia(): NovaEntregaEpc {
  return {
    trabalhadorId: '',
    catalogoEpcId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    quantidade: 1,
    motivoTipo: MotivoEntregaEpc.Inicial,
    observacoes: '',
  };
}

interface EntregaEpcTabProps {
  aoNavegarParaMatriz: () => void;
}

// Entrega de EPC — travada pela matriz da função (mesmo princípio de EntregaUniformeTab.tsx/
// EntregasTab.tsx do EPI: só aparecem itens que a matriz autoriza). Sem resolução de tamanho (EPC
// não tem tamanho, diferente de uniforme). O backend (CriarEntregaEpcCommand) revalida tudo de
// novo e bloqueia sem válvula de escape se algo estiver faltando.
export function EntregaEpcTab({ aoNavegarParaMatriz }: EntregaEpcTabProps) {
  const estilos = usePageStyles();
  const [entregas, setEntregas] = useState<EntregaEpc[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoEpc[]>([]);
  const [itensPermitidos, setItensPermitidos] = useState<CatalogoEpc[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaEpc>(entregaVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [entregaParaAssinar, setEntregaParaAssinar] = useState<EntregaEpc | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaItens, listaTrabalhadores] = await Promise.all([
        api.entregasEpc.listar(),
        api.catalogosEpc.listar(),
        api.trabalhadores.listar(),
      ]);
      setEntregas(lista);
      setItensCatalogo(listaItens);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPC.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  useEffect(() => {
    let cancelado = false;
    async function carregarItensPermitidos() {
      const trabalhador = trabalhadores.find((t) => t.id === novaEntrega.trabalhadorId);
      if (!trabalhador) {
        setItensPermitidos([]);
        return;
      }
      try {
        const permitidos = await api.funcoes.listarEpcs(trabalhador.funcaoId);
        if (!cancelado) setItensPermitidos(permitidos);
      } catch {
        if (!cancelado) setItensPermitidos([]);
      }
    }
    carregarItensPermitidos();
    return () => {
      cancelado = true;
    };
  }, [novaEntrega.trabalhadorId, trabalhadores]);

  function nomeItem(id: string) {
    return itensCatalogo.find((i) => i.id === id)?.nome ?? id;
  }

  function itemTemFoto(id: string) {
    return itensCatalogo.find((i) => i.id === id)?.temFoto ?? false;
  }

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novaEntrega.trabalhadorId || !novaEntrega.catalogoEpcId || !novaEntrega.dataEntrega || novaEntrega.quantidade < 1) {
      setErro('Preencha funcionário, item, data de entrega e quantidade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.entregasEpc.criar(novaEntrega);
      const entregaCriada = await api.entregasEpc.obterPorId(id);
      setEntregaParaAssinar(entregaCriada);
      setNovaEntrega(entregaVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar entrega de EPC.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova entrega de EPC</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Entrega</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Funcionário">
              <Select
                value={novaEntrega.trabalhadorId}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, trabalhadorId: d.value, catalogoEpcId: '' })}
              >
                <option value="">Selecione</option>
                {trabalhadores.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.nome} ({t.matricula})
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col4}>
            <Field label="Item de EPC">
              <Select
                value={novaEntrega.catalogoEpcId}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoEpcId: d.value })}
                disabled={!novaEntrega.trabalhadorId || itensPermitidos.length === 0}
              >
                <option value="">Selecione</option>
                {itensPermitidos.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.nome}
                  </option>
                ))}
              </Select>
              {novaEntrega.trabalhadorId && itensPermitidos.length === 0 && (
                <Text size={200}>
                  Esta função não tem itens cadastrados na matriz.{' '}
                  <Button appearance="transparent" size="small" onClick={aoNavegarParaMatriz}>
                    Cadastrar em Matriz por Função
                  </Button>
                </Text>
              )}
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Quantidade">
              <Input
                type="number"
                value={String(novaEntrega.quantidade)}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, quantidade: Number(d.value) })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data de entrega">
              <CampoData
                value={novaEntrega.dataEntrega}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Motivo">
              <Select
                value={novaEntrega.motivoTipo}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, motivoTipo: Number(d.value) })}
              >
                {Object.entries(motivoEntregaEpcLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col6}>
            <Field label="Observações (opcional)">
              <Input
                value={novaEntrega.observacoes ?? ''}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, observacoes: d.value })}
              />
            </Field>
          </div>
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

        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Funcionário</TableHeaderCell>
              <TableHeaderCell>Item</TableHeaderCell>
              <TableHeaderCell>Qtd.</TableHeaderCell>
              <TableHeaderCell>Entrega</TableHeaderCell>
              <TableHeaderCell>Motivo</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entregas.map((entrega) => (
              <TableRow key={entrega.id}>
                <TableCell>{nomeTrabalhador(entrega.trabalhadorId)}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <FotoCatalogoEpc
                      catalogoEpcId={entrega.catalogoEpcId}
                      temFoto={itemTemFoto(entrega.catalogoEpcId)}
                      tamanho={28}
                    />
                    {nomeItem(entrega.catalogoEpcId)}
                  </div>
                </TableCell>
                <TableCell>{entrega.quantidade}</TableCell>
                <TableCell>{entrega.dataEntrega?.slice(0, 10)}</TableCell>
                <TableCell>{motivoEntregaEpcLabel[entrega.motivoTipo]}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {entregaParaAssinar && (
        <AssinaturaEntregaEpcDialog
          open={!!entregaParaAssinar}
          onClose={() => setEntregaParaAssinar(null)}
          entregaId={entregaParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(entregaParaAssinar.trabalhadorId)}
          itemNome={nomeItem(entregaParaAssinar.catalogoEpcId)}
          catalogoEpcId={entregaParaAssinar.catalogoEpcId}
          itemTemFoto={itemTemFoto(entregaParaAssinar.catalogoEpcId)}
          quantidade={entregaParaAssinar.quantidade}
          dataEntrega={entregaParaAssinar.dataEntrega}
        />
      )}
    </div>
  );
}
