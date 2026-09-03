import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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
import { CampoData } from '../../components/CampoData';
import { ArrowLeft24Regular, CheckmarkCircle24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  metodologiaInvestigacaoLabel,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusAcidenteLabel,
  tipoAcaoPlanoLabel,
  tipoOcorrenciaLabel,
  StatusAcaoPlano,
  StatusAcidente,
  type AcidenteDetalhe,
  type NovaAcaoPlano,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

export function AcidenteDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<AcidenteDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [metodologia, setMetodologia] = useState<string>('');
  const [causas, setCausas] = useState<string>('');
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  const [usuarioValidador, setUsuarioValidador] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([api.acidentes.obterDetalhe(id), api.usuarios.listar()]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setMetodologia(det.acidente.metodologiaInvestigacao ? String(det.acidente.metodologiaInvestigacao) : '');
      setCausas(det.acidente.causas ?? '');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar acidente.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function salvarInvestigacao() {
    if (!id || !detalhe) return;
    try {
      setProcessando(true);
      setErro(null);
      const a = detalhe.acidente;
      await api.acidentes.atualizar(id, {
        tipo: a.tipo,
        obraId: a.obraId,
        trabalhadorId: a.trabalhadorId,
        atividadeId: a.atividadeId,
        local: a.local,
        data: a.data,
        hora: a.hora,
        descricao: a.descricao,
        lesao: a.lesao,
        consequencia: a.consequencia,
        atendimento: a.atendimento,
        houveAfastamento: a.houveAfastamento,
        diasAfastamento: a.diasAfastamento,
        numeroCat: a.numeroCat,
        gravidade: a.gravidade,
        metodologiaInvestigacao: metodologia ? Number(metodologia) : null,
        causas: causas || null,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar dados da investigação.');
    } finally {
      setProcessando(false);
    }
  }

  async function avancarStatus() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.acidentes.avancarStatus(id);
      await carregar();
    } catch (e) {
      setErro(
        e instanceof Error
          ? e.message
          : 'Falha ao avançar status. Confira se todas as ações do plano já foram concluídas.',
      );
    } finally {
      setProcessando(false);
    }
  }

  async function criarAcao() {
    if (!id) return;
    if (!novaAcao.descricao.trim()) {
      setErro('Informe a descrição da ação do plano.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.acoesPlano.criar({
        origemTipo: 'Acidente',
        origemId: id,
        ...novaAcao,
        responsavelUsuarioId: novaAcao.responsavelUsuarioId || null,
        prazo: novaAcao.prazo || null,
      });
      setNovaAcao(novaAcaoInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ação do plano.');
    } finally {
      setProcessando(false);
    }
  }

  async function validarAcao(acaoId: string) {
    if (!usuarioValidador) {
      setErro('Selecione o usuário responsável pela validação.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.acoesPlano.validar(acaoId, usuarioValidador);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao validar ação do plano.');
    } finally {
      setProcessando(false);
    }
  }

  if (!id) {
    return <Text>Acidente não encontrado.</Text>;
  }

  const a = detalhe?.acidente;
  // Acidentes/Incidentes/Quase-acidentes viraram abas de OcorrenciasPage (02/09) — volta pra aba
  // que corresponde ao tipo do registro aberto, não sempre pra "Acidentes".
  const secaoOcorrencia =
    a?.tipo === 2 ? 'incidentes' : a?.tipo === 3 ? 'quase-acidentes' : 'acidentes';

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate(`/ocorrencias?secao=${secaoOcorrencia}`)}
        style={{ marginBottom: 12 }}
      >
        Voltar para Ocorrências
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {a ? (
          <>
            <Text size={500} weight="semibold">
              {tipoOcorrenciaLabel[a.tipo]} — {a.local}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {a.obraNome ?? '—'}</Text>
              {a.trabalhadorNome && <Text>Trabalhador: {a.trabalhadorNome}</Text>}
              {a.atividadeNome && <Text>Atividade: {a.atividadeNome}</Text>}
              <Text>Data: {a.data?.slice(0, 10)}</Text>
              <Badge appearance="tint">{statusAcidenteLabel[a.status]}</Badge>
            </div>
            <Text style={{ display: 'block', marginTop: 8 }}>{a.descricao}</Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              {a.lesao && <Text>Lesão: {a.lesao}</Text>}
              {a.consequencia && <Text>Consequência: {a.consequencia}</Text>}
              {a.atendimento && <Text>Atendimento: {a.atendimento}</Text>}
              {a.houveAfastamento && <Text>Afastamento: {a.diasAfastamento ?? '—'} dia(s)</Text>}
              {a.numeroCat && <Text>CAT: {a.numeroCat}</Text>}
            </div>

            {a.status !== StatusAcidente.Concluido && (
              <div className={estilos.formActions} style={{ marginTop: 16 }}>
                <Button
                  appearance="primary"
                  icon={<CheckmarkCircle24Regular />}
                  onClick={avancarStatus}
                  disabled={processando}
                >
                  Avançar status ({statusAcidenteLabel[a.status]} → {statusAcidenteLabel[a.status + 1]})
                </Button>
              </div>
            )}
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Investigação (Seção 28 — análise de causas)</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Metodologia de investigação">
            <Select value={metodologia} onChange={(_, d) => setMetodologia(d.value)}>
              <option value="">Não definida</option>
              {Object.entries(metodologiaInvestigacaoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Causas identificadas">
            <Textarea value={causas} onChange={(_, d) => setCausas(d.value)} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Save24Regular />} onClick={salvarInvestigacao} disabled={processando}>
            Salvar investigação
          </Button>
        </div>
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova ação do plano</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Tipo">
            <Select
              value={String(novaAcao.tipo)}
              onChange={(_, d) => setNovaAcao({ ...novaAcao, tipo: Number(d.value) })}
            >
              {Object.entries(tipoAcaoPlanoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Descrição" required>
            <Input value={novaAcao.descricao} onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })} />
          </Field>
          <Field label="Responsável">
            <Select
              value={novaAcao.responsavelUsuarioId ?? ''}
              onChange={(_, d) => setNovaAcao({ ...novaAcao, responsavelUsuarioId: d.value })}
            >
              <option value="">Nenhum</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Prioridade">
            <Select
              value={String(novaAcao.prioridade)}
              onChange={(_, d) => setNovaAcao({ ...novaAcao, prioridade: Number(d.value) })}
            >
              {Object.entries(prioridadeAcaoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Prazo">
            <CampoData
              value={novaAcao.prazo ?? ''}
              onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" onClick={criarAcao} disabled={processando}>
            Adicionar ação
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Ações do plano</Text>
          <Field label="Validar como">
            <Select value={usuarioValidador} onChange={(_, d) => setUsuarioValidador(d.value)}>
              <option value="">Selecione um usuário</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Descrição</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Prioridade</TableHeaderCell>
              <TableHeaderCell>Prazo</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {detalhe?.acoesPlano.map((acao) => (
              <TableRow key={acao.id}>
                <TableCell>{tipoAcaoPlanoLabel[acao.tipo]}</TableCell>
                <TableCell>{acao.descricao}</TableCell>
                <TableCell>{acao.responsavelUsuarioNome ?? '—'}</TableCell>
                <TableCell>{prioridadeAcaoLabel[acao.prioridade]}</TableCell>
                <TableCell>{acao.prazo?.slice(0, 10) ?? '—'}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusAcaoPlanoLabel[acao.status]}</Badge>
                </TableCell>
                <TableCell>
                  {acao.status !== StatusAcaoPlano.Concluido && !acao.dataValidacao && (
                    <Button appearance="subtle" onClick={() => validarAcao(acao.id)} disabled={processando}>
                      Validar
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
