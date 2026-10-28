import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  FeedbackInline,
  Input,
  Legenda,
  Select,
  StatusChip,
} from '@ui';
import { Fingerprint24Regular } from '@fluentui/react-icons';
import {
  api,
  ConselhoInstrutor,
  FormacaoInstrutor,
  StatusSessaoTreinamento,
  type PerfilInstrutor,
  type SessaoTreinamentoDetalhe,
  type Trabalhador,
} from '../../lib/api';
import { capturarDigitalLocal, credencialLeitura, type DispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { tocarBipeAssinaturaAceita } from '../../lib/bipeAssinatura';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

interface Props {
  detalhe: SessaoTreinamentoDetalhe;
  agenteDisponivel: boolean | null;
  dispositivoLocal: DispositivoLocal | null;
  aoAtualizar: () => Promise<void> | void;
}

// Instrutor da turma (28/10). O instrutor é escolhido entre os perfis cadastrados e assina todos os
// certificados da turma com a digital dele, mesmo que seja de outra obra (a digital dele é sincronizada
// no leitor desta obra quando o perfil autoriza). Antes a "assinatura de instrutor" era a de quem estava
// logado ao encerrar a turma.
export function BlocoInstrutorTurma({ detalhe, agenteDisponivel, dispositivoLocal, aoAtualizar }: Props) {
  const souAdministrador = useSouAdministrador();
  const { sessao, instrutor } = detalhe;
  const concluida = sessao.status === StatusSessaoTreinamento.Concluida;
  const [perfis, setPerfis] = useState<PerfilInstrutor[]>([]);
  const [escolhido, setEscolhido] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [mensagem, setMensagem] = useState<{ tom: 'sucesso' | 'erro' | 'info'; texto: string } | null>(null);
  const [cadastrando, setCadastrando] = useState(false);

  async function carregarPerfis() {
    try {
      setPerfis(await api.instrutores.listar());
    } catch {
      setPerfis([]);
    }
  }

  useEffect(() => {
    void carregarPerfis();
  }, []);

  const todosAssinaram = !!instrutor && instrutor.totalCertificados > 0 && instrutor.certificadosAssinados >= instrutor.totalCertificados;
  const podeTrocar = !instrutor || instrutor.certificadosAssinados === 0;

  async function escolher() {
    if (!escolhido) return;
    try {
      setOcupado(true);
      setMensagem(null);
      await api.sessoesTreinamento.definirInstrutor(sessao.id, escolhido);
      setEscolhido('');
      await aoAtualizar();
    } catch (e) {
      setMensagem({ tom: 'erro', texto: e instanceof Error ? e.message : 'Falha ao definir o instrutor.' });
    } finally {
      setOcupado(false);
    }
  }

  async function assinar() {
    if (!instrutor || !dispositivoLocal) return;
    try {
      setOcupado(true);
      setMensagem({ tom: 'info', texto: `${instrutor.nome}: encoste o dedo no leitor.` });
      const captura = await capturarDigitalLocal(dispositivoLocal.dispositivoId);
      const resultado = await api.sessoesTreinamento.assinarComoInstrutor(
        sessao.id, credencialLeitura(dispositivoLocal, captura.leituras[0]), captura.trabalhadorId, captura.score, captura.imagemPng,
      );
      tocarBipeAssinaturaAceita();
      if (resultado.falhas > 0) {
        setMensagem({ tom: 'erro', texto: `Assinados ${resultado.assinados}, com ${resultado.falhas} falha(s): ${resultado.primeiraFalha ?? 'tente de novo.'}` });
      } else {
        setMensagem({ tom: 'sucesso', texto: `Instrutor assinou ${resultado.assinados} certificado(s).` });
      }
      await aoAtualizar();
    } catch (e) {
      setMensagem({ tom: 'erro', texto: e instanceof Error ? e.message : 'Não consegui assinar. Tente de novo.' });
    } finally {
      setOcupado(false);
    }
  }

  return (
    <Card titulo="Instrutor da turma">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {instrutor ? (
          <div style={{ display: 'flex', gap: 12, justifyContent: 'space-between', flexWrap: 'wrap', alignItems: 'center' }}>
            <div>
              <div style={{ fontWeight: 600 }}>{instrutor.nome}</div>
              <Legenda>
                {instrutor.formacaoDescricao}
                {instrutor.registroDescricao ? ` · ${instrutor.registroDescricao}` : ''}
              </Legenda>
            </div>
            <StatusChip tom={todosAssinaram ? 'ok' : 'atencao'}>
              {todosAssinaram ? 'Instrutor assinou' : `Assinatura pendente (${instrutor.certificadosAssinados}/${instrutor.totalCertificados})`}
            </StatusChip>
          </div>
        ) : (
          <FeedbackInline tom="aviso">Esta turma ainda não tem instrutor escolhido.</FeedbackInline>
        )}

        {podeTrocar && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <Field label={instrutor ? 'Trocar instrutor' : 'Escolher instrutor'}>
              <Select value={escolhido} onChange={(_, d) => setEscolhido(d.value)} aria-label="Instrutor da turma">
                <option value="">Selecione…</option>
                {perfis.map((p) => (
                  <option key={p.id} value={p.trabalhadorId}>
                    {p.trabalhadorNome} — {p.formacaoDescricao}
                  </option>
                ))}
              </Select>
            </Field>
            <Button appearance="primary" disabled={!escolhido || ocupado} onClick={escolher}>
              Definir instrutor
            </Button>
            {souAdministrador && (
              <Button appearance="secondary" onClick={() => setCadastrando(true)}>
                Cadastrar instrutor
              </Button>
            )}
          </div>
        )}

        {instrutor && concluida && !todosAssinaram && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div>
              <Button
                appearance="primary"
                icon={<Fingerprint24Regular />}
                onClick={assinar}
                disabled={ocupado || !agenteDisponivel || !dispositivoLocal}
              >
                Assinar como instrutor · digital
              </Button>
            </div>
            <Legenda>
              {agenteDisponivel === false
                ? 'Leitor não encontrado nesta máquina. Verifique o Agente Biométrico.'
                : 'O instrutor encosta o dedo uma vez e assina todos os certificados da turma.'}
            </Legenda>
          </div>
        )}
        {instrutor && !concluida && <Legenda>A assinatura do instrutor fica disponível depois de encerrar a turma.</Legenda>}

        {mensagem && <FeedbackInline tom={mensagem.tom}>{mensagem.texto}</FeedbackInline>}
      </div>

      {cadastrando && (
        <CadastroInstrutorDialog
          aoFechar={() => setCadastrando(false)}
          aoSalvar={async () => {
            setCadastrando(false);
            await carregarPerfis();
          }}
        />
      )}
    </Card>
  );
}

// Cadastro do perfil de instrutor — só Administrador, porque autorizar o uso em outras obras espalha a
// digital do instrutor para os leitores delas (dado biométrico sensível, LGPD).
function CadastroInstrutorDialog({ aoFechar, aoSalvar }: { aoFechar: () => void; aoSalvar: () => Promise<void> }) {
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [trabalhadorId, setTrabalhadorId] = useState('');
  const [formacao, setFormacao] = useState<number>(FormacaoInstrutor.TecnicoSegurancaTrabalho);
  const [conselho, setConselho] = useState<number>(ConselhoInstrutor.Mte);
  const [numero, setNumero] = useState('');
  const [uf, setUf] = useState('');
  const [outrasObras, setOutrasObras] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    api.trabalhadores.listar().then(setTrabalhadores).catch(() => setTrabalhadores([]));
  }, []);

  async function salvar() {
    try {
      setSalvando(true);
      setErro(null);
      await api.instrutores.salvar({
        trabalhadorId,
        formacao,
        conselho: numero.trim() ? conselho : null,
        numeroRegistro: numero.trim() || null,
        ufRegistro: uf.trim() || null,
        podeAssinarEmOutrasObras: outrasObras,
      });
      await aoSalvar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o perfil de instrutor.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Dialog open onOpenChange={(_, d) => { if (!d.open) aoFechar(); }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Cadastrar instrutor</DialogTitle>
          <DialogContent>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <Field label="Trabalhador">
                <Select value={trabalhadorId} onChange={(_, d) => setTrabalhadorId(d.value)} aria-label="Trabalhador">
                  <option value="">Selecione…</option>
                  {trabalhadores.map((t) => (
                    <option key={t.id} value={t.id}>{t.nome}</option>
                  ))}
                </Select>
              </Field>
              <Field label="Formação">
                <Select value={String(formacao)} onChange={(_, d) => setFormacao(Number(d.value))} aria-label="Formação">
                  <option value={FormacaoInstrutor.TecnicoSegurancaTrabalho}>Técnico de Segurança do Trabalho</option>
                  <option value={FormacaoInstrutor.EngenheiroSegurancaTrabalho}>Engenheiro de Segurança do Trabalho</option>
                </Select>
              </Field>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <Field label="Conselho">
                  <Select value={String(conselho)} onChange={(_, d) => setConselho(Number(d.value))} aria-label="Conselho">
                    <option value={ConselhoInstrutor.Mte}>MTE</option>
                    <option value={ConselhoInstrutor.Crea}>CREA</option>
                  </Select>
                </Field>
                <Field label="Número do registro">
                  <Input value={numero} onChange={(_, d) => setNumero(d.value)} maxLength={30} />
                </Field>
                <Field label="UF">
                  <Input value={uf} onChange={(_, d) => setUf(d.value.toUpperCase())} maxLength={2} style={{ width: 70 }} />
                </Field>
              </div>
              <Checkbox
                checked={outrasObras}
                onChange={(_, d) => setOutrasObras(!!d.checked)}
                label="Autorizado a assinar em outras obras (a digital dele será enviada aos leitores das outras obras)"
              />
              {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={aoFechar}>Cancelar</Button>
            <Button appearance="primary" disabled={!trabalhadorId || salvando} onClick={salvar}>Salvar</Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
