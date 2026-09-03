import { Fragment, useEffect, useMemo, useState } from 'react';
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
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const usuarioVazio: NovoUsuario = { email: '', nome: '', trabalhadorId: '' };
const perfilVazio: NovoPerfilAcesso = { nome: '', descricao: '' };
const escopos = [EscopoAcesso.Global, EscopoAcesso.Unidade, EscopoAcesso.Obra, EscopoAcesso.Proprio];

const corStatus: Record<number, 'success' | 'subtle' | 'danger'> = {
  1: 'success',
  2: 'subtle',
  3: 'danger',
};

function chave(permissaoId: string, escopo: number) {
  return `${permissaoId}|${escopo}`;
}

export function ControleAcessoTab() {
  const estilos = usePageStyles();

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
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  return (
    <div>
      {dialogElement}
      {erro && (
        <Text className={estilos.erro} style={{ display: 'block', marginBottom: 16 }}>
          {erro}
        </Text>
      )}

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(380px, 1fr))',
          gap: 16,
          alignItems: 'start',
        }}
      >
        <div className={estilos.card}>
          <div className={estilos.toolbar}>
            <Text weight="semibold">Usuários cadastrados</Text>
          </div>

          <div className={estilos.form}>
            <Field label="Nome">
              <Input value={novoUsuario.nome} onChange={(_, d) => setNovoUsuario({ ...novoUsuario, nome: d.value })} />
            </Field>
            <Field label="E-mail">
              <Input
                type="email"
                value={novoUsuario.email}
                onChange={(_, d) => setNovoUsuario({ ...novoUsuario, email: d.value })}
              />
            </Field>
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
          </div>
          <div className={estilos.formActions}>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criarUsuario} disabled={carregando}>
              Adicionar usuário
            </Button>
          </div>

          {carregandoLista ? (
            <ListaCarregando />
          ) : usuarios.length === 0 ? (
            <EstadoVazio mensagem="Nenhum usuário cadastrado ainda." />
          ) : (
          <Table noNativeElements>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Nome</TableHeaderCell>
                <TableHeaderCell>E-mail</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
                <TableHeaderCell>Acesso Teams</TableHeaderCell>
                <TableHeaderCell>Funcionário</TableHeaderCell>
                <TableHeaderCell>Perfis</TableHeaderCell>
                <TableHeaderCell></TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {usuarios.map((usuario) => (
                <TableRow
                  key={usuario.id}
                  onClick={() => selecionarUsuario(usuario)}
                  style={{ cursor: 'pointer', fontWeight: usuario.id === usuarioSelecionadoId ? 600 : 400 }}
                >
                  <TableCell>{usuario.nome}</TableCell>
                  <TableCell>{usuario.email}</TableCell>
                  <TableCell>
                    <Badge color={corStatus[usuario.status]} appearance="tint">
                      {statusUsuarioLabel[usuario.status]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge color={usuario.azureAdObjectId ? 'success' : 'warning'} appearance="tint">
                      {usuario.azureAdObjectId ? 'Vinculado' : 'Aguardando 1º login'}
                    </Badge>
                  </TableCell>
                  <TableCell>{nomeTrabalhador(usuario.trabalhadorId)}</TableCell>
                  <TableCell>{usuario.perfisPorObra.length}</TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={(evento) => {
                        evento.stopPropagation();
                        excluirUsuario(usuario.id);
                      }}
                      aria-label="Excluir"
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          )}
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {usuarioSelecionado ? (
            <div className={estilos.card}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Acessos de {usuarioSelecionado.nome}</Text>
              </div>

              <div className={estilos.form}>
                <Field label="Nome">
                  <Input value={nomeEdicao} onChange={(_, d) => setNomeEdicao(d.value)} />
                </Field>
                <Field label="Status">
                  <Select value={statusEdicao} onChange={(_, d) => setStatusEdicao(Number(d.value))}>
                    {Object.entries(statusUsuarioLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div className={estilos.formActions}>
                <Button appearance="primary" icon={<Save24Regular />} onClick={salvarEdicaoUsuario}>
                  Salvar
                </Button>
              </div>

              <Text weight="semibold" style={{ display: 'block', margin: '16px 0 8px' }}>
                Perfis por obra
              </Text>
              <Table noNativeElements>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>Perfil</TableHeaderCell>
                    <TableHeaderCell>Obra</TableHeaderCell>
                    <TableHeaderCell></TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {usuarioSelecionado.perfisPorObra.map((vinculo) => (
                    <TableRow key={vinculo.id}>
                      <TableCell>{vinculo.perfilAcessoNome}</TableCell>
                      <TableCell>{vinculo.obraNome ?? 'Todas as obras (global)'}</TableCell>
                      <TableCell>
                        <Button
                          appearance="subtle"
                          icon={<Delete24Regular />}
                          onClick={() => removerPerfilObra(vinculo.id)}
                          aria-label="Remover"
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              <div className={estilos.form} style={{ marginTop: 12 }}>
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
              </div>
              <div className={estilos.formActions}>
                <Button
                  appearance="primary"
                  icon={<Add24Regular />}
                  onClick={atribuirPerfil}
                  disabled={!perfilParaAtribuir}
                >
                  Atribuir perfil
                </Button>
              </div>
            </div>
          ) : (
            <div className={estilos.card}>
              <Text>Selecione um usuário à esquerda para gerenciar seus acessos.</Text>
            </div>
          )}

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Perfis de acesso</Text>
            </div>

            <div className={estilos.form}>
              <Field label="Nome do perfil personalizado">
                <Input value={novoPerfil.nome} onChange={(_, d) => setNovoPerfil({ ...novoPerfil, nome: d.value })} />
              </Field>
              <Field label="Descrição">
                <Textarea
                  value={novoPerfil.descricao ?? ''}
                  onChange={(_, d) => setNovoPerfil({ ...novoPerfil, descricao: d.value })}
                />
              </Field>
            </div>
            <div className={estilos.formActions}>
              <Button
                appearance="primary"
                icon={<Add24Regular />}
                onClick={criarPerfil}
                disabled={carregando || !novoPerfil.nome}
              >
                Criar perfil personalizado
              </Button>
            </div>

            {carregandoLista ? (
              <ListaCarregando />
            ) : perfis.length === 0 ? (
              <EstadoVazio mensagem="Nenhum perfil de acesso cadastrado ainda." />
            ) : (
            <Table noNativeElements>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Nome</TableHeaderCell>
                  <TableHeaderCell>Origem</TableHeaderCell>
                  <TableHeaderCell>Permissões</TableHeaderCell>
                  <TableHeaderCell></TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {perfis.map((perfil) => (
                  <TableRow
                    key={perfil.id}
                    onClick={() => selecionarPerfil(perfil)}
                    style={{ cursor: 'pointer', fontWeight: perfil.id === perfilSelecionadoId ? 600 : 400 }}
                  >
                    <TableCell>{perfil.nome}</TableCell>
                    <TableCell>
                      <Badge color={perfil.ehSistema ? 'informative' : 'subtle'} appearance="tint">
                        {perfil.ehSistema ? 'Sistema' : 'Personalizado'}
                      </Badge>
                    </TableCell>
                    <TableCell>{perfil.quantidadePermissoes}</TableCell>
                    <TableCell>
                      {!perfil.ehSistema && (
                        <Button
                          appearance="subtle"
                          icon={<Delete24Regular />}
                          onClick={(evento) => {
                            evento.stopPropagation();
                            excluirPerfil(perfil.id);
                          }}
                          aria-label="Excluir"
                        />
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            )}
          </div>

          {perfilSelecionado && (
            <div className={estilos.card}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Matriz de permissões — {perfilSelecionado.nome}</Text>
                <Button appearance="primary" icon={<Save24Regular />} onClick={salvarPermissoes}>
                  Salvar permissões
                </Button>
              </div>

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
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
