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
import { Add24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
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
import { usePageStyles } from '../pageStyles';
import { segundaFeiraAtualIso } from '../../lib/datas';

function semanalVazia(): NovaDdsSemanal {
  return { obraId: '', tipo: TipoDdsSemanal.Proprios, dataInicioSemana: segundaFeiraAtualIso() };
}

const corBadgeStatus: Record<number, 'informative' | 'success'> = {
  [StatusDdsSemanal.EmAndamento]: 'informative',
  [StatusDdsSemanal.Concluida]: 'success',
};

// Reformulação 31/08 — o DDS passou a ser organizado por semana (contêiner), seguindo o modelo em
// papel "Registro Semanal de DDS - Empregados Próprios/Terceirizados". Os registros diários (feitos
// e assinados todo dia) ficam dentro de cada semana — ver DdsSemanalDetalhePage.
export function DdsSemanalPage() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [registros, setRegistros] = useState<DdsSemanal[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [nova, setNova] = useState<NovaDdsSemanal>(semanalVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras] = await Promise.all([api.ddsSemanal.listar(), api.obras.listar()]);
      setRegistros(lista);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar as semanas de DDS.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!nova.obraId || !nova.dataInicioSemana || (nova.tipo === TipoDdsSemanal.Terceirizados && !nova.empresaTerceirizada)) {
      setErro('Preencha obra, data de início da semana e, se terceirizado, a empresa.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const resultado = await api.ddsSemanal.criar(nova);
      setNova(semanalVazia());
      await carregar();
      navigate(`/prevencao/dds/semana/${resultado.id}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar a semana de DDS.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          DDS — Diálogo Diário de Segurança (Registro Semanal)
        </Text>
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova semana de DDS</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Semana</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Obra">
              <Select value={nova.obraId} onChange={(_, d) => setNova({ ...nova, obraId: d.value })}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Tipo">
              <Select value={String(nova.tipo)} onChange={(_, d) => setNova({ ...nova, tipo: Number(d.value) })}>
                <option value={String(TipoDdsSemanal.Proprios)}>{tipoDdsSemanalLabel[TipoDdsSemanal.Proprios]}</option>
                <option value={String(TipoDdsSemanal.Terceirizados)}>{tipoDdsSemanalLabel[TipoDdsSemanal.Terceirizados]}</option>
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Início da semana">
              <CampoData
                value={nova.dataInicioSemana}
                onChange={(_, d) => setNova({ ...nova, dataInicioSemana: d.value })}
              />
            </Field>
          </div>
          {nova.tipo === TipoDdsSemanal.Terceirizados && (
            <div className={estilos.col4}>
              <Field label="Empresa terceirizada">
                <Input
                  value={nova.empresaTerceirizada ?? ''}
                  onChange={(_, d) => setNova({ ...nova, empresaTerceirizada: d.value })}
                />
              </Field>
            </div>
          )}
          <div className={estilos.col3}>
            <Field label="Nº do documento">
              <Input value={nova.numeroDocumento ?? ''} onChange={(_, d) => setNova({ ...nova, numeroDocumento: d.value })} />
            </Field>
          </div>
          <div className={estilos.col5}>
            <Field label="Local / Frente de serviço">
              <Input
                value={nova.localFrenteServico ?? ''}
                onChange={(_, d) => setNova({ ...nova, localFrenteServico: d.value })}
              />
            </Field>
          </div>
        </div>

        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Abrir semana
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Semanas registradas</Text>
        </div>

        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Semana</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Dias registrados</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {registros.map((semanal) => (
              <TableRow
                key={semanal.id}
                onClick={() => navigate(`/prevencao/dds/semana/${semanal.id}`)}
                style={{ cursor: 'pointer' }}
              >
                <TableCell>{semanal.obraNome}</TableCell>
                <TableCell>{tipoDdsSemanalLabel[semanal.tipo]}</TableCell>
                <TableCell>
                  {semanal.dataInicioSemana?.slice(0, 10)} a {semanal.dataFimSemana?.slice(0, 10)}
                </TableCell>
                <TableCell>{semanal.responsavelUsuarioNome}</TableCell>
                <TableCell>
                  {semanal.totalDiasConcluidos}/{semanal.totalDiasRegistrados} concluídos (de 5)
                </TableCell>
                <TableCell>
                  <Badge color={corBadgeStatus[semanal.status]} appearance="tint">
                    {statusDdsSemanalLabel[semanal.status]}
                  </Badge>
                </TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/prevencao/dds/semana/${semanal.id}`)}
                    aria-label="Ver semana"
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
