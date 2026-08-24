import { Fragment, useEffect, useMemo, useState } from 'react';
import {
  Badge,
  Button,
  Field,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  escopoAcessoLabel,
  EscopoAcesso,
  type ItemPermissaoPerfil,
  type NovoPerfilAcesso,
  type PerfilAcesso,
  type Permissao,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const perfilVazio: NovoPerfilAcesso = { nome: '', descricao: '' };
const escopos = [EscopoAcesso.Global, EscopoAcesso.Unidade, EscopoAcesso.Obra, EscopoAcesso.Proprio];

function chave(permissaoId: string, escopo: number) {
  return `${permissaoId}|${escopo}`;
}

export function PerfisAcessoTab() {
  const estilos = usePageStyles();
  const [perfis, setPerfis] = useState<PerfilAcesso[]>([]);
  const [permissoes, setPermissoes] = useState<Permissao[]>([]);
  const [novoPerfil, setNovoPerfil] = useState<NovoPerfilAcesso>(perfilVazio);
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);
  const [marcados, setMarcados] = useState<Set<string>>(new Set());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [prf, perm] = await Promise.all([api.perfisAcesso.listar(), api.permissoes.listar()]);
      setPerfis(prf);
      setPermissoes(perm);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar perfis de acesso.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const selecionado = perfis.find((p) => p.id === selecionadoId) ?? null;

  const permissoesPorModulo = useMemo(() => {
    const grupos = new Map<string, Permissao[]>();
    for (const p of permissoes) {
      const lista = grupos.get(p.modulo) ?? [];
      lista.push(p);
      grupos.set(p.modulo, lista);
    }
    return Array.from(grupos.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [permissoes]);

  async function selecionar(perfil: PerfilAcesso) {
    setSelecionadoId(perfil.id);
    setErro(null);
    try {
      const atuais = await api.perfisAcesso.listarPermissoes(perfil.id);
      setMarcados(new Set(atuais.filter((a) => a.permitido).map((a) => chave(a.permissaoId, a.escopo))));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar permissões do perfil.');
    }
  }

  function alternar(permissaoId: string, escopo: number) {
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
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar perfil.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluirPerfil(id: string) {
    try {
      await api.perfisAcesso.excluir(id);
      if (selecionadoId === id) setSelecionadoId(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir perfil.');
    }
  }

  async function salvarPermissoes() {
    if (!selecionado) return;
    try {
      setErro(null);
      const itens: ItemPermissaoPerfil[] = Array.from(marcados).map((k) => {
        const [permissaoId, escopoStr] = k.split('|');
        return { permissaoId, escopo: Number(escopoStr), permitido: true };
      });
      await api.perfisAcesso.definirPermissoes(selecionado.id, itens);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar permissões.');
    }
  }

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Perfis de acesso</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

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
          <Button appearance="primary" icon={<Add24Regular />} onClick={criarPerfil} disabled={carregando || !novoPerfil.nome}>
            Criar perfil personalizado
          </Button>
        </div>

        <Table>
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
                onClick={() => selecionar(perfil)}
                style={{ cursor: 'pointer', fontWeight: perfil.id === selecionadoId ? 600 : 400 }}
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
      </div>

      {selecionado && (
        <div className={estilos.card}>
          <div className={estilos.toolbar}>
            <Text weight="semibold">Matriz de permissões — {selecionado.nome}</Text>
            <Button appearance="primary" icon={<Save24Regular />} onClick={salvarPermissoes}>
              Salvar permissões
            </Button>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Permissão</TableHeaderCell>
                  {escopos.map((escopo) => (
                    <TableHeaderCell key={escopo}>{escopoAcessoLabel[escopo]}</TableHeaderCell>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {permissoesPorModulo.map(([modulo, lista]) => (
                  <Fragment key={`modulo-${modulo}`}>
                    <TableRow>
                      <TableCell colSpan={escopos.length + 1}>
                        <Text weight="semibold">{modulo}</Text>
                      </TableCell>
                    </TableRow>
                    {lista.map((permissao) => (
                      <TableRow key={permissao.id}>
                        <TableCell>
                          {permissao.acao} <Text size={200}>({permissao.codigo})</Text>
                        </TableCell>
                        {escopos.map((escopo) => (
                          <TableCell key={escopo}>
                            <input
                              type="checkbox"
                              checked={marcados.has(chave(permissao.id, escopo))}
                              onChange={() => alternar(permissao.id, escopo)}
                            />
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                  </Fragment>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      )}
    </div>
  );
}
