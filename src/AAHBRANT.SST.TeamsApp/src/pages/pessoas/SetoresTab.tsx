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
  SeletorPesquisavel,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovoSetor, type Obra, type Setor } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Camada ui/ (Onda 2, Task 1): formulário de criação foi para um PainelLateral (mesmo padrão do
// piloto 1); Obra vira SeletorPesquisavel (spec §3: lista de obras é candidata a busca em vez de
// <select>), com `opcaoVazia` preservando o prompt "Selecione a obra" que o <select> tinha.
export function SetoresTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [setores, setSetores] = useState<Setor[]>([]);
  const [novoSetor, setNovoSetor] = useState<NovoSetor>({ obraId: '', nome: '' });
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
      const [obrasResp, setoresResp] = await Promise.all([api.obras.listar(), api.setores.listar()]);
      setObras(obrasResp);
      setSetores(setoresResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar setores.');
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
    if (!novoSetor.obraId) {
      setErroPainel('Selecione a obra do setor.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.setores.criar(novoSetor);
      setNovoSetor({ obraId: '', nome: '' });
      await carregar();
      sucessoToast('Setor criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar setor.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este setor? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.setores.excluir(id);
      await carregar();
      sucessoToast('Setor excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir setor.');
    }
  }

  const opcoesObras = obras.map((o) => ({ id: o.id, rotulo: o.nome }));

  const colunas: Coluna<Setor>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (s) => s.obraNome },
    { chave: 'nome', rotulo: 'Setor' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Setores cadastrados"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar setor
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
          aria-label="Setores cadastrados"
          colunas={colunas}
          linhas={setores}
          chaveLinha={(s) => s.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum setor cadastrado ainda',
            acao: { rotulo: 'Adicionar setor', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(s) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(s.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Novo setor"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar setor
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
            <Field label="Obra" required>
              <SeletorPesquisavel
                placeholder="Selecione a obra"
                opcaoVazia="Selecione a obra"
                opcoes={opcoesObras}
                valor={novoSetor.obraId}
                aoMudar={(id) => setNovoSetor({ ...novoSetor, obraId: id })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Nome do setor">
              <Input value={novoSetor.nome} onChange={(_, d) => setNovoSetor({ ...novoSetor, nome: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
