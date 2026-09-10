import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Campo,
  DataTable,
  FeedbackInline,
  FormGrid,
  Field,
  Input,
  PainelLateral,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
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
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Tela de administração do Motor Central de Alertas (requisito do usuário, 2026-08-25): antes só
// dava para ajustar RegraAlerta.DiasAntecedencia/Severidade direto no banco. Um card por módulo
// (TipoModuloAlerta), com as regras atuais em linhas editáveis inline (Dias + Severidade + Salvar/
// Excluir) e um botão "Adicionar regra" por card, que abre um PainelLateral compartilhado — decisão
// de UI própria (não especificada no requisito): evita repetir um formulário por card e segue o
// mesmo padrão já usado em toda a Onda 2 para formulário de criação (ex.: AtividadesTab.tsx).
const modulosOrdenados = Object.entries(TipoModuloAlerta)
  .map(([, valor]) => valor)
  .sort((a, b) => a - b);

// Mapeamento 1:1 pelo nome semântico (Guia de conversão item 5): Info→info, Atenção→atencao,
// Crítico→alerta.
const tomPorSeveridade: Record<number, Tom> = {
  [SeveridadeAlerta.Info]: 'info',
  [SeveridadeAlerta.Atencao]: 'atencao',
  [SeveridadeAlerta.Critico]: 'alerta',
};

function rascunhoInicial(modulo: number): NovaRegraAlerta {
  return { modulo, diasAntecedencia: 30, severidade: SeveridadeAlerta.Info, responsavelUsuarioId: '' };
}

export function AlertasConfiguracaoTab() {
  const [regras, setRegras] = useState<RegraAlerta[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [moduloPainel, setModuloPainel] = useState<number | null>(null);
  const [rascunho, setRascunho] = useState<NovaRegraAlerta | null>(null);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

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
    } finally {
      setCarregandoLista(false);
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
      sucessoToast('Regra de alerta atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar regra de alerta.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta regra de alerta? Essa ação não pode ser desfeita.'))) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.regrasAlerta.excluir(id);
      await carregar();
      sucessoToast('Regra de alerta excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir regra de alerta.');
    } finally {
      setCarregando(false);
    }
  }

  function abrirPainel(modulo: number) {
    setModuloPainel(modulo);
    setRascunho(rascunhoInicial(modulo));
    setErroPainel(null);
    setPainelAberto(true);
  }

  function fecharPainel() {
    setPainelAberto(false);
    setModuloPainel(null);
    setRascunho(null);
    setErroPainel(null);
  }

  function atualizarRascunho(campo: 'diasAntecedencia' | 'severidade' | 'responsavelUsuarioId', valor: number | string) {
    setRascunho((atual) => (atual ? { ...atual, [campo]: valor } : atual));
  }

  async function adicionar() {
    if (!rascunho) return;
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.regrasAlerta.criar({
        ...rascunho,
        responsavelUsuarioId: rascunho.responsavelUsuarioId || null,
      });
      await carregar();
      sucessoToast('Regra de alerta adicionada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao adicionar regra de alerta.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <p>
        Defina, por módulo, os limiares de antecedência (em dias) que disparam cada nível de
        severidade. O motor de alertas escolhe sempre a regra mais urgente cujo limiar cobre os dias
        restantes até o vencimento; um item já vencido gera severidade Crítico automaticamente,
        mesmo sem regra cadastrada.
      </p>

      {modulosOrdenados.map((modulo) => {
        const regrasDoModulo = regras
          .filter((r) => r.modulo === modulo)
          .sort((a, b) => b.diasAntecedencia - a.diasAntecedencia);

        const colunas: Coluna<RegraAlerta>[] = [
          {
            chave: 'diasAntecedencia',
            rotulo: 'Dias de antecedência',
            render: (regra) => (
              <Input
                aria-label={`Dias de antecedência para ${moduloAlertaLabel[modulo] ?? String(modulo)}`}
                type="number"
                min={0}
                value={String(regra.diasAntecedencia)}
                onChange={(_, d) => atualizarCampoLocal(regra.id, 'diasAntecedencia', Number(d.value))}
                style={{ maxWidth: 120 }}
              />
            ),
          },
          {
            chave: 'severidade',
            rotulo: 'Severidade',
            render: (regra) => (
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Select
                  aria-label={`Severidade para ${moduloAlertaLabel[modulo] ?? String(modulo)}`}
                  value={String(regra.severidade)}
                  onChange={(_, d) => atualizarCampoLocal(regra.id, 'severidade', Number(d.value))}
                >
                  {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
                <StatusChip tom={tomPorSeveridade[regra.severidade] ?? 'neutro'}>
                  {severidadeAlertaLabel[regra.severidade]}
                </StatusChip>
              </div>
            ),
          },
          {
            chave: 'responsavel',
            rotulo: 'Responsável (notificação no Teams)',
            render: (regra) => (
              <Select
                aria-label={`Responsável por notificação de ${moduloAlertaLabel[modulo] ?? String(modulo)}`}
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
            ),
          },
        ];

        return (
          <div style={{ marginBottom: 16 }} key={modulo}>
            <Card
              titulo={moduloAlertaLabel[modulo] ?? String(modulo)}
              acoes={
                <Button
                  appearance="subtle"
                  icon={<AddCircle24Regular />}
                  onClick={() => abrirPainel(modulo)}
                  disabled={carregando}
                >
                  Adicionar regra
                </Button>
              }
            >
              <DataTable
                aria-label={`Regras de alerta — ${moduloAlertaLabel[modulo] ?? modulo}`}
                colunas={colunas}
                linhas={regrasDoModulo}
                chaveLinha={(r) => r.id}
                carregando={carregandoLista}
                vazio={{
                  titulo: 'Nenhuma regra cadastrada para este módulo',
                  acao: { rotulo: 'Adicionar regra', aoClicar: () => abrirPainel(modulo) },
                }}
                acoesLinha={(regra) => (
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
                )}
              />
            </Card>
          </div>
        );
      })}

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo={`Nova regra — ${moduloPainel !== null ? moduloAlertaLabel[moduloPainel] ?? moduloPainel : ''}`}
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={adicionar} disabled={carregando || !rascunho}>
              Adicionar regra
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}
        {rascunho && (
          <FormGrid>
            <Campo span={4}>
              <Field label="Dias de antecedência">
                <Input
                  type="number"
                  min={0}
                  value={String(rascunho.diasAntecedencia)}
                  onChange={(_, d) => atualizarRascunho('diasAntecedencia', Number(d.value))}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Severidade">
                <Select
                  value={String(rascunho.severidade)}
                  onChange={(_, d) => atualizarRascunho('severidade', Number(d.value))}
                >
                  {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Responsável">
                <Select
                  value={rascunho.responsavelUsuarioId ?? ''}
                  onChange={(_, d) => atualizarRascunho('responsavelUsuarioId', d.value)}
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
        )}
      </PainelLateral>
    </div>
  );
}
