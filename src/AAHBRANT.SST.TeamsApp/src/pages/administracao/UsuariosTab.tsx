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
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  statusUsuarioLabel,
  StatusUsuario,
  type NovoUsuario,
  type Obra,
  type PerfilAcesso,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const usuarioVazio: NovoUsuario = { azureAdObjectId: '', email: '', nome: '', trabalhadorId: '' };

const corStatus: Record<number, 'success' | 'subtle' | 'danger'> = {
  1: 'success',
  2: 'subtle',
  3: 'danger',
};

export function UsuariosTab() {
  const estilos = usePageStyles();
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [perfis, setPerfis] = useState<PerfilAcesso[]>([]);
  const [novoUsuario, setNovoUsuario] = useState<NovoUsuario>(usuarioVazio);
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);
  const [nomeEdicao, setNomeEdicao] = useState('');
  const [statusEdicao, setStatusEdicao] = useState<number>(StatusUsuario.Ativo);
  const [perfilParaAtribuir, setPerfilParaAtribuir] = useState('');
  const [obraParaAtribuir, setObraParaAtribuir] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [usu, trabs, obs, prf] = await Promise.all([
        api.usuarios.listar(),
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.perfisAcesso.listar(),
      ]);
      setUsuarios(usu);
      setTrabalhadores(trabs);
      setObras(obs);
      setPerfis(prf);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar usuários.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const selecionado = usuarios.find((u) => u.id === selecionadoId) ?? null;

  function selecionar(usuario: Usuario) {
    setSelecionadoId(usuario.id);
    setNomeEdicao(usuario.nome);
    setStatusEdicao(usuario.status);
    setPerfilParaAtribuir('');
    setObraParaAtribuir('');
  }

  function nomeTrabalhador(id?: string | null) {
    if (!id) return '—';
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.usuarios.criar({ ...novoUsuario, trabalhadorId: novoUsuario.trabalhadorId || null });
      setNovoUsuario(usuarioVazio);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar usuário.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.usuarios.excluir(id);
      if (selecionadoId === id) setSelecionadoId(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir usuário.');
    }
  }

  async function salvarEdicao() {
    if (!selecionado) return;
    try {
      setErro(null);
      await api.usuarios.atualizar(selecionado.id, {
        id: selecionado.id,
        nome: nomeEdicao,
        status: statusEdicao,
        trabalhadorId: selecionado.trabalhadorId,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar usuário.');
    }
  }

  async function atribuirPerfil() {
    if (!selecionado || !perfilParaAtribuir) return;
    try {
      setErro(null);
      await api.usuarios.atribuirPerfilObra(selecionado.id, perfilParaAtribuir, obraParaAtribuir || null);
      setPerfilParaAtribuir('');
      setObraParaAtribuir('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atribuir perfil.');
    }
  }

  async function removerPerfil(id: string) {
    try {
      setErro(null);
      await api.usuarios.removerPerfilObra(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover perfil.');
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Usuários cadastrados</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

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
          <Field label="Azure AD Object ID" hint="Claim 'oid' do token do Entra ID">
            <Input
              value={novoUsuario.azureAdObjectId}
              onChange={(_, d) => setNovoUsuario({ ...novoUsuario, azureAdObjectId: d.value })}
            />
          </Field>
          <Field label="Trabalhador vinculado (opcional)">
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
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Adicionar usuário
          </Button>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>E-mail</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Trabalhador</TableHeaderCell>
              <TableHeaderCell>Perfis</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {usuarios.map((usuario) => (
              <TableRow
                key={usuario.id}
                onClick={() => selecionar(usuario)}
                style={{ cursor: 'pointer', fontWeight: usuario.id === selecionadoId ? 600 : 400 }}
              >
                <TableCell>{usuario.nome}</TableCell>
                <TableCell>{usuario.email}</TableCell>
                <TableCell>
                  <Badge color={corStatus[usuario.status]} appearance="tint">
                    {statusUsuarioLabel[usuario.status]}
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
                      excluir(usuario.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {selecionado && (
        <div className={estilos.card}>
          <div className={estilos.toolbar}>
            <Text weight="semibold">Acessos de {selecionado.nome}</Text>
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
            <Button appearance="primary" icon={<Save24Regular />} onClick={salvarEdicao}>
              Salvar
            </Button>
          </div>

          <Text weight="semibold" style={{ display: 'block', margin: '16px 0 8px' }}>
            Perfis por obra
          </Text>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Perfil</TableHeaderCell>
                <TableHeaderCell>Obra</TableHeaderCell>
                <TableHeaderCell></TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {selecionado.perfisPorObra.map((vinculo) => (
                <TableRow key={vinculo.id}>
                  <TableCell>{vinculo.perfilAcessoNome}</TableCell>
                  <TableCell>{vinculo.obraNome ?? 'Todas as obras (global)'}</TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={() => removerPerfil(vinculo.id)}
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
            <Button appearance="primary" icon={<Add24Regular />} onClick={atribuirPerfil} disabled={!perfilParaAtribuir}>
              Atribuir perfil
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
