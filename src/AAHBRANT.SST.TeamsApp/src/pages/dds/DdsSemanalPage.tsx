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
  TipoDdsSemanal,
  type DdsSemanal,
  type NovaDdsSemanal,
  type Obra,
} from '../../lib/api';
import { segundaFeiraAtualIso } from '../../lib/datas';

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
  const [painelAberto, setPainelAberto] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setCarregandoLista(true);
      const [lista, listaObras] = await Promise.all([api.ddsSemanal.listar(), api.obras.listar()]);
      setRegistros(lista);
      setObras(listaObras);
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
    if (!nova.obraId || !nova.dataInicioSemana || (nova.tipo === TipoDdsSemanal.Terceirizados && !nova.empresaTerceirizada)) {
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

  const colunas: Coluna<DdsSemanal>[] = [
    { chave: 'numeroDocumento', rotulo: 'Nº do documento', render: (s) => s.numeroDocumento ?? '-' },
    { chave: 'obra', rotulo: 'Obra', render: (s) => s.obraNome },
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
          <Button
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

      <div id="painel-nova-semana-dds">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova semana de DDS">
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
                    {obras.map((obra) => (
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
              {nova.tipo === TipoDdsSemanal.Terceirizados && (
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
          linhas={registros}
          chaveLinha={(s) => s.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma semana de DDS registrada ainda.',
            acao: { rotulo: 'Nova semana', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(s) => navigate(`/prevencao/dds/semana/${s.id}`)}
        />
      </Card>
    </div>
  );
}
