import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  PageHeader,
  DataTable,
  ChipCheckboxGroup,
  FeedbackInline,
  Text,
  useConfirmar,
  type Coluna,
} from '@ui';
import { api, type CatalogoEpi, type Funcao } from '../../lib/api';

// Matriz de EPI por função: define quais EPIs são obrigatórios para cada função (filtra o seletor de
// EPI em Entregas). Piloto 3 da camada ui/ (spec §5.1): mantém expandir-linha, que o usuário conhece,
// com salvar único no header em vez de um botão por linha. Como o salvar deixa de ficar dentro da
// linha, sair de uma função com edição pendente passa a pedir confirmação — antes o descarte era
// silencioso, mas o botão estava à vista logo abaixo dos chips.
export function MatrizEpiTab() {
  const { confirmar, dialogElement } = useConfirmar();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [episCatalogo, setEpisCatalogo] = useState<CatalogoEpi[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [expandidoId, setExpandidoId] = useState<string | null>(null);
  const [vinculosSelecionados, setVinculosSelecionados] = useState<string[]>([]);
  const [vinculosOriginais, setVinculosOriginais] = useState<string[]>([]);
  const [salvando, setSalvando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setCarregando(true);
      const [listaFuncoes, listaEpis] = await Promise.all([api.funcoes.listar(), api.catalogosEpi.listar()]);
      setFuncoes(listaFuncoes);
      setEpisCatalogo(listaEpis);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funções.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const alterado =
    expandidoId !== null &&
    (vinculosSelecionados.length !== vinculosOriginais.length ||
      vinculosSelecionados.some((id) => !vinculosOriginais.includes(id)));

  async function alternarExpansao(funcao: Funcao) {
    if (
      alterado &&
      !(await confirmar({
        titulo: 'Descartar alterações?',
        mensagem: 'Os EPIs marcados nesta função ainda não foram salvos. Sair agora descarta as alterações.',
        rotuloConfirmar: 'Descartar',
      }))
    ) {
      return;
    }
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarEpis(funcao.id);
      const ids = vinculados.map((e) => e.id);
      setVinculosSelecionados(ids);
      setVinculosOriginais(ids);
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de EPI da função.');
    }
  }

  async function salvar() {
    if (!expandidoId) return;
    try {
      setSalvando(true);
      setErro(null);
      await api.funcoes.definirEpis(expandidoId, vinculosSelecionados);
      setVinculosOriginais(vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de EPI.');
    } finally {
      setSalvando(false);
    }
  }

  const colunas: Coluna<Funcao>[] = [
    { chave: 'nome', rotulo: 'Função' },
    { chave: 'cboCodigo', rotulo: 'CBO', largura: '110px' },
    { chave: 'descricao', rotulo: 'Descrição' },
  ];

  return (
    <div>
      <PageHeader
        titulo="Matriz de EPI por função"
        subtitulo="Clique numa função para editar os EPIs obrigatórios. Funções são cadastradas em Operação → Pessoas → Funções."
        acoes={
          <Button appearance="primary" onClick={salvar} disabled={!alterado || salvando}>
            {salvando ? 'Salvando…' : 'Salvar alterações'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <Card densidade="compacta">
        <DataTable
          aria-label="Funções e seus EPIs"
          densidade="compacta"
          colunas={colunas}
          linhas={funcoes}
          chaveLinha={(f) => f.id}
          carregando={carregando}
          vazio={{
            titulo: 'Nenhuma função cadastrada',
            descricao: 'Cadastre funções em Operação → Pessoas → Funções para montar a matriz.',
          }}
          aoClicarLinha={alternarExpansao}
          expansivel={{
            aberta: (f) => f.id === expandidoId,
            render: (f) => (
              <>
                <Text weight="semibold">Matriz de EPI — {f.nome}</Text>
                {episCatalogo.length === 0 ? (
                  <Text>Nenhum EPI cadastrado no catálogo ainda.</Text>
                ) : (
                  <ChipCheckboxGroup
                    aria-label="EPIs obrigatórios"
                    opcoes={episCatalogo.map((e) => ({
                      id: e.id,
                      rotulo: e.fabricante ? `${e.nome} (${e.fabricante})` : e.nome,
                    }))}
                    selecionados={vinculosSelecionados}
                    aoMudar={setVinculosSelecionados}
                  />
                )}
              </>
            ),
          }}
        />
      </Card>
      {dialogElement}
    </div>
  );
}
