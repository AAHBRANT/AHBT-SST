import { useEffect, useState } from 'react';
import {
  BotaoAcao,
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
  PageHeader,
  PainelCriacaoInline,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  TipoArea,
  tipoAreaLabel,
  statusAreaLabel,
  type AreaSst,
  type NovaAreaSst,
  type Obra,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

const areaVazia: NovaAreaSst = {
  codigo: '',
  nome: '',
  tipo: TipoArea.AreaDeTrabalho,
  obraId: '',
  detalhesLocalizacao: '',
  riscos: [],
  requisitos: [],
  status: 1,
};

const tomPorStatusArea: Record<number, Tom> = {
  1: 'ok',
  2: 'atencao',
  3: 'alerta',
  4: 'neutro',
};

export function AreasSstTab() {
  const souAdministrador = useSouAdministrador();
  const [areas, setAreas] = useState<AreaSst[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaArea, setNovaArea] = useState<NovaAreaSst>(areaVazia);
  const [riscosTexto, setRiscosTexto] = useState('');
  const [requisitosTexto, setRequisitosTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [ars, obrs] = await Promise.all([api.areasSst.listar(), api.obras.listar()]);
      setAreas(ars);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar áreas.');
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

  function dividirLista(texto: string): string[] {
    return texto
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovaArea(areaVazia);
    setRiscosTexto('');
    setRequisitosTexto('');
  }

  async function criar() {
    if (!novaArea.codigo.trim() || !novaArea.nome.trim() || !novaArea.obraId) {
      setErroPainel('Informe código, nome e obra da área.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.areasSst.criar({
        ...novaArea,
        riscos: dividirLista(riscosTexto),
        requisitos: dividirLista(requisitosTexto),
      });
      fecharPainel();
      await carregar();
      sucessoToast('Área criada com sucesso.');
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar área.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta área? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.areasSst.excluir(id);
      await carregar();
      sucessoToast('Área excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir área.');
    }
  }

  const colunas: Coluna<AreaSst>[] = [
    { chave: 'codigo', rotulo: 'Código' },
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoAreaLabel[a.tipo] },
    { chave: 'obra', rotulo: 'Obra', render: (a) => nomeObra(a.obraId) },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomPorStatusArea[a.status] ?? 'neutro'}>{statusAreaLabel[a.status]}</StatusChip>,
    },
    { chave: 'riscos', rotulo: 'Riscos', render: (a) => a.riscos.join(', ') || '—' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Áreas de SST"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-area-sst"
          >
            {painelAberto ? 'Fechar' : 'Nova área'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-nova-area-sst">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova área de SST">
          <FormSection titulo="Informações da área" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={2}>
                <Field label="Código" required>
                  <Input value={novaArea.codigo} onChange={(_, d) => setNovaArea({ ...novaArea, codigo: d.value })} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Nome" required>
                  <Input value={novaArea.nome} onChange={(_, d) => setNovaArea({ ...novaArea, nome: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Tipo">
                  <Select
                    value={String(novaArea.tipo)}
                    onChange={(_, d) => setNovaArea({ ...novaArea, tipo: Number(d.value) })}
                  >
                    {Object.entries(tipoAreaLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Obra" required>
                  <Select value={novaArea.obraId} onChange={(_, d) => setNovaArea({ ...novaArea, obraId: d.value })}>
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Detalhes da localização">
                  <Input
                    value={novaArea.detalhesLocalizacao ?? ''}
                    onChange={(_, d) => setNovaArea({ ...novaArea, detalhesLocalizacao: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Riscos (separados por vírgula)">
                  <Input value={riscosTexto} onChange={(_, d) => setRiscosTexto(d.value)} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Requisitos (separados por vírgula)">
                  <Input value={requisitosTexto} onChange={(_, d) => setRequisitosTexto(d.value)} />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
                Adicionar área
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        <DataTable
          aria-label="Áreas de SST cadastradas"
          colunas={colunas}
          linhas={areas}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma área cadastrada ainda.',
            acao: { rotulo: 'Nova área', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={
            souAdministrador
              ? (a) => <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
              : undefined
          }
        />
      </Card>
    </div>
  );
}
