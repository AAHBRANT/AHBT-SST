import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  RadioGroup,
  Radio,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type ItemQuestionarioAplicabilidade, type Obra, type RespostaQuestionarioObra } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

export function QuestionarioAplicabilidadeTab() {
  const estilos = usePageStyles();
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
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  return (
    <div>
      {dialogElement}
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Catálogo de perguntas</Text>
        </div>
        <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
          Perguntas usadas como critério de aplicabilidade quando não dá para derivar de Perigo/Função/Equipamento
          já cadastrados (ex.: "a obra realiza trabalho em espaço confinado?"). A mesma pergunta vale para todas as
          obras — só a resposta é por obra.
        </Text>
        <div className={estilos.form}>
          <Field label="Pergunta" required>
            <Input value={novaPergunta} onChange={(_, d) => setNovaPergunta(d.value)} />
          </Field>
          <Field label="Texto de apoio">
            <Input value={novoTextoApoio} onChange={(_, d) => setNovoTextoApoio(d.value)} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criarItem} disabled={processando}>
            Adicionar pergunta
          </Button>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : itens.length === 0 ? (
          <EstadoVazio mensagem="Nenhuma pergunta cadastrada ainda." />
        ) : (
        <Table noNativeElements style={{ marginTop: 12 }}>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Pergunta</TableHeaderCell>
              <TableHeaderCell>Texto de apoio</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {itens.map((i) => (
              <TableRow key={i.id}>
                <TableCell>{i.pergunta}</TableCell>
                <TableCell>{i.textoApoio ?? '—'}</TableCell>
                <TableCell>
                  <Button appearance="subtle" icon={<Delete24Regular />} aria-label="Excluir" onClick={() => excluirItem(i.id)} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        )}
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Responder por obra</Text>
        </div>
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
          <Table noNativeElements style={{ marginTop: 12 }}>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Pergunta</TableHeaderCell>
                <TableHeaderCell>Resposta</TableHeaderCell>
                <TableHeaderCell>Observação</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {respostas.map((r) => (
                <TableRow key={r.itemId}>
                  <TableCell>
                    {r.pergunta}
                    {r.textoApoio && (
                      <Text size={200} style={{ display: 'block' }}>
                        {r.textoApoio}
                      </Text>
                    )}
                  </TableCell>
                  <TableCell>
                    <RadioGroup
                      layout="horizontal"
                      value={r.resposta === null ? '' : r.resposta ? 'sim' : 'nao'}
                      onChange={(_, d) => responder(r.itemId, d.value === 'sim')}
                    >
                      <Radio value="sim" label="Sim" />
                      <Radio value="nao" label="Não" />
                    </RadioGroup>
                  </TableCell>
                  <TableCell>
                    <Input
                      value={observacoesEdicao[r.itemId] ?? ''}
                      onChange={(_, d) => setObservacoesEdicao({ ...observacoesEdicao, [r.itemId]: d.value })}
                      onBlur={() => {
                        if (r.resposta !== null) responder(r.itemId, r.resposta);
                      }}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </div>
  );
}
