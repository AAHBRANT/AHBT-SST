import { Fragment, useEffect, useMemo, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  Select,
  StatusChip,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import {
  Add24Regular,
  ChevronDown20Regular,
  ChevronRight20Regular,
  Delete24Regular,
  Save24Regular,
  Search24Regular,
} from '@fluentui/react-icons';
import {
  api,
  statusUsuarioLabel,
  StatusUsuario,
  escopoAcessoLabel,
  EscopoAcesso,
  type ItemPermissaoPerfil,
  type NovoPerfilAcesso,
  type NovoUsuario,
  type Obra,
  type PerfilAcesso,
  type Permissao,
  type Trabalhador,
  type Usuario,
  type UsuarioPerfilObra,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const usuarioVazio: NovoUsuario = { email: '', nome: '', trabalhadorId: '' };
const perfilVazio: NovoPerfilAcesso = { nome: '', descricao: '' };
const escopos = [EscopoAcesso.Global, EscopoAcesso.Unidade, EscopoAcesso.Obra, EscopoAcesso.Proprio];

const tomPorStatusUsuario: Record<number, Tom> = {
  [StatusUsuario.Ativo]: 'ok',
  [StatusUsuario.Inativo]: 'neutro',
  [StatusUsuario.Bloqueado]: 'alerta',
};

function chave(permissaoId: string, escopo: number) {
  return `${permissaoId}|${escopo}`;
}

// Onda 2 Task 17 (camada ui/): a matriz de permissões (módulo → ação × escopo, com bulk-toggle por
// coluna e vários módulos abertos ao mesmo tempo) é uma grade/árvore genuína, não um caso de
// "lista + detalhe" — decisão registrada (ver comentário completo logo antes da matriz, mais abaixo)
// de MANTER a árvore custom em vez de forçar `DataTable expansivel`. As outras duas tabelas da tela
// (Usuários cadastrados, Perfis de acesso) e a mini-lista de Perfis por obra SÃO lista+ação simples
// e viraram `DataTable` de verdade (conversão 1). `Badge` → `StatusChip` (5), `estilos.erro` →
// `FeedbackInline` (4), `useConfirmarExclusao` → `useConfirmar` (6). Formulários de criação (Novo
// Usuário / Novo Perfil) permaneceram inline, sem `PainelLateral`: esta tela já é o template de
// dashboard de 2 colunas (não o template §4.2 "Lista"), e mover os dois formulários para gaveta
// dobraria a complexidade de estado sem estar no escopo pedido — julgamento documentado, não omissão.
export function ControleAcessoTab() {
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [perfis, setPerfis] = useState<PerfilAcesso[]>([]);
  const [permissoes, setPermissoes] = useState<Permissao[]>([]);

  const [novoUsuario, setNovoUsuario] = useState<NovoUsuario>(usuarioVazio);
  const [usuarioSelecionadoId, setUsuarioSelecionadoId] = useState<string | null>(null);
  const [nomeEdicao, setNomeEdicao] = useState('');
  const [statusEdicao, setStatusEdicao] = useState<number>(StatusUsuario.Ativo);
  const [perfilParaAtribuir, setPerfilParaAtribuir] = useState('');
  const [obraParaAtribuir, setObraParaAtribuir] = useState('');

  const [novoPerfil, setNovoPerfil] = useState<NovoPerfilAcesso>(perfilVazio);
  const [perfilSelecionadoId, setPerfilSelecionadoId] = useState<string | null>(null);
  const [marcados, setMarcados] = useState<Set<string>>(new Set());
  const [modulosAbertos, setModulosAbertos] = useState<Set<string>>(new Set());
  const [filtroPermissao, setFiltroPermissao] = useState('');

  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [usu, trabs, obs, prf, perm] = await Promise.all([
        api.usuarios.listar(),
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.perfisAcesso.listar(),
        api.permissoes.listar(),
      ]);
      setUsuarios(usu);
      setTrabalhadores(trabs);
      setObras(obs);
      setPerfis(prf);
      setPermissoes(perm);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar controle de acesso.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const usuarioSelecionado = usuarios.find((u) => u.id === usuarioSelecionadoId) ?? null;
  const perfilSelecionado = perfis.find((p) => p.id === perfilSelecionadoId) ?? null;

  const permissoesPorModulo = useMemo(() => {
    const grupos = new Map<string, Permissao[]>();
    for (const p of permissoes) {
      const lista = grupos.get(p.modulo) ?? [];
      lista.push(p);
      grupos.set(p.modulo, lista);
    }
    return Array.from(grupos.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [permissoes]);

  // Com ~90 permissões x 4 escopos, a matriz virava uma tabela de centenas de linhas expandida de
  // uma vez — cada módulo passa a vir fechado por padrão (contador de quantas já estão marcadas
  // mesmo fechado), com busca por módulo/ação/código e um "marcar/desmarcar coluna inteira do
  // módulo" por escopo, pra não precisar clicar item por item (pedido do usuário, 02/09).
  const termoBusca = filtroPermissao.trim().toLowerCase();
  const permissoesPorModuloFiltradas = useMemo(() => {
    if (!termoBusca) return permissoesPorModulo;
    return permissoesPorModulo
      .map(([modulo, lista]): [string, Permissao[]] => [
        modulo,
        lista.filter(
          (p) =>
            modulo.toLowerCase().includes(termoBusca) ||
            p.acao.toLowerCase().includes(termoBusca) ||
            p.codigo.toLowerCase().includes(termoBusca),
        ),
      ])
      .filter(([, lista]) => lista.length > 0);
  }, [permissoesPorModulo, termoBusca]);

  function alternarModuloAberto(modulo: string) {
    setModulosAbertos((atual) => {
      const proximo = new Set(atual);
      if (proximo.has(modulo)) proximo.delete(modulo);
      else proximo.add(modulo);
      return proximo;
    });
  }

  function alternarColunaModulo(lista: Permissao[], escopo: number, marcarTudo: boolean) {
    setMarcados((atual) => {
      const proximo = new Set(atual);
      for (const p of lista) {
        const k = chave(p.id, escopo);
        if (marcarTudo) proximo.add(k);
        else proximo.delete(k);
      }
      return proximo;
    });
  }

  function nomeTrabalhador(id?: string | null) {
    if (!id) return '—';
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  function selecionarUsuario(usuario: Usuario) {
    setUsuarioSelecionadoId(usuario.id);
    setNomeEdicao(usuario.nome);
    setStatusEdicao(usuario.status);
    setPerfilParaAtribuir('');
    setObraParaAtribuir('');
  }

  async function criarUsuario() {
    try {
      setCarregando(true);
      setErro(null);
      await api.usuarios.criar({ ...novoUsuario, trabalhadorId: novoUsuario.trabalhadorId || null });
      setNovoUsuario(usuarioVazio);
      await carregar();
      sucessoToast('Usuário criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar usuário.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluirUsuario(id: string) {
    if (!(await confirmar('Excluir este usuário? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.usuarios.excluir(id);
      if (usuarioSelecionadoId === id) setUsuarioSelecionadoId(null);
      await carregar();
      sucessoToast('Usuário excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir usuário.');
    }
  }

  async function salvarEdicaoUsuario() {
    if (!usuarioSelecionado) return;
    try {
      setErro(null);
      await api.usuarios.atualizar(usuarioSelecionado.id, {
        id: usuarioSelecionado.id,
        nome: nomeEdicao,
        status: statusEdicao,
        trabalhadorId: usuarioSelecionado.trabalhadorId,
      });
      await carregar();
      sucessoToast('Usuário atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar usuário.');
    }
  }

  async function atribuirPerfil() {
    if (!usuarioSelecionado || !perfilParaAtribuir) return;
    try {
      setErro(null);
      await api.usuarios.atribuirPerfilObra(usuarioSelecionado.id, perfilParaAtribuir, obraParaAtribuir || null);
      setPerfilParaAtribuir('');
      setObraParaAtribuir('');
      await carregar();
      sucessoToast('Perfil atribuído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atribuir perfil.');
    }
  }

  async function removerPerfilObra(id: string) {
    if (!(await confirmar('Remover este perfil do usuário? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.usuarios.removerPerfilObra(id);
      await carregar();
      sucessoToast('Perfil removido com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover perfil.');
    }
  }

  async function selecionarPerfil(perfil: PerfilAcesso) {
    setPerfilSelecionadoId(perfil.id);
    setErro(null);
    setModulosAbertos(new Set());
    setFiltroPermissao('');
    try {
      const atuais = await api.perfisAcesso.listarPermissoes(perfil.id);
      setMarcados(new Set(atuais.filter((a) => a.permitido).map((a) => chave(a.permissaoId, a.escopo))));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar permissões do perfil.');
    }
  }

  function alternarPermissao(permissaoId: string, escopo: number) {
    const k = chave(permissaoId, escopo);
    setMarcados((atual) => {
      const proximo = new Set(atual);
      if (proximo.has(k)) proximo.delete(k);
      else proximo.add(k);
      return proximo;
    });
  }

  async function criarPerfil() {
    try {
      setCarregando(true);
      setErro(null);
      await api.perfisAcesso.criar(novoPerfil);
      setNovoPerfil(perfilVazio);
      await carregar();
      sucessoToast('Perfil criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar perfil.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluirPerfil(id: string) {
    if (!(await confirmar('Excluir este perfil de acesso? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.perfisAcesso.excluir(id);
      if (perfilSelecionadoId === id) setPerfilSelecionadoId(null);
      await carregar();
      sucessoToast('Perfil excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir perfil.');
    }
  }

  async function salvarPermissoes() {
    if (!perfilSelecionado) return;
    try {
      setErro(null);
      const itens: ItemPermissaoPerfil[] = Array.from(marcados).map((k) => {
        const [permissaoId, escopoStr] = k.split('|');
        return { permissaoId, escopo: Number(escopoStr), permitido: true };
      });
      await api.perfisAcesso.definirPermissoes(perfilSelecionado.id, itens);
      await carregar();
      sucessoToast('Permissões salvas com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar permissões.');
    }
  }

  const colunasUsuarios: Coluna<Usuario>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'email', rotulo: 'E-mail' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (u) => <StatusChip tom={tomPorStatusUsuario[u.status] ?? 'neutro'}>{statusUsuarioLabel[u.status]}</StatusChip>,
    },
    {
      chave: 'acessoTeams',
      rotulo: 'Acesso Teams',
      render: (u) => (
        <StatusChip tom={u.azureAdObjectId ? 'ok' : 'atencao'}>
          {u.azureAdObjectId ? 'Vinculado' : 'Aguardando 1º login'}
        </StatusChip>
      ),
    },
    { chave: 'funcionario', rotulo: 'Funcionário', render: (u) => nomeTrabalhador(u.trabalhadorId) },
    { chave: 'perfis', rotulo: 'Perfis', render: (u) => u.perfisPorObra.length },
  ];

  const colunasPerfisPorObra: Coluna<UsuarioPerfilObra>[] = [
    { chave: 'perfil', rotulo: 'Perfil', render: (v) => v.perfilAcessoNome },
    { chave: 'obra', rotulo: 'Obra', render: (v) => v.obraNome ?? 'Todas as obras (global)' },
  ];

  const colunasPerfis: Coluna<PerfilAcesso>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    {
      chave: 'origem',
      rotulo: 'Origem',
      render: (p) => <StatusChip tom={p.ehSistema ? 'info' : 'neutro'}>{p.ehSistema ? 'Sistema' : 'Personalizado'}</StatusChip>,
    },
    { chave: 'permissoes', rotulo: 'Permissões', render: (p) => p.quantidadePermissoes },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader titulo="Controle de acesso" />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(380px, 1fr))',
          gap: 16,
          alignItems: 'start',
        }}
      >
        <Card titulo="Usuários cadastrados">
          <FormSection titulo="Novo Usuário" primeira>
            <FormGrid>
              <Campo span={4}>
                <Field label="Nome">
                  <Input value={novoUsuario.nome} onChange={(_, d) => setNovoUsuario({ ...novoUsuario, nome: d.value })} />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="E-mail">
                  <Input
                    type="email"
                    value={novoUsuario.email}
                    onChange={(_, d) => setNovoUsuario({ ...novoUsuario, email: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Funcionário vinculado (opcional)">
                  <Select
                    value={novoUsuario.trabalhadorId ?? ''}
                    onChange={(_, d) => setNovoUsuario({ ...novoUsuario, trabalhadorId: d.value })}
                  >
                    <option value="">Nenhum</option>
                    {trabalhadores.map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button appearance="primary" icon={<Add24Regular />} onClick={criarUsuario} disabled={carregando}>
                Adicionar usuário
              </Button>
            </FormRodape>
          </FormSection>

          <DataTable
            aria-label="Usuários cadastrados"
            colunas={colunasUsuarios}
            linhas={usuarios}
            chaveLinha={(u) => u.id}
            carregando={carregandoLista}
            vazio={{ titulo: 'Nenhum usuário cadastrado ainda.' }}
            aoClicarLinha={selecionarUsuario}
            acoesLinha={(usuario) => (
              <Button
                appearance="subtle"
                icon={<Delete24Regular />}
                onClick={() => excluirUsuario(usuario.id)}
                aria-label="Excluir"
              />
            )}
          />
        </Card>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {usuarioSelecionado ? (
            <Card titulo={`Acessos de ${usuarioSelecionado.nome}`}>
              <FormSection titulo="Editar Usuário" primeira>
                <FormGrid>
                  <Campo span={6}>
                    <Field label="Nome">
                      <Input value={nomeEdicao} onChange={(_, d) => setNomeEdicao(d.value)} />
                    </Field>
                  </Campo>
                  <Campo span={3}>
                    <Field label="Status">
                      <Select value={statusEdicao} onChange={(_, d) => setStatusEdicao(Number(d.value))}>
                        {Object.entries(statusUsuarioLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button appearance="primary" icon={<Save24Regular />} onClick={salvarEdicaoUsuario}>
                    Salvar
                  </Button>
                </FormRodape>
              </FormSection>

              <Text weight="semibold" style={{ display: 'block', margin: '16px 0 8px' }}>
                Perfis por obra
              </Text>
              <DataTable
                aria-label="Perfis por obra do usuário"
                colunas={colunasPerfisPorObra}
                linhas={usuarioSelecionado.perfisPorObra}
                chaveLinha={(v) => v.id}
                vazio={{ titulo: 'Nenhum perfil atribuído ainda.' }}
                acoesLinha={(vinculo) => (
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => removerPerfilObra(vinculo.id)}
                    aria-label="Remover"
                  />
                )}
              />

              <FormSection titulo="Atribuir Perfil">
                <FormGrid>
                  <Campo span={4}>
                    <Field label="Perfil">
                      <Select value={perfilParaAtribuir} onChange={(_, d) => setPerfilParaAtribuir(d.value)}>
                        <option value="">Selecione</option>
                        {perfis.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                  <Campo span={5}>
                    <Field label="Obra (vazio = escopo global/unidade)">
                      <Select value={obraParaAtribuir} onChange={(_, d) => setObraParaAtribuir(d.value)}>
                        <option value="">Todas as obras (global)</option>
                        {obras.map((o) => (
                          <option key={o.id} value={o.id}>
                            {o.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button
                    appearance="primary"
                    icon={<Add24Regular />}
                    onClick={atribuirPerfil}
                    disabled={!perfilParaAtribuir}
                  >
                    Atribuir perfil
                  </Button>
                </FormRodape>
              </FormSection>
            </Card>
          ) : (
            <Card>
              <Text>Selecione um usuário à esquerda para gerenciar seus acessos.</Text>
            </Card>
          )}

          <Card titulo="Perfis de acesso">
            <FormSection titulo="Novo Perfil" primeira>
              <FormGrid>
                <Campo span={4}>
                  <Field label="Nome do perfil personalizado">
                    <Input value={novoPerfil.nome} onChange={(_, d) => setNovoPerfil({ ...novoPerfil, nome: d.value })} />
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field label="Descrição">
                    <Textarea
                      value={novoPerfil.descricao ?? ''}
                      onChange={(_, d) => setNovoPerfil({ ...novoPerfil, descricao: d.value })}
                    />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button
                  appearance="primary"
                  icon={<Add24Regular />}
                  onClick={criarPerfil}
                  disabled={carregando || !novoPerfil.nome}
                >
                  Criar perfil personalizado
                </Button>
              </FormRodape>
            </FormSection>

            <DataTable
              aria-label="Perfis de acesso cadastrados"
              colunas={colunasPerfis}
              linhas={perfis}
              chaveLinha={(p) => p.id}
              carregando={carregandoLista}
              vazio={{ titulo: 'Nenhum perfil de acesso cadastrado ainda.' }}
              aoClicarLinha={selecionarPerfil}
              acoesLinha={(perfil) =>
                !perfil.ehSistema ? (
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => excluirPerfil(perfil.id)}
                    aria-label="Excluir"
                  />
                ) : null
              }
            />
          </Card>

          {perfilSelecionado && (
            <Card
              titulo={`Matriz de permissões — ${perfilSelecionado.nome}`}
              acoes={
                <Button appearance="primary" icon={<Save24Regular />} onClick={salvarPermissoes}>
                  Salvar permissões
                </Button>
              }
            >
              {/*
                A matriz módulo × escopo NÃO virou `DataTable expansivel` (avaliado conforme a task
                pedia). Motivo: em `DataTable`, a linha inteira fica clicável para abrir/fechar o
                `expansivel` (mesmo padrão de MatrizEpiTab.tsx) — mas aqui cada linha de módulo já
                carrega, nas próprias células, os checkboxes de bulk-toggle por escopo (marcar/
                desmarcar a coluna inteira daquele módulo). Um clique no checkbox teria que
                `stopPropagation` para não também expandir/recolher a linha sem querer — risco de
                interação que esta tela nunca teve (hoje só o botão de seta alterna). Além disso,
                vários módulos ficam abertos ao mesmo tempo (busca força todos os que casam a ficar
                abertos), e o "detalhe" de cada módulo (as permissões) usa os MESMOS controles
                interativos (checkbox por escopo) que a linha-pai — não é um detalhe auxiliar de UM
                item, é a mesma grade repetida em dois níveis. Mantida como tabela crua (`Table`/
                `TableRow`/`TableCell` agora importados de `@ui`, não mais de
                `@fluentui/react-components` — ver `src/ui/index.ts`, mesmo precedente de grades
                genuínas usado em `MatrizRiscoTab.tsx`), preservando exatamente a mesma lógica de
                estado (`modulosAbertos`, `marcados`) e handlers desta tela.
              */}
              <Field style={{ marginBottom: 12 }}>
                <Input
                  contentBefore={<Search24Regular />}
                  placeholder="Buscar por módulo, ação ou código (ex.: obra, ver, organizacional:ver)"
                  value={filtroPermissao}
                  onChange={(_, d) => setFiltroPermissao(d.value)}
                />
              </Field>

              <div style={{ overflowX: 'auto' }}>
                <Table noNativeElements>
                  <TableHeader>
                    <TableRow>
                      <TableHeaderCell style={{ width: '40%' }}>Módulo / permissão</TableHeaderCell>
                      {escopos.map((escopo) => (
                        <TableHeaderCell key={escopo} style={{ width: '15%' }}>
                          {escopoAcessoLabel[escopo]}
                        </TableHeaderCell>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {permissoesPorModuloFiltradas.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={escopos.length + 1}>
                          <Text>Nenhuma permissão encontrada para "{filtroPermissao}".</Text>
                        </TableCell>
                      </TableRow>
                    )}
                    {permissoesPorModuloFiltradas.map(([modulo, lista]) => {
                      const aberto = !!termoBusca || modulosAbertos.has(modulo);
                      // Contagem por AÇÃO concedida (pelo menos um escopo marcado), não por célula
                      // da tabela — "3/12" (3 ações x 4 escopos) confundia o usuário, que pensava
                      // em "12 opções" quando as únicas 3 ações do módulo já estavam concedidas.
                      const acoesConcedidasNoModulo = lista.filter((p) =>
                        escopos.some((e) => marcados.has(chave(p.id, e))),
                      ).length;
                      return (
                        <Fragment key={`modulo-${modulo}`}>
                          <TableRow style={{ backgroundColor: 'rgba(0,0,0,0.02)' }}>
                            <TableCell>
                              <Button
                                appearance="subtle"
                                size="small"
                                icon={aberto ? <ChevronDown20Regular /> : <ChevronRight20Regular />}
                                onClick={() => alternarModuloAberto(modulo)}
                                style={{ height: 'auto', whiteSpace: 'normal', textAlign: 'left' }}
                              >
                                <Text weight="semibold">{modulo}</Text>
                                <Text size={200} style={{ marginLeft: 8 }}>
                                  ({acoesConcedidasNoModulo}/{lista.length} ações concedidas)
                                </Text>
                              </Button>
                            </TableCell>
                            {escopos.map((escopo) => {
                              const todosMarcadosNoEscopo = lista.every((p) => marcados.has(chave(p.id, escopo)));
                              return (
                                <TableCell key={escopo}>
                                  <input
                                    type="checkbox"
                                    aria-label={`Marcar ou desmarcar todas as permissões do módulo ${modulo} no escopo ${escopoAcessoLabel[escopo]}`}
                                    checked={todosMarcadosNoEscopo}
                                    onChange={() => alternarColunaModulo(lista, escopo, !todosMarcadosNoEscopo)}
                                    title={`Marcar/desmarcar toda a coluna "${escopoAcessoLabel[escopo]}" deste módulo`}
                                  />
                                </TableCell>
                              );
                            })}
                          </TableRow>
                          {aberto &&
                            lista.map((permissao) => (
                              <TableRow key={permissao.id}>
                                <TableCell style={{ paddingLeft: 32 }}>
                                  {permissao.acao} <Text size={200}>({permissao.codigo})</Text>
                                </TableCell>
                                {escopos.map((escopo) => (
                                  <TableCell key={escopo}>
                                    <input
                                      type="checkbox"
                                      aria-label={`${permissao.acao} - ${escopoAcessoLabel[escopo]}`}
                                      checked={marcados.has(chave(permissao.id, escopo))}
                                      onChange={() => alternarPermissao(permissao.id, escopo)}
                                    />
                                  </TableCell>
                                ))}
                              </TableRow>
                            ))}
                        </Fragment>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
