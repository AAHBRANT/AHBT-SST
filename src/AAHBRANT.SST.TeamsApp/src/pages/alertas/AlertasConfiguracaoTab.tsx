import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
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
import { AddCircle24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  moduloAlertaLabel,
  severidadeAlertaLabel,
  SeveridadeAlerta,
  TipoModuloAlerta,
  type NovaRegraAlerta,
  type RegraAlerta,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Tela de administração do Motor Central de Alertas (requisito do usuário, 2026-08-25): antes só
// dava para ajustar RegraAlerta.DiasAntecedencia/Severidade direto no banco. Um card por módulo
// (TipoModuloAlerta), com as regras atuais em linhas editáveis inline (Dias + Severidade + Salvar/
// Excluir) e uma linha de inclusão no rodapé de cada card — decisão de UI própria (não especificada
// no requisito): evita modal e deixa visível de uma vez só o "degrau" de urgência de cada módulo.
const modulosOrdenados = Object.entries(TipoModuloAlerta)
  .map(([, valor]) => valor)
  .sort((a, b) => a - b);

function severidadeCor(severidade: number): 'informative' | 'warning' | 'danger' {
  if (severidade === SeveridadeAlerta.Critico) return 'danger';
  if (severidade === SeveridadeAlerta.Atencao) return 'warning';
  return 'informative';
}

function rascunhoInicial(modulo: number): NovaRegraAlerta {
  return { modulo, diasAntecedencia: 30, severidade: SeveridadeAlerta.Info, responsavelUsuarioId: '' };
}

export function AlertasConfiguracaoTab() {
  const estilos = usePageStyles();
  const [regras, setRegras] = useState<RegraAlerta[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [rascunhos, setRascunhos] = useState<Record<number, NovaRegraAlerta>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [regrasCarregadas, usuariosCarregados] = await Promise.all([
        api.regrasAlerta.listar(),
        api.usuarios.listar(),
      ]);
      setRegras(regrasCarregadas);
      setUsuarios(usuariosCarregados);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar regras de alerta.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function atualizarCampoLocal(
    id: string,
    campo: 'diasAntecedencia' | 'severidade' | 'responsavelUsuarioId',
    valor: number | string
  ) {
    setRegras((atual) => atual.map((r) => (r.id === id ? { ...r, [campo]: valor } : r)));
  }

  async function salvar(regra: RegraAlerta) {
    try {
      setCarregando(true);
      setErro(null);
      await api.regrasAlerta.atualizar(regra.id, {
        ...regra,
        responsavelUsuarioId: regra.responsavelUsuarioId || null,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar regra de alerta.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      setCarregando(true);
      setErro(null);
      await api.regrasAlerta.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir regra de alerta.');
    } finally {
      setCarregando(false);
    }
  }

  async function adicionar(modulo: number) {
    const rascunho = rascunhos[modulo] ?? rascunhoInicial(modulo);
    try {
      setCarregando(true);
      setErro(null);
      await api.regrasAlerta.criar({
        ...rascunho,
        responsavelUsuarioId: rascunho.responsavelUsuarioId || null,
      });
      setRascunhos((atual) => ({ ...atual, [modulo]: rascunhoInicial(modulo) }));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao adicionar regra de alerta.');
    } finally {
      setCarregando(false);
    }
  }

  function rascunhoDoModulo(modulo: number): NovaRegraAlerta {
    return rascunhos[modulo] ?? rascunhoInicial(modulo);
  }

  function atualizarRascunho(
    modulo: number,
    campo: 'diasAntecedencia' | 'severidade' | 'responsavelUsuarioId',
    valor: number | string
  ) {
    setRascunhos((atual) => ({
      ...atual,
      [modulo]: { ...rascunhoDoModulo(modulo), [campo]: valor },
    }));
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Text as="p">
        Defina, por módulo, os limiares de antecedência (em dias) que disparam cada nível de
        severidade. O motor de alertas escolhe sempre a regra mais urgente cujo limiar cobre os dias
        restantes até o vencimento; um item já vencido gera severidade Crítico automaticamente,
        mesmo sem regra cadastrada.
      </Text>

      {modulosOrdenados.map((modulo) => {
        const regrasDoModulo = regras
          .filter((r) => r.modulo === modulo)
          .sort((a, b) => b.diasAntecedencia - a.diasAntecedencia);
        const rascunho = rascunhoDoModulo(modulo);

        return (
          <div className={estilos.card} style={{ marginBottom: 16 }} key={modulo}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">{moduloAlertaLabel[modulo] ?? modulo}</Text>
            </div>

            <Table>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Dias de antecedência</TableHeaderCell>
                  <TableHeaderCell>Severidade</TableHeaderCell>
                  <TableHeaderCell>Responsável (notificação no Teams)</TableHeaderCell>
                  <TableHeaderCell></TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {regrasDoModulo.map((regra) => (
                  <TableRow key={regra.id}>
                    <TableCell>
                      <Input
                        type="number"
                        min={0}
                        value={String(regra.diasAntecedencia)}
                        onChange={(_, d) =>
                          atualizarCampoLocal(regra.id, 'diasAntecedencia', Number(d.value))
                        }
                        style={{ maxWidth: 120 }}
                      />
                    </TableCell>
                    <TableCell>
                      <Select
                        value={String(regra.severidade)}
                        onChange={(_, d) => atualizarCampoLocal(regra.id, 'severidade', Number(d.value))}
                      >
                        {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </Select>
                      <Badge appearance="tint" color={severidadeCor(regra.severidade)} style={{ marginLeft: 8 }}>
                        {severidadeAlertaLabel[regra.severidade]}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Select
                        value={regra.responsavelUsuarioId ?? ''}
                        onChange={(_, d) => atualizarCampoLocal(regra.id, 'responsavelUsuarioId', d.value)}
                        style={{ maxWidth: 220 }}
                      >
                        <option value="">Nenhum</option>
                        {usuarios.map((usuario) => (
                          <option key={usuario.id} value={usuario.id}>
                            {usuario.nome}
                          </option>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell>
                      <div style={{ display: 'flex', gap: 4 }}>
                        <Button
                          appearance="subtle"
                          icon={<Save24Regular />}
                          title="Salvar"
                          disabled={carregando}
                          onClick={() => salvar(regra)}
                        />
                        <Button
                          appearance="subtle"
                          icon={<Delete24Regular />}
                          title="Excluir"
                          disabled={carregando}
                          onClick={() => excluir(regra.id)}
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                <TableRow>
                  <TableCell>
                    <Input
                      type="number"
                      min={0}
                      value={String(rascunho.diasAntecedencia)}
                      onChange={(_, d) => atualizarRascunho(modulo, 'diasAntecedencia', Number(d.value))}
                      style={{ maxWidth: 120 }}
                    />
                  </TableCell>
                  <TableCell>
                    <Select
                      value={String(rascunho.severidade)}
                      onChange={(_, d) => atualizarRascunho(modulo, 'severidade', Number(d.value))}
                    >
                      {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Select
                      value={rascunho.responsavelUsuarioId ?? ''}
                      onChange={(_, d) => atualizarRascunho(modulo, 'responsavelUsuarioId', d.value)}
                      style={{ maxWidth: 220 }}
                    >
                      <option value="">Nenhum</option>
                      {usuarios.map((usuario) => (
                        <option key={usuario.id} value={usuario.id}>
                          {usuario.nome}
                        </option>
                      ))}
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<AddCircle24Regular />}
                      title="Adicionar regra"
                      disabled={carregando}
                      onClick={() => adicionar(modulo)}
                    >
                      Adicionar
                    </Button>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        );
      })}
    </div>
  );
}
