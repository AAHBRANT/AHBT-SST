import { useEffect, useState } from 'react';
import { Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { Add24Regular, Copy24Regular, Delete24Regular, Warning20Regular } from '@fluentui/react-icons';
import {
  BotaoAcao,
  Button,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  Input,
  Legenda,
  PageHeader,
  Select,
  StatusChip,
  Text,
  useConfirmar,
  type Coluna,
} from '@ui';
import { API_BASE_URL } from '../../lib/apiBase';
import { api, type DispositivoAgente, type Obra, type RegistroDispositivoAgente } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { lerInstanteUtc } from '../../lib/datas';

// Sincronizou nos últimos 10 min = agente conectado (ele sincroniza a cada 2 min por padrão).
const JANELA_ONLINE_MS = 10 * 60 * 1000;

function situacao(d: DispositivoAgente): { tom: 'ok' | 'atencao' | 'info'; texto: string } {
  if (!d.ultimaSincronizacaoEm) return { tom: 'atencao', texto: 'Nunca conectou' };
  const idade = Date.now() - lerInstanteUtc(d.ultimaSincronizacaoEm).getTime();
  return idade <= JANELA_ONLINE_MS ? { tom: 'ok', texto: 'Conectado' } : { tom: 'info', texto: 'Sem sincronizar' };
}

function formatarData(iso: string): string {
  return lerInstanteUtc(iso).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'short' });
}

// PowerShell: aspas simples literais; ' dentro do valor vira ''.
function aspas(valor: string): string {
  return `'${valor.replace(/'/g, "''")}'`;
}

function montarComandoInstalacao(registro: RegistroDispositivoAgente): string {
  return [
    '.\\instalar.ps1',
    `-DispositivoId ${registro.dispositivoId}`,
    `-SegredoDispositivo ${aspas(registro.segredo)}`,
    `-BackendBaseUrl ${aspas(API_BASE_URL)}`,
    `-OrigemPermitida ${aspas(window.location.origin)}`,
  ].join(' ');
}

function LinhaCopiavel({ rotulo, valor, multilinha }: { rotulo: string; valor: string; multilinha?: boolean }) {
  const [copiado, setCopiado] = useState(false);

  async function copiar() {
    try {
      await navigator.clipboard.writeText(valor);
      setCopiado(true);
      window.setTimeout(() => setCopiado(false), 2000);
    } catch {
      // Sem permissão de área de transferência: o texto continua visível para copiar à mão.
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <Text weight="semibold">{rotulo}</Text>
      <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
        <code
          style={{
            flex: 1,
            background: 'var(--colorNeutralBackground3)',
            border: '1px solid var(--colorNeutralStroke1)',
            borderRadius: 4,
            padding: '6px 8px',
            fontSize: 12,
            wordBreak: 'break-all',
            whiteSpace: multilinha ? 'pre-wrap' : 'normal',
          }}
        >
          {valor}
        </code>
        <Button size="small" icon={<Copy24Regular />} onClick={() => void copiar()}>
          {copiado ? 'Copiado' : 'Copiar'}
        </Button>
      </div>
    </div>
  );
}

// Registro dos agentes de biometria (um por PC de obra com leitor Futronic). O segredo do dispositivo
// só existe na resposta do registro — no banco fica o hash —, por isso o diálogo o mostra uma única vez.
export function LeitoresDigitalTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [dispositivos, setDispositivos] = useState<DispositivoAgente[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [obraId, setObraId] = useState('');
  const [nome, setNome] = useState('');
  const [registrando, setRegistrando] = useState(false);
  const [registro, setRegistro] = useState<RegistroDispositivoAgente | null>(null);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaObras, lista] = await Promise.all([api.obras.listar(), api.dispositivosAgente.listar()]);
      setObras(listaObras);
      setDispositivos(lista);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os leitores.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    void carregar();
  }, []);

  async function registrar() {
    if (!obraId || !nome.trim()) return;
    try {
      setRegistrando(true);
      setErro(null);
      setRegistro(await api.dispositivosAgente.registrar(obraId, nome.trim()));
      setNome('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar o leitor.');
    } finally {
      setRegistrando(false);
    }
  }

  async function revogar(d: DispositivoAgente) {
    if (!(await confirmar(`Revogar o leitor "${d.nome}" (${d.obraNome})? O agente instalado nesse PC deixará de funcionar.`))) return;
    try {
      await api.dispositivosAgente.revogar(d.id);
      await carregar();
      sucessoToast('Leitor revogado.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao revogar o leitor.');
    }
  }

  const colunas: Coluna<DispositivoAgente>[] = [
    { chave: 'obraNome', rotulo: 'Obra' },
    { chave: 'nome', rotulo: 'PC / leitor' },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (d) => {
        const s = situacao(d);
        return <StatusChip tom={s.tom}>{s.texto}</StatusChip>;
      },
    },
    {
      chave: 'ultimaSincronizacaoEm',
      rotulo: 'Última sincronização',
      render: (d) => (d.ultimaSincronizacaoEm ? formatarData(d.ultimaSincronizacaoEm) : '—'),
    },
    { chave: 'registradoEm', rotulo: 'Registrado em', render: (d) => formatarData(d.registradoEm) },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader titulo="Leitores de digital (Futronic)" />

      <Card titulo="Registrar leitor">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <Legenda>
            Cada PC de obra com leitor Futronic precisa ser registrado uma vez. O sistema gera o Id e o segredo que o
            agente usa para sincronizar as digitais da obra e validar assinaturas.
          </Legenda>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
            <div style={{ flex: '1 1 220px' }}>
              <Field label="Obra">
                <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                  <option value="">Selecione a obra…</option>
                  {obras.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.codigo} — {o.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            <div style={{ flex: '1 1 220px' }}>
              <Field label="Nome do PC / posto">
                <Input value={nome} maxLength={150} placeholder="Ex.: PC da portaria" onChange={(_, d) => setNome(d.value)} />
              </Field>
            </div>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              disabled={registrando || !obraId || !nome.trim()}
              onClick={() => void registrar()}
            >
              Registrar leitor
            </Button>
          </div>
        </div>
      </Card>

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Leitores registrados">
        <DataTable
          aria-label="Leitores de digital registrados"
          colunas={colunas}
          linhas={dispositivos}
          chaveLinha={(d) => d.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum leitor registrado ainda.', descricao: 'Registre o primeiro leitor acima.' }}
          acoesLinha={(d) => (
            <BotaoAcao
              tom="excluir"
              icon={<Delete24Regular />}
              onClick={(evento) => {
                evento.stopPropagation();
                void revogar(d);
              }}
              aria-label={`Revogar ${d.nome}`}
            />
          )}
        />
      </Card>

      <Dialog open={registro !== null} onOpenChange={(_, d) => !d.open && setRegistro(null)}>
        <DialogSurface style={{ maxWidth: 680 }}>
          <DialogBody>
            <DialogTitle>Leitor registrado</DialogTitle>
            <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
                <Warning20Regular aria-hidden />
                <Text>
                  Guarde estes dados agora: o <strong>segredo não será mostrado novamente</strong>. Se perder, revogue o
                  leitor e registre outro.
                </Text>
              </div>
              {registro && (
                <>
                  <LinhaCopiavel rotulo="Id do dispositivo" valor={registro.dispositivoId} />
                  <LinhaCopiavel rotulo="Segredo do dispositivo" valor={registro.segredo} />
                  <LinhaCopiavel
                    rotulo="Comando de instalação (rode na pasta do pacote do agente, no PC da obra)"
                    valor={montarComandoInstalacao(registro)}
                    multilinha
                  />
                  <Legenda>
                    O instalador ainda pergunta a chave de criptografia de biometria (a mesma configurada na API), que não
                    é exibida aqui por segurança.
                  </Legenda>
                </>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="primary" onClick={() => setRegistro(null)}>
                Já guardei, fechar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
