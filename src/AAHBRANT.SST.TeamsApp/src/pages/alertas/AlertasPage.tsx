import { useEffect, useState } from 'react';
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
import { AddCircle24Regular, CheckmarkCircle24Regular, Delete24Regular, DismissCircle24Regular, PlayCircle24Regular } from '@fluentui/react-icons';
import {
  api,
  severidadeAlertaLabel,
  statusAlertaLabel,
  tipoAlertaLabel,
  StatusAlerta,
  type Alerta,
  type NovoAlerta,
  type Obra,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function novaInicial(): NovoAlerta {
  return {
    tipo: 1,
    severidade: 2,
    titulo: '',
    descricao: '',
    entidadeOrigemTipo: '',
    entidadeOrigemId: '',
    trabalhadorId: '',
    obraId: '',
    destinatarioUsuarioId: '',
    dataLimiteTratamento: '',
  };
}

function severidadeCor(severidade: number): 'informative' | 'warning' | 'danger' {
  if (severidade === 3) return 'danger';
  if (severidade === 2) return 'warning';
  return 'informative';
}

export function AlertasPage() {
  const estilos = usePageStyles();
  const [alertas, setAlertas] = useState<Alerta[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [filtroStatus, setFiltroStatus] = useState<string>('');
  const [filtroSeveridade, setFiltroSeveridade] = useState<string>('');
  const [novo, setNovo] = useState<NovoAlerta>(novaInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaAlertas, listaObras, listaTrabalhadores, listaUsuarios] = await Promise.all([
        api.alertas.listar({
          status: filtroStatus ? Number(filtroStatus) : undefined,
          severidade: filtroSeveridade ? Number(filtroSeveridade) : undefined,
        }),
        api.obras.listar(),
        api.trabalhadores.listar(),
        api.usuarios.listar(),
      ]);
      setAlertas(listaAlertas);
      setObras(listaObras);
      setTrabalhadores(listaTrabalhadores);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar alertas.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroStatus, filtroSeveridade]);

  async function criar() {
    if (!novo.titulo.trim() || !novo.entidadeOrigemTipo.trim() || !novo.entidadeOrigemId.trim()) {
      setErro('Informe título, tipo de origem e id de origem.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.alertas.criar({
        ...novo,
        descricao: novo.descricao || null,
        trabalhadorId: novo.trabalhadorId || null,
        obraId: novo.obraId || null,
        destinatarioUsuarioId: novo.destinatarioUsuarioId || null,
        dataLimiteTratamento: novo.dataLimiteTratamento || null,
      });
      setNovo(novaInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar alerta.');
    } finally {
      setCarregando(false);
    }
  }

  async function executar(acao: (id: string) => Promise<void>, id: string, mensagemErro: string) {
    try {
      setErro(null);
      await acao(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : mensagemErro);
    }
  }

  async function excluir(id: string) {
    await executar((alertaId) => api.alertas.excluir(alertaId), id, 'Falha ao excluir alerta.');
  }

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Alertas
        </Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo alerta</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Tipo">
            <Select value={String(novo.tipo)} onChange={(_, d) => setNovo({ ...novo, tipo: Number(d.value) })}>
              {Object.entries(tipoAlertaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Severidade">
            <Select
              value={String(novo.severidade)}
              onChange={(_, d) => setNovo({ ...novo, severidade: Number(d.value) })}
            >
              {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Título" required>
            <Input value={novo.titulo} onChange={(_, d) => setNovo({ ...novo, titulo: d.value })} />
          </Field>
          <Field label="Descrição">
            <Input value={novo.descricao ?? ''} onChange={(_, d) => setNovo({ ...novo, descricao: d.value })} />
          </Field>
          <Field label="Tipo da entidade de origem" required hint="Ex.: Aso, Treinamento, Epp, NaoConformidade">
            <Input
              value={novo.entidadeOrigemTipo}
              onChange={(_, d) => setNovo({ ...novo, entidadeOrigemTipo: d.value })}
            />
          </Field>
          <Field label="Id da entidade de origem" required>
            <Input
              value={novo.entidadeOrigemId}
              onChange={(_, d) => setNovo({ ...novo, entidadeOrigemId: d.value })}
            />
          </Field>
          <Field label="Obra">
            <Select value={novo.obraId ?? ''} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
              <option value="">Nenhuma</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Trabalhador">
            <Select
              value={novo.trabalhadorId ?? ''}
              onChange={(_, d) => setNovo({ ...novo, trabalhadorId: d.value })}
            >
              <option value="">Nenhum</option>
              {trabalhadores.map((trabalhador) => (
                <option key={trabalhador.id} value={trabalhador.id}>
                  {trabalhador.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Destinatário">
            <Select
              value={novo.destinatarioUsuarioId ?? ''}
              onChange={(_, d) => setNovo({ ...novo, destinatarioUsuarioId: d.value })}
            >
              <option value="">Nenhum</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Prazo para tratamento">
            <Input
              type="date"
              value={novo.dataLimiteTratamento ?? ''}
              onChange={(_, d) => setNovo({ ...novo, dataLimiteTratamento: d.value })}
            />
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
          <Text weight="semibold">Alertas</Text>
          <div style={{ display: 'flex', gap: 8 }}>
            <Select value={filtroStatus} onChange={(_, d) => setFiltroStatus(d.value)}>
              <option value="">Todos os status</option>
              {Object.entries(statusAlertaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
            <Select value={filtroSeveridade} onChange={(_, d) => setFiltroSeveridade(d.value)}>
              <option value="">Todas as severidades</option>
              {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </div>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Título</TableHeaderCell>
              <TableHeaderCell>Severidade</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Destinatário</TableHeaderCell>
              <TableHeaderCell>Prazo</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {alertas.map((alerta) => (
              <TableRow key={alerta.id}>
                <TableCell>{tipoAlertaLabel[alerta.tipo]}</TableCell>
                <TableCell>{alerta.titulo}</TableCell>
                <TableCell>
                  <Badge appearance="tint" color={severidadeCor(alerta.severidade)}>
                    {severidadeAlertaLabel[alerta.severidade]}
                  </Badge>
                </TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusAlertaLabel[alerta.status]}</Badge>
                </TableCell>
                <TableCell>{alerta.destinatarioUsuarioNome ?? '—'}</TableCell>
                <TableCell>{alerta.dataLimiteTratamento?.slice(0, 10) ?? '—'}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    {alerta.status === StatusAlerta.Aberto && (
                      <Button
                        appearance="subtle"
                        icon={<PlayCircle24Regular />}
                        title="Iniciar tratamento"
                        onClick={() => executar(api.alertas.iniciarTratamento, alerta.id, 'Falha ao iniciar tratamento.')}
                      />
                    )}
                    {(alerta.status === StatusAlerta.Aberto || alerta.status === StatusAlerta.EmTratamento) && (
                      <Button
                        appearance="subtle"
                        icon={<CheckmarkCircle24Regular />}
                        title="Resolver"
                        onClick={() => executar(api.alertas.resolver, alerta.id, 'Falha ao resolver alerta.')}
                      />
                    )}
                    {(alerta.status === StatusAlerta.Aberto || alerta.status === StatusAlerta.EmTratamento) && (
                      <Button
                        appearance="subtle"
                        icon={<DismissCircle24Regular />}
                        title="Ignorar"
                        onClick={() => executar(api.alertas.ignorar, alerta.id, 'Falha ao ignorar alerta.')}
                      />
                    )}
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      title="Excluir"
                      onClick={() => excluir(alerta.id)}
                    />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
