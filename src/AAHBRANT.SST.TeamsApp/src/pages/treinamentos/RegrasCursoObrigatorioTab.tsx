import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
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
} from '@ui';
import {
  api,
  type CursoTreinamento,
  type RegraCursoObrigatorio,
  type SalvarRegraCursoObrigatorio,
  type SimulacaoRegraCursoObrigatorio,
} from '../../lib/api';

// Regras "quando uma NR passa a ser obrigatória" (21/10): o QSMS edita. A geração procura as
// palavras-chave nos perigos, atividades e ferramentas do PGR de cada função. Desativar tira a regra
// de uso sem apagar; a carga inicial é proposta do sistema e precisa ser validada pelo QSMS.
const vazia: SalvarRegraCursoObrigatorio = {
  id: null,
  norma: '',
  cursoTreinamentoId: null,
  descricao: '',
  aplicaTodasFuncoes: false,
  palavrasChave: '',
  perigos: '',
  ativo: true,
};

function listaPerigos(texto?: string | null) {
  return (texto ?? '').split(',').map((p) => p.trim()).filter(Boolean);
}

export function RegrasCursoObrigatorioTab() {
  const [regras, setRegras] = useState<RegraCursoObrigatorio[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [editando, setEditando] = useState<SalvarRegraCursoObrigatorio | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [perigosPadrao, setPerigosPadrao] = useState<string[]>([]);
  const [simulacao, setSimulacao] = useState<SimulacaoRegraCursoObrigatorio | null>(null);
  const [simulando, setSimulando] = useState(false);

  useEffect(() => {
    api.cursosObrigatorios.perigosPadrao().then(setPerigosPadrao).catch(() => undefined);
  }, []);

  function abrir(regra: SalvarRegraCursoObrigatorio) {
    setSimulacao(null);
    setEditando(regra);
  }

  async function simular() {
    if (!editando) return;
    try {
      setSimulando(true);
      setErro(null);
      setSimulacao(await api.cursosObrigatorios.simularRegra(editando));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao simular a regra.');
    } finally {
      setSimulando(false);
    }
  }

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      const [r, c] = await Promise.all([api.cursosObrigatorios.regras(), api.cursosTreinamento.listar()]);
      setRegras(r);
      setCursos(c);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar as regras.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function salvar() {
    if (!editando) return;
    try {
      setSalvando(true);
      setErro(null);
      await api.cursosObrigatorios.salvarRegra(editando);
      setEditando(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar a regra.');
    } finally {
      setSalvando(false);
    }
  }

  const colunas: Coluna<RegraCursoObrigatorio>[] = [
    { chave: 'norma', rotulo: 'NR', largura: '90px', render: (r) => <Text weight="semibold">{r.norma}</Text> },
    {
      chave: 'curso',
      rotulo: 'Curso do catálogo',
      render: (r) => (
        <div>
          <Text style={{ display: 'block' }}>{r.descricao}</Text>
          <Text size={200}>{r.cursoNome ?? 'Sem curso desta NR no catálogo'}</Text>
        </div>
      ),
    },
    {
      chave: 'palavras',
      rotulo: 'Aplica quando o PGR tiver',
      render: (r) =>
        r.aplicaTodasFuncoes ? (
          <Text>—</Text>
        ) : (
          <div>
            {r.perigos && (
              <Text style={{ display: 'block' }}>
                <b>Perigo:</b> {r.perigos}
              </Text>
            )}
            {r.palavrasChave && (
              <Text size={200} style={{ display: 'block' }}>
                <b>Palavras:</b> {r.palavrasChave}
              </Text>
            )}
          </div>
        ),
    },
    {
      chave: 'alcance',
      rotulo: 'Alcance',
      largura: '150px',
      render: (r) =>
        !r.ativo ? <StatusChip tom="neutro">Desativada</StatusChip> : <Text>{r.aplicaTodasFuncoes ? 'Todas as funções' : 'Por risco'}</Text>,
    },
  ];

  return (
    <div>
      <PageHeader
        titulo="Quando uma NR passa a ser obrigatória"
        subtitulo="O sistema procura o perigo (ou as palavras) no PGR de cada função e sugere o curso — sempre como sugestão para o Engenheiro de Segurança aprovar na obra."
        acoes={<Button appearance="primary" onClick={() => abrir({ ...vazia })}>+ Nova regra</Button>}
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <div style={{ marginBottom: 12 }}>
        <FeedbackInline tom="info">
          Mudar uma regra não altera as obras na hora: cada obra recebe a mudança quando o PGR dela for aprovado de novo (próxima
          revisão pela EAP). Use <b>Simular</b> para ver antes quem seria afetado.
        </FeedbackInline>
      </div>
      <Card densidade="compacta">
        <DataTable
          aria-label="Regras de curso obrigatório por NR"
          densidade="compacta"
          colunas={colunas}
          linhas={regras}
          chaveLinha={(r) => r.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma regra cadastrada', descricao: 'Crie a primeira regra com "+ Nova regra".' }}
          aoClicarLinha={(r) => abrir({ ...r, palavrasChave: r.palavrasChave ?? '', perigos: r.perigos ?? '' })}
        />
      </Card>
      <Text size={200} style={{ display: 'block', marginTop: 12 }}>
        Valores iniciais propostos pelo sistema — o QSMS deve validar antes do uso, assim como foi feito com os Requisitos Legais.
      </Text>

      <Dialog open={editando !== null} onOpenChange={(_, d) => !d.open && setEditando(null)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>{editando?.id ? 'Editar regra' : 'Nova regra'}</DialogTitle>
            <DialogContent>
              {editando && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                  <Field label="NR" required hint="No formato NR-35">
                    <Input value={editando.norma} onChange={(_, d) => setEditando({ ...editando, norma: d.value })} />
                  </Field>
                  <Field label="Descrição" required>
                    <Input value={editando.descricao} onChange={(_, d) => setEditando({ ...editando, descricao: d.value })} />
                  </Field>
                  <Field label="Curso do catálogo" hint="Em branco, o sistema usa o curso do catálogo com esta NR.">
                    <Select
                      value={editando.cursoTreinamentoId ?? ''}
                      onChange={(_, d) => setEditando({ ...editando, cursoTreinamentoId: d.value || null })}
                    >
                      <option value="">Automático pela NR</option>
                      {cursos.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.normaReferencia ? `${c.normaReferencia} · ${c.nome}` : c.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <Checkbox
                    label="Obrigatório para todas as funções"
                    checked={editando.aplicaTodasFuncoes}
                    onChange={(_, d) => setEditando({ ...editando, aplicaTodasFuncoes: !!d.checked })}
                  />
                  {!editando.aplicaTodasFuncoes && (
                    <Field label="Perigos do PGR" hint="Mais preciso: o PGR gerado pela EAP usa estes nomes padronizados.">
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, maxHeight: 160, overflowY: 'auto' }}>
                        {perigosPadrao.map((p) => {
                          const marcados = listaPerigos(editando.perigos);
                          const marcado = marcados.includes(p);
                          return (
                            <button
                              key={p}
                              type="button"
                              aria-pressed={marcado}
                              onClick={() =>
                                setEditando({
                                  ...editando,
                                  perigos: (marcado ? marcados.filter((x) => x !== p) : [...marcados, p]).join(', '),
                                })
                              }
                              style={{
                                borderRadius: 999,
                                padding: '4px 10px',
                                fontSize: 12,
                                cursor: 'pointer',
                                border: `1px solid ${marcado ? '#670000' : '#d1d5db'}`,
                                background: marcado ? '#670000' : '#fff',
                                color: marcado ? '#fff' : '#374151',
                              }}
                            >
                              {p}
                            </button>
                          );
                        })}
                      </div>
                    </Field>
                  )}
                  {!editando.aplicaTodasFuncoes && (
                    <Field label="Palavras no PGR (reserva)" hint="Para PGR importado ou o que não tem perigo próprio. Separadas por vírgula; acento e maiúscula não importam.">
                      <Textarea
                        value={editando.palavrasChave ?? ''}
                        onChange={(_, d) => setEditando({ ...editando, palavrasChave: d.value })}
                        maxLength={1000}
                      />
                    </Field>
                  )}
                  <Checkbox
                    label="Regra ativa"
                    checked={editando.ativo}
                    onChange={(_, d) => setEditando({ ...editando, ativo: !!d.checked })}
                  />
                  {simulacao && (
                    <div style={{ border: '1px solid #e5e7eb', borderRadius: 8, padding: 10 }}>
                      <Text weight="semibold" style={{ display: 'block', marginBottom: 6 }}>
                        Simulação: {simulacao.itens.length} função(ões) receberiam {simulacao.cursoNome ?? 'o curso (sem curso desta NR no catálogo)'}
                      </Text>
                      <Text size={200} style={{ display: 'block', marginBottom: 6 }}>
                        Analisadas {simulacao.obrasAnalisadas} obra(s) com PGR. Nada foi gravado.
                      </Text>
                      <div style={{ maxHeight: 200, overflowY: 'auto' }}>
                        {simulacao.itens.length === 0 ? (
                          <Text size={200}>Nenhuma função seria afetada.</Text>
                        ) : (
                          simulacao.itens.map((i, n) => (
                            <div key={n} style={{ fontSize: 12, padding: '4px 0', borderTop: '1px solid #f0f0f0' }}>
                              <b>{i.obraNome}</b> · {i.funcaoNome}
                              <span style={{ display: 'block', color: '#6b7280' }}>{i.motivo}</span>
                            </div>
                          ))
                        )}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setEditando(null)}>Cancelar</Button>
              <Button onClick={simular} disabled={simulando || !editando?.norma}>
                {simulando ? 'Simulando…' : 'Simular'}
              </Button>
              <Button appearance="primary" onClick={salvar} disabled={salvando}>{salvando ? 'Salvando…' : 'Salvar'}</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
