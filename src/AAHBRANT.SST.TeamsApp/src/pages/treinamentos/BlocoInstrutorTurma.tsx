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
import { useSouAdministrador, useUsuarioLogado } from '../../lib/UsuarioLogadoContext';
import { AssinaturaBiometricaBotoes } from '../../components/assinatura/AssinaturaBiometricaBotoes';
import { CadastroBiometriaDialog } from '../integracao/componentes/CadastroBiometriaDialog';
import { dataHoraBrasil } from '../integracao/componentes/helpers';
import { rotuloMetodo } from '../integracao/componentes/helpersInstrutor';

interface Props {
  detalhe: SessaoTreinamentoDetalhe;
  agenteDisponivel: boolean | null;
  dispositivoLocal: DispositivoLocal | null;
  aoAtualizar: () => Promise<void> | void;
}

// Instrutor da turma (28/10). O instrutor é escolhido entre os perfis cadastrados e assina todos os
// certificados da turma, mesmo que seja de outra obra (a digital dele é sincronizada no leitor desta obra
// quando o perfil autoriza). Desde 16/11/2026 ele assina ANTES de encerrar, por digital ou facial (uma
// leitura vale para os certificados de todos os participantes), e o encerramento fica bloqueado até assinar;
// a assinatura automática pelo login saiu. Trocar o instrutor depois de assinado o servidor recusa (400) e a
// mensagem aparece aqui. Turmas ANTIGAS já encerradas com certificados sem a assinatura dele continuam com o
// botão da digital pós-encerramento (rota antiga), que some quando todos os certificados estão assinados.
export function BlocoInstrutorTurma({ detalhe, agenteDisponivel, dispositivoLocal, aoAtualizar }: Props) {
  const souAdministrador = useSouAdministrador();
  const { usuario } = useUsuarioLogado();
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

  const [cadastrandoBiometria, setCadastrandoBiometria] = useState(false);
  const meuPerfilInstrutor = usuario?.trabalhadorId ? perfis.find((p) => p.trabalhadorId === usuario.trabalhadorId) ?? null : null;
  const perfisDisponiveis = meuPerfilInstrutor ? [meuPerfilInstrutor] : perfis;

  // Se a turma ainda não tem instrutor e quem abriu já é instrutor cadastrado, vincula automaticamente a ele mesmo.
  useEffect(() => {
    if (!instrutor && meuPerfilInstrutor && !ocupado && !concluida) {
      void (async () => {
        try {
          setOcupado(true);
          await api.sessoesTreinamento.definirInstrutor(sessao.id, meuPerfilInstrutor.trabalhadorId);
          await aoAtualizar();
        } catch {
          // Ignora se a sessão não puder mais alterar instrutor
        } finally {
          setOcupado(false);
        }
      })();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [instrutor?.trabalhadorId, meuPerfilInstrutor?.trabalhadorId, sessao.id, concluida]);

  const assinouAntes = !!instrutor?.assinouAntesDeEncerrar;
  const todosAssinaram = !!instrutor && (assinouAntes || (instrutor.totalCertificados > 0 && instrutor.certificadosAssinados >= instrutor.totalCertificados));
  // Quando o próprio instrutor está logado (ou já está definido na turma para quem não é admin), fica fixo nele mesmo e não troca por outro.
  const fixoNoInstrutorLogado = !!meuPerfilInstrutor && instrutor?.trabalhadorId === meuPerfilInstrutor.trabalhadorId;
  const podeTrocar = !fixoNoInstrutorLogado && (!instrutor || (souAdministrador && instrutor.certificadosAssinados === 0 && !assinouAntes));
  const semBiometria = !!instrutor && instrutor.temDigital === false && instrutor.temFacial === false;
  const metodoAssinatura = rotuloMetodo(instrutor?.metodoAssinatura);

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
            <StatusChip tom={todosAssinaram ? 'ok' : semBiometria && !concluida ? 'alerta' : 'atencao'}>
              {todosAssinaram
                ? 'Instrutor assinou'
                : concluida
                  ? `Assinatura pendente (${instrutor.certificadosAssinados}/${instrutor.totalCertificados})`
                  : semBiometria
                    ? 'Sem biometria'
                    : 'Assinatura pendente'}
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
                {perfisDisponiveis.map((p) => (
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

        {/* Antes de encerrar: assinatura biométrica do instrutor (16/11/2026). */}
        {instrutor && !concluida && assinouAntes && (
          <FeedbackInline tom="sucesso">
            <b>Instrutor assinou{metodoAssinatura ? ` por ${metodoAssinatura}` : ''}.</b> {instrutor.nome}
            {instrutor.assinadoEmUtc ? ` · ${dataHoraBrasil(instrutor.assinadoEmUtc).replace(' ', ' às ')}` : ''} · vale para os certificados de todos os participantes da turma.
          </FeedbackInline>
        )}
        {instrutor && !concluida && !assinouAntes && semBiometria && (
          <>
            <FeedbackInline tom="erro">
              <b>O instrutor ainda não tem biometria cadastrada.</b> A turma só encerra com a assinatura biométrica dele.
            </FeedbackInline>
            <div>
              <Button appearance="primary" size="large" icon={<Fingerprint24Regular />} onClick={() => setCadastrandoBiometria(true)}>
                Cadastrar biometria do instrutor
              </Button>
            </div>
          </>
        )}
        {instrutor && !concluida && !assinouAntes && !semBiometria && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <AssinaturaBiometricaBotoes
              quem="turma"
              agenteDisponivel={agenteDisponivel}
              dispositivoLocal={dispositivoLocal}
              temDigital={instrutor.temDigital}
              temFacial={instrutor.temFacial}
              aoJaAssinou={() => void aoAtualizar()}
              aoDigital={async (captura, leitor) => {
                await api.sessoesTreinamento.assinarInstrutorDigital(sessao.id, credencialLeitura(leitor, captura.leituras[0]), {
                  trabalhadorId: captura.trabalhadorId,
                  score: captura.score,
                  imagemDigital: captura.imagemPng,
                });
                await aoAtualizar();
              }}
              aoFacial={async (foto) => {
                await api.sessoesTreinamento.assinarInstrutorFacial(sessao.id, foto);
                await aoAtualizar();
              }}
            />
            <Legenda>Uma leitura assina os certificados de todos os participantes. Faça antes de encerrar a turma.</Legenda>
          </div>
        )}

        {/* Só turmas antigas, já encerradas, com certificados ainda sem a assinatura do instrutor (rota antiga). */}
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

        {mensagem && <FeedbackInline tom={mensagem.tom}>{mensagem.texto}</FeedbackInline>}
      </div>

      {cadastrandoBiometria && instrutor && (
        <CadastroBiometriaDialog
          trabalhadorId={instrutor.trabalhadorId}
          trabalhadorNome={instrutor.nome}
          aoFechar={() => setCadastrandoBiometria(false)}
          aoAlterar={() => void aoAtualizar()}
        />
      )}

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
