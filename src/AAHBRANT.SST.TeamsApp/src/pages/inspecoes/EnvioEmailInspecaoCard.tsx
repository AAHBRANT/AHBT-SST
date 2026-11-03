import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Checkbox, Input, Text, Textarea } from '@fluentui/react-components';
import { Delete24Regular, Mail24Regular } from '@fluentui/react-icons';
import { BotaoAcao, Card, FeedbackInline, StatusChip } from '@ui';
import { api, type EnvioEmailInspecaoPainel } from '../../lib/api';

// Envio do relatório da inspeção por e-mail (mockup aprovado em 03/11/2026,
// docs/mockups/inspecao-envio-email_2026-11-03_v1.html): contatos padrão da obra e "Meus contatos",
// e-mail avulso com opção de salvar, mensagem opcional e histórico de envios. Só para inspeção Concluída.

const LIMITE_DESTINATARIOS = 20;
const emailValido = (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim());
const formatarDataHora = (iso: string) =>
  // O servidor grava em UTC, mas o JSON pode vir sem o "Z".
  new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

interface Avulso { email: string; salvar: boolean }

const linha = { display: 'flex', alignItems: 'center', gap: 12, padding: '10px 12px', borderBottom: '1px solid var(--sst-color-card-border, #e0e0e0)' } as const;
const selo = (obra: boolean) => ({
  marginLeft: 'auto', fontSize: 11, padding: '2px 8px', borderRadius: 10, whiteSpace: 'nowrap' as const,
  background: obra ? '#ebe9ad' : '#f1e6e6', color: obra ? '#4a4600' : '#670000',
});

export function EnvioEmailInspecaoCard({ inspecaoId }: { inspecaoId: string }) {
  const [painel, setPainel] = useState<EnvioEmailInspecaoPainel | null>(null);
  const [marcados, setMarcados] = useState<Set<string>>(new Set());
  const [avulsos, setAvulsos] = useState<Avulso[]>([]);
  const [novoEmail, setNovoEmail] = useState('');
  const [salvarNovo, setSalvarNovo] = useState(true);
  const [salvarNaObra, setSalvarNaObra] = useState(false);
  const [mensagem, setMensagem] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [sucesso, setSucesso] = useState<string | null>(null);

  const carregar = useCallback(async () => {
    try {
      const p = await api.inspecoes.envioEmail(inspecaoId);
      setPainel(p);
      // Padrão da obra já vem marcado, como no mockup; "Meus contatos" o usuário escolhe.
      setMarcados(new Set(p.contatos.filter(c => c.padraoObra).map(c => c.email)));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os contatos de e-mail.');
    }
  }, [inspecaoId]);

  useEffect(() => { void carregar(); }, [carregar]);

  const selecionados = useMemo(
    () => Array.from(new Set([...marcados, ...avulsos.map(a => a.email)])),
    [marcados, avulsos]);

  function alternar(email: string) {
    setMarcados(atual => {
      const novo = new Set(atual);
      if (novo.has(email)) novo.delete(email); else novo.add(email);
      return novo;
    });
  }

  function adicionar() {
    const email = novoEmail.trim().toLowerCase();
    if (!emailValido(email)) { setErro('Digite um e-mail válido.'); return; }
    setErro(null);
    if (painel?.contatos.some(c => c.email === email)) setMarcados(atual => new Set(atual).add(email));
    else if (!avulsos.some(a => a.email === email)) setAvulsos(atual => [...atual, { email, salvar: salvarNovo }]);
    setNovoEmail('');
  }

  async function remover(contatoId: string) {
    try {
      await api.inspecoes.removerContatoEmail(contatoId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover o contato.');
    }
  }

  async function enviar() {
    if (selecionados.length === 0) { setErro('Selecione ao menos um destinatário.'); return; }
    if (selecionados.length > LIMITE_DESTINATARIOS) { setErro(`No máximo ${LIMITE_DESTINATARIOS} destinatários por envio.`); return; }
    setEnviando(true);
    setErro(null);
    setSucesso(null);
    try {
      const envio = await api.inspecoes.enviarEmail(inspecaoId, {
        emails: selecionados,
        mensagem: mensagem.trim() || null,
        salvarEmails: avulsos.filter(a => a.salvar).map(a => a.email),
        salvarNaObra,
      });
      setSucesso(envio.falhas.length > 0
        ? `Enviado para ${envio.destinatarios.length}. Não foi possível enviar para: ${envio.falhas.join(', ')}.`
        : `Relatório enviado para ${envio.destinatarios.length} destinatário(s).`);
      setAvulsos([]);
      setMensagem('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o relatório.');
    } finally {
      setEnviando(false);
    }
  }

  if (!painel) return erro ? <FeedbackInline tom="erro">{erro}</FeedbackInline> : null;
  if (!painel.podeEnviar) return null;

  return (
    <>
      <Card
        titulo="Enviar relatório por e-mail"
        subtitulo="O PDF assinado da inspeção vai em anexo, pela caixa sst@aahbrant.com."
        acoes={<StatusChip tom="ok">Encerrada e assinada</StatusChip>}
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
          {sucesso && <FeedbackInline tom="sucesso" aoFechar={() => setSucesso(null)}>{sucesso}</FeedbackInline>}

          <div>
            <Text weight="semibold" size={200}>DESTINATÁRIOS</Text>
            <div style={{ marginTop: 8, border: '1px solid var(--sst-color-card-border, #e0e0e0)', borderRadius: 8, overflow: 'hidden' }}>
              {painel.contatos.length === 0 && avulsos.length === 0 && (
                <div style={linha}><Text size={200}>Nenhum contato salvo ainda. Adicione um e-mail abaixo.</Text></div>
              )}
              {painel.contatos.map(c => (
                <div key={c.id} style={linha}>
                  <Checkbox checked={marcados.has(c.email)} onChange={() => alternar(c.email)} aria-label={`Enviar para ${c.email}`} />
                  <div style={{ minWidth: 0, overflowWrap: 'anywhere' }}>
                    {c.nome && <Text weight="semibold" block>{c.nome}</Text>}
                    <Text size={200}>{c.email}</Text>
                  </div>
                  <span style={selo(c.padraoObra)}>{c.padraoObra ? 'Padrão da obra' : 'Meus contatos'}</span>
                  <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => void remover(c.id)} aria-label={`Remover ${c.email} dos contatos`} />
                </div>
              ))}
              {avulsos.map(a => (
                <div key={a.email} style={linha}>
                  <Checkbox checked onChange={() => setAvulsos(atual => atual.filter(x => x.email !== a.email))} aria-label={`Enviar para ${a.email}`} />
                  <Text size={200} style={{ overflowWrap: 'anywhere' }}>{a.email}</Text>
                  <span style={selo(salvarNaObra)}>{a.salvar ? (salvarNaObra ? 'Será salvo na obra' : 'Será salvo nos meus contatos') : 'Só neste envio'}</span>
                </div>
              ))}
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
              <Input
                type="email"
                value={novoEmail}
                onChange={(_, d) => setNovoEmail(d.value)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); adicionar(); } }}
                placeholder="Adicionar outro e-mail"
                style={{ flex: 1, minWidth: 200 }}
              />
              <Button onClick={adicionar}>Adicionar</Button>
            </div>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginTop: 4 }}>
              <Checkbox checked={salvarNovo} onChange={(_, d) => setSalvarNovo(Boolean(d.checked))} label="Salvar este e-mail para as próximas vezes" />
              <Checkbox checked={salvarNaObra} onChange={(_, d) => setSalvarNaObra(Boolean(d.checked))} label="Salvar como padrão da obra (todos os técnicos veem)" />
            </div>
          </div>

          <div>
            <Text weight="semibold" size={200}>MENSAGEM (OPCIONAL)</Text>
            <Textarea
              value={mensagem}
              onChange={(_, d) => setMensagem(d.value)}
              maxLength={2000}
              placeholder="Ex.: Segue o relatório da patrulha de hoje, com 2 itens para tratativa."
              style={{ width: '100%', marginTop: 8 }}
              resize="vertical"
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <Text size={200}>{selecionados.length} destinatário(s) selecionado(s) · PDF assinado em anexo</Text>
            <Button appearance="primary" icon={<Mail24Regular />} onClick={() => void enviar()} disabled={enviando || selecionados.length === 0}>
              {enviando ? 'Enviando…' : 'Enviar relatório'}
            </Button>
          </div>
        </div>
      </Card>

      {painel.envios.length > 0 && (
        <Card densidade="compacta" titulo="Envios desta inspeção">
          <div style={{ display: 'flex', flexDirection: 'column' }}>
            {painel.envios.map(e => (
              <div key={e.id} style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', padding: '8px 0', borderBottom: '1px dashed var(--sst-color-card-border, #e0e0e0)' }}>
                <Text size={200}>{formatarDataHora(e.enviadoEmUtc)} · por {e.enviadoPorNome}</Text>
                <Text size={200} style={{ overflowWrap: 'anywhere' }}>
                  {e.destinatarios.join(', ')}
                  {e.destinatarios.length > 0 && <> · <span style={{ color: '#0e7a3e', fontWeight: 600 }}>Enviado</span></>}
                  {e.falhas.length > 0 && <> · <span style={{ color: '#b10e1c', fontWeight: 600 }}>Falhou: {e.falhas.join(', ')}</span></>}
                </Text>
              </div>
            ))}
          </div>
        </Card>
      )}
    </>
  );
}
