import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
  motivoEntregaUniformeLabel,
  MotivoEntregaUniforme,
  type CatalogoUniforme,
  type EntregaUniforme,
  type NovaEntregaUniforme,
  type TamanhoUniformeTrabalhador,
  type Trabalhador,
} from '../../lib/api';
import { AssinaturaEntregaUniformeDialog } from '../../components/assinatura/AssinaturaEntregaUniformeDialog';
import { usePageStyles } from '../pageStyles';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';

function entregaVazia(): NovaEntregaUniforme {
  return {
    trabalhadorId: '',
    catalogoUniformeId: '',
    dataEntrega: new Date().toISOString().slice(0, 10),
    quantidade: 1,
    motivoTipo: MotivoEntregaUniforme.Inicial,
    observacoes: '',
  };
}

interface EntregaUniformeTabProps {
  aoNavegarParaMatriz: () => void;
}

// Entrega de Uniforme — travada pela matriz da função (mesmo princípio de bloqueio via dropdown
// filtrado já usado em EntregasTab.tsx do EPI: só aparecem peças que a matriz autoriza) e pelo
// tamanho cadastrado do trabalhador (resolvido automaticamente, somente leitura — nunca digitado
// nem escolhido manualmente). O backend (CriarEntregaUniformeCommand) revalida tudo de novo e
// bloqueia sem válvula de escape se algo estiver faltando; os erros retornados são exibidos como
// vieram, mesmo padrão do resto do frontend.
export function EntregaUniformeTab({ aoNavegarParaMatriz }: EntregaUniformeTabProps) {
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [entregas, setEntregas] = useState<EntregaUniforme[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [itensPermitidos, setItensPermitidos] = useState<CatalogoUniforme[]>([]);
  const [tamanhosTrabalhador, setTamanhosTrabalhador] = useState<TamanhoUniformeTrabalhador[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaUniforme>(entregaVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [entregaParaAssinar, setEntregaParaAssinar] = useState<EntregaUniforme | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaItens, listaTrabalhadores] = await Promise.all([
        api.entregasUniforme.listar(),
        api.catalogosUniforme.listar(),
        api.trabalhadores.listar(),
      ]);
      setEntregas(lista);
      setItensCatalogo(listaItens);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de uniforme.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  useEffect(() => {
    let cancelado = false;
    async function carregarItensPermitidosETamanhos() {
      const trabalhador = trabalhadores.find((t) => t.id === novaEntrega.trabalhadorId);
      if (!trabalhador) {
        setItensPermitidos([]);
        setTamanhosTrabalhador([]);
        return;
      }
      try {
        const [permitidos, tamanhos] = await Promise.all([
          api.funcoes.listarUniformes(trabalhador.funcaoId),
          api.trabalhadores.listarTamanhosUniforme(trabalhador.id),
        ]);
        if (!cancelado) {
          setItensPermitidos(permitidos);
          setTamanhosTrabalhador(tamanhos);
        }
      } catch {
        if (!cancelado) {
          setItensPermitidos([]);
          setTamanhosTrabalhador([]);
        }
      }
    }
    carregarItensPermitidosETamanhos();
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

  function obraIdTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.obraId ?? '';
  }

  const tamanhoResolvido = tamanhosTrabalhador.find((t) => t.catalogoUniformeId === novaEntrega.catalogoUniformeId)?.tamanho ?? null;
  const pecaSelecionadaSemTamanho = !!novaEntrega.catalogoUniformeId && tamanhoResolvido === null;

  async function criar() {
    if (!novaEntrega.trabalhadorId || !novaEntrega.catalogoUniformeId || !novaEntrega.dataEntrega || novaEntrega.quantidade < 1) {
      setErro('Preencha funcionário, peça, data de entrega e quantidade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.entregasUniforme.criar(novaEntrega);
      const entregaCriada = await api.entregasUniforme.obterPorId(id);
      setEntregaParaAssinar(entregaCriada);
      setNovaEntrega(entregaVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar entrega de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova entrega de uniforme</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Entrega</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Funcionário">
              <Select
                value={novaEntrega.trabalhadorId}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, trabalhadorId: d.value, catalogoUniformeId: '' })}
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
            <Field label="Peça">
              <Select
                value={novaEntrega.catalogoUniformeId}
                onChange={(_, d) => setNovaEntrega({ ...novaEntrega, catalogoUniformeId: d.value })}
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
                  Esta função não tem peças cadastradas na matriz.{' '}
                  <Button appearance="transparent" size="small" onClick={aoNavegarParaMatriz}>
                    Cadastrar em Matriz por Função
                  </Button>
                </Text>
              )}
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Tamanho (resolvido automaticamente)">
              <Input value={tamanhoResolvido ?? ''} readOnly disabled placeholder="—" />
              {pecaSelecionadaSemTamanho && (
                <Text size={200} className={estilos.erro}>
                  Trabalhador sem tamanho cadastrado para esta peça.{' '}
                  <Button appearance="transparent" size="small" onClick={() => navigate(`/pessoas/${novaEntrega.trabalhadorId}`)}>
                    Cadastrar em Tamanhos
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
                {Object.entries(motivoEntregaUniformeLabel).map(([valor, rotulo]) => (
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
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={criar}
            disabled={carregando || pecaSelecionadaSemTamanho}
          >
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
              <TableHeaderCell>Peça</TableHeaderCell>
              <TableHeaderCell>Tamanho</TableHeaderCell>
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
                    <FotoCatalogoUniforme
                      catalogoUniformeId={entrega.catalogoUniformeId}
                      temFoto={itemTemFoto(entrega.catalogoUniformeId)}
                      tamanho={28}
                    />
                    {nomeItem(entrega.catalogoUniformeId)}
                  </div>
                </TableCell>
                <TableCell>{entrega.tamanho}</TableCell>
                <TableCell>{entrega.quantidade}</TableCell>
                <TableCell>{entrega.dataEntrega?.slice(0, 10)}</TableCell>
                <TableCell>{motivoEntregaUniformeLabel[entrega.motivoTipo]}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {entregaParaAssinar && (
        <AssinaturaEntregaUniformeDialog
          open={!!entregaParaAssinar}
          onClose={() => setEntregaParaAssinar(null)}
          entregaId={entregaParaAssinar.id}
          trabalhadorNome={nomeTrabalhador(entregaParaAssinar.trabalhadorId)}
          pecaNome={nomeItem(entregaParaAssinar.catalogoUniformeId)}
          catalogoUniformeId={entregaParaAssinar.catalogoUniformeId}
          itemTemFoto={itemTemFoto(entregaParaAssinar.catalogoUniformeId)}
          tamanho={entregaParaAssinar.tamanho}
          quantidade={entregaParaAssinar.quantidade}
          dataEntrega={entregaParaAssinar.dataEntrega}
          obraId={obraIdTrabalhador(entregaParaAssinar.trabalhadorId)}
        />
      )}
    </div>
  );
}
