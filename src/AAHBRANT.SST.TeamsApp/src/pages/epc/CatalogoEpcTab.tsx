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
import { api, type CatalogoEpc, type NovoCatalogoEpc } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { FotoCatalogoEpc } from './FotoCatalogoEpc';

const epcVazio: NovoCatalogoEpc = {
  nome: '',
  fabricante: '',
  certificadoAprovacaoNumero: '',
  certificadoAprovacaoValidade: '',
  vidaUtilEmMeses: 12,
};

// Catálogo de EPC — mesma estrutura do Catálogo de EPI (pedido do usuário, 04/09: aba própria e
// separada de EPI). Sem CódigoBarras (isso ficou só na Entrega Rápida de EPI, não usado aqui).
// Onda 3 Task 22.5 (camada ui/): mesmo padrão de CatalogoTab.tsx (EPI, Onda 2 Task 19) — formulário
// de cadastro em PainelLateral (erro do painel isolado do erro da lista), edição inline por linha
// preservada, lista em DataTable.
export function CatalogoEpcTab() {
  const [epcs, setEpcs] = useState<CatalogoEpc[]>([]);
  const [novoEpc, setNovoEpc] = useState<NovoCatalogoEpc>(epcVazio);
  const [fotoNovoEpc, setFotoNovoEpc] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoEpc | null>(null);
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
      setEpcs(await api.catalogosEpc.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de EPC.');
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
    setNovoEpc(epcVazio);
    setFotoNovoEpc(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.catalogosEpc.criar({
        ...novoEpc,
        certificadoAprovacaoValidade: novoEpc.certificadoAprovacaoValidade || null,
      });
      if (fotoNovoEpc) {
        await api.catalogosEpc.anexarFoto(id, fotoNovoEpc);
      }
      await carregar();
      sucessoToast('EPC cadastrado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar EPC de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(epcId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosEpc.anexarFoto(epcId, arquivo);
      await carregar();
      sucessoToast('Foto do EPC atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto do EPC.');
    }
  }

  function iniciarEdicao(epc: CatalogoEpc) {
    setEdicaoId(epc.id);
    setEdicao({ ...epc });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpc.atualizar({
        ...edicao,
        certificadoAprovacaoValidade: edicao.certificadoAprovacaoValidade || null,
      });
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('EPC atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar EPC de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este item do catálogo de EPC? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosEpc.excluir(id);
      await carregar();
      sucessoToast('EPC excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir EPC de catálogo.');
    }
  }

  const colunas: Coluna<CatalogoEpc>[] = [
    {
      chave: 'foto',
      rotulo: 'Foto',
      render: (epc) =>
        edicaoId === epc.id && edicao ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }} onClick={(ev) => ev.stopPropagation()}>
            <FotoCatalogoEpc catalogoEpcId={epc.id} temFoto={epc.temFoto} tamanho={36} />
            <SeletorFotoCamera
              apenasIcone
              tamanho="small"
              rotulo="Trocar foto"
              tiposAceitos="image/jpeg,image/png"
              aoSelecionarArquivo={(arquivo) => trocarFoto(epc.id, arquivo)}
              aoErroValidacao={setErro}
            />
          </div>
        ) : (
          <FotoCatalogoEpc catalogoEpcId={epc.id} temFoto={epc.temFoto} tamanho={36} />
        ),
    },
    {
      chave: 'nome',
      rotulo: 'Nome',
      render: (epc) =>
        edicaoId === epc.id && edicao ? (
          <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
        ) : (
          epc.nome
        ),
    },
    {
      chave: 'fabricante',
      rotulo: 'Fabricante',
      render: (epc) =>
        edicaoId === epc.id && edicao ? (
          <Input value={edicao.fabricante ?? ''} onChange={(_, d) => setEdicao({ ...edicao, fabricante: d.value })} />
        ) : (
          epc.fabricante
        ),
    },
    {
      chave: 'ca',
      rotulo: 'Nº do CA',
      render: (epc) =>
        edicaoId === epc.id && edicao ? (
          <Input
            value={edicao.certificadoAprovacaoNumero ?? ''}
            onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoNumero: d.value })}
          />
        ) : (
          epc.certificadoAprovacaoNumero
        ),
    },
    {
      chave: 'validadeCa',
      rotulo: 'Validade do CA',
      render: (epc) =>
        edicaoId === epc.id && edicao ? (
          <CampoData
            value={edicao.certificadoAprovacaoValidade?.slice(0, 10) ?? ''}
            onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoValidade: d.value })}
          />
        ) : (
          epc.certificadoAprovacaoValidade?.slice(0, 10)
        ),
    },
    {
      chave: 'vidaUtil',
      rotulo: 'Vida útil (meses)',
      render: (epc) =>
        edicaoId === epc.id && edicao ? (
          <Input
            type="number"
            value={String(edicao.vidaUtilEmMeses)}
            onChange={(_, d) => setEdicao({ ...edicao, vidaUtilEmMeses: Number(d.value) })}
          />
        ) : (
          epc.vidaUtilEmMeses
        ),
    },
    { chave: 'estoque', rotulo: 'Estoque total', alinhar: 'direita', render: (epc) => epc.saldoTotal },
  ];

  return (
    <>
      <PageHeader
        titulo="Catálogo de EPCs"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Novo EPC
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
          aria-label="Catálogo de EPCs"
          colunas={colunas}
          linhas={epcs}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum EPC cadastrado no catálogo ainda.',
            acao: { rotulo: 'Cadastrar EPC', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(epc) => {
            if (edicaoId !== epc.id) iniciarEdicao(epc);
          }}
          acoesLinha={(epc) =>
            edicaoId === epc.id ? (
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
                onClick={() => excluir(epc.id)}
                aria-label="Excluir"
              />
            )
          }
        />
        <Legenda>
          Clique em uma linha para editar os dados do EPC. O estoque é controlado por Obra na aba Estoque.
        </Legenda>
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Novo EPC"
        subtitulo="Nada é salvo até você adicionar."
        largura="lg"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar EPC
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados do EPC" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Nome">
                <Input value={novoEpc.nome} onChange={(_, d) => setNovoEpc({ ...novoEpc, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Fabricante">
                <Input
                  value={novoEpc.fabricante ?? ''}
                  onChange={(_, d) => setNovoEpc({ ...novoEpc, fabricante: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Nº do CA (se houver)">
                <Input
                  value={novoEpc.certificadoAprovacaoNumero ?? ''}
                  onChange={(_, d) => setNovoEpc({ ...novoEpc, certificadoAprovacaoNumero: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade do CA">
                <CampoData
                  value={novoEpc.certificadoAprovacaoValidade ?? ''}
                  onChange={(_, d) => setNovoEpc({ ...novoEpc, certificadoAprovacaoValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Vida útil (meses)">
                <Input
                  type="number"
                  value={String(novoEpc.vidaUtilEmMeses)}
                  onChange={(_, d) => setNovoEpc({ ...novoEpc, vidaUtilEmMeses: Number(d.value) })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Foto do EPC">
                <SeletorFotoCamera
                  rotulo={fotoNovoEpc ? fotoNovoEpc.name : 'Tirar foto ou escolher arquivo'}
                  tiposAceitos="image/jpeg,image/png"
                  aoSelecionarArquivo={(arquivo) => setFotoNovoEpc(arquivo)}
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
