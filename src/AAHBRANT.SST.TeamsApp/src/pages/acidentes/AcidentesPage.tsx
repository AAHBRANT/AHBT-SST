import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
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
  Textarea,
} from '@fluentui/react-components';
import { AddCircle24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import {
  api,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type Acidente,
  type Atividade,
  type NovoAcidente,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

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
  };
}

export function AcidentesPage() {
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [nova, setNova] = useState<NovoAcidente>(novaInicial());
  const [filtroStatus, setFiltroStatus] = useState<string>('');
  const [filtroTipo, setFiltroTipo] = useState<string>('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

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

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Registrar acidente / incidente</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Tipo" required>
            <Select value={String(nova.tipo)} onChange={(_, d) => setNova({ ...nova, tipo: Number(d.value) })}>
              {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
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
          <Field label="Trabalhador">
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
          <Field label="Local" required>
            <Input value={nova.local} onChange={(_, d) => setNova({ ...nova, local: d.value })} />
          </Field>
          <Field label="Data" required>
            <Input type="date" value={nova.data} onChange={(_, d) => setNova({ ...nova, data: d.value })} />
          </Field>
          <Field label="Hora">
            <Input type="time" value={nova.hora ?? ''} onChange={(_, d) => setNova({ ...nova, hora: d.value })} />
          </Field>
          <Field label="Descrição" required>
            <Textarea value={nova.descricao} onChange={(_, d) => setNova({ ...nova, descricao: d.value })} />
          </Field>
          <Field label="Lesão">
            <Input value={nova.lesao ?? ''} onChange={(_, d) => setNova({ ...nova, lesao: d.value })} />
          </Field>
          <Field label="Consequência">
            <Input value={nova.consequencia ?? ''} onChange={(_, d) => setNova({ ...nova, consequencia: d.value })} />
          </Field>
          <Field label="Atendimento prestado">
            <Input value={nova.atendimento ?? ''} onChange={(_, d) => setNova({ ...nova, atendimento: d.value })} />
          </Field>
          <Field label="Houve afastamento?">
            <Select
              value={nova.houveAfastamento ? '1' : '0'}
              onChange={(_, d) => setNova({ ...nova, houveAfastamento: d.value === '1' })}
            >
              <option value="0">Não</option>
              <option value="1">Sim</option>
            </Select>
          </Field>
          {nova.houveAfastamento && (
            <Field label="Dias de afastamento">
              <Input
                type="number"
                min={0}
                value={nova.diasAfastamento?.toString() ?? ''}
                onChange={(_, d) => setNova({ ...nova, diasAfastamento: d.value ? Number(d.value) : undefined })}
              />
            </Field>
          )}
          <Field label="Número da CAT">
            <Input value={nova.numeroCat ?? ''} onChange={(_, d) => setNova({ ...nova, numeroCat: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Registrar
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Acidentes e incidentes</Text>
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
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Trabalhador</TableHeaderCell>
              <TableHeaderCell>Data</TableHeaderCell>
              <TableHeaderCell>Local</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {acidentes.map((acidente) => (
              <TableRow
                key={acidente.id}
                onClick={() => navigate(`/melhoria/acidentes/${acidente.id}`)}
                style={{ cursor: 'pointer' }}
              >
                <TableCell>{tipoOcorrenciaLabel[acidente.tipo]}</TableCell>
                <TableCell>{acidente.obraNome ?? '—'}</TableCell>
                <TableCell>{acidente.trabalhadorNome ?? '—'}</TableCell>
                <TableCell>{acidente.data?.slice(0, 10)}</TableCell>
                <TableCell>{acidente.local}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusAcidenteLabel[acidente.status]}</Badge>
                </TableCell>
                <TableCell>
                  <ChevronRight24Regular />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
