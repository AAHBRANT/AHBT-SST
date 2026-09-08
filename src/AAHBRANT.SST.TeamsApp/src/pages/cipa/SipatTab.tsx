import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  PageHeader,
  PainelLateral,
  Select,
  Textarea,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type EventoSipat, type NovoEventoSipat, type Obra } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function vazio(): NovoEventoSipat {
  return { obraId: '', anoReferencia: new Date().getFullYear(), dataInicio: '', dataFim: '', tema: '', programacao: '' };
}

// Camada ui/ (Onda 2, Task 4): formulário de cadastro saiu para PainelLateral (Guia §2); a linha
// inteira já navega para o detalhe, então o botão "ver" redundante saiu (Guia §1).
export function SipatTab() {
  const navigate = useNavigate();
  const [lista, setLista] = useState<EventoSipat[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovoEventoSipat>(vazio());
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
      const [listaEventos, listaObras] = await Promise.all([api.cipa.eventosSipat.listar(), api.obras.listar()]);
      setLista(listaEventos);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar eventos SIPAT.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    if (!novo.obraId || !novo.dataInicio || !novo.dataFim) {
      setErroPainel('Preencha obra e o período do evento.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cipa.eventosSipat.criar({ ...novo, tema: novo.tema || null, programacao: novo.programacao || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Evento SIPAT criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar evento SIPAT.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este evento SIPAT? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.eventosSipat.excluir(id);
      await carregar();
      sucessoToast('Evento SIPAT excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir evento SIPAT.');
    }
  }

  const colunas: Coluna<EventoSipat>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (e) => nomeObra(e.obraId) },
    { chave: 'anoReferencia', rotulo: 'Ano' },
    { chave: 'periodo', rotulo: 'Período', render: (e) => `${e.dataInicio?.slice(0, 10) ?? ''} a ${e.dataFim?.slice(0, 10) ?? ''}` },
    { chave: 'tema', rotulo: 'Tema', render: (e) => e.tema ?? '—' },
    { chave: 'totalAtividades', rotulo: 'Atividades' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="SIPAT"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Criar evento
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
          aria-label="Eventos SIPAT"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum evento SIPAT cadastrado ainda',
            acao: { rotulo: 'Criar evento', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(e) => navigate(`/operacao/cipa/sipat/${e.id}`)}
          acoesLinha={(e) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(e.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Novo evento SIPAT"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Criar evento
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
          <Campo span={4}>
            <Field label="Obra" required>
              <Select value={novo.obraId} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Ano de referência" required>
              <Input
                type="number"
                value={String(novo.anoReferencia)}
                onChange={(_, d) => setNovo({ ...novo, anoReferencia: Number(d.value) })}
              />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Início" required>
              <CampoData value={novo.dataInicio} onChange={(_, d) => setNovo({ ...novo, dataInicio: d.value })} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Fim" required>
              <CampoData value={novo.dataFim} onChange={(_, d) => setNovo({ ...novo, dataFim: d.value })} />
            </Field>
          </Campo>
          <Campo span={4}>
            <Field label="Tema">
              <Input value={novo.tema ?? ''} onChange={(_, d) => setNovo({ ...novo, tema: d.value })} />
            </Field>
          </Campo>
          <Campo span={12}>
            <Field label="Programação">
              <Textarea value={novo.programacao ?? ''} onChange={(_, d) => setNovo({ ...novo, programacao: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
