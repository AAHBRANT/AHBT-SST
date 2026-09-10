import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Abas,
  Button,
  Campo,
  Card,
  CampoData,
  ChipsField,
  Carregando,
  DataTable,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  StatusChip,
  Textarea,
  nivelVencimento,
  rotuloDeVencimento,
  tomDeVencimento,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusPcmsoDocumentoLabel,
  tipoAcaoPlanoLabel,
  StatusAcaoPlano,
  type AcaoPlano,
  type AtualizarPcmsoPayload,
  type NovaAcaoPlano,
  type Obra,
  type Pcmso,
  type Setor,
  type Usuario,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { VisualizadorDocumentoPdf } from '../../components/VisualizadorDocumentoPdf';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

// StatusPcmsoDocumento: Rascunho=1, EmAprovacao=2, Vigente=3, Obsoleto=4, Cancelado=5.
const tomPorStatusPcmso: Record<number, Tom> = { 1: 'neutro', 2: 'atencao', 3: 'ok', 4: 'neutro', 5: 'alerta' };

// Mesmo mapeamento de NaoConformidadeDetalhePage.tsx: StatusAcaoPlano é reaproveitado de
// StatusControleRisco (Pendente/EmAndamento/Concluido/Vencido).
const tomAcaoPlano: Record<number, Tom> = {
  [StatusAcaoPlano.Pendente]: 'atencao',
  [StatusAcaoPlano.EmAndamento]: 'atencao',
  [StatusAcaoPlano.Concluido]: 'ok',
  [StatusAcaoPlano.Vencido]: 'alerta',
};

function chipVencimento(data?: string | null) {
  const nivel = nivelVencimento(data);
  return nivel ? <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip> : null;
}

// Onda 2 Task 12 (camada ui/): detalhe do PCMSO — edição completa dos campos clínicos (a criação em
// PcmsoTab.tsx só pede o essencial) + Plano de Ação vinculado via api.acoesPlano (origemTipo="Pcmso",
// origemId = id do PCMSO — mesmo mecanismo genérico usado por NaoConformidadeDetalhePage.tsx). Sem
// WorkflowActions: não há um conjunto de ações nomeadas com formulário próprio aqui — o status segue
// o fluxo documental de Gestão Documental, "não editável diretamente aqui" (ver footer do form).
export function PcmsoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [aba, setAba] = useState<'documento' | 'dados'>('documento');
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
  const { confirmar, dialogElement } = useConfirmar();
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

  const colunasAcoes: Coluna<AcaoPlano>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoAcaoPlanoLabel[a.tipo] },
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'responsavel', rotulo: 'Responsável', render: (a) => a.responsavelUsuarioNome ?? '—' },
    { chave: 'prioridade', rotulo: 'Prioridade', render: (a) => prioridadeAcaoLabel[a.prioridade] },
    { chave: 'prazo', rotulo: 'Prazo', largura: '108px', render: (a) => a.prazo?.slice(0, 10) ?? '—' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => (
        <StatusChip tom={tomAcaoPlano[a.status] ?? 'neutro'}>{statusAcaoPlanoLabel[a.status]}</StatusChip>
      ),
    },
  ];

  if (!id) return <FeedbackInline tom="erro">PCMSO não encontrado.</FeedbackInline>;
  // Mesma ordem de guarda de NaoConformidadeDetalhePage.tsx: erro na carga inicial precisa aparecer
  // aqui, senão o skeleton fica para sempre e a falha (API fora, id inexistente) não teria onde ser
  // lida.
  if (!pcmso || !edicao) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: `${pcmso.numeroDocumento ? `${pcmso.numeroDocumento} — ` : ''}${pcmso.nome}`,
        status: (
          <>
            <StatusChip tom={tomPorStatusPcmso[pcmso.status] ?? 'neutro'}>
              {statusPcmsoDocumentoLabel[pcmso.status]}
            </StatusChip>{' '}
            {chipVencimento(pcmso.validade)}
          </>
        ),
        voltarPara: '/saude-ocupacional',
        rotuloVoltar: 'Voltar para Saúde Ocupacional',
      }}
    >
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ marginBottom: 16 }}>
        <Abas
          nivel="modulo"
          aria-label="Seções do PCMSO"
          valor={aba}
          aoMudar={setAba}
          abas={[
            { valor: 'documento', rotulo: 'PCMSO' },
            { valor: 'dados', rotulo: 'Dados' },
          ]}
        />
      </div>

      {aba === 'documento' && (
        <VisualizadorDocumentoPdf
          id={id}
          obterDocumento={() => api.pcmsos.obterDocumento(id)}
          enviarDocumento={(arquivo) => api.pcmsos.enviarDocumento(id, arquivo)}
        />
      )}

      {aba === 'dados' && (
        <>
      <Card>
        <FormSection titulo="Dados gerais do documento" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Nome do Documento" required>
                <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Versão">
                <Input value={edicao.versao ?? ''} onChange={(_, d) => setEdicao({ ...edicao, versao: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
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
            </Campo>
            <Campo span={3}>
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
            </Campo>
            <Campo span={3}>
              <Field label="Data de emissão" required>
                <CampoData
                  value={edicao.dataEmissao?.slice(0, 10) ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, dataEmissao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade">
                <CampoData
                  value={edicao.validade?.slice(0, 10) ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, validade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Médico responsável">
                <Input
                  value={edicao.medicoResponsavelNome ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, medicoResponsavelNome: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="CRM">
                <Input
                  value={edicao.medicoResponsavelCrm ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, medicoResponsavelCrm: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
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
            </Campo>
          </FormGrid>
        </FormSection>

        <FormSection titulo="Abrangência, riscos e exames" numero={2}>
          <FormGrid>
            <Campo span={6}>
              <Field label="Unidades/Obras abrangidas">
                <ChipsField
                  value={edicao.unidadesObrasAbrangidas ?? ''}
                  onChange={(v) => setEdicao({ ...edicao, unidadesObrasAbrangidas: v })}
                  placeholder="Digite e pressione Enter"
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Funções contempladas">
                <Textarea
                  value={edicao.funcoesContempladas ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, funcoesContempladas: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Riscos considerados">
                <Textarea
                  value={edicao.riscosConsiderados ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, riscosConsiderados: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Exames previstos">
                <Textarea
                  value={edicao.examesPrevistos ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, examesPrevistos: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Periodicidades">
                <Textarea
                  value={edicao.periodicidades ?? ''}
                  onChange={(_, d) => setEdicao({ ...edicao, periodicidades: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape info="O status segue o fluxo documental padrão (Rascunho → Em aprovação → Vigente → Obsoleto → Cancelado) e é alterado pelo mesmo fluxo de Gestão Documental usado pelos demais documentos controlados — não editável diretamente aqui.">
            <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={salvando}>
              Salvar alterações
            </Button>
          </FormRodape>
        </FormSection>
      </Card>

      <Card titulo="Nova ação do plano">
        <FormGrid>
          <Campo span={2}>
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
          </Campo>
          <Campo span={4}>
            <Field label="Descrição" required>
              <Input
                value={novaAcao.descricao}
                onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={3}>
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
          </Campo>
          <Campo span={2}>
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
          </Campo>
          <Campo span={2}>
            <Field label="Prazo">
              <CampoData
                value={novaAcao.prazo ?? ''}
                onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })}
              />
            </Field>
          </Campo>
        </FormGrid>
        <FormRodape>
          <Button appearance="primary" onClick={criarAcao} disabled={salvando}>
            Adicionar ação
          </Button>
        </FormRodape>
      </Card>

      <Card
        titulo="Plano de ação"
        acoes={
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
        }
      >
        <DataTable
          aria-label="Plano de ação do PCMSO"
          colunas={colunasAcoes}
          linhas={acoesPlano}
          chaveLinha={(a) => a.id}
          vazio={{ titulo: 'Nenhuma ação do plano cadastrada ainda.' }}
          acoesLinha={(a) => (
            <>
              {a.status !== StatusAcaoPlano.Concluido && !a.dataValidacao && (
                <Button appearance="subtle" onClick={() => validarAcao(a.id)} disabled={salvando}>
                  Validar
                </Button>
              )}
              <Button
                appearance="subtle"
                icon={<Delete24Regular />}
                onClick={() => excluirAcao(a.id)}
                aria-label="Excluir"
              />
            </>
          )}
        />
      </Card>
      </>
      )}
      {dialogElement}
    </DetailPageLayout>
  );
}
