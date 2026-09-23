import { useEffect, useState } from 'react';
import {
  Button,
  Checkbox,
  Field,
  Input,
  Textarea,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  FeedbackInline,
  StatusChip,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Switch } from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoCursoTreinamento } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const cursoVazio: NovoCursoTreinamento = {
  nome: '',
  normaReferencia: '',
  cargaHorariaMinima: 0,
  validadeEmMeses: 12,
  conteudoProgramatico: '',
  ehIntegracaoSeguranca: false,
  atendeNr6: false,
};

// Migração do formulário inline (spec 2026-09-11): o PainelLateral (drawer) saiu — mesmo padrão de
// AtividadesTab.tsx/InspecoesTab.tsx. Agora é um PainelCriacaoInline, que cresce acima da lista até
// a altura do próprio formulário, em vez de cobrir a tela com uma gaveta.
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

  async function marcarComoIntegracaoSeguranca(curso: CursoTreinamento) {
    try {
      await api.cursosTreinamento.atualizar(curso.id, { ...curso, ehIntegracaoSeguranca: true });
      await carregar();
      sucessoToast(`"${curso.nome}" agora é o curso de Integração de Segurança.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao marcar curso como Integração de Segurança.');
    }
  }

  const colunas: Coluna<CursoTreinamento>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    {
      chave: 'normaReferencia',
      rotulo: 'Norma',
      render: (c) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span>{c.normaReferencia ?? '—'}</span>
          {c.atendeNr6 && <StatusChip tom="info">Habilita EPI</StatusChip>}
        </div>
      ),
    },
    { chave: 'cargaHorariaMinima', rotulo: 'CH mínima', render: (c) => `${c.cargaHorariaMinima}h` },
    { chave: 'validadeEmMeses', rotulo: 'Validade (meses)' },
    {
      chave: 'integracaoSeguranca',
      rotulo: 'Integração de Segurança',
      render: (c) => (c.ehIntegracaoSeguranca ? <StatusChip tom="ok">Sim</StatusChip> : '—'),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Cursos de treinamento (catálogo)"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-novo-curso"
          >
            {painelAberto ? 'Fechar' : 'Adicionar curso'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <div id="painel-novo-curso">
        <PainelCriacaoInline aberto={painelAberto} titulo="Novo curso de treinamento">
          <FormSection titulo="Dados do curso" numero={1} primeira>
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
              <Campo span={12}>
                {/* Marcador explícito (22/09): é ele que libera a entrega de EPI, e não mais o texto
                    digitado em "Norma de referência". Marcar o curso errado aqui destrava entrega de
                    EPI para quem não tem NR-06 — por isso o aviso fica no próprio rótulo. */}
                <Field hint="Só marque para cursos que de fato capacitam no uso de EPI (NR-06). A entrega de EPI só é liberada para quem tem um curso marcado aqui, dentro da validade.">
                  <Checkbox
                    label="Este curso atende à NR-06 (habilita a entrega de EPI)"
                    checked={novoCurso.atendeNr6 ?? false}
                    onChange={(_, d) => setNovoCurso({ ...novoCurso, atendeNr6: !!d.checked })}
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
              <Campo span={12}>
                <Field label="Conteúdo programático (um tópico por linha — vira o verso do certificado)">
                  <Textarea
                    rows={6}
                    value={novoCurso.conteudoProgramatico ?? ''}
                    onChange={(_, d) => setNovoCurso({ ...novoCurso, conteudoProgramatico: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={12}>
                <Switch
                  label="Este é o curso de Integração de Segurança obrigatório para todo terceirizado"
                  checked={novoCurso.ehIntegracaoSeguranca}
                  onChange={(_, d) => setNovoCurso({ ...novoCurso, ehIntegracaoSeguranca: d.checked })}
                />
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Adicionar curso
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>
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
            <>
              {!c.ehIntegracaoSeguranca && (
                <Button appearance="subtle" onClick={() => marcarComoIntegracaoSeguranca(c)}>
                  Marcar como Integração de Segurança
                </Button>
              )}
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(c.id)} aria-label="Excluir" />
            </>
          )}
        />
      </Card>
    </div>
  );
}
