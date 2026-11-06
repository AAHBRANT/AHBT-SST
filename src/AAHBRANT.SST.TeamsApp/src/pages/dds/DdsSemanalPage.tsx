import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  Select,
  CampoData,
  Card,
  PageHeader,
  DataTable,
  StatusChip,
  FeedbackInline,
  PainelCriacaoInline,
  FormSection,
  FormGrid,
  FormRodape,
  Campo,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  statusDdsSemanalLabel,
  StatusDdsSemanal,
  tipoDdsSemanalLabel,
  tipoDdsSemanalIncluiTerceirizados,
  TipoDdsSemanal,
  type DdsSemanal,
  type NovaDdsSemanal,
  type Obra,
} from '../../lib/api';
import { segundaFeiraAtualIso } from '../../lib/datas';
import { usePodeNaObra } from '../../lib/UsuarioLogadoContext';
import { FaixaSupervisao } from '../../components/FaixaSupervisao';

function semanalVazia(): NovaDdsSemanal {
  return { obraId: '', tipo: TipoDdsSemanal.Proprios, dataInicioSemana: segundaFeiraAtualIso() };
}

const tomStatusSemanal: Record<number, Tom> = {
  [StatusDdsSemanal.EmAndamento]: 'info',
  [StatusDdsSemanal.Concluida]: 'ok',
};

// Reformulação 31/08 — o DDS passou a ser organizado por semana (contêiner), seguindo o modelo em
// papel "Registro Semanal de DDS - Empregados Próprios/Terceirizados". Os registros diários (feitos
// e assinados todo dia) ficam dentro de cada semana — ver DdsSemanalDetalhePage.
// Onda 2 (Task 14): conversões 1 (Table → DataTable), 5 (Badge color → StatusChip), 3
// (Text size=500 → PageHeader.titulo), 4 (erro → FeedbackInline). Migrado para o padrão
// PainelCriacaoInline (spec 2026-09-11): o PainelLateral (drawer) saiu — agora o formulário de
// criação cresce acima da lista, sem cobrir a tela com uma gaveta.
export function DdsSemanalPage() {
  const navigate = useNavigate();
  const [registros, setRegistros] = useState<DdsSemanal[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [nova, setNova] = useState<NovaDdsSemanal>(semanalVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [listaCarregada, setListaCarregada] = useState(false);
  const [obrasIndisponiveis, setObrasIndisponiveis] = useState(false);
  const [painelAberto, setPainelAberto] = useState(false);
  const [obraFiltro, setObraFiltro] = useState('');
  const pode = usePodeNaObra();

  async function carregar() {
    try {
      setErro(null);
      setCarregandoLista(true);
      const [lista, listaObras] = await Promise.allSettled([api.ddsSemanal.listar(), api.obras.listar()]);
      if (lista.status === 'rejected') throw lista.reason;
      setRegistros(lista.value);
      setListaCarregada(true);
      setObrasIndisponiveis(listaObras.status === 'rejected');
      if (listaObras.status === 'fulfilled') setObras(listaObras.value);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar as semanas de DDS.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainel() {
    setPainelAberto(false);
    setNova(semanalVazia());
    setErroPainel(null);
  }

  async function criar() {
    if (!nova.obraId || !nova.dataInicioSemana || (tipoDdsSemanalIncluiTerceirizados(nova.tipo) && !nova.empresaTerceirizada)) {
      setErroPainel('Preencha obra, data de início da semana e, se terceirizado, a empresa.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      const resultado = await api.ddsSemanal.criar(nova);
      setNova(semanalVazia());
      await carregar();
      navigate(`/prevencao/dds/semana/${resultado.id}`);
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar a semana de DDS.');
    } finally {
      setCarregando(false);
    }
  }

  // Responsável x Supervisão (06/11/2026): "Minhas obras" = onde pode abrir semana; as demais
  // obras visíveis são só acompanhadas (consulta).
  const ehMinha = (obraId: string) => pode('dds:criar', obraId) || pode('dds:conduzir', obraId);
  const minhasObras = obras.filter((o) => ehMinha(o.id));
  const obrasSupervisao = obras.filter((o) => !ehMinha(o.id));
  const obrasParaCriar = obras.filter((o) => pode('dds:criar', o.id));
  const podeCriar = obrasParaCriar.length > 0;
  const obraFiltroSelecionada = obras.find((o) => o.id === obraFiltro);
  const linhasVisiveis = obraFiltro ? registros.filter((r) => r.obraId === obraFiltro) : registros;

  const colunas: Coluna<DdsSemanal>[] = [
    { chave: 'numeroDocumento', rotulo: 'Nº do documento', render: (s) => s.numeroDocumento ?? '-' },
    {
      chave: 'obra',
      rotulo: 'Obra',
      render: (s) => (
        <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
          {s.obraNome}
          {!ehMinha(s.obraId) && <StatusChip tom="neutro">Acompanhando</StatusChip>}
        </span>
      ),
    },
    { chave: 'tipo', rotulo: 'Tipo', render: (s) => tipoDdsSemanalLabel[s.tipo] },
    {
      chave: 'semana',
      rotulo: 'Semana',
      render: (s) => `${s.dataInicioSemana?.slice(0, 10)} a ${s.dataFimSemana?.slice(0, 10)}`,
    },
    { chave: 'responsavel', rotulo: 'Responsável', render: (s) => s.responsavelUsuarioNome },
    {
      chave: 'dias',
      rotulo: 'Dias registrados',
      render: (s) => `${s.totalDiasConcluidos}/${s.totalDiasRegistrados} concluídos (de 5)`,
    },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (s) => <StatusChip tom={tomStatusSemanal[s.status] ?? 'neutro'}>{statusDdsSemanalLabel[s.status]}</StatusChip>,
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="DDS — Diálogo Diário de Segurança (Registro Semanal)"
        acoes={
          podeCriar && <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-semana-dds"
          >
            {painelAberto ? 'Fechar' : 'Nova semana'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {obras.length > 1 && (
        <div style={{ maxWidth: 360 }}>
          <Field label="Obra">
            <Select value={obraFiltro} onChange={(_, d) => setObraFiltro(d.value)}>
              <option value="">Todas as obras</option>
              {minhasObras.length > 0 && (
                <optgroup label="Minhas obras">
                  {minhasObras.map((o) => (
                    <option key={o.id} value={o.id}>{o.nome}</option>
                  ))}
                </optgroup>
              )}
              {obrasSupervisao.length > 0 && (
                <optgroup label="Supervisão (somente consulta)">
                  {obrasSupervisao.map((o) => (
                    <option key={o.id} value={o.id}>{o.nome}</option>
                  ))}
                </optgroup>
              )}
            </Select>
          </Field>
        </div>
      )}

      {obraFiltroSelecionada && !ehMinha(obraFiltroSelecionada.id) && <FaixaSupervisao obraNome={obraFiltroSelecionada.nome} />}

      <div id="painel-nova-semana-dds">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova semana de DDS">
          {obrasIndisponiveis && (
            <FeedbackInline tom="aviso">A lista de obras não está disponível neste dispositivo. Conecte-se à internet e reabra esta tela para carregar as opções.</FeedbackInline>
          )}
          <FormSection titulo="Dados da Semana" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={4}>
                <Field label="Obra" required>
                  <Select value={nova.obraId} onChange={(_, d) => setNova({ ...nova, obraId: d.value })}>
                    <option value="">Selecione</option>
                    {obrasParaCriar.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Tipo">
                  <Select value={String(nova.tipo)} onChange={(_, d) => setNova({ ...nova, tipo: Number(d.value) })}>
                    <option value={String(TipoDdsSemanal.Proprios)}>{tipoDdsSemanalLabel[TipoDdsSemanal.Proprios]}</option>
                    <option value={String(TipoDdsSemanal.Terceirizados)}>{tipoDdsSemanalLabel[TipoDdsSemanal.Terceirizados]}</option>
                    <option value={String(TipoDdsSemanal.ProprioseTerceirizados)}>
                      {tipoDdsSemanalLabel[TipoDdsSemanal.ProprioseTerceirizados]}
                    </option>
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Início da semana" required>
                  <CampoData
                    value={nova.dataInicioSemana}
                    onChange={(_, d) => setNova({ ...nova, dataInicioSemana: d.value })}
                  />
                </Field>
              </Campo>
              {tipoDdsSemanalIncluiTerceirizados(nova.tipo) && (
                <Campo span={4}>
                  <Field label="Empresa terceirizada" required>
                    <Input
                      value={nova.empresaTerceirizada ?? ''}
                      onChange={(_, d) => setNova({ ...nova, empresaTerceirizada: d.value })}
                    />
                  </Field>
                </Campo>
              )}
              <Campo span={5}>
                <Field label="Local / Frente de serviço">
                  <Input
                    value={nova.localFrenteServico ?? ''}
                    onChange={(_, d) => setNova({ ...nova, localFrenteServico: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Abrir semana
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card densidade="compacta">
        <DataTable
          aria-label="Semanas de DDS registradas"
          colunas={colunas}
          linhas={linhasVisiveis}
          chaveLinha={(s) => s.id}
          carregando={carregandoLista}
          vazio={listaCarregada ? {
            titulo: 'Nenhuma semana de DDS registrada ainda.',
            acao: podeCriar ? { rotulo: 'Nova semana', aoClicar: () => setPainelAberto(true) } : undefined,
          } : {
            titulo: 'Lista indisponível neste dispositivo.',
            descricao: 'Conecte-se à internet e carregue as semanas de DDS antes de usá-las offline.',
            acao: { rotulo: 'Tentar novamente', aoClicar: () => void carregar() },
          }}
          aoClicarLinha={(s) => navigate(`/prevencao/dds/semana/${s.id}`)}
        />
      </Card>
    </div>
  );
}
