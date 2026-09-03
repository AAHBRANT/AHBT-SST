import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
  Field,
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
import { Add24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
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
import { usePageStyles } from '../pageStyles';

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
// empregador (que não passam por votação) — ver disclosure em Cipa.cs.
export function MembrosCipaTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [lista, setLista] = useState<MembroCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [somenteMandatoAtivo, setSomenteMandatoAtivo] = useState(true);
  const [novo, setNovo] = useState<NovoMembroCipa>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

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

  async function criar() {
    if (!novo.obraId || !novo.trabalhadorId || !novo.dataInicioMandato || !novo.dataFimMandato) {
      setErro('Preencha obra, trabalhador e o período do mandato.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.membros.criar(novo);
      setNovo(vazio());
      setTrabalhadores([]);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao cadastrar membro.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Indicar membro (empregador)</Text>
        </div>
        {erro && <Text className={estilos.erro}>{erro}</Text>}
        <div className={estilos.form}>
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
          <Field label="Trabalhador" required>
            <Select
              value={novo.trabalhadorId}
              onChange={(_, d) => setNovo({ ...novo, trabalhadorId: d.value })}
              disabled={!novo.obraId}
            >
              <option value="">Selecione</option>
              {trabalhadores.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome} ({t.matricula})
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Origem">
            <Select value={String(novo.origemMembro)} onChange={(_, d) => setNovo({ ...novo, origemMembro: Number(d.value) })}>
              {Object.entries(origemMembroCipaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Cargo">
            <Select value={String(novo.cargo)} onChange={(_, d) => setNovo({ ...novo, cargo: Number(d.value) })}>
              {Object.entries(cargoMembroCipaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Início do mandato" required>
            <CampoData value={novo.dataInicioMandato} onChange={(_, d) => setNovo({ ...novo, dataInicioMandato: d.value })} />
          </Field>
          <Field label="Fim do mandato" required>
            <CampoData value={novo.dataFimMandato} onChange={(_, d) => setNovo({ ...novo, dataFimMandato: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Cadastrar membro
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Membros da CIPA</Text>
          <Checkbox
            label="Somente mandato ativo"
            checked={somenteMandatoAtivo}
            onChange={(_, d) => setSomenteMandatoAtivo(!!d.checked)}
          />
        </div>
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Origem</TableHeaderCell>
              <TableHeaderCell>Cargo</TableHeaderCell>
              <TableHeaderCell>Mandato</TableHeaderCell>
              <TableHeaderCell>Treinamentos</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lista.map((m) => (
              <TableRow key={m.id} onClick={() => navigate(`/operacao/cipa/membro/${m.id}`)} style={{ cursor: 'pointer' }}>
                <TableCell>{m.trabalhadorNome}</TableCell>
                <TableCell>{m.obraNome}</TableCell>
                <TableCell>{origemMembroCipaLabel[m.origemMembro]}</TableCell>
                <TableCell>{cargoMembroCipaLabel[m.cargo]}</TableCell>
                <TableCell>
                  {m.dataInicioMandato?.slice(0, 10)} a {m.dataFimMandato?.slice(0, 10)}{' '}
                  {m.mandatoAtivo && <Badge appearance="tint" color="success">Ativo</Badge>}
                </TableCell>
                <TableCell>{m.totalTreinamentos}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/operacao/cipa/membro/${m.id}`)}
                    aria-label="Ver membro"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
