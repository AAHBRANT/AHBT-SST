import { useEffect, useState } from 'react';
import {
  BotaoAcao,
  Button,
  Card,
  Campo,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelCriacaoInline,
  Select,
  Textarea,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type DimensionamentoCipa, type NovoDimensionamentoCipa, type Obra, type SituacaoCipaObra } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { dataBr } from '../../lib/datas';

function vazio(): NovoDimensionamentoCipa {
  return { obraId: '', cnae: '', grauRisco: 1, numeroFuncionarios: 0, numeroTitulares: 0, numeroSuplentes: 0, observacoes: '' };
}

// Dimensionamento CIPA: número de titulares/suplentes é sempre informado manualmente por quem
// cadastra — este sistema não calcula automaticamente o Quadro I da NR-5 (deve ser validado por
// técnico/engenheiro de segurança do trabalho habilitado). Ver disclosure completo em Cipa.cs.
// Migração para PainelCriacaoInline (mesmo padrão de AtividadesTab.tsx/InspecoesTab.tsx): o
// PainelLateral (drawer) saiu — agora o formulário cresce acima da lista só até a altura do
// próprio formulário. O subtítulo de disclosure do Quadro I da NR-5 virou texto de ajuda no
// rodapé do formulário (FormRodape info).
export function DimensionamentoCipaTab() {
  const [lista, setLista] = useState<DimensionamentoCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  // Empregados ativos por obra (contagem automática) — preenche o nº de funcionários ao escolher a obra.
  const [situacoes, setSituacoes] = useState<SituacaoCipaObra[]>([]);
  const [novo, setNovo] = useState<NovoDimensionamentoCipa>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaDim, listaObras] = await Promise.all([api.cipa.dimensionamento.listar(), api.obras.listar()]);
      setLista(listaDim);
      setObras(listaObras);
      api.cipa.dimensionamento.situacaoObras().then(setSituacoes).catch(() => setSituacoes([]));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar dimensionamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    if (!novo.obraId || !novo.cnae.trim() || novo.numeroFuncionarios <= 0) {
      setErroPainel('Preencha obra, CNAE e número de funcionários.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cipa.dimensionamento.criar({ ...novo, observacoes: novo.observacoes || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Dimensionamento criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar dimensionamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este dimensionamento? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.dimensionamento.excluir(id);
      await carregar();
      sucessoToast('Dimensionamento excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir dimensionamento.');
    }
  }

  const colunas: Coluna<DimensionamentoCipa>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (d) => nomeObra(d.obraId) },
    { chave: 'cnae', rotulo: 'CNAE' },
    { chave: 'grauRisco', rotulo: 'Grau de risco (NR-04)', render: (d) => `Grau ${d.grauRisco}` },
    { chave: 'numeroFuncionarios', rotulo: 'Funcionários' },
    { chave: 'numeroTitulares', rotulo: 'Titulares' },
    { chave: 'numeroSuplentes', rotulo: 'Suplentes' },
    { chave: 'dataCalculo', rotulo: 'Data do cálculo', render: (d) => dataBr(d.dataCalculo) },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Dimensionamento CIPA"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-novo-dimensionamento"
          >
            {painelAberto ? 'Fechar' : 'Adicionar dimensionamento'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <div id="painel-novo-dimensionamento">
        <PainelCriacaoInline aberto={painelAberto} titulo="Novo dimensionamento">
          <FormSection titulo="Dados do dimensionamento" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={4}>
                <Field label="Obra" required>
                  <Select
                    value={novo.obraId}
                    onChange={(_, d) => {
                      const obraSel = obras.find((o) => o.id === d.value);
                      const contagem = situacoes.find((s) => s.obraId === d.value)?.empregadosAtivos;
                      const codigoCnae = obraSel?.cnae ? obraSel.cnae.split(/\s+[—–-]\s+/)[0].trim().slice(0, 20) : '';
                      setNovo({
                        ...novo,
                        obraId: d.value,
                        cnae: codigoCnae || novo.cnae,
                        grauRisco: obraSel?.grauRisco ?? novo.grauRisco,
                        numeroFuncionarios: contagem ?? novo.numeroFuncionarios,
                      });
                    }}
                  >
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={2}>
                <Field label="CNAE" required>
                  <Input value={novo.cnae} maxLength={20} onChange={(_, d) => setNovo({ ...novo, cnae: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Grau de risco (NR-04)">
                  <Select value={String(novo.grauRisco)} onChange={(_, d) => setNovo({ ...novo, grauRisco: Number(d.value) })}>
                    {[1, 2, 3, 4].map((grau) => (
                      <option key={grau} value={grau}>
                        Grau {grau}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Número de funcionários" required hint="Preenchido com os empregados CLT ativos da obra; ajuste se necessário.">
                  <Input
                    type="number"
                    value={String(novo.numeroFuncionarios)}
                    onChange={(_, d) => setNovo({ ...novo, numeroFuncionarios: Number(d.value) })}
                  />
                </Field>
              </Campo>
              <Campo span={2}>
                <Field label="Titulares" required>
                  <Input
                    type="number"
                    value={String(novo.numeroTitulares)}
                    onChange={(_, d) => setNovo({ ...novo, numeroTitulares: Number(d.value) })}
                  />
                </Field>
              </Campo>
              <Campo span={2}>
                <Field label="Suplentes" required>
                  <Input
                    type="number"
                    value={String(novo.numeroSuplentes)}
                    onChange={(_, d) => setNovo({ ...novo, numeroSuplentes: Number(d.value) })}
                  />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Observações">
                  <Textarea value={novo.observacoes ?? ''} onChange={(_, d) => setNovo({ ...novo, observacoes: d.value })} />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape info="O número de titulares/suplentes é definido manualmente por quem cadastra, conforme o Quadro I da NR-5 para o CNAE e grau de risco informados. Recomenda-se validação por técnico ou engenheiro de segurança do trabalho habilitado.">
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Adicionar dimensionamento
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>
      <Card>
        <DataTable
          aria-label="Dimensionamentos cadastrados"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(d) => d.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum dimensionamento cadastrado ainda',
            acao: { rotulo: 'Adicionar dimensionamento', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={
            souAdministrador
              ? (d) => <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(d.id)} aria-label="Excluir" />
              : undefined
          }
        />
      </Card>
    </div>
  );
}
