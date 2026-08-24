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
  statusRequisitoLegalLabel,
  type NovoRequisitoLegal,
  type Obra,
  type RequisitoLegal,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function novoInicial(): NovoRequisitoLegal {
  return {
    codigo: '',
    norma: '',
    item: '',
    tema: '',
    requisito: '',
    aplicabilidade: true,
    justificativa: '',
    evidencia: '',
    responsavelUsuarioId: '',
    periodicidade: '',
    prazo: '',
    obraId: '',
  };
}

export function MatrizLegalPage() {
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [requisitos, setRequisitos] = useState<RequisitoLegal[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovoRequisitoLegal>(novoInicial());
  const [filtroNorma, setFiltroNorma] = useState('');
  const [filtroTema, setFiltroTema] = useState('');
  const [filtroStatus, setFiltroStatus] = useState('');
  const [filtroAplicabilidade, setFiltroAplicabilidade] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaUsuarios, listaObras] = await Promise.all([
        api.matrizLegal.listar({
          norma: filtroNorma || undefined,
          tema: filtroTema || undefined,
          status: filtroStatus ? Number(filtroStatus) : undefined,
          aplicabilidade: filtroAplicabilidade ? filtroAplicabilidade === '1' : undefined,
        }),
        api.usuarios.listar(),
        api.obras.listar(),
      ]);
      setRequisitos(lista);
      setUsuarios(listaUsuarios);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a matriz legal.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroNorma, filtroTema, filtroStatus, filtroAplicabilidade]);

  async function criar() {
    if (!novo.codigo.trim()) {
      setErro('Informe o código do requisito.');
      return;
    }
    if (!novo.norma.trim()) {
      setErro('Informe a norma.');
      return;
    }
    if (!novo.tema.trim()) {
      setErro('Informe o tema.');
      return;
    }
    if (!novo.requisito.trim()) {
      setErro('Informe a descrição do requisito.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.matrizLegal.criar({
        ...novo,
        item: novo.item || null,
        justificativa: novo.justificativa || null,
        evidencia: novo.evidencia || null,
        responsavelUsuarioId: novo.responsavelUsuarioId || null,
        periodicidade: novo.periodicidade || null,
        prazo: novo.prazo || null,
        obraId: novo.obraId || null,
      });
      setNovo(novoInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar requisito legal.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo requisito legal</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Código" required>
            <Input value={novo.codigo} onChange={(_, d) => setNovo({ ...novo, codigo: d.value })} />
          </Field>
          <Field label="Norma" required>
            <Input value={novo.norma} onChange={(_, d) => setNovo({ ...novo, norma: d.value })} />
          </Field>
          <Field label="Item">
            <Input value={novo.item ?? ''} onChange={(_, d) => setNovo({ ...novo, item: d.value })} />
          </Field>
          <Field label="Tema" required>
            <Input value={novo.tema} onChange={(_, d) => setNovo({ ...novo, tema: d.value })} />
          </Field>
          <Field label="Requisito" required>
            <Textarea value={novo.requisito} onChange={(_, d) => setNovo({ ...novo, requisito: d.value })} />
          </Field>
          <Field label="Aplicabilidade">
            <Select
              value={novo.aplicabilidade ? '1' : '0'}
              onChange={(_, d) => setNovo({ ...novo, aplicabilidade: d.value === '1' })}
            >
              <option value="1">Sim</option>
              <option value="0">Não</option>
            </Select>
          </Field>
          <Field label="Justificativa">
            <Input
              value={novo.justificativa ?? ''}
              onChange={(_, d) => setNovo({ ...novo, justificativa: d.value })}
            />
          </Field>
          <Field label="Evidência">
            <Input value={novo.evidencia ?? ''} onChange={(_, d) => setNovo({ ...novo, evidencia: d.value })} />
          </Field>
          <Field label="Obra">
            <Select value={novo.obraId ?? ''} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
              <option value="">Global (todas as obras)</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Responsável">
            <Select
              value={novo.responsavelUsuarioId ?? ''}
              onChange={(_, d) => setNovo({ ...novo, responsavelUsuarioId: d.value })}
            >
              <option value="">Nenhum</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Periodicidade">
            <Input
              value={novo.periodicidade ?? ''}
              placeholder="Ex.: Anual"
              onChange={(_, d) => setNovo({ ...novo, periodicidade: d.value })}
            />
          </Field>
          <Field label="Prazo">
            <Input type="date" value={novo.prazo ?? ''} onChange={(_, d) => setNovo({ ...novo, prazo: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Adicionar requisito
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Matriz de requisitos legais</Text>
          <Field label="Norma">
            <Input value={filtroNorma} onChange={(_, d) => setFiltroNorma(d.value)} />
          </Field>
          <Field label="Tema">
            <Input value={filtroTema} onChange={(_, d) => setFiltroTema(d.value)} />
          </Field>
          <Field label="Aplicabilidade">
            <Select value={filtroAplicabilidade} onChange={(_, d) => setFiltroAplicabilidade(d.value)}>
              <option value="">Todas</option>
              <option value="1">Sim</option>
              <option value="0">Não</option>
            </Select>
          </Field>
          <Field label="Status">
            <Select value={filtroStatus} onChange={(_, d) => setFiltroStatus(d.value)}>
              <option value="">Todos</option>
              {Object.entries(statusRequisitoLegalLabel).map(([valor, rotulo]) => (
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
              <TableHeaderCell>Código</TableHeaderCell>
              <TableHeaderCell>Norma</TableHeaderCell>
              <TableHeaderCell>Tema</TableHeaderCell>
              <TableHeaderCell>Aplicável</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Prazo</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {requisitos.map((requisito) => (
              <TableRow
                key={requisito.id}
                onClick={() => navigate(`/conformidade/matriz-legal/${requisito.id}`)}
                style={{ cursor: 'pointer' }}
              >
                <TableCell>{requisito.codigo}</TableCell>
                <TableCell>{requisito.norma}</TableCell>
                <TableCell>{requisito.tema}</TableCell>
                <TableCell>{requisito.aplicabilidade ? 'Sim' : 'Não'}</TableCell>
                <TableCell>{requisito.responsavelUsuarioNome ?? '—'}</TableCell>
                <TableCell>{requisito.prazo?.slice(0, 10) ?? '—'}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusRequisitoLegalLabel[requisito.status]}</Badge>
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
