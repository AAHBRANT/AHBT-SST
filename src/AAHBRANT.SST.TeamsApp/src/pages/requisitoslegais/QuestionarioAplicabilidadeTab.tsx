import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Legenda,
  Radio,
  RadioGroup,
  Select,
  Text,
  useConfirmar,
  type Coluna,
} from '@ui';
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type ItemQuestionarioAplicabilidade, type Obra, type RespostaQuestionarioObra } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Onda 2 Task 18 (camada ui/): catálogo de perguntas do questionário de aplicabilidade + respostas
// por obra. Duas tabelas (catálogo e respostas) viram DataTable; Radio/RadioGroup do Fluent
// ganharam re-export em @ui (ui/index.ts) nesta mesma task — único consumidor no app hoje.
export function QuestionarioAplicabilidadeTab() {
  const [itens, setItens] = useState<ItemQuestionarioAplicabilidade[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraSelecionadaId, setObraSelecionadaId] = useState('');
  const [respostas, setRespostas] = useState<RespostaQuestionarioObra[]>([]);
  const [novaPergunta, setNovaPergunta] = useState('');
  const [novoTextoApoio, setNovoTextoApoio] = useState('');
  const [observacoesEdicao, setObservacoesEdicao] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregarCatalogoEObras() {
    try {
      setErro(null);
      const [listaItens, listaObras] = await Promise.all([api.questionarioAplicabilidade.listarItens(), api.obras.listar()]);
      setItens(listaItens);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar questionário de aplicabilidade.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregarCatalogoEObras();
  }, []);

  async function carregarRespostasDaObra(obraId: string) {
    if (!obraId) {
      setRespostas([]);
      return;
    }
    try {
      setErro(null);
      const lista = await api.questionarioAplicabilidade.obterQuestionarioObra(obraId);
      setRespostas(lista);
      setObservacoesEdicao(Object.fromEntries(lista.map((r) => [r.itemId, r.observacao ?? ''])));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar respostas da obra.');
    }
  }

  async function criarItem() {
    if (!novaPergunta.trim()) {
      setErro('Informe a pergunta.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.questionarioAplicabilidade.criarItem(novaPergunta, novoTextoApoio || null);
      setNovaPergunta('');
      setNovoTextoApoio('');
      await carregarCatalogoEObras();
      sucessoToast('Pergunta adicionada ao questionário com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar item do questionário.');
    } finally {
      setProcessando(false);
    }
  }

  async function excluirItem(id: string) {
    if (!(await confirmar('Excluir esta pergunta do questionário de aplicabilidade? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.questionarioAplicabilidade.excluirItem(id);
      await carregarCatalogoEObras();
      sucessoToast('Pergunta excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir item do questionário.');
    }
  }

  async function responder(itemId: string, resposta: boolean) {
    try {
      setProcessando(true);
      setErro(null);
      await api.questionarioAplicabilidade.responder(obraSelecionadaId, itemId, resposta, observacoesEdicao[itemId] || null);
      await carregarRespostasDaObra(obraSelecionadaId);
      sucessoToast('Resposta registrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar resposta.');
    } finally {
      setProcessando(false);
    }
  }

  const colunasCatalogo: Coluna<ItemQuestionarioAplicabilidade>[] = [
    { chave: 'pergunta', rotulo: 'Pergunta' },
    { chave: 'textoApoio', rotulo: 'Texto de apoio', render: (i) => i.textoApoio ?? '—' },
  ];

  const colunasRespostas: Coluna<RespostaQuestionarioObra>[] = [
    {
      chave: 'pergunta',
      rotulo: 'Pergunta',
      render: (r) => (
        <>
          {r.pergunta}
          {r.textoApoio && <Text size={200} style={{ display: 'block' }}>{r.textoApoio}</Text>}
        </>
      ),
    },
    {
      chave: 'resposta',
      rotulo: 'Resposta',
      render: (r) => (
        <RadioGroup
          layout="horizontal"
          value={r.resposta === null ? '' : r.resposta ? 'sim' : 'nao'}
          onChange={(_, d) => responder(r.itemId, d.value === 'sim')}
        >
          <Radio value="sim" label="Sim" />
          <Radio value="nao" label="Não" />
        </RadioGroup>
      ),
    },
    {
      chave: 'observacao',
      rotulo: 'Observação',
      render: (r) => (
        <Input
          value={observacoesEdicao[r.itemId] ?? ''}
          onChange={(_, d) => setObservacoesEdicao({ ...observacoesEdicao, [r.itemId]: d.value })}
          onBlur={() => {
            if (r.resposta !== null) responder(r.itemId, r.resposta);
          }}
        />
      ),
    },
  ];

  return (
    <>
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ marginBottom: 16 }}>
        <Card titulo="Catálogo de perguntas">
          <Legenda>
            Perguntas usadas como critério de aplicabilidade quando não dá para derivar de Perigo/Função/Equipamento
            já cadastrados (ex.: "a obra realiza trabalho em espaço confinado?"). A mesma pergunta vale para todas as
            obras — só a resposta é por obra.
          </Legenda>

          <FormSection titulo="Dados da Pergunta" numero={1}>
            <FormGrid>
              <Campo span={6}>
                <Field label="Pergunta" required>
                  <Input value={novaPergunta} onChange={(_, d) => setNovaPergunta(d.value)} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Texto de apoio">
                  <Input value={novoTextoApoio} onChange={(_, d) => setNovoTextoApoio(d.value)} />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criarItem} disabled={processando}>
                Adicionar pergunta
              </Button>
            </FormRodape>
          </FormSection>

          <DataTable
            aria-label="Catálogo de perguntas do questionário de aplicabilidade"
            colunas={colunasCatalogo}
            linhas={itens}
            chaveLinha={(i) => i.id}
            carregando={carregandoLista}
            vazio={{ titulo: 'Nenhuma pergunta cadastrada ainda.' }}
            acoesLinha={(i) => (
              <Button appearance="subtle" icon={<Delete24Regular />} aria-label="Excluir" onClick={() => excluirItem(i.id)} />
            )}
          />
        </Card>
      </div>

      <Card titulo="Responder por obra">
        <Field label="Obra">
          <Select
            value={obraSelecionadaId}
            onChange={(_, d) => {
              setObraSelecionadaId(d.value);
              carregarRespostasDaObra(d.value);
            }}
          >
            <option value="">Selecione uma obra</option>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>

        {obraSelecionadaId && (
          <DataTable
            aria-label="Respostas do questionário de aplicabilidade da obra selecionada"
            colunas={colunasRespostas}
            linhas={respostas}
            chaveLinha={(r) => r.itemId}
            vazio={{ titulo: 'Nenhuma pergunta cadastrada ainda.' }}
          />
        )}
      </Card>
    </>
  );
}
