import { useCallback, useEffect, useState } from 'react';
import { Card, DataTable, FeedbackInline, Field, PageHeader, Select, StatusChip, Text, type Coluna, type Tom } from '@ui';
import { api, type ComparativoFuncaoPgrPcmso, type ComparativoPgrPcmso, type Obra } from '../../lib/api';
import { BotaoRascunhoPcmso } from './BotaoRascunhoPcmso';

const situacao: Record<number, { rotulo: string; tom: Tom }> = {
  1: { rotulo: 'Em ordem', tom: 'ok' },
  2: { rotulo: 'Falta no PCMSO', tom: 'atencao' },
  3: { rotulo: 'Falta no PGR', tom: 'alerta' },
  4: { rotulo: 'Falta nos dois', tom: 'alerta' },
};
const ghe = (n: number) => String(n).padStart(2, '0');

function aviso(f: ComparativoFuncaoPgrPcmso): string | null {
  if (f.situacao === 2 && f.trabalhadores > 0) return `${f.trabalhadores} trabalhador(es) ativo(s) sem exames definidos.`;
  if (f.situacao === 3 && f.examesPcmso > 0) return 'Exames definidos para uma função que não está no PGR.';
  if (f.situacao === 4) return `${f.trabalhadores} trabalhador(es) ativo(s) numa função que não está no PGR nem no PCMSO.`;
  return null;
}

// Funções do PGR vigente contra as funções com exames no PCMSO e os trabalhadores ativos da obra (apontamento da
// supervisão: funções diferentes entre os dois documentos). O sistema não decide qual nome está certo: PGR e PCMSO
// usam o mesmo cadastro de funções, então duas linhas parecidas pedem correção no cadastro.
export function ComparativoPgrPcmsoTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [dados, setDados] = useState<ComparativoPgrPcmso | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    api.obras.listar()
      .then((lista) => {
        setObras(lista);
        if (lista.length === 1) setObraId(lista[0].id);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar obras.'));
  }, []);

  const carregar = useCallback(() => {
    setDados(null);
    if (!obraId) return;
    setCarregando(true);
    setErro(null);
    api.escopoPcmso.comparativo(obraId)
      .then(setDados)
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao montar o comparativo.'))
      .finally(() => setCarregando(false));
  }, [obraId]);

  useEffect(carregar, [carregar]);

  const contar = (s: number) => dados?.funcoes.filter((f) => f.situacao === s).length ?? 0;
  const faltamPcmso = contar(2);
  const faltamPgr = contar(3) + contar(4);

  const colunas: Coluna<ComparativoFuncaoPgrPcmso>[] = [
    {
      chave: 'funcao',
      rotulo: 'Função',
      render: (f) => (
        <div>
          <Text weight="semibold">{f.funcao}</Text>
          {aviso(f) && <Text size={200} style={{ display: 'block' }}>{aviso(f)}</Text>}
        </div>
      ),
    },
    { chave: 'pgr', rotulo: 'PGR (GHE)', render: (f) => (f.ghesPgr.length > 0 ? `Sim · ${f.ghesPgr.map((n) => `GHE ${ghe(n)}`).join(', ')}` : 'Não') },
    { chave: 'pcmso', rotulo: 'PCMSO', render: (f) => (f.examesPcmso > 0 ? `Sim · ${f.examesPcmso} exame(s)` : 'Não') },
    { chave: 'trab', rotulo: 'Trabalhadores', largura: '120px', alinhar: 'direita', render: (f) => f.trabalhadores },
    { chave: 'situacao', rotulo: 'Situação', largura: '170px', render: (f) => <StatusChip tom={situacao[f.situacao].tom}>{situacao[f.situacao].rotulo}</StatusChip> },
  ];

  return (
    <div>
      <PageHeader
        titulo="Comparativo PGR × PCMSO"
        subtitulo="Funções do PGR vigente contra as funções com exames no PCMSO e os trabalhadores ativos da obra."
        filtros={
          <Field label="Obra">
            <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
              <option value="">Selecione a obra</option>
              {obras.map((o) => <option key={o.id} value={o.id}>{o.nome}</option>)}
            </Select>
          </Field>
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {!obraId ? (
        <Card><Text>Escolha a obra para comparar o PGR com o PCMSO.</Text></Card>
      ) : carregando || !dados ? (
        <Card><Text>{carregando ? 'Montando o comparativo…' : ''}</Text></Card>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo={`${dados.obraNome} · funções por documento`} subtitulo={dados.pgrNome ? `Base: ${dados.pgrNome}` : "Sem PGR vigente"}>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 8 }}>
              <StatusChip tom="ok">{contar(1)} em ordem</StatusChip>
              {faltamPcmso > 0 && <StatusChip tom="atencao">{faltamPcmso} faltam no PCMSO</StatusChip>}
              {faltamPgr > 0 && <StatusChip tom="alerta">{faltamPgr} faltam no PGR</StatusChip>}
            </div>
            <DataTable
              aria-label="Funções do PGR e do PCMSO"
              densidade="compacta"
              colunas={colunas}
              linhas={dados.funcoes}
              chaveLinha={(f) => f.funcaoId}
              vazio={{ titulo: 'Sem funções para comparar', descricao: 'A obra não tem funções no PGR vigente nem exames definidos no PCMSO.' }}
            />
            <Text size={200} style={{ display: 'block', marginTop: 8 }}>
              O sistema não decide qual nome está certo. Quando duas funções parecem a mesma, corrija no cadastro de funções e o comparativo se acerta.
              Só entram no PCMSO as funções com exames definidos; um PCMSO que só existe como PDF, sem exames cadastrados, não aparece aqui.
            </Text>
          </Card>

          <Card titulo="Rascunho do PCMSO a partir do PGR">
            <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
              Preenche só o que vem do PGR: funções contempladas e riscos considerados. Exames, periodicidades, médico e CRM ficam em branco para o médico.
              O rascunho não vira PCMSO em vigor sozinho.
            </Text>
            {dados.motivoSemRascunho
              ? <FeedbackInline tom="info">{dados.motivoSemRascunho}</FeedbackInline>
              : <BotaoRascunhoPcmso obraId={obraId} />}
          </Card>
        </div>
      )}
    </div>
  );
}
