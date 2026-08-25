import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
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
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, StatusPcmso, statusPcmsoLabel, type NovoPcmso, type Obra, type Pcmso } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function pcmsoVazio(): NovoPcmso {
  return {
    obraId: '',
    nome: '',
    objetivo: '',
    medicoCoordenadorNome: '',
    medicoCoordenadorCrm: '',
    medicoCoordenadorUsuarioId: null,
    dataElaboracao: new Date().toISOString().slice(0, 10),
    dataVigenciaInicio: '',
    dataVigenciaFim: '',
    status: StatusPcmso.EmElaboracao,
  };
}

export function PcmsosPage() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [pcmsos, setPcmsos] = useState<Pcmso[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novoPcmso, setNovoPcmso] = useState<NovoPcmso>(pcmsoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaPcmsos, listaObras] = await Promise.all([api.pcmsos.listar(), api.obras.listar()]);
      setPcmsos(listaPcmsos);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PCMSOs.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(obraId: string) {
    return obras.find((o) => o.id === obraId)?.nome ?? obraId;
  }

  async function criar() {
    if (!novoPcmso.obraId) {
      setErro('Selecione a obra.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.pcmsos.criar({
        ...novoPcmso,
        dataVigenciaInicio: novoPcmso.dataVigenciaInicio || null,
        dataVigenciaFim: novoPcmso.dataVigenciaFim || null,
      });
      setNovoPcmso(pcmsoVazio());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar PCMSO.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.pcmsos.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir PCMSO.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">PCMSO — Programa de Controle Médico de Saúde Ocupacional</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Obra">
          <Select
            value={novoPcmso.obraId}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, obraId: d.value })}
          >
            <option value="">Selecione...</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>
                {obra.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Nome do programa">
          <Input value={novoPcmso.nome} onChange={(_, d) => setNovoPcmso({ ...novoPcmso, nome: d.value })} />
        </Field>
        <Field label="Médico coordenador">
          <Input
            value={novoPcmso.medicoCoordenadorNome}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, medicoCoordenadorNome: d.value })}
          />
        </Field>
        <Field label="CRM">
          <Input
            value={novoPcmso.medicoCoordenadorCrm ?? ''}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, medicoCoordenadorCrm: d.value })}
          />
        </Field>
        <Field label="Data de elaboração">
          <Input
            type="date"
            value={novoPcmso.dataElaboracao}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, dataElaboracao: d.value })}
          />
        </Field>
        <Field label="Vigência - início">
          <Input
            type="date"
            value={novoPcmso.dataVigenciaInicio ?? ''}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, dataVigenciaInicio: d.value })}
          />
        </Field>
        <Field label="Vigência - fim">
          <Input
            type="date"
            value={novoPcmso.dataVigenciaFim ?? ''}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, dataVigenciaFim: d.value })}
          />
        </Field>
        <Field label="Status">
          <Select
            value={novoPcmso.status}
            onChange={(_, d) => setNovoPcmso({ ...novoPcmso, status: Number(d.value) })}
          >
            {Object.entries(statusPcmsoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Criar PCMSO
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Médico coordenador</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {pcmsos.map((pcmso) => (
            <TableRow key={pcmso.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`/prevencao/pcmso/${pcmso.id}`)}>
              <TableCell>{pcmso.nome}</TableCell>
              <TableCell>{nomeObra(pcmso.obraId)}</TableCell>
              <TableCell>{pcmso.medicoCoordenadorNome}</TableCell>
              <TableCell>{statusPcmsoLabel[pcmso.status]}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  aria-label="Excluir"
                  onClick={(evento) => {
                    evento.stopPropagation();
                    excluir(pcmso.id);
                  }}
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
