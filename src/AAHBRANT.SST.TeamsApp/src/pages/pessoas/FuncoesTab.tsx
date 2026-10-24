import { useEffect, useState } from 'react';
import {
  BotaoAcao,
  Button,
  Checkbox,
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
import { Add24Regular, ArrowUndo24Regular, Delete24Regular, Merge24Regular } from '@fluentui/react-icons';
import {
  api,
  type Funcao,
  type FuncaoInativaComTrabalhador,
  type FuncaoOrfa,
  type FuncaoSemTrabalhador,
  type GrupoFuncaoDuplicada,
  type NovaFuncao,
} from '../../lib/api';
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

  const [orfas, setOrfas] = useState<FuncaoOrfa[]>([]);
  const [orfasSelecionadasIds, setOrfasSelecionadasIds] = useState<Set<string>>(new Set());
  const [excluindoOrfas, setExcluindoOrfas] = useState(false);
  const [erroOrfas, setErroOrfas] = useState<string | null>(null);

  const [inativasComTrabalhador, setInativasComTrabalhador] = useState<FuncaoInativaComTrabalhador[]>([]);
  const [reativandoId, setReativandoId] = useState<string | null>(null);
  const [erroInativas, setErroInativas] = useState<string | null>(null);

  const [semTrabalhador, setSemTrabalhador] = useState<FuncaoSemTrabalhador[] | null>(null);
  const [carregandoSemTrabalhador, setCarregandoSemTrabalhador] = useState(false);
  const [erroSemTrabalhador, setErroSemTrabalhador] = useState<string | null>(null);
  const [excluindoSemTrabalhadorId, setExcluindoSemTrabalhadorId] = useState<string | null>(null);

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

  // Pedido do usuário (22/09): limpeza em massa de funções "lixo" — sem CBO, sem EPI/Treinamento/
  // Uniforme na matriz e sem trabalhador vinculado (as 5 proteções, ver DeteccaoFuncaoOrfa no
  // backend). Lista vem toda pré-selecionada — o backend só devolve o que já é seguro de excluir.
  async function carregarOrfas() {
    try {
      setErroOrfas(null);
      const lista = await api.funcoes.listarOrfas();
      setOrfas(lista);
      setOrfasSelecionadasIds(new Set(lista.map((f) => f.id)));
    } catch (e) {
      setErroOrfas(e instanceof Error ? e.message : 'Falha ao verificar funções sem uso.');
    }
  }

  // Correção de emergência (22/09): o botão "Excluir" de linha, logo abaixo, nunca teve as 5
  // proteções acima — sempre foi possível desativar uma função com trabalhador ativo vinculado a
  // ela (ver ListarFuncoesInativasComTrabalhadorQuery no backend). Isso detecta esse estado quebrado
  // e permite reverter (reativar) com um clique, um por vez.
  async function carregarInativasComTrabalhador() {
    try {
      setErroInativas(null);
      setInativasComTrabalhador(await api.funcoes.listarInativasComTrabalhador());
    } catch (e) {
      setErroInativas(e instanceof Error ? e.message : 'Falha ao verificar funções desativadas com trabalhador vinculado.');
    }
  }

  useEffect(() => {
    carregar();
    carregarDuplicadas();
    carregarOrfas();
    carregarInativasComTrabalhador();
  }, []);

  // Pedido do usuário (22/09): diferente do card "Funções sem uso" acima (só o subconjunto seguro
  // pra exclusão automática — sem CBO e sem nenhuma matriz), aqui aparece QUALQUER função sem
  // trabalhador vinculado, mesmo com CBO ou com matriz de EPI/treinamento/uniforme cadastrada — pra
  // revisão manual, com o usuário decidindo caso a caso. Carrega só sob demanda (botão), não junto
  // com o resto da tela, porque é uma lista potencialmente maior e não é um alerta de segurança.
  async function carregarSemTrabalhador() {
    if (semTrabalhador !== null) {
      setSemTrabalhador(null);
      return;
    }
    try {
      setCarregandoSemTrabalhador(true);
      setErroSemTrabalhador(null);
      setSemTrabalhador(await api.funcoes.listarSemTrabalhador());
    } catch (e) {
      setErroSemTrabalhador(e instanceof Error ? e.message : 'Falha ao verificar funções sem trabalhador vinculado.');
    } finally {
      setCarregandoSemTrabalhador(false);
    }
  }

  async function excluirSemTrabalhador(id: string, nome: string) {
    if (!(await confirmar(`Excluir a função "${nome}"? Essa ação não pode ser desfeita.`))) return;
    try {
      setExcluindoSemTrabalhadorId(id);
      setErroSemTrabalhador(null);
      await api.funcoes.excluir(id);
      setSemTrabalhador((atual) => atual?.filter((f) => f.id !== id) ?? null);
      await Promise.all([carregar(), carregarDuplicadas(), carregarOrfas(), carregarInativasComTrabalhador()]);
      sucessoToast('Função excluída com sucesso.');
    } catch (e) {
      setErroSemTrabalhador(e instanceof Error ? e.message : 'Falha ao excluir função.');
    } finally {
      setExcluindoSemTrabalhadorId(null);
    }
  }

  function alternarSelecaoOrfa(id: string) {
    setOrfasSelecionadasIds((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  async function excluirOrfasSelecionadas() {
    const quantidade = orfasSelecionadasIds.size;
    if (quantidade === 0) return;
    if (
      !(await confirmar(
        `Excluir ${quantidade} função${quantidade === 1 ? '' : 'ões'} sem uso (sem CBO, sem EPI/treinamento/uniforme na matriz e sem trabalhador vinculado)? Essa ação não pode ser desfeita.`,
      ))
    )
      return;
    try {
      setExcluindoOrfas(true);
      setErroOrfas(null);
      const { quantidadeExcluida } = await api.funcoes.excluirOrfas(Array.from(orfasSelecionadasIds));
      await Promise.all([carregar(), carregarDuplicadas(), carregarOrfas()]);
      sucessoToast(`${quantidadeExcluida} função${quantidadeExcluida === 1 ? '' : 'ões'} excluída${quantidadeExcluida === 1 ? '' : 's'} com sucesso.`);
    } catch (e) {
      setErroOrfas(e instanceof Error ? e.message : 'Falha ao excluir funções sem uso.');
    } finally {
      setExcluindoOrfas(false);
    }
  }

  async function reativar(id: string, nome: string) {
    if (
      !(await confirmar(
        `Reativar a função "${nome}"? Ela volta a aparecer normalmente para o(s) trabalhador(es) vinculado(s) a ela.`,
      ))
    )
      return;
    try {
      setReativandoId(id);
      setErroInativas(null);
      await api.funcoes.reativar(id);
      await Promise.all([carregar(), carregarOrfas(), carregarInativasComTrabalhador()]);
      sucessoToast('Função reativada com sucesso.');
    } catch (e) {
      setErroInativas(e instanceof Error ? e.message : 'Falha ao reativar função.');
    } finally {
      setReativandoId(null);
    }
  }

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
      await Promise.all([carregar(), carregarDuplicadas(), carregarOrfas()]);
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
      await Promise.all([carregar(), carregarDuplicadas(), carregarOrfas(), carregarInativasComTrabalhador()]);
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
          <div style={{ display: 'flex', gap: 8 }}>
            <Button onClick={carregarSemTrabalhador} disabled={carregandoSemTrabalhador}>
              {semTrabalhador !== null ? 'Ocultar' : 'Ver'} funções sem trabalhador vinculado
            </Button>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
              aria-expanded={painelAberto}
              aria-controls="painel-nova-funcao"
            >
              {painelAberto ? 'Fechar' : 'Adicionar função'}
            </Button>
          </div>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      {erroInativas && (
        <FeedbackInline tom="erro" aoFechar={() => setErroInativas(null)}>
          {erroInativas}
        </FeedbackInline>
      )}
      {inativasComTrabalhador.length > 0 && (
        <Card titulo="Funções desativadas com trabalhador vinculado">
          <div style={{ fontSize: 12, marginBottom: 8 }}>
            Estas funções foram excluídas (botão "Excluir" da lista abaixo) mas ainda têm trabalhador ativo
            vinculado a elas — por isso ficaram invisíveis em fichas de EPI, dashboard e outras telas.
            Reative para corrigir.
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {inativasComTrabalhador.map((f) => (
              <div
                key={f.id}
                style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}
              >
                <div>
                  <strong>{f.nome}</strong>
                  <div style={{ fontSize: 12 }}>
                    {f.quantidadeTrabalhadores} trabalhador{f.quantidadeTrabalhadores === 1 ? '' : 'es'} afetado
                    {f.quantidadeTrabalhadores === 1 ? '' : 's'}
                  </div>
                </div>
                <Button
                  appearance="primary"
                  icon={<ArrowUndo24Regular />}
                  disabled={reativandoId === f.id}
                  onClick={() => reativar(f.id, f.nome)}
                >
                  Reativar
                </Button>
              </div>
            ))}
          </div>
        </Card>
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
      {erroOrfas && (
        <FeedbackInline tom="erro" aoFechar={() => setErroOrfas(null)}>
          {erroOrfas}
        </FeedbackInline>
      )}
      {orfas.length > 0 && (
        <Card titulo="Funções sem uso encontradas">
          <div style={{ fontSize: 12, marginBottom: 8 }}>
            Sem CBO, sem EPI/treinamento/uniforme cadastrado na matriz e sem nenhum trabalhador vinculado.
            Desmarque as que não quiser excluir.
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginBottom: 12, maxHeight: 240, overflowY: 'auto' }}>
            {orfas.map((f) => (
              <Checkbox
                key={f.id}
                label={f.nome}
                checked={orfasSelecionadasIds.has(f.id)}
                onChange={() => alternarSelecaoOrfa(f.id)}
              />
            ))}
          </div>
          <Button
            appearance="primary"
            icon={<Delete24Regular />}
            disabled={excluindoOrfas || orfasSelecionadasIds.size === 0}
            onClick={excluirOrfasSelecionadas}
          >
            Excluir {orfasSelecionadasIds.size} selecionada{orfasSelecionadasIds.size === 1 ? '' : 's'}
          </Button>
        </Card>
      )}
      {erroSemTrabalhador && (
        <FeedbackInline tom="erro" aoFechar={() => setErroSemTrabalhador(null)}>
          {erroSemTrabalhador}
        </FeedbackInline>
      )}
      {semTrabalhador !== null && (
        <Card titulo={`Funções sem trabalhador vinculado (${semTrabalhador.length})`}>
          <div style={{ fontSize: 12, marginBottom: 8 }}>
            Revisão manual — inclui também funções com CBO ou com EPI/treinamento/uniforme cadastrado
            na matriz. Confira antes de excluir: as com matriz cadastrada podem estar reservadas para
            uso futuro.
          </div>
          {carregandoSemTrabalhador ? (
            <div style={{ fontSize: 12 }}>Carregando...</div>
          ) : semTrabalhador.length === 0 ? (
            <div style={{ fontSize: 12 }}>Nenhuma função sem trabalhador vinculado.</div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 320, overflowY: 'auto' }}>
              {semTrabalhador.map((f) => (
                <div
                  key={f.id}
                  style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}
                >
                  <div>
                    <strong>{f.nome}</strong>
                    <div style={{ fontSize: 12 }}>
                      {f.cboCodigo ? `CBO ${f.cboCodigo}` : 'Sem CBO'}
                      {f.temEpiNaMatriz ? ' · com EPI na matriz' : ''}
                      {f.temTreinamentoNaMatriz ? ' · com treinamento na matriz' : ''}
                      {f.temUniformeNaMatriz ? ' · com uniforme na matriz' : ''}
                    </div>
                  </div>
                  <BotaoAcao
                    tom="excluir"
                    icon={<Delete24Regular />}
                    disabled={excluindoSemTrabalhadorId === f.id}
                    onClick={() => excluirSemTrabalhador(f.id, f.nome)}
                    aria-label="Excluir"
                  >
                    Excluir
                  </BotaoAcao>
                </div>
              ))}
            </div>
          )}
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
            <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(f.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
