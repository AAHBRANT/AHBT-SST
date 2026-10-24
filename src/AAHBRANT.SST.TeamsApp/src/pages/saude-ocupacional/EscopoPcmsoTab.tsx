import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  DataTable,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Field,
  Input,
  PageHeader,
  Select,
  StatusChip,
  Text,
  Textarea,
  type Coluna,
  type Tom,
} from '@ui';
import { api, type EscopoPcmso, type Obra } from '../../lib/api';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';

// Escopo para o médico elaborar o PCMSO (Etapa 2, desenho aprovado em 21/10): tudo sai do PGR
// vigente da obra. O sistema NÃO sugere exames — exames, periodicidade e critérios de aptidão
// ficam com o médico (decisão do usuário).
type Funcao = EscopoPcmso['funcoes'][number];
type Risco = EscopoPcmso['riscos'][number];

const nivelInfo: Record<number, { rotulo: string; tom: Tom }> = {
  1: { rotulo: 'Trivial', tom: 'neutro' },
  2: { rotulo: 'Baixo', tom: 'ok' },
  3: { rotulo: 'Moderado', tom: 'atencao' },
  4: { rotulo: 'Alto', tom: 'alerta' },
  5: { rotulo: 'Crítico', tom: 'alerta' },
};

const data = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-BR') : null);
const ghe = (n: number) => String(n).padStart(2, '0');

export function EscopoPcmsoTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [escopo, setEscopo] = useState<EscopoPcmso | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [dialogoEnvio, setDialogoEnvio] = useState(false);
  const [email, setEmail] = useState('');
  const [nomeMedico, setNomeMedico] = useState('');
  const [mensagem, setMensagem] = useState('');
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  useEffect(() => {
    api.obras
      .listar()
      .then((lista) => {
        setObras(lista);
        if (lista.length === 1) setObraId(lista[0].id);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar obras.'));
  }, []);

  useEffect(() => {
    setEscopo(null);
    setAviso(null);
    if (!obraId) return;
    setCarregando(true);
    setErro(null);
    api.escopoPcmso
      .obter(obraId)
      .then(setEscopo)
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao montar o escopo.'))
      .finally(() => setCarregando(false));
  }, [obraId]);

  const nomeArquivo = escopo ? `Escopo PCMSO - ${escopo.obraNome}.pdf` : 'escopo-pcmso.pdf';

  async function baixar() {
    try {
      setErro(null);
      salvarBlob(await api.escopoPcmso.pdf(obraId), nomeArquivo);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF.');
    }
  }

  async function enviar() {
    try {
      setEnviando(true);
      setErro(null);
      await api.escopoPcmso.enviar(obraId, email.trim(), nomeMedico.trim(), mensagem.trim());
      setDialogoEnvio(false);
      setAviso(`Escopo enviado para ${email.trim()} com o PDF anexo.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o e-mail.');
    } finally {
      setEnviando(false);
    }
  }

  const colunasFuncoes: Coluna<Funcao>[] = [
    { chave: 'ghe', rotulo: 'GHE', largura: '160px', render: (f) => [ghe(f.gheNumero), f.gheSetor].filter(Boolean).join(' · ') },
    { chave: 'funcao', rotulo: 'Função', render: (f) => <Text weight="semibold">{f.funcao}</Text> },
    { chave: 'qtde', rotulo: 'Qtde', largura: '70px', alinhar: 'direita', render: (f) => f.trabalhadores },
    { chave: 'atividades', rotulo: 'Atividades', render: (f) => f.atividades ?? '—' },
  ];

  const colunasRiscos: Coluna<Risco>[] = [
    { chave: 'ghe', rotulo: 'GHE', largura: '60px', render: (r) => ghe(r.gheNumero) },
    { chave: 'tipo', rotulo: 'Tipo', largura: '120px', render: (r) => r.tipo ?? '—' },
    { chave: 'perigo', rotulo: 'Perigo / agente', render: (r) => <Text weight="semibold">{r.perigo}</Text> },
    { chave: 'fonte', rotulo: 'Fonte / atividade', render: (r) => [r.fonte, r.atividade].filter(Boolean).join(' · ') },
    {
      chave: 'nivel',
      rotulo: 'Nível',
      largura: '120px',
      render: (r) => <StatusChip tom={nivelInfo[r.nivel]?.tom ?? 'neutro'}>{nivelInfo[r.nivel]?.rotulo ?? '—'}</StatusChip>,
    },
  ];

  return (
    <div>
      <PageHeader
        titulo="Escopo para o médico elaborar o PCMSO"
        subtitulo="Montado automaticamente a partir do PGR vigente da obra. Exames, periodicidade e critérios de aptidão ficam com o médico."
        filtros={
          <Field label="Obra">
            <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
              <option value="">Selecione a obra</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.nome}
                </option>
              ))}
            </Select>
          </Field>
        }
        acoes={
          escopo ? (
            <>
              <Button onClick={() => visualizar({ titulo: 'Escopo para o PCMSO', nomeArquivo, obter: () => api.escopoPcmso.pdf(obraId) })}>
                Visualizar PDF
              </Button>
              <Button onClick={baixar}>Baixar PDF</Button>
              <Button appearance="primary" onClick={() => setDialogoEnvio(true)}>
                Enviar ao médico por e-mail
              </Button>
            </>
          ) : undefined
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {aviso && <FeedbackInline tom="sucesso" aoFechar={() => setAviso(null)}>{aviso}</FeedbackInline>}
      {escopo && escopo.pendencias.length > 0 && (
        <FeedbackInline tom="aviso">
          <strong>Pendências no cadastro:</strong> {escopo.pendencias.join(' ')} Esses campos saem como "não informado" no documento.
        </FeedbackInline>
      )}

      {!obraId ? (
        <Card>
          <Text>Escolha a obra para montar o escopo a partir do PGR dela.</Text>
        </Card>
      ) : carregando || !escopo ? (
        <Card>
          <Text>{carregando ? 'Montando o escopo a partir do PGR…' : ''}</Text>
        </Card>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo="1. Identificação da obra" subtitulo={escopo.pgrNome ? `Base: ${escopo.pgrNome}${escopo.pgrDataElaboracao ? ` · elaborado em ${data(escopo.pgrDataElaboracao)}` : ''}` : 'Obra sem PGR vigente'}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 12 }}>
              {[
                ['Obra', escopo.obraNome],
                ['CNPJ', escopo.cnpj],
                ['Endereço', escopo.endereco],
                ['Período', [data(escopo.dataInicio), data(escopo.dataPrevisaoTermino)].filter(Boolean).join(' a ') || null],
                ['Trabalhadores ativos', String(escopo.trabalhadoresAtivos)],
              ].map(([rotulo, valor]) => (
                <div key={rotulo}>
                  <Text size={200} style={{ display: 'block' }}>{rotulo}</Text>
                  {valor ? <Text weight="semibold">{valor}</Text> : <Text italic>não informado</Text>}
                </div>
              ))}
            </div>
          </Card>

          <Card titulo="2. Funções por GHE" densidade="compacta">
            <DataTable
              aria-label="Funções por GHE"
              densidade="compacta"
              colunas={colunasFuncoes}
              linhas={escopo.funcoes}
              chaveLinha={(f) => `${f.gheNumero}-${f.funcao}`}
              vazio={{ titulo: 'Sem funções no PGR', descricao: 'O PGR da obra não tem GHE com funções cadastradas.' }}
            />
          </Card>

          <Card titulo="3. Riscos ocupacionais por GHE (inventário do PGR)" densidade="compacta">
            <DataTable
              aria-label="Riscos ocupacionais por GHE"
              densidade="compacta"
              colunas={colunasRiscos}
              linhas={escopo.riscos}
              chaveLinha={(r) => `${r.gheNumero}-${r.perigo}`}
              vazio={{ titulo: 'Sem riscos no PGR', descricao: 'O PGR da obra não tem riscos cadastrados nas atividades dos GHE.' }}
            />
          </Card>

          <Card titulo="4. Atividades críticas (exigem aptidão específica no ASO)" subtitulo="Pelos cursos obrigatórios da obra (Treinamentos › Cursos obrigatórios por obra)">
            {escopo.atividadesCriticas.length === 0 ? (
              <Text italic>Nenhuma atividade crítica identificada pelos cursos obrigatórios da obra.</Text>
            ) : (
              <ul style={{ margin: 0, paddingLeft: 20, lineHeight: 1.7 }}>
                {escopo.atividadesCriticas.map((a) => (
                  <li key={a.descricao}>
                    <strong>{a.descricao}</strong> — {a.funcoes.join(', ')}
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <Card titulo="5. A cargo do médico responsável">
            <Text>
              Definir os exames por função, a periodicidade, os critérios de aptidão e assinar o PCMSO. Devolver o documento concluído para
              anexar na obra (Saúde Ocupacional › PCMSO).
            </Text>
          </Card>
        </div>
      )}

      <Dialog open={dialogoEnvio} onOpenChange={(_, d) => !d.open && setDialogoEnvio(false)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Enviar escopo ao médico</DialogTitle>
            <DialogContent>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <Text size={200}>Sai pela caixa sst@aahbrant.com com o PDF do escopo anexo.</Text>
                <Field label="E-mail do médico" required>
                  <Input type="email" value={email} onChange={(_, d) => setEmail(d.value)} />
                </Field>
                <Field label="Nome do médico">
                  <Input value={nomeMedico} onChange={(_, d) => setNomeMedico(d.value)} />
                </Field>
                <Field label="Mensagem adicional">
                  <Textarea value={mensagem} onChange={(_, d) => setMensagem(d.value)} maxLength={2000} />
                </Field>
              </div>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setDialogoEnvio(false)}>Cancelar</Button>
              <Button appearance="primary" onClick={enviar} disabled={enviando || !/^\S+@\S+\.\S+$/.test(email.trim())}>
                {enviando ? 'Enviando…' : 'Enviar'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
      {dialogoVisualizador}
    </div>
  );
}
