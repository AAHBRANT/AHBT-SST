import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  Checkbox,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  PageHeader,
  PainelCriacaoInline,
  Select,
  StatusChip,
  type Coluna,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  cargoMembroCipaLabel,
  CargoMembroCipa,
  OrigemMembroCipa,
  type MembroCipa,
  type NovoMembroCipa,
  type Obra,
  type Trabalhador,
} from '../../lib/api';

function vazio(): NovoMembroCipa {
  return {
    obraId: '',
    trabalhadorId: '',
    origemMembro: OrigemMembroCipa.Empregador,
    cargo: CargoMembroCipa.Titular,
    dataInicioMandato: '',
    dataFimMandato: '',
  };
}

// 16/11/2026 (pedido do usuário): tela deixou claro que aqui só entram os REPRESENTANTES DA EMPRESA
// (indicados, sem voto). Origem fixa = Empregador; cargos sem Vice-presidente (o vice sai dos eleitos);
// datas do mandato sugeridas a partir do mandato vigente da obra (ou 1 ano a partir de hoje).
const CARGOS_EMPRESA = [CargoMembroCipa.Presidente, CargoMembroCipa.Titular, CargoMembroCipa.Suplente, CargoMembroCipa.Secretario];

function hojeIso() {
  const d = new Date();
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
}

function umAnoMenosUmDia(inicio: string) {
  const d = new Date(`${inicio}T12:00:00`);
  d.setFullYear(d.getFullYear() + 1);
  d.setDate(d.getDate() - 1);
  return d.toISOString().slice(0, 10);
}

// Membros eleitos pelos empregados normalmente entram aqui pela apuração do Processo Eleitoral
// (aba "Processo Eleitoral"). Este formulário serve para cadastrar diretamente os indicados pelo
// empregador (que não passam por votação) — ver disclosure em Cipa.cs. Migração para
// PainelCriacaoInline (mesmo padrão de AtividadesTab.tsx/InspecoesTab.tsx): o PainelLateral (drawer)
// saiu — agora o formulário cresce acima da lista. Filtro "somente mandato ativo" continua nos
// filtros do PageHeader.
export function MembrosCipaTab() {
  const navigate = useNavigate();
  const [lista, setLista] = useState<MembroCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [somenteMandatoAtivo, setSomenteMandatoAtivo] = useState(true);
  const [novo, setNovo] = useState<NovoMembroCipa>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaMembros, listaObras] = await Promise.all([
        api.cipa.membros.listar(undefined, somenteMandatoAtivo),
        api.obras.listar(),
      ]);
      setLista(listaMembros);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar membros da CIPA.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [somenteMandatoAtivo]);

  async function trocarObra(obraId: string) {
    // Sugere o mesmo período do mandato vigente da obra; sem mandato vigente, 1 ano a partir de hoje.
    const vigente = lista.find((m) => m.obraId === obraId && m.mandatoAtivo);
    const inicio = vigente?.dataInicioMandato?.slice(0, 10) ?? hojeIso();
    const fim = vigente?.dataFimMandato?.slice(0, 10) ?? umAnoMenosUmDia(inicio);
    setNovo({ ...novo, obraId, trabalhadorId: '', origemMembro: OrigemMembroCipa.Empregador, dataInicioMandato: inicio, dataFimMandato: fim });
    setTrabalhadores(obraId ? await api.trabalhadores.listar(obraId) : []);
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    if (!novo.obraId || !novo.trabalhadorId || !novo.dataInicioMandato || !novo.dataFimMandato) {
      setErroPainel('Preencha obra, funcionário e o período do mandato.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cipa.membros.criar(novo);
      setNovo(vazio());
      setTrabalhadores([]);
      await carregar();
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar o representante.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<MembroCipa>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (m) => m.trabalhadorNome },
    { chave: 'obra', rotulo: 'Obra', render: (m) => m.obraNome },
    {
      chave: 'origem',
      rotulo: 'Origem',
      render: (m) => (
        <StatusChip tom={m.origemMembro === OrigemMembroCipa.Empregador ? 'neutro' : 'info'}>
          {m.origemMembro === OrigemMembroCipa.Empregador ? 'Empresa' : 'Eleito'}
        </StatusChip>
      ),
    },
    { chave: 'cargo', rotulo: 'Cargo', render: (m) => cargoMembroCipaLabel[m.cargo] },
    {
      chave: 'mandato',
      rotulo: 'Mandato',
      render: (m) => (
        <>
          {m.dataInicioMandato?.slice(0, 10)} a {m.dataFimMandato?.slice(0, 10)}{' '}
          {m.mandatoAtivo && <StatusChip tom="ok">Ativo</StatusChip>}
        </>
      ),
    },
    { chave: 'totalTreinamentos', rotulo: 'Treinamentos' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Membros da CIPA"
        filtros={
          <Checkbox
            label="Somente mandato ativo"
            checked={somenteMandatoAtivo}
            onChange={(_, d) => setSomenteMandatoAtivo(!!d.checked)}
          />
        }
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-novo-membro-cipa"
          >
            {painelAberto ? 'Fechar' : 'Indicar representante da empresa'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <div id="painel-novo-membro-cipa">
        <PainelCriacaoInline aberto={painelAberto} titulo="Representante da empresa (indicado)">
          <FormSection titulo="Dados do representante" numero={1} primeira>
            <FeedbackInline tom="info">
              Aqui a empresa indica os representantes dela, como o Presidente da CIPA. Os representantes dos empregados não são
              cadastrados aqui: eles entram sozinhos pela eleição, quando você registra a apuração no Processo Eleitoral.
            </FeedbackInline>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Obra" required>
                  <Select value={novo.obraId} onChange={(_, d) => trocarObra(d.value)}>
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Funcionário" required>
                  <Select
                    value={novo.trabalhadorId}
                    onChange={(_, d) => setNovo({ ...novo, trabalhadorId: d.value })}
                    disabled={!novo.obraId}
                  >
                    <option value="">Selecione</option>
                    {trabalhadores.map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.matricula ? `${t.nome} (${t.matricula})` : t.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Cargo">
                  <Select value={String(novo.cargo)} onChange={(_, d) => setNovo({ ...novo, cargo: Number(d.value) })}>
                    {CARGOS_EMPRESA.map((valor) => (
                      <option key={valor} value={valor}>
                        {cargoMembroCipaLabel[valor]}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Início do mandato" required hint="Sugerido pelo mandato atual da obra; ajuste se precisar.">
                  <CampoData value={novo.dataInicioMandato} onChange={(_, d) => setNovo({ ...novo, dataInicioMandato: d.value })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Fim do mandato" required>
                  <CampoData value={novo.dataFimMandato} onChange={(_, d) => setNovo({ ...novo, dataFimMandato: d.value })} />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Cadastrar representante
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>
      <Card>
        <DataTable
          aria-label="Membros da CIPA"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(m) => m.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum membro cadastrado ainda' }}
          aoClicarLinha={(m) => navigate(`/operacao/cipa/membro/${m.id}`)}
        />
      </Card>
    </div>
  );
}
