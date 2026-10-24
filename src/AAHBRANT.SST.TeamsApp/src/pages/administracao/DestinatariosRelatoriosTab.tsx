import { useCallback, useEffect, useState } from 'react';
import {
  BotaoAcao,
  Button,
  Card,
  Checkbox,
  DataTable,
  EstadoVazio,
  FeedbackInline,
  Field,
  Legenda,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, PersonAdd24Regular } from '@fluentui/react-icons';
import { api, StatusUsuario, type DestinatarioRelatorio, type Obra, type Usuario } from '../../lib/api';

const TODAS_AS_OBRAS = '';

function mensagemDoErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trecho = e.message.match(/\{.*\}$/);
  if (trecho) {
    try {
      const corpo = JSON.parse(trecho[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON
    }
  }
  return e.message || fallback;
}

// Destinatários dos relatórios (lista de presença às 08:00, boletim semanal de segunda, aviso de ocorrência): quem
// recebe quais relatórios de quais obras. Obra em branco = todas as obras (diretoria, consolidado). Só Administrador.
// "Sugerir pelos perfis" pré-preenche com os Técnicos e Engenheiros de Segurança de cada obra.
export function DestinatariosRelatoriosTab() {
  const [lista, setLista] = useState<DestinatarioRelatorio[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();

  const [novoUsuarioId, setNovoUsuarioId] = useState('');
  const [novaObraId, setNovaObraId] = useState(TODAS_AS_OBRAS);
  const [novoPresenca, setNovoPresenca] = useState(true);
  const [novoBoletim, setNovoBoletim] = useState(true);
  const [novoOcorrencia, setNovoOcorrencia] = useState(true);

  const carregar = useCallback(async () => {
    setCarregando(true);
    setErro(null);
    try {
      const [d, u, o] = await Promise.all([
        api.destinatariosRelatorio.listar(),
        api.usuarios.listar(StatusUsuario.Ativo),
        api.obras.listar(),
      ]);
      setLista(d);
      setUsuarios(u);
      setObras(o);
    } catch (e) {
      setErro(mensagemDoErro(e, 'Não foi possível carregar os destinatários.'));
    } finally {
      setCarregando(false);
    }
  }, []);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  async function executar(acao: () => Promise<void>, sucesso?: string) {
    setOcupado(true);
    setErro(null);
    setAviso(null);
    try {
      await acao();
      await carregar();
      if (sucesso) setAviso(sucesso);
    } catch (e) {
      setErro(mensagemDoErro(e, 'Não foi possível salvar.'));
    } finally {
      setOcupado(false);
    }
  }

  const adicionar = () =>
    executar(
      async () => {
        await api.destinatariosRelatorio.salvar({
          usuarioId: novoUsuarioId,
          obraId: novaObraId || null,
          listaPresenca: novoPresenca,
          boletimSemanal: novoBoletim,
          ocorrencia: novoOcorrencia,
        });
        setNovoUsuarioId('');
      },
      'Destinatário salvo.',
    );

  const sugerir = () =>
    executar(async () => {
      const r = await api.destinatariosRelatorio.sugerirPorPerfil();
      setAviso(
        r.criados === 0
          ? 'Nenhum cadastro novo: todos os técnicos e engenheiros de segurança já estão na lista.'
          : `${r.criados} cadastro(s) criado(s) a partir dos perfis. Ajuste o que cada pessoa recebe abaixo.`,
      );
    });

  const alternar = (d: DestinatarioRelatorio, campo: 'listaPresenca' | 'boletimSemanal' | 'ocorrencia', valor: boolean) => {
    const proximo = { listaPresenca: d.listaPresenca, boletimSemanal: d.boletimSemanal, ocorrencia: d.ocorrencia, [campo]: valor };
    if (!proximo.listaPresenca && !proximo.boletimSemanal && !proximo.ocorrencia) {
      setErro('Marque ao menos um relatório. Para não receber nada, remova o destinatário.');
      return;
    }
    return executar(async () => {
      await api.destinatariosRelatorio.salvar({ usuarioId: d.usuarioId, obraId: d.obraId, ...proximo });
    });
  };

  const remover = async (d: DestinatarioRelatorio) => {
    const ok = await confirmar({
      titulo: 'Remover destinatário',
      mensagem: `${d.usuarioNome} deixa de receber os relatórios de ${d.obraNome ?? 'todas as obras'}.`,
      rotuloConfirmar: 'Remover',
    });
    if (ok) await executar(() => api.destinatariosRelatorio.remover(d.id), 'Destinatário removido.');
  };

  const colunas: Coluna<DestinatarioRelatorio>[] = [
    {
      chave: 'usuario',
      rotulo: 'Usuário',
      render: (d) => (
        <div>
          <div>{d.usuarioNome}</div>
          {d.usuarioEmail && <Legenda>{d.usuarioEmail}</Legenda>}
        </div>
      ),
    },
    { chave: 'obra', rotulo: 'Obra', render: (d) => d.obraNome ?? 'Todas as obras' },
    {
      chave: 'presenca',
      rotulo: 'Lista de presença',
      render: (d) => (
        <Checkbox
          checked={d.listaPresenca}
          disabled={ocupado}
          aria-label={`${d.usuarioNome}: lista de presença`}
          onChange={(_, v) => void alternar(d, 'listaPresenca', !!v.checked)}
        />
      ),
    },
    {
      chave: 'boletim',
      rotulo: 'Boletim semanal',
      render: (d) => (
        <Checkbox
          checked={d.boletimSemanal}
          disabled={ocupado}
          aria-label={`${d.usuarioNome}: boletim semanal`}
          onChange={(_, v) => void alternar(d, 'boletimSemanal', !!v.checked)}
        />
      ),
    },
    {
      chave: 'ocorrencia',
      rotulo: 'Ocorrência',
      render: (d) => (
        <Checkbox
          checked={d.ocorrencia}
          disabled={ocupado}
          aria-label={`${d.usuarioNome}: ocorrência`}
          onChange={(_, v) => void alternar(d, 'ocorrencia', !!v.checked)}
        />
      ),
    },
    {
      chave: 'app',
      rotulo: 'Sininho',
      render: (d) =>
        d.jaEntrouNoApp ? (
          <StatusChip tom="ok">Pronto</StatusChip>
        ) : (
          <StatusChip tom="atencao">Ainda não abriu o app</StatusChip>
        ),
    },
  ];

  return (
    <div style={{ display: 'grid', gap: 16 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {aviso && <FeedbackInline tom="sucesso" aoFechar={() => setAviso(null)}>{aviso}</FeedbackInline>}

      <Card titulo="Destinatários dos relatórios">
        <div style={{ display: 'grid', gap: 12 }}>
          <Legenda>
            Quem recebe a <strong>lista de presença</strong> (todo dia, 08:00), o <strong>boletim semanal</strong> (segunda, 07:00) e o{' '}
            <strong>aviso de ocorrência</strong> (na hora), por obra. Escolha &quot;Todas as obras&quot; para a diretoria receber o
            consolidado. O aviso chega no sininho do Teams; quem ainda não abriu o app recebe pelo e-mail, mas o app precisa estar
            instalado no Teams dele.
          </Legenda>
          <div>
            <Button icon={<PersonAdd24Regular />} disabled={ocupado} onClick={() => void sugerir()}>
              Sugerir pelos perfis
            </Button>
          </div>
        </div>
      </Card>

      <Card titulo="Adicionar destinatário">
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
          <Field label="Usuário">
            <Select value={novoUsuarioId} onChange={(_, d) => setNovoUsuarioId(d.value)}>
              <option value="">Escolha…</option>
              {usuarios.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Obra">
            <Select value={novaObraId} onChange={(_, d) => setNovaObraId(d.value)}>
              <option value={TODAS_AS_OBRAS}>Todas as obras</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Checkbox checked={novoPresenca} label="Lista de presença" onChange={(_, v) => setNovoPresenca(!!v.checked)} />
          <Checkbox checked={novoBoletim} label="Boletim semanal" onChange={(_, v) => setNovoBoletim(!!v.checked)} />
          <Checkbox checked={novoOcorrencia} label="Ocorrência" onChange={(_, v) => setNovoOcorrencia(!!v.checked)} />
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            disabled={ocupado || !novoUsuarioId || !(novoPresenca || novoBoletim || novoOcorrencia)}
            onClick={() => void adicionar()}
          >
            Adicionar
          </Button>
        </div>
      </Card>

      <Card titulo={`Quem recebe (${lista.length})`}>
        <DataTable
          aria-label="Destinatários dos relatórios"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(d) => d.id}
          carregando={carregando}
          vazio={{
            titulo: 'Ninguém cadastrado ainda',
            descricao: 'Use Sugerir pelos perfis para começar com os técnicos e engenheiros de segurança de cada obra.',
          }}
          acoesLinha={(d) => (
            <BotaoAcao tom="excluir" size="small" icon={<Delete24Regular />} aria-label={`Remover ${d.usuarioNome}`} onClick={() => void remover(d)}>
              Remover
            </BotaoAcao>
          )}
        />
        {!carregando && lista.length === 0 && <EstadoVazio titulo="Ninguém recebe os relatórios por enquanto" />}
      </Card>
      {dialogElement}
    </div>
  );
}
