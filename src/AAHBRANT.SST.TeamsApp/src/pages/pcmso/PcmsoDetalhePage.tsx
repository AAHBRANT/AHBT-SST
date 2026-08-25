import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Tab,
  TabList,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  type SelectTabData,
  type SelectTabEvent,
} from '@fluentui/react-components';
import { Add24Regular, ArrowLeft24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusPcmsoLabel,
  type Funcao,
  type NovaPcmsoRevisao,
  type NovoPcmsoItemMatriz,
  type Obra,
  type PcmsoDetalhe,
  type Risco,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

type AbaPcmso = 'matriz' | 'calendario' | 'relatorio' | 'revisoes';

export function PcmsoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [aba, setAba] = useState<AbaPcmso>('matriz');
  const [detalhe, setDetalhe] = useState<PcmsoDetalhe | null>(null);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaObras, listaFuncoes, listaRiscos] = await Promise.all([
        api.pcmsos.obterDetalhe(id),
        api.obras.listar(),
        api.funcoes.listar(),
        api.riscos.listar(),
      ]);
      setDetalhe(det);
      setObras(listaObras);
      setFuncoes(listaFuncoes);
      setRiscos(listaRiscos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PCMSO.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function nomeObra(obraId: string) {
    return obras.find((o) => o.id === obraId)?.nome ?? obraId;
  }

  if (!id) {
    return <Text>PCMSO não encontrado.</Text>;
  }

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/prevencao/pcmso')}
        style={{ marginBottom: 12 }}
      >
        Voltar para PCMSO
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {detalhe ? (
          <>
            <Text size={500} weight="semibold">
              {detalhe.pcmso.nome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {nomeObra(detalhe.pcmso.obraId)}</Text>
              <Text>Médico coordenador: {detalhe.pcmso.medicoCoordenadorNome}</Text>
              <Badge appearance="tint">{statusPcmsoLabel[detalhe.pcmso.status]}</Badge>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPcmso)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="matriz">Matriz de exames</Tab>
        <Tab value="calendario">Calendário</Tab>
        <Tab value="relatorio">Relatório analítico</Tab>
        <Tab value="revisoes">Revisões</Tab>
      </TabList>

      {aba === 'matriz' && (
        <MatrizTab pcmsoId={id} itens={detalhe?.itensMatriz ?? []} funcoes={funcoes} riscos={riscos} onMudou={carregar} />
      )}
      {aba === 'calendario' && <CalendarioTab calendario={detalhe?.calendario ?? []} />}
      {aba === 'relatorio' && <RelatorioTab linhas={detalhe?.relatorioAnalitico ?? []} />}
      {aba === 'revisoes' && <RevisoesTab pcmsoId={id} revisoes={detalhe?.revisoes ?? []} onMudou={carregar} />}
    </div>
  );
}

function itemMatrizVazio(pcmsoId: string): NovoPcmsoItemMatriz {
  return {
    pcmsoId,
    funcaoId: '',
    riscoId: null,
    nomeExame: '',
    periodicidadeEmMeses: 12,
    obrigatorioNoAdmissional: true,
    obrigatorioNoDemissional: true,
    observacoes: '',
  };
}

function MatrizTab({
  pcmsoId,
  itens,
  funcoes,
  riscos,
  onMudou,
}: {
  pcmsoId: string;
  itens: PcmsoDetalhe['itensMatriz'];
  funcoes: Funcao[];
  riscos: Risco[];
  onMudou: () => void;
}) {
  const estilos = usePageStyles();
  const [novoItem, setNovoItem] = useState<NovoPcmsoItemMatriz>(() => itemMatrizVazio(pcmsoId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function criar() {
    if (!novoItem.funcaoId || !novoItem.nomeExame) {
      setErro('Preencha a função e o nome do exame.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.pcmsoItensMatriz.criar(novoItem);
      setNovoItem(itemMatrizVazio(pcmsoId));
      onMudou();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao adicionar item à matriz.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(itemId: string) {
    try {
      await api.pcmsoItensMatriz.excluir(itemId);
      onMudou();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir item da matriz.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Matriz de exames obrigatórios por função (NR-7)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Função">
          <Select value={novoItem.funcaoId} onChange={(_, d) => setNovoItem({ ...novoItem, funcaoId: d.value })}>
            <option value="">Selecione...</option>
            {funcoes.map((funcao) => (
              <option key={funcao.id} value={funcao.id}>
                {funcao.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Risco relacionado (opcional)">
          <Select
            value={novoItem.riscoId ?? ''}
            onChange={(_, d) => setNovoItem({ ...novoItem, riscoId: d.value || null })}
          >
            <option value="">Nenhum</option>
            {riscos.map((risco) => (
              <option key={risco.id} value={risco.id}>
                {risco.exposicao ?? risco.id}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Nome do exame">
          <Input value={novoItem.nomeExame} onChange={(_, d) => setNovoItem({ ...novoItem, nomeExame: d.value })} />
        </Field>
        <Field label="Periodicidade (meses)">
          <Input
            type="number"
            min={1}
            value={String(novoItem.periodicidadeEmMeses)}
            onChange={(_, d) => setNovoItem({ ...novoItem, periodicidadeEmMeses: Number(d.value) || 1 })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar à matriz
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Função</TableHeaderCell>
            <TableHeaderCell>Exame</TableHeaderCell>
            <TableHeaderCell>Periodicidade</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) => (
            <TableRow key={item.id}>
              <TableCell>{item.funcaoNome}</TableCell>
              <TableCell>{item.nomeExame}</TableCell>
              <TableCell>a cada {item.periodicidadeEmMeses} meses</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  aria-label="Excluir"
                  onClick={() => excluir(item.id)}
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function CalendarioTab({ calendario }: { calendario: PcmsoDetalhe['calendario'] }) {
  const estilos = usePageStyles();

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Calendário de exames</Text>
      </div>
      <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
        Calculado a partir do último ASO de cada trabalhador (independente do exame específico) somado à
        periodicidade da matriz — ver limitação registrada no código do módulo.
      </Text>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Trabalhador</TableHeaderCell>
            <TableHeaderCell>Função</TableHeaderCell>
            <TableHeaderCell>Exame</TableHeaderCell>
            <TableHeaderCell>Último exame</TableHeaderCell>
            <TableHeaderCell>Próxima data prevista</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {calendario.map((item, indice) => (
            <TableRow key={`${item.trabalhadorId}-${item.nomeExame}-${indice}`}>
              <TableCell>{item.trabalhadorNome}</TableCell>
              <TableCell>{item.funcaoNome}</TableCell>
              <TableCell>{item.nomeExame}</TableCell>
              <TableCell>{item.ultimoExameData?.slice(0, 10) ?? '—'}</TableCell>
              <TableCell>{item.proximaDataPrevista.slice(0, 10)}</TableCell>
              <TableCell>
                {item.vencido && (
                  <Badge color="danger" appearance="tint">
                    Vencido
                  </Badge>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function RelatorioTab({ linhas }: { linhas: PcmsoDetalhe['relatorioAnalitico'] }) {
  const estilos = usePageStyles();

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Relatório analítico de saúde (agregado por função — NR-7)</Text>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Função</TableHeaderCell>
            <TableHeaderCell>Total de ASOs</TableHeaderCell>
            <TableHeaderCell>Aptos</TableHeaderCell>
            <TableHeaderCell>Aptos c/ restrição</TableHeaderCell>
            <TableHeaderCell>Inaptos</TableHeaderCell>
            <TableHeaderCell>Pendentes</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {linhas.map((linha) => (
            <TableRow key={linha.funcaoId}>
              <TableCell>{linha.funcaoNome}</TableCell>
              <TableCell>{linha.totalAsos}</TableCell>
              <TableCell>{linha.aptos}</TableCell>
              <TableCell>{linha.aptosComRestricao}</TableCell>
              <TableCell>{linha.inaptos}</TableCell>
              <TableCell>{linha.pendentes}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function revisaoVazia(pcmsoId: string): NovaPcmsoRevisao {
  return { pcmsoId, dataRevisao: '', motivo: '', responsavelUsuarioId: null };
}

function RevisoesTab({
  pcmsoId,
  revisoes,
  onMudou,
}: {
  pcmsoId: string;
  revisoes: PcmsoDetalhe['revisoes'];
  onMudou: () => void;
}) {
  const estilos = usePageStyles();
  const [novaRevisao, setNovaRevisao] = useState<NovaPcmsoRevisao>(() => revisaoVazia(pcmsoId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function criar() {
    if (!novaRevisao.dataRevisao || !novaRevisao.motivo) {
      setErro('Preencha a data e o motivo da revisão.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.pcmsoRevisoes.criar(novaRevisao);
      setNovaRevisao(revisaoVazia(pcmsoId));
      onMudou();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar revisão.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Histórico de revisões</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Data da revisão">
          <Input
            type="date"
            value={novaRevisao.dataRevisao}
            onChange={(_, d) => setNovaRevisao({ ...novaRevisao, dataRevisao: d.value })}
          />
        </Field>
        <Field label="Motivo">
          <Input value={novaRevisao.motivo} onChange={(_, d) => setNovaRevisao({ ...novaRevisao, motivo: d.value })} />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Registrar revisão
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nº</TableHeaderCell>
            <TableHeaderCell>Data</TableHeaderCell>
            <TableHeaderCell>Motivo</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {revisoes.map((revisao) => (
            <TableRow key={revisao.id}>
              <TableCell>{revisao.numeroRevisao}</TableCell>
              <TableCell>{revisao.dataRevisao?.slice(0, 10)}</TableCell>
              <TableCell>{revisao.motivo}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
