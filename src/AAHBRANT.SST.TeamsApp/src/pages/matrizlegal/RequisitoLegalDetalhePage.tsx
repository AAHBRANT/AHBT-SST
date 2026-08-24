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
} from '@fluentui/react-components';
import { ArrowLeft24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusRequisitoLegalLabel,
  tipoAcaoPlanoLabel,
  StatusAcaoPlano,
  type NovaAcaoPlano,
  type RequisitoLegalDetalhe,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

export function RequisitoLegalDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<RequisitoLegalDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novoStatus, setNovoStatus] = useState<string>('');
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  const [usuarioValidador, setUsuarioValidador] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([
        api.matrizLegal.obterDetalhe(id),
        api.usuarios.listar(),
      ]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setNovoStatus(String(det.requisitoLegal.status));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar requisito legal.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function reclassificarStatus() {
    if (!id || !novoStatus) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.matrizLegal.atualizarStatus(id, Number(novoStatus));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao reclassificar status.');
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
        origemTipo: 'RequisitoLegal',
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
    return <Text>Requisito legal não encontrado.</Text>;
  }

  const r = detalhe?.requisitoLegal;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/conformidade/matriz-legal')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Matriz Legal
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {r ? (
          <>
            <Text size={500} weight="semibold">
              {r.codigo} — {r.norma}
              {r.item ? ` (${r.item})` : ''}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Tema: {r.tema}</Text>
              <Text>Aplicável: {r.aplicabilidade ? 'Sim' : 'Não'}</Text>
              <Badge appearance="tint">{statusRequisitoLegalLabel[r.status]}</Badge>
            </div>
            <Text style={{ display: 'block', marginTop: 8 }}>{r.requisito}</Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              {r.obraNome && <Text>Obra: {r.obraNome}</Text>}
              {!r.obraNome && <Text>Obra: Global (todas as obras)</Text>}
              {r.responsavelUsuarioNome && <Text>Responsável: {r.responsavelUsuarioNome}</Text>}
              {r.periodicidade && <Text>Periodicidade: {r.periodicidade}</Text>}
              {r.prazo && <Text>Prazo: {r.prazo.slice(0, 10)}</Text>}
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              {r.justificativa && <Text>Justificativa: {r.justificativa}</Text>}
              {r.evidencia && <Text>Evidência: {r.evidencia}</Text>}
              {r.ultimaRevisao && <Text>Última revisão: {r.ultimaRevisao.slice(0, 10)}</Text>}
              {r.proximaRevisao && <Text>Próxima revisão: {r.proximaRevisao.slice(0, 10)}</Text>}
            </div>

            <div className={estilos.formActions} style={{ marginTop: 16, alignItems: 'flex-end' }}>
              <Field label="Reclassificar status">
                <Select value={novoStatus} onChange={(_, d) => setNovoStatus(d.value)}>
                  {Object.entries(statusRequisitoLegalLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
              <Button
                appearance="primary"
                icon={<Save24Regular />}
                onClick={reclassificarStatus}
                disabled={processando || Number(novoStatus) === r.status}
              >
                Salvar status
              </Button>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
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
            <Input
              type="date"
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
        <Table>
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
