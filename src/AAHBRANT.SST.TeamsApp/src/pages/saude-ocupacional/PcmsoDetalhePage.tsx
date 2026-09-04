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
import { ChipsField } from '../../components/ChipsField';
import { ArrowLeft24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusPcmsoDocumentoLabel,
  tipoAcaoPlanoLabel,
  StatusAcaoPlano,
  StatusPcmsoDocumento,
  type AcaoPlano,
  type AtualizarPcmsoPayload,
  type NovaAcaoPlano,
  type Obra,
  type Pcmso,
  type Setor,
  type Usuario,
} from '../../lib/api';
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

// Detalhe do PCMSO: edição completa dos campos clínicos (a criação em PcmsoTab.tsx só pede o
// essencial) + Plano de Ação vinculado via api.acoesPlano (origemTipo="Pcmso", origemId = id do
// PCMSO — mesmo mecanismo genérico usado por NaoConformidadeDetalhePage.tsx).
export function PcmsoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [pcmso, setPcmso] = useState<Pcmso | null>(null);
  const [edicao, setEdicao] = useState<AtualizarPcmsoPayload | null>(null);
  const [obras, setObras] = useState<Obra[]>([]);
  const [setores, setSetores] = useState<Setor[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [acoesPlano, setAcoesPlano] = useState<AcaoPlano[]>([]);
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  const [usuarioValidador, setUsuarioValidador] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [dados, listaObras, listaUsuarios, listaAcoes] = await Promise.all([
        api.pcmsos.obterPorId(id),
        api.obras.listar(),
        api.usuarios.listar(),
        api.acoesPlano.listar('Pcmso', id),
      ]);
      setPcmso(dados);
      setEdicao({ ...dados });
      setObras(listaObras);
      setUsuarios(listaUsuarios);
      setAcoesPlano(listaAcoes);
      setSetores(dados.obraId ? await api.setores.listar(dados.obraId) : []);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PCMSO.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function trocarObra(obraId: string) {
    if (!edicao) return;
    setEdicao({ ...edicao, obraId: obraId || null, setorId: null });
    setSetores(obraId ? await api.setores.listar(obraId) : []);
  }

  async function salvar() {
    if (!id || !edicao) return;
    if (!edicao.nome.trim() || !edicao.dataEmissao) {
      setErro('Preencha nome e data de emissão.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.pcmsos.atualizar(id, edicao);
      await carregar();
      sucessoToast('PCMSO atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar PCMSO.');
    } finally {
      setSalvando(false);
    }
  }

  async function criarAcao() {
    if (!id) return;
    if (!novaAcao.descricao.trim()) {
      setErro('Informe a descrição da ação do plano.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.acoesPlano.criar({
        origemTipo: 'Pcmso',
        origemId: id,
        ...novaAcao,
        responsavelUsuarioId: novaAcao.responsavelUsuarioId || null,
        prazo: novaAcao.prazo || null,
      });
      setNovaAcao(novaAcaoInicial());
      await carregar();
      sucessoToast('Ação do plano criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ação do plano.');
    } finally {
      setSalvando(false);
    }
  }

  async function validarAcao(acaoId: string) {
    if (!usuarioValidador) {
      setErro('Selecione o usuário responsável pela validação.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.acoesPlano.validar(acaoId, usuarioValidador);
      await carregar();
      sucessoToast('Ação do plano validada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao validar ação do plano.');
    } finally {
      setSalvando(false);
    }
  }

  async function excluirAcao(acaoId: string) {
    if (!(await confirmar('Excluir esta ação do plano? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.acoesPlano.excluir(acaoId);
      await carregar();
      sucessoToast('Ação do plano excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir ação do plano.');
    }
  }

  if (!id) {
    return <Text>PCMSO não encontrado.</Text>;
  }

  return (
    <div>
      {dialogElement}
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/saude-ocupacional')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Saúde Ocupacional
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      {!pcmso || !edicao ? (
        <Text>Carregando...</Text>
      ) : (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div style={{ display: 'flex', gap: 16, alignItems: 'center', marginBottom: 8 }}>
              <Text size={500} weight="semibold">
                {pcmso.numeroDocumento ? `${pcmso.numeroDocumento} — ` : ''}
                {pcmso.nome}
              </Text>
              <Badge appearance="tint" color={pcmso.status === StatusPcmsoDocumento.Vigente ? 'success' : 'informative'}>
                {statusPcmsoDocumentoLabel[pcmso.status]}
              </Badge>
              <BadgeVencimento dataValidade={pcmso.validade} />
            </div>

            <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>1. Dados gerais do documento</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col4}>
                <Field label="Nome do Documento" required>
                  <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
                </Field>
              </div>
              <div className={estilos.col2}>
                <Field label="Versão">
                  <Input
                    value={edicao.versao ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, versao: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Obra">
                  <Select value={edicao.obraId ?? ''} onChange={(_, d) => trocarObra(d.value)}>
                    <option value="">Nenhuma</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Setor">
                  <Select
                    value={edicao.setorId ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, setorId: d.value || null })}
                    disabled={!edicao.obraId}
                  >
                    <option value="">Nenhum</option>
                    {setores.map((setor) => (
                      <option key={setor.id} value={setor.id}>
                        {setor.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Data de emissão" required>
                  <CampoData
                    value={edicao.dataEmissao?.slice(0, 10) ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, dataEmissao: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Validade">
                  <CampoData
                    value={edicao.validade?.slice(0, 10) ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, validade: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Médico responsável">
                  <Input
                    value={edicao.medicoResponsavelNome ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, medicoResponsavelNome: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="CRM">
                  <Input
                    value={edicao.medicoResponsavelCrm ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, medicoResponsavelCrm: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Responsável">
                  <Select
                    value={edicao.responsavelUsuarioId ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, responsavelUsuarioId: d.value || null })}
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
            </div>

            <div className={estilos.sectionTitle}>2. Abrangência, riscos e exames</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col6}>
                <Field label="Unidades/Obras abrangidas">
                  <ChipsField
                    value={edicao.unidadesObrasAbrangidas ?? ''}
                    onChange={(v) => setEdicao({ ...edicao, unidadesObrasAbrangidas: v })}
                    placeholder="Digite e pressione Enter"
                  />
                </Field>
              </div>
              <div className={estilos.col6}>
                <Field label="Funções contempladas">
                  <Textarea
                    value={edicao.funcoesContempladas ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, funcoesContempladas: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Riscos considerados">
                  <Textarea
                    value={edicao.riscosConsiderados ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, riscosConsiderados: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Exames previstos">
                  <Textarea
                    value={edicao.examesPrevistos ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, examesPrevistos: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col4}>
                <Field label="Periodicidades">
                  <Textarea
                    value={edicao.periodicidades ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, periodicidades: d.value })}
                  />
                </Field>
              </div>
            </div>

            <div className={estilos.footer}>
              <Text className={estilos.footerInfo}>
                O status segue o fluxo documental padrão (Rascunho → Em aprovação → Vigente → Obsoleto →
                Cancelado) e é alterado pelo mesmo fluxo de Gestão Documental usado pelos demais
                documentos controlados — não editável diretamente aqui.
              </Text>
              <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={salvando}>
                Salvar alterações
              </Button>
            </div>
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Nova ação do plano</Text>
            </div>
            <div className={estilos.formGrid}>
              <div className={estilos.col2}>
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
              </div>
              <div className={estilos.col4}>
                <Field label="Descrição" required>
                  <Input
                    value={novaAcao.descricao}
                    onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })}
                  />
                </Field>
              </div>
              <div className={estilos.col3}>
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
              </div>
              <div className={estilos.col2}>
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
              </div>
              <div className={estilos.col2}>
                <Field label="Prazo">
                  <CampoData
                    value={novaAcao.prazo ?? ''}
                    onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })}
                  />
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button appearance="primary" onClick={criarAcao} disabled={salvando}>
                Adicionar ação
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Plano de ação</Text>
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
            {acoesPlano.length === 0 ? (
              <EstadoVazio mensagem="Nenhuma ação do plano cadastrada ainda." />
            ) : (
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
                {acoesPlano.map((acao) => (
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
                      <div style={{ display: 'flex', gap: 4 }}>
                        {acao.status !== StatusAcaoPlano.Concluido && !acao.dataValidacao && (
                          <Button appearance="subtle" onClick={() => validarAcao(acao.id)} disabled={salvando}>
                            Validar
                          </Button>
                        )}
                        <Button
                          appearance="subtle"
                          icon={<Delete24Regular />}
                          onClick={() => excluirAcao(acao.id)}
                          aria-label="Excluir"
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
