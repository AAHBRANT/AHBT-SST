import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Abas,
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
  Select,
  StatusChip,
  Text,
  Textarea,
  type Coluna,
  type Tom,
} from '@ui';
import { AddCircle24Regular } from '@fluentui/react-icons';
import {
  api,
  GravidadeAcidente,
  gravidadeAcidenteLabel,
  StatusAcidente,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type Acidente,
  type Atividade,
  type NovoAcidente,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { HhtMensalTab } from './HhtMensalTab';

function novaInicial(): NovoAcidente {
  return {
    tipo: 1,
    obraId: '',
    trabalhadorId: '',
    atividadeId: '',
    local: '',
    data: '',
    hora: '',
    descricao: '',
    lesao: '',
    consequencia: '',
    atendimento: '',
    houveAfastamento: false,
    diasAfastamento: undefined,
    numeroCat: '',
    causas: '',
    gravidade: GravidadeAcidente.SemAfastamento,
    diasDebitadosInformados: undefined,
  };
}

// Registrado/EmInvestigacao/Concluido — mesmo padrão de progressão neutro→atencao→ok já usado em
// StatusPcmsoDocumento (Task 12) para os 3 estágios de um fluxo sem workflow de aprovação formal.
const tomPorStatusAcidente: Record<number, Tom> = {
  [StatusAcidente.Registrado]: 'neutro',
  [StatusAcidente.EmInvestigacao]: 'atencao',
  [StatusAcidente.Concluido]: 'ok',
};

// Onda 2 Task 20 (camada ui/): lista + formulário de registro de acidentes/incidentes/quase-acidentes,
// sempre aninhada como aba de OcorrenciasPage (nunca teve PageHeader próprio). Abas internas
// "Acidentes & Incidentes"/"HHT Mensal" viram Abas nivel="interno" — navegação de conteúdo, não de
// página-pilar, então sem useAbaNaUrl (mesmo critério já usado nas sub-abas de TrabalhadorDetalhePage).
export function AcidentesPage({ tipoFixo }: { tipoFixo?: number } = {}) {
  const navigate = useNavigate();
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [nova, setNova] = useState<NovoAcidente>(novaInicial());
  // Suporta abrir a tela já filtrada por tipo via URL (?tipo=3) — legado, de quando "Acidentes",
  // "Incidentes" e "Quase-acidentes" eram itens de menu apontando pra essa mesma tela. Hoje viraram
  // abas de OcorrenciasPage, que passa tipoFixo diretamente (sem depender de querystring).
  const [searchParams] = useSearchParams();
  const [filtroStatus, setFiltroStatus] = useState<string>('');
  const [filtroTipo, setFiltroTipo] = useState<string>(
    tipoFixo != null ? String(tipoFixo) : searchParams.get('tipo') ?? '',
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [aba, setAba] = useState<'ocorrencias' | 'hht'>('ocorrencias');

  async function carregar() {
    try {
      setErro(null);
      const [listaAcidentes, listaObras, listaTrabalhadores, listaAtividades] = await Promise.all([
        api.acidentes.listar({
          status: filtroStatus ? Number(filtroStatus) : undefined,
          tipo: filtroTipo ? Number(filtroTipo) : undefined,
        }),
        api.obras.listar(),
        api.trabalhadores.listar(),
        api.atividades.listar(),
      ]);
      setAcidentes(listaAcidentes);
      setObras(listaObras);
      setTrabalhadores(listaTrabalhadores);
      setAtividades(listaAtividades);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar acidentes.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroStatus, filtroTipo]);

  async function criar() {
    if (!nova.obraId) {
      setErro('Selecione a obra.');
      return;
    }
    if (!nova.local.trim()) {
      setErro('Informe o local da ocorrência.');
      return;
    }
    if (!nova.data) {
      setErro('Informe a data da ocorrência.');
      return;
    }
    if (!nova.descricao.trim()) {
      setErro('Informe a descrição da ocorrência.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.acidentes.criar({
        ...nova,
        trabalhadorId: nova.trabalhadorId || null,
        atividadeId: nova.atividadeId || null,
        // input type="time" retorna "HH:mm"; TimeSpan? no backend exige segundos ("HH:mm:ss").
        hora: nova.hora ? `${nova.hora}:00` : null,
        lesao: nova.lesao || null,
        consequencia: nova.consequencia || null,
        atendimento: nova.atendimento || null,
        diasAfastamento: nova.houveAfastamento ? nova.diasAfastamento ?? null : null,
        numeroCat: nova.numeroCat || null,
        causas: nova.causas || null,
      });
      setNova(novaInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar ocorrência.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<Acidente>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoOcorrenciaLabel[a.tipo] },
    { chave: 'obra', rotulo: 'Obra', render: (a) => a.obraNome ?? '—' },
    { chave: 'funcionario', rotulo: 'Funcionário', render: (a) => a.trabalhadorNome ?? '—' },
    { chave: 'data', rotulo: 'Data', render: (a) => a.data?.slice(0, 10) ?? '' },
    { chave: 'local', rotulo: 'Local' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomPorStatusAcidente[a.status] ?? 'neutro'}>{statusAcidenteLabel[a.status]}</StatusChip>,
    },
    { chave: 'gravidade', rotulo: 'Gravidade', render: (a) => gravidadeAcidenteLabel[a.gravidade] },
  ];

  return (
    <div>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Abas
        nivel="interno"
        abas={[
          { valor: 'ocorrencias', rotulo: 'Acidentes & Incidentes' },
          { valor: 'hht', rotulo: 'HHT Mensal' },
        ]}
        valor={aba}
        aoMudar={setAba}
      />

      {aba === 'hht' && <HhtMensalTab obras={obras} />}

      {aba === 'ocorrencias' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo="Registrar acidente / incidente">
            <FormSection titulo="Dados Gerais da Ocorrência" numero={1} primeira>
              <FormGrid>
                <Campo span={2}>
                  <Field label="Tipo" required>
                    <Select value={String(nova.tipo)} onChange={(_, d) => setNova({ ...nova, tipo: Number(d.value) })}>
                      {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={3}>
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
                  <Field label="Funcionário">
                    <Select
                      value={nova.trabalhadorId ?? ''}
                      onChange={(_, d) => setNova({ ...nova, trabalhadorId: d.value })}
                    >
                      <option value="">Nenhum</option>
                      {trabalhadores.map((trabalhador) => (
                        <option key={trabalhador.id} value={trabalhador.id}>
                          {trabalhador.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Atividade">
                    <Select
                      value={nova.atividadeId ?? ''}
                      onChange={(_, d) => setNova({ ...nova, atividadeId: d.value })}
                    >
                      <option value="">Nenhuma</option>
                      {atividades.map((atividade) => (
                        <option key={atividade.id} value={atividade.id}>
                          {atividade.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Local" required>
                    <Input value={nova.local} onChange={(_, d) => setNova({ ...nova, local: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Data" required>
                    <CampoData value={nova.data} onChange={(_, d) => setNova({ ...nova, data: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Hora">
                    <Input type="time" value={nova.hora ?? ''} onChange={(_, d) => setNova({ ...nova, hora: d.value })} />
                  </Field>
                </Campo>
              </FormGrid>
            </FormSection>

            <FormSection titulo="Lesão, Gravidade e Consequências" numero={2}>
              <FormGrid>
                <Campo span={12}>
                  <Field label="Descrição" required>
                    <Textarea value={nova.descricao} onChange={(_, d) => setNova({ ...nova, descricao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Lesão">
                    <Input value={nova.lesao ?? ''} onChange={(_, d) => setNova({ ...nova, lesao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Consequência">
                    <Input value={nova.consequencia ?? ''} onChange={(_, d) => setNova({ ...nova, consequencia: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Atendimento prestado">
                    <Input value={nova.atendimento ?? ''} onChange={(_, d) => setNova({ ...nova, atendimento: d.value })} />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Houve afastamento?">
                    <Select
                      value={nova.houveAfastamento ? '1' : '0'}
                      onChange={(_, d) => setNova({ ...nova, houveAfastamento: d.value === '1' })}
                    >
                      <option value="0">Não</option>
                      <option value="1">Sim</option>
                    </Select>
                  </Field>
                </Campo>
                {nova.houveAfastamento && (
                  <Campo span={3}>
                    <Field label="Dias de afastamento">
                      <Input
                        type="number"
                        min={0}
                        value={nova.diasAfastamento?.toString() ?? ''}
                        onChange={(_, d) => setNova({ ...nova, diasAfastamento: d.value ? Number(d.value) : undefined })}
                      />
                    </Field>
                  </Campo>
                )}
                <Campo span={3}>
                  <Field label="Gravidade" required>
                    <Select
                      value={String(nova.gravidade)}
                      onChange={(_, d) =>
                        setNova({ ...nova, gravidade: Number(d.value), diasDebitadosInformados: undefined })
                      }
                    >
                      {Object.entries(gravidadeAcidenteLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                {nova.gravidade === GravidadeAcidente.IncapacidadePermanenteParcial && (
                  <Campo span={6}>
                    <Field
                      label="Dias Debitados (consultar Quadro III da NBR 14280)"
                      required
                      hint="Valor não calculado automaticamente pelo sistema — consulte a tabela oficial de Dias Debitados por lesão/parte do corpo."
                    >
                      <Input
                        type="number"
                        min={1}
                        value={nova.diasDebitadosInformados?.toString() ?? ''}
                        onChange={(_, d) =>
                          setNova({ ...nova, diasDebitadosInformados: d.value ? Number(d.value) : undefined })
                        }
                      />
                    </Field>
                  </Campo>
                )}
                {(nova.gravidade === GravidadeAcidente.Obito ||
                  nova.gravidade === GravidadeAcidente.IncapacidadePermanenteTotal) && (
                  <Campo span={3}>
                    <Field label="Dias Debitados">
                      <Text>6.000 dias (fixo, calculado automaticamente)</Text>
                    </Field>
                  </Campo>
                )}
                <Campo span={3}>
                  <Field label="Número da CAT">
                    <Input value={nova.numeroCat ?? ''} onChange={(_, d) => setNova({ ...nova, numeroCat: d.value })} />
                  </Field>
                </Campo>
              </FormGrid>
            </FormSection>

            <FormRodape>
              <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
                Registrar
              </Button>
            </FormRodape>
          </Card>

          <Card
            titulo="Acidentes e incidentes"
            acoes={
              <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end' }}>
                {tipoFixo == null && (
                  <Field label="Tipo">
                    <Select value={filtroTipo} onChange={(_, d) => setFiltroTipo(d.value)}>
                      <option value="">Todos</option>
                      {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
                <Field label="Status">
                  <Select value={filtroStatus} onChange={(_, d) => setFiltroStatus(d.value)}>
                    <option value="">Todos</option>
                    {Object.entries(statusAcidenteLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
            }
          >
            <DataTable
              aria-label="Acidentes e incidentes"
              colunas={colunas}
              linhas={acidentes}
              chaveLinha={(a) => a.id}
              vazio={{ titulo: 'Nenhuma ocorrência cadastrada ainda.' }}
              aoClicarLinha={(a) => navigate(`/acidentes/${a.id}`)}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
