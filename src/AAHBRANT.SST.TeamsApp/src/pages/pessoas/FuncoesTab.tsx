import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Merge24Regular } from '@fluentui/react-icons';
import { api, type Funcao, type GrupoFuncaoDuplicada, type NovaFuncao } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const funcaoVazia: NovaFuncao = { nome: '', cboCodigo: '', descricao: '' };

// A matriz de EPI por função fica no módulo EPI (ver MatrizEpiTab.tsx em pages/epi) — aqui é só o
// cadastro (CRUD) da função em si, usado também por Trabalhadores/Equipes. Migração do formulário
// inline (spec 2026-09-11): o PainelLateral (drawer) saiu — mesmo padrão de
// AtividadesTab.tsx/InspecoesTab.tsx. Agora é um PainelCriacaoInline, que cresce acima da lista até a
// altura do próprio formulário. O aviso sobre a matriz de EPI, que era o `subtitulo` do PainelLateral,
// virou o `info` do FormRodape (mesmo uso: texto de ajuda à esquerda das ações). Erro do formulário
// fica em estado próprio, separado do erro de carga da lista.
export function FuncoesTab() {
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [novaFuncao, setNovaFuncao] = useState<NovaFuncao>(funcaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  const [duplicadas, setDuplicadas] = useState<GrupoFuncaoDuplicada[]>([]);
  const [mesclandoId, setMesclandoId] = useState<string | null>(null);
  const [erroDuplicadas, setErroDuplicadas] = useState<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const listaFuncoes = await api.funcoes.listar();
      setFuncoes(listaFuncoes);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
    } finally {
      setCarregandoLista(false);
    }
  }

  // Pedido do usuário (22/09): detecta funções duplicadas (mesmo nome — uma com CBO vindo do G-RH,
  // outra sem, criada manualmente antes da integração) e oferece mesclar com um clique, sem tocar em
  // função sem CBO que não seja duplicata de nenhuma outra (essas são reais, só nunca sincronizadas).
  async function carregarDuplicadas() {
    try {
      setErroDuplicadas(null);
      setDuplicadas(await api.funcoes.listarDuplicadas());
    } catch (e) {
      setErroDuplicadas(e instanceof Error ? e.message : 'Falha ao verificar funções duplicadas.');
    }
  }

  useEffect(() => {
    carregar();
    carregarDuplicadas();
  }, []);

  async function mesclar(funcaoManterId: string, funcaoRemoverId: string, nome: string) {
    if (
      !(await confirmar(
        `Mesclar a função duplicada "${nome}"? Os trabalhadores e matrizes da duplicata sem CBO passam a usar a função com CBO, e a duplicata vazia é excluída. Essa ação não pode ser desfeita.`,
      ))
    )
      return;
    try {
      setMesclandoId(funcaoRemoverId);
      setErroDuplicadas(null);
      await api.funcoes.mesclarDuplicada(funcaoManterId, funcaoRemoverId);
      await Promise.all([carregar(), carregarDuplicadas()]);
      sucessoToast('Função duplicada mesclada com sucesso.');
    } catch (e) {
      setErroDuplicadas(e instanceof Error ? e.message : 'Falha ao mesclar função duplicada.');
    } finally {
      setMesclandoId(null);
    }
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.funcoes.criar(novaFuncao);
      setNovaFuncao(funcaoVazia);
      await Promise.all([carregar(), carregarDuplicadas()]);
      sucessoToast('Função criada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar função.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta função? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.funcoes.excluir(id);
      await Promise.all([carregar(), carregarDuplicadas()]);
      sucessoToast('Função excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir função.');
    }
  }

  const colunas: Coluna<Funcao>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'cboCodigo', rotulo: 'CBO' },
    { chave: 'descricao', rotulo: 'Descrição' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Funções cadastradas"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-funcao"
          >
            {painelAberto ? 'Fechar' : 'Adicionar função'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      {erroDuplicadas && (
        <FeedbackInline tom="erro" aoFechar={() => setErroDuplicadas(null)}>
          {erroDuplicadas}
        </FeedbackInline>
      )}
      {duplicadas.length > 0 && (
        <Card titulo="Funções duplicadas encontradas">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {duplicadas.map((grupo) => (
              <div
                key={grupo.nome}
                style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}
              >
                <div>
                  <strong>{grupo.nome}</strong>
                  <div style={{ fontSize: 12 }}>
                    Manter: CBO {grupo.manter.cboCodigo} ({grupo.manter.quantidadeTrabalhadores} trabalhador
                    {grupo.manter.quantidadeTrabalhadores === 1 ? '' : 'es'}) — Mesclar:{' '}
                    {grupo.remover
                      .map((f) => `sem CBO (${f.quantidadeTrabalhadores} trabalhador${f.quantidadeTrabalhadores === 1 ? '' : 'es'})`)
                      .join(', ')}
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 8 }}>
                  {grupo.remover.map((f) => (
                    <Button
                      key={f.id}
                      appearance="primary"
                      icon={<Merge24Regular />}
                      disabled={mesclandoId === f.id}
                      onClick={() => mesclar(grupo.manter.id, f.id, grupo.nome)}
                    >
                      Mesclar
                    </Button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}
      <div id="painel-nova-funcao">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova função">
          <FormSection titulo="Dados da função" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Nome">
                  <Input value={novaFuncao.nome} onChange={(_, d) => setNovaFuncao({ ...novaFuncao, nome: d.value })} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Código CBO">
                  <Input
                    value={novaFuncao.cboCodigo ?? ''}
                    onChange={(_, d) => setNovaFuncao({ ...novaFuncao, cboCodigo: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Descrição">
                  <Input
                    value={novaFuncao.descricao ?? ''}
                    onChange={(_, d) => setNovaFuncao({ ...novaFuncao, descricao: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape info="A matriz de EPI de cada função é definida em EPI → Matriz de EPI por Função.">
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Adicionar função
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>
      <Card>
        <DataTable
          aria-label="Funções cadastradas"
          colunas={colunas}
          linhas={funcoes}
          chaveLinha={(f) => f.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma função cadastrada ainda',
            acao: { rotulo: 'Adicionar função', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(f) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(f.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
