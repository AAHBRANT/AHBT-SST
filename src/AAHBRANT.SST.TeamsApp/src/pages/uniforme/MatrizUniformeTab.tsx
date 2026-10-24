import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
import { api, type CatalogoUniforme, type Funcao } from '../../lib/api';

// Matriz de uniforme por função — mesmo padrão de MatrizEpiTab.tsx. Define quais peças são
// obrigatórias para cada função; o tamanho de cada peça vem do cadastro do trabalhador (aba
// Tamanhos), nunca daqui. Camada ui/ (template §4.5): mantém expandir-linha, com salvar único no
// header em vez de um botão por linha. Como o salvar deixa de ficar dentro da linha, sair de uma
// função com edição pendente passa a pedir confirmação.
export function MatrizUniformeTab() {
  const navigate = useNavigate();
  const { confirmar, dialogElement } = useConfirmar();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
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
      const [listaFuncoes, listaItens] = await Promise.all([api.funcoes.listar(), api.catalogosUniforme.listar()]);
      setFuncoes(listaFuncoes);
      setItensCatalogo(listaItens);
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
    if (alterado) {
      const descartou = await confirmar({
        titulo: 'Descartar alterações?',
        mensagem: 'As peças marcadas nesta função ainda não foram salvas. Sair agora descarta as alterações.',
        rotuloConfirmar: 'Descartar',
        tom: 'destrutivo',
      });
      if (!descartou) return;
      setExpandidoId(null);
    }
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarUniformes(funcao.id);
      const ids = vinculados.map((i) => i.id);
      setVinculosSelecionados(ids);
      setVinculosOriginais(ids);
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de uniforme da função.');
    }
  }

  async function salvar() {
    if (!expandidoId) return;
    try {
      setSalvando(true);
      setErro(null);
      await api.funcoes.definirUniformes(expandidoId, vinculosSelecionados);
      setVinculosOriginais(vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de uniforme.');
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
        titulo="Matriz de uniforme por função"
        subtitulo="Clique numa função para editar as peças obrigatórias. Funções são cadastradas em Pessoas → Funções."
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
          aria-label="Funções e suas peças de uniforme"
          densidade="compacta"
          colunas={colunas}
          linhas={funcoes}
          chaveLinha={(f) => f.id}
          carregando={carregando}
          vazio={{
            titulo: 'Nenhuma função cadastrada',
            descricao: 'Cadastre funções em Pessoas → Funções para montar a matriz.',
            acao: { rotulo: 'Gerenciar funções', aoClicar: () => navigate('/pessoas?aba=funcoes') },
          }}
          aoClicarLinha={alternarExpansao}
          expansivel={{
            aberta: (f) => f.id === expandidoId,
            render: (f) => (
              <>
                <Text weight="semibold">Matriz de uniforme — {f.nome}</Text>
                {itensCatalogo.length === 0 ? (
                  <Text>Nenhuma peça cadastrada no catálogo ainda.</Text>
                ) : (
                  <ChipCheckboxGroup
                    aria-label="Peças de uniforme obrigatórias"
                    opcoes={itensCatalogo.map((i) => ({
                      id: i.id,
                      rotulo: i.categoria ? `${i.nome} (${i.categoria})` : i.nome,
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
