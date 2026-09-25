import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
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
  Legenda,
  PageHeader,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
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
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { AssinaturaEntregaUniformeDialog } from '../../components/assinatura/AssinaturaEntregaUniformeDialog';
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
  const [entregas, setEntregas] = useState<EntregaUniforme[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [itensPermitidos, setItensPermitidos] = useState<CatalogoUniforme[]>([]);
  const [tamanhosTrabalhador, setTamanhosTrabalhador] = useState<TamanhoUniformeTrabalhador[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaEntrega, setNovaEntrega] = useState<NovaEntregaUniforme>(entregaVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
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

  const tamanhoResolvido =
    tamanhosTrabalhador.find((t) => t.catalogoUniformeId === novaEntrega.catalogoUniformeId)?.tamanho ?? null;
  const pecaSelecionadaSemTamanho = !!novaEntrega.catalogoUniformeId && tamanhoResolvido === null;

  async function criar() {
    if (
      !novaEntrega.trabalhadorId ||
      !novaEntrega.catalogoUniformeId ||
      !novaEntrega.dataEntrega ||
      novaEntrega.quantidade < 1
    ) {
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

  const colunas: Coluna<EntregaUniforme>[] = [
    { chave: 'funcionario', rotulo: 'Funcionário', render: (e) => nomeTrabalhador(e.trabalhadorId) },
    {
      chave: 'peca',
      rotulo: 'Peça',
      render: (e) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <FotoCatalogoUniforme
            catalogoUniformeId={e.catalogoUniformeId}
            temFoto={itemTemFoto(e.catalogoUniformeId)}
            tamanho={28}
          />
          {nomeItem(e.catalogoUniformeId)}
        </div>
      ),
    },
    { chave: 'tamanho', rotulo: 'Tamanho' },
    { chave: 'quantidade', rotulo: 'Qtd.', alinhar: 'direita' },
    { chave: 'entrega', rotulo: 'Entrega', render: (e) => e.dataEntrega?.slice(0, 10) ?? '' },
    { chave: 'motivo', rotulo: 'Motivo', render: (e) => motivoEntregaUniformeLabel[e.motivoTipo] },
  ];

  // Exclusão definitiva, só para Administrador (o servidor recusa os demais — ver
  // PoliticasAutorizacao). A peça volta para o estoque do tamanho correspondente na obra.
  async function excluir(entrega: EntregaUniforme) {
    const confirmado = await confirmar({
      titulo: 'Excluir entrega de uniforme',
      mensagem:
        `Excluir a entrega de ${nomeItem(entrega.catalogoUniformeId)} (tamanho ${entrega.tamanho})` +
        ` para ${nomeTrabalhador(entrega.trabalhadorId)}?` +
        ` ${entrega.quantidade} unidade(s) voltam para o estoque da obra. Esta ação não pode ser desfeita.`,
      rotuloConfirmar: 'Excluir',
    });
    if (!confirmado) return;
    try {
      setExcluindoId(entrega.id);
      setErro(null);
      await api.entregasUniforme.excluir(entrega.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir a entrega de uniforme.');
    } finally {
      setExcluindoId(null);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader titulo="Entrega de Uniforme" />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Nova entrega de uniforme" densidade="compacta">
        <FormSection titulo="Dados da entrega" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
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
            </Campo>
            <Campo span={4}>
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
                  <Legenda>
                    Esta função não tem peças cadastradas na matriz.{' '}
                    <Button appearance="transparent" size="small" onClick={aoNavegarParaMatriz}>
                      Cadastrar em Matriz por Função
                    </Button>
                  </Legenda>
                )}
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Tamanho (resolvido automaticamente)">
                <Input value={tamanhoResolvido ?? ''} readOnly disabled placeholder="—" />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Quantidade">
                <Input
                  type="number"
                  value={String(novaEntrega.quantidade)}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, quantidade: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data de entrega">
                <CampoData
                  value={novaEntrega.dataEntrega}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, dataEntrega: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
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
            </Campo>
            <Campo span={6}>
              <Field label="Observações (opcional)">
                <Input
                  value={novaEntrega.observacoes ?? ''}
                  onChange={(_, d) => setNovaEntrega({ ...novaEntrega, observacoes: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>

          {pecaSelecionadaSemTamanho && (
            <FeedbackInline
              tom="aviso"
              acao={{
                rotulo: 'Cadastrar em Tamanhos',
                aoClicar: () => navigate(`/pessoas/${novaEntrega.trabalhadorId}`),
              }}
            >
              Trabalhador sem tamanho cadastrado para esta peça.
            </FeedbackInline>
          )}

          <FormRodape>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              onClick={criar}
              disabled={carregando || pecaSelecionadaSemTamanho}
            >
              Registrar entrega
            </Button>
          </FormRodape>
        </FormSection>
      </Card>

      <Card titulo="Entregas registradas" densidade="compacta">
        <DataTable
          aria-label="Entregas de uniforme registradas"
          densidade="compacta"
          colunas={colunas}
          linhas={entregas}
          chaveLinha={(e) => e.id}
          vazio={{ titulo: 'Nenhuma entrega de uniforme registrada ainda.' }}
          acoesLinha={(e) => (
            <>
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && (
                <BotaoAcao
                  tom="excluir"
                  icon={<Delete24Regular />}
                  disabled={excluindoId === e.id}
                  onClick={() => excluir(e)}
                  aria-label="Excluir entrega de uniforme"
                  title="Excluir esta entrega (somente administrador)"
                />
              )}
            </>
          )}
        />
      </Card>

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
