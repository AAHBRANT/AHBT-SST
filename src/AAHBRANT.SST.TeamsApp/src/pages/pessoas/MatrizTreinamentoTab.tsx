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
import { api, type CursoTreinamento, type Funcao } from '../../lib/api';

// Matriz de obrigatoriedade de treinamento por função — mesmo princípio de MatrizEpiTab.tsx
// (EpiPage), aqui em Pessoas por não existir um módulo próprio de Treinamento equivalente ao de EPI.
// Base do Motor de Aplicabilidade Legal para treinamentos obrigatórios gerados a partir de um
// RequisitoLegal aplicável, mas também editável manualmente, igual à matriz de EPI. Camada ui/
// (Onda 2, Task 1): réplica literal do template §4.5 já validado no piloto 3 (MatrizEpiTab) — mantém
// expandir-linha (chevron ▸/▾ embutido no DataTable), salvar único no header em vez de um botão por
// linha, e confirmação de descarte ao trocar de função com edição pendente.
export function MatrizTreinamentoTab() {
  const navigate = useNavigate();
  const { confirmar, dialogElement } = useConfirmar();
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [cursosCatalogo, setCursosCatalogo] = useState<CursoTreinamento[]>([]);
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
      const [listaFuncoes, listaCursos] = await Promise.all([api.funcoes.listar(), api.cursosTreinamento.listar()]);
      setFuncoes(listaFuncoes);
      setCursosCatalogo(listaCursos);
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
        mensagem: 'Os cursos marcados nesta função ainda não foram salvos. Sair agora descarta as alterações.',
        rotuloConfirmar: 'Descartar',
        tom: 'destrutivo',
      });
      if (!descartou) return;
      // Descarte confirmado: fecha a linha atual antes de buscar a próxima, mesmo cuidado de
      // MatrizEpiTab — senão "Salvar alterações" continuaria habilitado gravando o que já foi
      // descartado, caso o carregamento seguinte falhe.
      setExpandidoId(null);
    }
    if (expandidoId === funcao.id) {
      setExpandidoId(null);
      return;
    }
    try {
      setErro(null);
      const vinculados = await api.funcoes.listarTreinamentosObrigatorios(funcao.id);
      const ids = vinculados.map((c) => c.id);
      setVinculosSelecionados(ids);
      setVinculosOriginais(ids);
      setExpandidoId(funcao.id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matriz de treinamento da função.');
    }
  }

  async function salvar() {
    if (!expandidoId) return;
    try {
      setSalvando(true);
      setErro(null);
      await api.funcoes.definirTreinamentosObrigatorios(expandidoId, vinculosSelecionados);
      setVinculosOriginais(vinculosSelecionados);
      setExpandidoId(null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de treinamento.');
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
        titulo="Matriz de treinamento por função"
        subtitulo="Clique numa função para editar quais cursos são obrigatórios para ela. Funções são cadastradas em Pessoas → Funções; cursos, em Treinamentos → Treinamentos."
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
          aria-label="Funções e seus treinamentos obrigatórios"
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
                <Text weight="semibold">Matriz de treinamento — {f.nome}</Text>
                {cursosCatalogo.length === 0 ? (
                  <Text>Nenhum curso cadastrado no catálogo ainda.</Text>
                ) : (
                  <ChipCheckboxGroup
                    aria-label="Cursos obrigatórios"
                    opcoes={cursosCatalogo.map((c) => ({
                      id: c.id,
                      rotulo: c.normaReferencia ? `${c.nome} (${c.normaReferencia})` : c.nome,
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
