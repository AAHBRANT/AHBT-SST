import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button, Card, Carregando, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  FeedbackInline, StatusChip, Text, designTokens, type Tom,
} from '@ui';
import { api, nivelRiscoLabel, type MinutaOrdemServico, type PendenciaOs } from '../../lib/api';

const tomPorNivel: Record<number, Tom> = { 1: 'ok', 2: 'info', 3: 'atencao', 4: 'alerta', 5: 'alerta' };
const data = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-BR') : '—');

// Aba "Ordem de Serviço" do perfil do trabalhador (20/10/2026): minuta da OS na obra dele, montada
// só com conteúdo validado, e as pendências que bloqueiam a emissão. Uma OS por trabalhador por obra.
// A emissão com número e assinatura é a próxima etapa.
export function OrdemServicoTab({ trabalhadorId, aoIrParaAba }: {
  trabalhadorId: string;
  aoIrParaAba: (aba: 'geral' | 'treinamentos') => void;
}) {
  const [minuta, setMinuta] = useState<MinutaOrdemServico | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [popAberto, setPopAberto] = useState(false);
  const [previaAberta, setPreviaAberta] = useState(false);
  const [avisoEmissao, setAvisoEmissao] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    void (async () => {
      try {
        const m = await api.ordensServico.obterMinuta(trabalhadorId);
        setMinuta(m);
        // Com pendência, o pop-up explicativo abre sozinho ao entrar na aba.
        setPopAberto(m.pendencias.length > 0);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao montar a Ordem de Serviço.');
      }
    })();
  }, [trabalhadorId]);

  if (!minuta) return erro ? <FeedbackInline tom="erro">{erro}</FeedbackInline> : <Carregando variante="detalhe" linhas={6} />;

  function irPara(p: PendenciaOs) {
    setPopAberto(false);
    if (p.destino === 'pgr' && p.destinoId) navigate(`/prevencao/pgr/${p.destinoId}`);
    else if (p.destino === 'pgr') navigate('/gestao-sst?secao=pgr');
    else if (p.destino === 'treinamentos') aoIrParaAba('treinamentos');
    else if (p.destino === 'aso') aoIrParaAba('geral');
  }

  const rotuloDestino = (p: PendenciaOs) =>
    p.destino === 'pgr' ? 'Abrir no PGR' : p.destino === 'treinamentos' ? 'Ver treinamentos' : 'Ver ASO';

  const riscos = minuta.atividades.flatMap((a) => a.riscos);
  const medidas = riscos.reduce((t, r) => t + r.antes.length + r.durante.length + r.apos.length, 0);
  const itens = minuta.atividades.reduce((t, a) => t + a.interromperQuando.length + a.restricoes.length + a.autorizacoes.length, 0);
  const procedimentos = new Set(minuta.atividades.flatMap((a) => a.procedimentos)).size;
  const pendSst = minuta.pendencias.filter((p) => p.quemResolve === 'SST');
  const pendRh = minuta.pendencias.filter((p) => p.quemResolve === 'RH');
  const base = [
    minuta.pgr ? `PGR ${minuta.pgr.nome}` : 'sem PGR vigente',
    minuta.ghes.length ? `GHE ${minuta.ghes.map((g) => String(g.numero).padStart(2, '0')).join(', ')}` : null,
    `função ${minuta.trabalhador.funcao}`,
    `obra ${minuta.obra.nome}`,
  ].filter(Boolean).join(' · ');

  const linhaResumo = (rotulo: string, conteudo: React.ReactNode) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, padding: '6px 0', borderBottom: '1px solid var(--colorNeutralStroke2)', flexWrap: 'wrap' }}>
      <Text>{rotulo}</Text>
      <span style={{ display: 'inline-flex', gap: 6, flexWrap: 'wrap' }}>{conteudo}</span>
    </div>
  );

  const ListaPendencias = ({ titulo, lista }: { titulo: string; lista: PendenciaOs[] }) =>
    lista.length === 0 ? null : (
      <div style={{ marginTop: 12 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 6 }}>{titulo}</Text>
        {lista.map((p, i) => (
          <div
            key={i}
            style={{
              border: '1px solid var(--colorNeutralStroke2)',
              borderLeft: `4px solid ${p.tipo === 'SugestoesPendentes' ? 'var(--colorPaletteMarigoldBorderActive)' : 'var(--colorPaletteRedBorderActive)'}`,
              borderRadius: 6, padding: '8px 10px', marginBottom: 8,
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start', flexWrap: 'wrap' }}>
              <Text weight="semibold">{p.titulo}</Text>
              {p.destino && <Button size="small" onClick={() => irPara(p)}>{rotuloDestino(p)}</Button>}
            </div>
            <Text size={200} style={{ display: 'block', marginTop: 2 }}>{p.porQueBloqueia}</Text>
          </div>
        ))}
      </div>
    );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <div>
            {minuta.podeEmitir
              ? <StatusChip tom="ok">PRONTA PARA EMISSÃO</StatusChip>
              : <StatusChip tom="alerta">MINUTA — EMISSÃO BLOQUEADA</StatusChip>}
            <Text weight="semibold" size={400} style={{ display: 'block', marginTop: 4 }}>
              {minuta.podeEmitir
                ? 'Todos os dados estão validados'
                : `${minuta.pendencias.length} pendência(s) impedem a emissão da OS`}
            </Text>
            <Text size={200}>Base: {base}</Text>
          </div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {!minuta.podeEmitir && <Button onClick={() => setPopAberto(true)}>Ver pendências</Button>}
            <Button onClick={() => setPreviaAberta(true)}>Ver prévia</Button>
            <Button
              appearance="primary"
              onClick={() => (minuta.podeEmitir ? setAvisoEmissao(true) : setPopAberto(true))}
            >
              Emitir OS
            </Button>
          </div>
        </div>
        {avisoEmissao && (
          <FeedbackInline tom="info" aoFechar={() => setAvisoEmissao(false)}>
            A emissão com número e assinatura do trabalhador entra na próxima etapa.
          </FeedbackInline>
        )}
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, alignItems: 'start' }}>
        <Card titulo="O que vai na OS deste trabalhador">
          {linhaResumo('Atividades da função (PGR)', <StatusChip tom={minuta.atividades.length ? 'ok' : 'alerta'}>{minuta.atividades.length} atividade(s)</StatusChip>)}
          {linhaResumo('Riscos', <StatusChip tom="ok">{riscos.length}</StatusChip>)}
          {linhaResumo('Medidas de prevenção validadas', <StatusChip tom="ok">{medidas}</StatusChip>)}
          {linhaResumo('Parada, restrições e autorizações', <StatusChip tom="ok">{itens}</StatusChip>)}
          {linhaResumo('EPIs da função', <StatusChip tom="ok">{minuta.epis.length}</StatusChip>)}
          {linhaResumo('Procedimentos vigentes', <StatusChip tom={procedimentos ? 'ok' : 'atencao'}>{procedimentos}</StatusChip>)}
          {linhaResumo('Treinamentos exigidos', minuta.treinamentos.length === 0
            ? <StatusChip tom="neutro">Nenhum na Matriz</StatusChip>
            : minuta.treinamentos.map((t) => (
              <StatusChip key={t.curso} tom={t.situacao === 'Valido' ? 'ok' : 'alerta'}>
                {t.norma ?? t.curso} {t.situacao === 'Valido' ? 'válido' : t.situacao === 'Vencido' ? 'vencido' : 'não feito'}
              </StatusChip>
            )))}
          {linhaResumo('ASO', minuta.aso
            ? <StatusChip tom={minuta.aso.valido ? 'ok' : 'alerta'}>{minuta.aso.valido ? `Apto até ${data(minuta.aso.validade)}` : 'Vencido ou sem aptidão'}</StatusChip>
            : <StatusChip tom="alerta">Sem ASO</StatusChip>)}
          {linhaResumo('Políticas', minuta.politicas.length === 0
            ? <StatusChip tom="neutro">Nenhuma</StatusChip>
            : minuta.politicas.map((p) => <StatusChip key={p.titulo} tom="ok">{p.titulo} rev. {p.revisao}</StatusChip>))}

          <ListaPendencias titulo="Pendências — responsável de SST" lista={pendSst} />
          <ListaPendencias titulo="Pendências — trabalhador / RH" lista={pendRh} />
        </Card>

        <Card titulo="Histórico de OS emitidas">
          <Text size={200}>Nenhuma OS emitida ainda. A emissão com número e assinatura vem na próxima etapa.</Text>
        </Card>
      </div>

      <Dialog open={popAberto} onOpenChange={(_, d) => setPopAberto(d.open)}>
        <DialogSurface style={{ maxWidth: 640 }}>
          <DialogBody>
            <DialogTitle>⚠ Esta OS ainda não pode ser emitida</DialogTitle>
            <DialogContent>
              <Text style={{ display: 'block', textAlign: 'justify' }}>
                A Ordem de Serviço só pode ser emitida quando todos os riscos tiverem avaliação, todas as medidas
                tiverem sido validadas pelo responsável de SST e o trabalhador estiver com treinamentos e ASO em dia.
                Por isso ela fica como <b>minuta</b> até as <b>{minuta.pendencias.length} pendência(s)</b> abaixo serem resolvidas.
              </Text>
              {minuta.pendencias.map((p, i) => (
                <div key={i} style={{ border: '1px solid var(--colorNeutralStroke2)', borderLeft: '4px solid var(--colorPaletteRedBorderActive)', borderRadius: 4, padding: '8px 10px', marginTop: 10 }}>
                  <Text weight="semibold" style={{ display: 'block' }}>{i + 1}. {p.titulo}</Text>
                  <Text size={200} style={{ display: 'block' }}><b>Por que bloqueia:</b> {p.porQueBloqueia}</Text>
                  <Text size={200} style={{ display: 'block' }}><b>Quem resolve:</b> {p.quemResolve === 'SST' ? 'Responsável de SST' : 'RH / trabalhador'}</Text>
                  {p.destino && <Button size="small" style={{ marginTop: 6 }} onClick={() => irPara(p)}>{rotuloDestino(p)}</Button>}
                </div>
              ))}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { setPopAberto(false); setPreviaAberta(true); }}>Ver minuta</Button>
              <Button appearance="primary" onClick={() => setPopAberto(false)}>Entendi</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={previaAberta} onOpenChange={(_, d) => setPreviaAberta(d.open)}>
        <DialogSurface style={{ maxWidth: 900, width: '95vw' }}>
          <DialogBody>
            <DialogTitle>{minuta.podeEmitir ? 'Prévia da Ordem de Serviço' : 'Minuta da Ordem de Serviço (não emitida)'}</DialogTitle>
            <DialogContent>
              <PreviaOs minuta={minuta} />
            </DialogContent>
            <DialogActions>
              <Button appearance="primary" onClick={() => setPreviaAberta(false)}>Fechar</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}

// Mesmo conteúdo e ordem do modelo aprovado (v5); o PDF oficial vem na etapa de emissão.
function PreviaOs({ minuta }: { minuta: MinutaOrdemServico }) {
  const secao = (n: number, titulo: string) => (
    <div style={{ background: designTokens.colorPrimary, color: designTokens.colorWhite, padding: '4px 8px', fontWeight: 600, fontSize: 12, marginTop: 12 }}>
      {n}. {titulo.toUpperCase()}
    </div>
  );
  const lista = (itens: string[]) => (itens.length ? <ul style={{ margin: '4px 0', paddingLeft: 18 }}>{itens.map((i) => <li key={i}>{i}</li>)}</ul> : <Text size={200}>—</Text>);

  return (
    <div style={{ fontSize: 13, lineHeight: 1.45 }}>
      {secao(1, 'Identificação da empresa e da obra')}
      <Text style={{ display: 'block' }}>{minuta.obra.nome}{minuta.obra.cliente ? ` · ${minuta.obra.cliente}` : ''}{minuta.obra.cnpj ? ` · CNPJ ${minuta.obra.cnpj}` : ''}</Text>
      {minuta.obra.endereco && <Text style={{ display: 'block' }}>{minuta.obra.endereco}</Text>}

      {secao(2, 'Identificação do trabalhador')}
      <Text style={{ display: 'block' }}>
        {minuta.trabalhador.nome} · CPF {minuta.trabalhador.cpfMascarado}{minuta.trabalhador.matricula ? ` · Matrícula ${minuta.trabalhador.matricula}` : ''}
      </Text>
      <Text style={{ display: 'block' }}>
        {minuta.trabalhador.funcao}{minuta.trabalhador.cbo ? ` · CBO ${minuta.trabalhador.cbo}` : ''} · Admissão {data(minuta.trabalhador.dataAdmissao)}
      </Text>

      {secao(3, 'Atividades da função (conforme PGR)')}
      {minuta.pgr && <Text size={200} style={{ display: 'block' }}>Fonte: {minuta.pgr.nome}{minuta.pgr.revisao != null ? ` — revisão ${minuta.pgr.revisao}` : ''}</Text>}
      {minuta.ghes.map((g) => (
        <Text key={g.numero} style={{ display: 'block', whiteSpace: 'pre-line', textAlign: 'justify' }}>
          <b>GHE {String(g.numero).padStart(2, '0')}:</b> {g.descricaoAtividades || '— descrição não informada no PGR —'}
        </Text>
      ))}

      {secao(4, 'Atividades: riscos, prevenção e requisitos')}
      {minuta.atividades.map((a) => (
        <div key={a.id} style={{ border: '1px solid var(--colorNeutralStroke2)', padding: 8, marginTop: 8 }}>
          <Text weight="semibold" style={{ display: 'block' }}>{a.nome}</Text>
          <Text size={200} style={{ display: 'block', whiteSpace: 'pre-line' }}><b>Condições:</b> {a.condicoes || '—'}</Text>
          {a.riscos.map((r) => (
            <div key={r.id} style={{ marginTop: 6 }}>
              <StatusChip tom={tomPorNivel[r.nivel] ?? 'neutro'}>{nivelRiscoLabel[r.nivel]}</StatusChip>{' '}
              <b>{r.perigo}</b>{r.consequencia ? ` — ${r.consequencia}` : ''}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 8 }}>
                <div><Text size={200} weight="semibold">Antes</Text>{lista(r.antes)}</div>
                <div><Text size={200} weight="semibold">Durante</Text>{lista(r.durante)}</div>
                <div><Text size={200} weight="semibold">Após</Text>{lista(r.apos)}</div>
              </div>
            </div>
          ))}
          <Text size={200} style={{ display: 'block', marginTop: 6 }}><b>Ferramentas:</b> {a.ferramentas.join(', ') || '—'} · <b>EPCs:</b> {a.epcs.join(', ') || '—'}</Text>
          <Text size={200} style={{ display: 'block' }}><b>Procedimentos:</b> {a.procedimentos.join('; ') || '—'}</Text>
          <Text size={200} weight="semibold" style={{ display: 'block', marginTop: 4 }}>Interromper e avisar a liderança quando</Text>{lista(a.interromperQuando)}
          <Text size={200} weight="semibold" style={{ display: 'block' }}>Não executar sem capacitação, autorização ou liberação</Text>
          {lista(a.restricoes.map((r) => (r.requisito ? `${r.texto} → ${r.requisito}` : r.texto)))}
          <Text size={200} weight="semibold" style={{ display: 'block' }}>Autorizações exigidas</Text>{lista(a.autorizacoes)}
        </div>
      ))}

      {secao(5, 'EPIs obrigatórios')}
      {lista(minuta.epis.map((e) => `${e.nome}${e.ca ? ` — CA ${e.ca}` : ''}`))}

      {secao(6, 'Qualificações e autorizações do trabalhador')}
      {lista([
        ...minuta.treinamentos.map((t) => `${t.curso}: ${t.situacao === 'Valido' ? `válido até ${data(t.validade)}` : t.situacao === 'Vencido' ? `vencido em ${data(t.validade)}` : 'não realizado'}`),
        minuta.aso ? `ASO: ${minuta.aso.valido ? `apto até ${data(minuta.aso.validade)}` : 'vencido ou sem aptidão'}` : 'ASO: não cadastrado',
      ])}

      {secao(7, 'Responsabilidades do trabalhador')}
      {lista([
        'Cumprir as disposições legais e regulamentares de SST e as instruções desta OS (NR-01, item 1.4.2).',
        'Utilizar máquinas, ferramentas e equipamentos somente para os quais esteja capacitado e autorizado, obedecendo aos procedimentos e normas da empresa.',
        'Usar os EPIs fornecidos, guardá-los, conservá-los e comunicar qualquer alteração que os torne impróprios (NR-06).',
        'Comunicar prontamente acidentes, incidentes, quase acidentes, condições inseguras, falhas de equipamentos e mudanças nas condições de trabalho.',
        'Participar das orientações e capacitações aplicáveis e colaborar com as medidas de prevenção.',
        'Exercer o direito de recusa diante de risco grave e iminente, comunicando imediatamente a liderança (NR-01, item 1.4.3).',
      ])}

      {secao(8, 'Políticas corporativas aplicáveis')}
      {lista(minuta.politicas.map((p) => `${p.codigo ? `${p.codigo} — ` : ''}${p.titulo} (rev. ${p.revisao ?? '—'})${p.formaConsulta ? ` · Consulta: ${p.formaConsulta}` : ''}`))}

      {secao(9, 'Ciência e responsabilidade')}
      <Text style={{ display: 'block', textAlign: 'justify' }}>
        Declaro ter recebido esta Ordem de Serviço, ter sido informado(a) de forma clara sobre as atividades, os riscos, as medidas
        de prevenção e os limites da minha função, e comprometo-me a cumpri-la. Estou ciente de que a recusa injustificada em observar
        as instruções de segurança e em usar os EPIs constitui ato faltoso (CLT, art. 158, parágrafo único).
      </Text>
    </div>
  );
}
