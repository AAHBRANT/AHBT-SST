import { useEffect, useRef, useState } from 'react';
import { pages } from '@microsoft/teams-js';
import { Card, EstadoVazio, FeedbackInline, Field, PageHeader, Select, Spinner, Button } from '@ui';
import { aguardarInicializacaoTeams } from '../../teams/teamsInit';
import { plataformaApi, type ModuloPlataforma, type ObraModulo } from '../../lib/plataformaApi';
import '../qualidade/plataforma.css';

export function TeamsConfigPage() {
  const [disponivel, setDisponivel] = useState<boolean | null>(null);
  const [modulo, setModulo] = useState<ModuloPlataforma>('qualidade');
  const [obraId, setObraId] = useState('');
  const [obras, setObras] = useState<ObraModulo[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState('');
  const [tentativa, setTentativa] = useState(0);
  const selecao = useRef({ modulo, obraId, valida: false });
  const entityId = useRef(crypto.randomUUID() as string);

  useEffect(() => {
    let ativo = true;
    let registrado = false;
    void (async () => {
      try {
        if (!await aguardarInicializacaoTeams() || !pages.config.isSupported()) {
          if (ativo) setDisponivel(false);
          return;
        }
        const config = await pages.getConfig();
        if (!ativo) return;
        if (config.entityId) entityId.current = config.entityId;
        if (config.contentUrl) {
          const anterior = new URL(config.contentUrl);
          const [rota, consulta] = anterior.hash.slice(1).split('?');
          if (rota === '/qualidade' || rota === '/modulos/sst') {
            setModulo(rota === '/qualidade' ? 'qualidade' : 'sst');
            setObraId(new URLSearchParams(consulta).get('obraId') ?? '');
          }
        }
        pages.config.setValidityState(false);
        pages.config.registerOnSaveHandler(evento => {
          const atual = { ...selecao.current };
          if (!atual.valida) { evento.notifyFailure('Selecione um módulo e uma obra autorizada.'); return; }
          setSalvando(true);
          // A autorização é consultada novamente no momento do salvamento, sem confiar na URL.
          void plataformaApi.aba(atual.modulo, atual.obraId, AbortSignal.timeout(20000)).then(async aba => {
            if (!ativo || selecao.current.modulo !== atual.modulo || selecao.current.obraId !== atual.obraId)
              throw new Error('A seleção mudou. Confira a obra e salve novamente.');
            const url = new URL(window.location.href);
            url.search = '';
            url.hash = aba.caminho;
            await pages.config.setConfig({ entityId: entityId.current, contentUrl: url.href, websiteUrl: url.href, suggestedDisplayName: aba.nome });
            evento.notifySuccess();
          }).catch((e: unknown) => {
            const mensagem = e instanceof Error ? e.message : 'Não foi possível salvar a aba.';
            if (ativo) setErro(mensagem);
            evento.notifyFailure(mensagem);
          }).finally(() => { if (ativo) setSalvando(false); });
        });
        registrado = true;
        setDisponivel(true);
      } catch { if (ativo) { setErro('Não foi possível iniciar a configuração. Reabra a aba no Teams.'); setDisponivel(false); } }
    })();
    return () => {
      ativo = false;
      if (registrado) {
        pages.config.setValidityState(false);
        pages.config.registerOnSaveHandler(evento => evento.notifyFailure('Reabra a configuração da aba.'));
      }
    };
  }, []);

  useEffect(() => {
    if (!disponivel) return;
    const controller = new AbortController();
    setCarregando(true); setErro(''); setObras([]);
    selecao.current.valida = false;
    pages.config.setValidityState(false);
    plataformaApi.obras(modulo, true, controller.signal).then(lista => {
      if (!controller.signal.aborted) setObras(lista);
    }).catch((e: unknown) => {
      if (!controller.signal.aborted) setErro(e instanceof Error ? e.message : 'Não foi possível carregar as obras.');
    }).finally(() => { if (!controller.signal.aborted) setCarregando(false); });
    return () => controller.abort();
  }, [disponivel, modulo, tentativa]);

  useEffect(() => {
    const valida = !carregando && !salvando && !erro && obras.some(o => o.id === obraId);
    selecao.current = { modulo, obraId, valida };
    if (disponivel) pages.config.setValidityState(valida);
  }, [disponivel, modulo, obraId, obras, carregando, salvando, erro]);

  return <div className="plataforma-pagina plataforma-config">
    <PageHeader titulo="Configurar aba no Teams" subtitulo="Escolha o módulo e a obra que esta aba abrirá." />
    {disponivel === null && <Spinner label="Conectando ao Teams…" />}
    {disponivel === false && <FeedbackInline tom="info">Para configurar, adicione o G-SST como aba de um canal ou chat no Teams.</FeedbackInline>}
    {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
    {disponivel && <Card titulo="Conteúdo da aba">
      <Field label="Módulo"><Select value={modulo} disabled={salvando} onChange={(_, data) => {
        selecao.current.valida = false; pages.config.setValidityState(false);
        setModulo(data.value as ModuloPlataforma); setObraId('');
      }}><option value="qualidade">Qualidade</option><option value="sst">SST</option></Select></Field>
      {carregando ? <Spinner label="Consultando obras autorizadas…" /> : <Field label="Obra"><Select value={obraId} disabled={salvando || !!erro} onChange={(_, data) => {
        selecao.current.valida = false; pages.config.setValidityState(false); setObraId(data.value);
      }}><option value="">Selecione uma obra</option>{obras.map(o => <option key={o.id} value={o.id}>{o.codigo} · {o.nome}</option>)}</Select></Field>}
      {!carregando && !erro && obras.length === 0 && <EstadoVazio titulo="Nenhuma obra disponível" descricao="Solicite acesso ao módulo e à configuração de abas para a mesma obra." />}
      {erro && <Button onClick={() => setTentativa(t => t + 1)}>Tentar novamente</Button>}
      <p>A aba será salva pelo botão Salvar do Teams. Cada pessoa precisará ter acesso ao módulo e à obra para consultar seu conteúdo.</p>
    </Card>}
  </div>;
}
