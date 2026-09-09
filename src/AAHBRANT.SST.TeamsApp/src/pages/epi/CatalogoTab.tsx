import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Input,
  Legenda,
  PageHeader,
  PainelLateral,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type NovoCatalogoEpi } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

const epiVazio: NovoCatalogoEpi = {
  nome: '',
  fabricante: '',
  certificadoAprovacaoNumero: '',
  certificadoAprovacaoValidade: '',
  vidaUtilEmMeses: 12,
};

// Catálogo de EPI (item + estoque) do módulo dedicado /epi — antes vivia como aba dentro de
// Pessoas (CatalogoEpiTab), mas o catálogo não é dado de uma pessoa, e sim operacional/compartilhado
// entre entregas; com o módulo próprio de EPI aprovado pelo usuário, a gestão de catálogo/estoque
// passou para cá por inteiro.
// Onda 2 Task 19 (camada ui/): o formulário de cadastro empurrava a lista pra baixo (padrão do Guia
// §2) — sai para um PainelLateral, mesmo golden rule já aplicado em FuncoesTab.tsx (Task 1). Erro do
// painel isolado do erro da lista. Edição inline por linha preservada (clicar na linha edita nela
// mesma, mesmo padrão de AsosTab.tsx/Task 12), só a criação de item novo é que muda de lugar.
export function CatalogoTab() {
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [novoEpi, setNovoEpi] = useState<NovoCatalogoEpi>(epiVazio);
  const [fotoNovoEpi, setFotoNovoEpi] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoEpi | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  // Erro do painel de criação fica separado do erro da lista — mesmo motivo já registrado em
  // EntregasTab.tsx: o PainelLateral é um drawer modal, uma mensagem de nível de página apareceria
  // atrás dele, fora do foco do usuário.
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setEpis(await api.catalogosEpi.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de EPI.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Todo caminho de fechar o painel limpa o erro e o rascunho — senão reabrir mostra dado/mensagem velha.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovoEpi(epiVazio);
    setFotoNovoEpi(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.catalogosEpi.criar(novoEpi);
      if (fotoNovoEpi) {
        await api.catalogosEpi.anexarFoto(id, fotoNovoEpi);
      }
      await carregar();
      sucessoToast('EPI cadastrado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar EPI de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(epiId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosEpi.anexarFoto(epiId, arquivo);
      await carregar();
      sucessoToast('Foto do EPI atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto do EPI.');
    }
  }

  function iniciarEdicao(epi: CatalogoEpi) {
    setEdicaoId(epi.id);
    setEdicao({ ...epi });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpi.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('EPI atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar EPI de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este item do catálogo de EPI? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosEpi.excluir(id);
      await carregar();
      sucessoToast('EPI excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir EPI de catálogo.');
    }
  }

  const colunas: Coluna<CatalogoEpi>[] = [
    {
      chave: 'foto',
      rotulo: 'Foto',
      render: (epi) =>
        edicaoId === epi.id && edicao ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }} onClick={(ev) => ev.stopPropagation()}>
            <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={36} />
            <SeletorFotoCamera
              apenasIcone
              tamanho="small"
              rotulo="Trocar foto"
              tiposAceitos="image/jpeg,image/png"
              aoSelecionarArquivo={(arquivo) => trocarFoto(epi.id, arquivo)}
              aoErroValidacao={setErro}
            />
          </div>
        ) : (
          <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={36} />
        ),
    },
    {
      chave: 'nome',
      rotulo: 'Nome',
      render: (epi) =>
        edicaoId === epi.id && edicao ? (
          <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
        ) : (
          epi.nome
        ),
    },
    {
      chave: 'fabricante',
      rotulo: 'Fabricante',
      render: (epi) =>
        edicaoId === epi.id && edicao ? (
          <Input value={edicao.fabricante ?? ''} onChange={(_, d) => setEdicao({ ...edicao, fabricante: d.value })} />
        ) : (
          epi.fabricante
        ),
    },
    {
      chave: 'ca',
      rotulo: 'Nº do CA',
      render: (epi) =>
        edicaoId === epi.id && edicao ? (
          <Input
            value={edicao.certificadoAprovacaoNumero ?? ''}
            onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoNumero: d.value })}
          />
        ) : (
          epi.certificadoAprovacaoNumero
        ),
    },
    {
      chave: 'validadeCa',
      rotulo: 'Validade do CA',
      render: (epi) =>
        edicaoId === epi.id && edicao ? (
          <CampoData
            value={edicao.certificadoAprovacaoValidade?.slice(0, 10) ?? ''}
            onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoValidade: d.value })}
          />
        ) : (
          epi.certificadoAprovacaoValidade?.slice(0, 10)
        ),
    },
    {
      chave: 'vidaUtil',
      rotulo: 'Vida útil (meses)',
      render: (epi) =>
        edicaoId === epi.id && edicao ? (
          <Input
            type="number"
            value={String(edicao.vidaUtilEmMeses)}
            onChange={(_, d) => setEdicao({ ...edicao, vidaUtilEmMeses: Number(d.value) })}
          />
        ) : (
          epi.vidaUtilEmMeses
        ),
    },
    { chave: 'estoque', rotulo: 'Estoque total', alinhar: 'direita', render: (epi) => epi.saldoTotal },
  ];

  return (
    <>
      <PageHeader
        titulo="Catálogo de EPIs"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Novo EPI
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
          aria-label="Catálogo de EPIs"
          colunas={colunas}
          linhas={epis}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum EPI cadastrado no catálogo ainda.',
            acao: { rotulo: 'Cadastrar EPI', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(epi) => {
            if (edicaoId !== epi.id) iniciarEdicao(epi);
          }}
          acoesLinha={(epi) =>
            edicaoId === epi.id ? (
              <Button
                appearance="subtle"
                size="small"
                icon={<Save24Regular />}
                onClick={salvarEdicao}
                disabled={carregando}
                aria-label="Salvar"
              />
            ) : (
              <Button
                appearance="subtle"
                size="small"
                icon={<Delete24Regular />}
                onClick={() => excluir(epi.id)}
                aria-label="Excluir"
              />
            )
          }
        />
        <Legenda>
          Clique em uma linha para editar os dados do EPI. O estoque é controlado por Obra na aba Estoque.
        </Legenda>
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Novo EPI"
        subtitulo="Nada é salvo até você adicionar."
        largura="lg"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar EPI
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados do EPI" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Nome">
                <Input value={novoEpi.nome} onChange={(_, d) => setNovoEpi({ ...novoEpi, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Fabricante">
                <Input
                  value={novoEpi.fabricante ?? ''}
                  onChange={(_, d) => setNovoEpi({ ...novoEpi, fabricante: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Nº do CA">
                <Input
                  value={novoEpi.certificadoAprovacaoNumero ?? ''}
                  onChange={(_, d) => setNovoEpi({ ...novoEpi, certificadoAprovacaoNumero: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade do CA">
                <CampoData
                  value={novoEpi.certificadoAprovacaoValidade ?? ''}
                  onChange={(_, d) => setNovoEpi({ ...novoEpi, certificadoAprovacaoValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Vida útil (meses)">
                <Input
                  type="number"
                  value={String(novoEpi.vidaUtilEmMeses)}
                  onChange={(_, d) => setNovoEpi({ ...novoEpi, vidaUtilEmMeses: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Foto do EPI">
                <SeletorFotoCamera
                  rotulo={fotoNovoEpi ? fotoNovoEpi.name : 'Tirar foto ou escolher arquivo'}
                  tiposAceitos="image/jpeg,image/png"
                  aoSelecionarArquivo={(arquivo) => setFotoNovoEpi(arquivo)}
                  aoErroValidacao={setErroPainel}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>

      {dialogElement}
    </>
  );
}
