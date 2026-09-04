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
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { AddCircle24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import {
  api,
  origemNaoConformidadeLabel,
  statusNaoConformidadeLabel,
  type Atividade,
  type NaoConformidade,
  type NovaNaoConformidade,
  type Risco,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function novaInicial(): NovaNaoConformidade {
  return {
    origemDeteccao: 1,
    requisitoRelacionado: '',
    descricao: '',
    local: '',
    atividadeId: '',
    riscoId: '',
    responsavelUsuarioId: '',
    prazo: '',
  };
}

export function NaoConformidadesTab() {
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [naoConformidades, setNaoConformidades] = useState<NaoConformidade[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [nova, setNova] = useState<NovaNaoConformidade>(novaInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaNc, listaAtividades, listaRiscos, listaUsuarios] = await Promise.all([
        api.naoConformidades.listar(),
        api.atividades.listar(),
        api.riscos.listar(),
        api.usuarios.listar(),
      ]);
      setNaoConformidades(listaNc);
      setAtividades(listaAtividades);
      setRiscos(listaRiscos);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar não conformidades.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!nova.descricao.trim()) {
      setErro('Informe a descrição da não conformidade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.naoConformidades.criar({
        ...nova,
        requisitoRelacionado: nova.requisitoRelacionado || null,
        local: nova.local || null,
        atividadeId: nova.atividadeId || null,
        riscoId: nova.riscoId || null,
        responsavelUsuarioId: nova.responsavelUsuarioId || null,
        prazo: nova.prazo || null,
      });
      setNova(novaInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar não conformidade.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova não conformidade</Text>
        </div>
        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Não Conformidade</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col3}>
            <Field label="Origem">
              <Select
                value={String(nova.origemDeteccao)}
                onChange={(_, d) => setNova({ ...nova, origemDeteccao: Number(d.value) })}
              >
                {Object.entries(origemNaoConformidadeLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col4}>
            <Field label="Requisito relacionado">
              <Input
                value={nova.requisitoRelacionado ?? ''}
                onChange={(_, d) => setNova({ ...nova, requisitoRelacionado: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col5}>
            <Field label="Descrição" required>
              <Input value={nova.descricao} onChange={(_, d) => setNova({ ...nova, descricao: d.value })} />
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Local">
              <Input value={nova.local ?? ''} onChange={(_, d) => setNova({ ...nova, local: d.value })} />
            </Field>
          </div>
          <div className={estilos.col3}>
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
          </div>
          <div className={estilos.col3}>
            <Field label="Risco associado">
              <Select value={nova.riscoId ?? ''} onChange={(_, d) => setNova({ ...nova, riscoId: d.value })}>
                <option value="">Nenhum</option>
                {riscos.map((risco) => (
                  <option key={risco.id} value={risco.id}>
                    {risco.ambiente || risco.id}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Responsável">
              <Select
                value={nova.responsavelUsuarioId ?? ''}
                onChange={(_, d) => setNova({ ...nova, responsavelUsuarioId: d.value })}
              >
                <option value="">Nenhum</option>
                {usuarios.map((usuario) => (
                  <option key={usuario.id} value={usuario.id}>
                    {usuario.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Prazo">
              <CampoData value={nova.prazo ?? ''} onChange={(_, d) => setNova({ ...nova, prazo: d.value })} />
            </Field>
          </div>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Registrar
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Não conformidades</Text>
        </div>
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Origem</TableHeaderCell>
              <TableHeaderCell>Descrição</TableHeaderCell>
              <TableHeaderCell>Atividade</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Prazo</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {naoConformidades.map((nc) => (
              <TableRow
                key={nc.id}
                onClick={() => navigate(`/nao-conformidades/${nc.id}`)}
                style={{ cursor: 'pointer' }}
              >
                <TableCell>{origemNaoConformidadeLabel[nc.origemDeteccao]}</TableCell>
                <TableCell>{nc.descricao}</TableCell>
                <TableCell>{nc.atividadeNome ?? '—'}</TableCell>
                <TableCell>{nc.responsavelUsuarioNome ?? '—'}</TableCell>
                <TableCell>{nc.prazo?.slice(0, 10) ?? '—'}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusNaoConformidadeLabel[nc.status]}</Badge>
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
