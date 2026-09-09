import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelLateral,
  FormGrid,
  Campo,
  FeedbackInline,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoCursoTreinamento } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const cursoVazio: NovoCursoTreinamento = {
  nome: '',
  normaReferencia: '',
  cargaHorariaMinima: 0,
  validadeEmMeses: 12,
};

// Camada ui/ (Onda 2, Task 1): formulário de criação foi para um PainelLateral, mesmo padrão dos
// demais cadastros deste módulo.
export function CursosTreinamentoTab() {
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoCurso, setNovoCurso] = useState<NovoCursoTreinamento>(cursoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setCursos(await api.cursosTreinamento.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar cursos de treinamento.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cursosTreinamento.criar(novoCurso);
      setNovoCurso(cursoVazio);
      await carregar();
      sucessoToast('Curso de treinamento criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar curso de treinamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este curso de treinamento? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cursosTreinamento.excluir(id);
      await carregar();
      sucessoToast('Curso de treinamento excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir curso de treinamento.');
    }
  }

  const colunas: Coluna<CursoTreinamento>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'normaReferencia', rotulo: 'Norma' },
    { chave: 'cargaHorariaMinima', rotulo: 'CH mínima', render: (c) => `${c.cargaHorariaMinima}h` },
    { chave: 'validadeEmMeses', rotulo: 'Validade (meses)' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Cursos de treinamento (catálogo)"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar curso
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <Card>
        <DataTable
          aria-label="Cursos de treinamento"
          colunas={colunas}
          linhas={cursos}
          chaveLinha={(c) => c.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum curso de treinamento cadastrado ainda',
            acao: { rotulo: 'Adicionar curso', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(c) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(c.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Novo curso de treinamento"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar curso
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}
        <FormGrid>
          <Campo span={6}>
            <Field label="Nome">
              <Input value={novoCurso.nome} onChange={(_, d) => setNovoCurso({ ...novoCurso, nome: d.value })} />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Norma de referência">
              <Input
                value={novoCurso.normaReferencia ?? ''}
                onChange={(_, d) => setNovoCurso({ ...novoCurso, normaReferencia: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Carga horária mínima (h)">
              <Input
                type="number"
                value={String(novoCurso.cargaHorariaMinima)}
                onChange={(_, d) => setNovoCurso({ ...novoCurso, cargaHorariaMinima: Number(d.value) })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Validade (meses)">
              <Input
                type="number"
                value={String(novoCurso.validadeEmMeses)}
                onChange={(_, d) => setNovoCurso({ ...novoCurso, validadeEmMeses: Number(d.value) })}
              />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
