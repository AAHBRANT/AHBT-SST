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
import { api, type CursoTreinamento, type RegraCursoObrigatorio, type SalvarRegraCursoObrigatorio } from '../../lib/api';

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
  ativo: true,
};

export function RegrasCursoObrigatorioTab() {
  const [regras, setRegras] = useState<RegraCursoObrigatorio[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [editando, setEditando] = useState<SalvarRegraCursoObrigatorio | null>(null);
  const [salvando, setSalvando] = useState(false);

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
      render: (r) => <Text>{r.aplicaTodasFuncoes ? '—' : r.palavrasChave}</Text>,
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
        subtitulo="O sistema procura estas palavras nos perigos, atividades e ferramentas do PGR de cada função e sugere o curso — sempre como sugestão para o Engenheiro de Segurança aprovar na obra."
        acoes={<Button appearance="primary" onClick={() => setEditando({ ...vazia })}>+ Nova regra</Button>}
      />
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <Card densidade="compacta">
        <DataTable
          aria-label="Regras de curso obrigatório por NR"
          densidade="compacta"
          colunas={colunas}
          linhas={regras}
          chaveLinha={(r) => r.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma regra cadastrada', descricao: 'Crie a primeira regra com "+ Nova regra".' }}
          aoClicarLinha={(r) => setEditando({ ...r, palavrasChave: r.palavrasChave ?? '' })}
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
                    <Field label="Aplica quando o PGR tiver" required hint="Palavras separadas por vírgula; acento e maiúscula não importam.">
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
                </div>
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setEditando(null)}>Cancelar</Button>
              <Button appearance="primary" onClick={salvar} disabled={salvando}>{salvando ? 'Salvando…' : 'Salvar'}</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
