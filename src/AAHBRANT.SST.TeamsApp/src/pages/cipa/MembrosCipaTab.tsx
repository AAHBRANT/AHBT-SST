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
  PageHeader,
  PainelLateral,
  Select,
  StatusChip,
  type Coluna,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  cargoMembroCipaLabel,
  origemMembroCipaLabel,
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

// Membros eleitos pelos empregados normalmente entram aqui pela apuração do Processo Eleitoral
// (aba "Processo Eleitoral"). Este formulário serve para cadastrar diretamente os indicados pelo
// empregador (que não passam por votação) — ver disclosure em Cipa.cs. Camada ui/ (Onda 2, Task 4):
// formulário saiu para PainelLateral; filtro "somente mandato ativo" migrou do toolbar da lista para
// os filtros do PageHeader.
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
    setNovo({ ...novo, obraId, trabalhadorId: '' });
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
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar membro.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<MembroCipa>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (m) => m.trabalhadorNome },
    { chave: 'obra', rotulo: 'Obra', render: (m) => m.obraNome },
    { chave: 'origem', rotulo: 'Origem', render: (m) => origemMembroCipaLabel[m.origemMembro] },
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
    <div>
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
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Indicar membro
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
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
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Indicar membro (empregador)"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Cadastrar membro
            </Button>
          </>
        }
      >
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
            <Field label="Origem">
              <Select value={String(novo.origemMembro)} onChange={(_, d) => setNovo({ ...novo, origemMembro: Number(d.value) })}>
                {Object.entries(origemMembroCipaLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Cargo">
              <Select value={String(novo.cargo)} onChange={(_, d) => setNovo({ ...novo, cargo: Number(d.value) })}>
                {Object.entries(cargoMembroCipaLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Início do mandato" required>
              <CampoData value={novo.dataInicioMandato} onChange={(_, d) => setNovo({ ...novo, dataInicioMandato: d.value })} />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Fim do mandato" required>
              <CampoData value={novo.dataFimMandato} onChange={(_, d) => setNovo({ ...novo, dataFimMandato: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
