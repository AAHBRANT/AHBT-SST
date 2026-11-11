import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, FeedbackInline, Select, StatusChip, type Tom } from '@ui';
import type { CandidatoIntegracao, EntradaIntegracao, Obra, SituacaoPessoaEtapaIntegracao, TipoEtapaIntegracao } from '../../../lib/api';
import { AvatarIniciais, TituloSecao } from './pecas';
import { corDoPonto, mergeClasses, useEstilosIntegracao } from './estilos';
import { TIPOS_EM_ORDEM, primeiroNome, rotuloEtapa } from './helpers';

interface Props {
  obras: Obra[];
  obraId: string;
  aoMudarObra: (obraId: string) => void;
  entrada: EntradaIntegracao | null;
  instrutorId: string;
  aoMudarInstrutor: (id: string) => void;
}

type SituacaoEtapa = SituacaoPessoaEtapaIntegracao;

// NR-06, NR-18 e a OS valem para todos. NR-12 e NR-35 dependem da função e do PGR: o servidor só
// confirma ao iniciar (a prévia por pessoa, se vier em `etapas`, prevalece).
function situacaoDa(c: CandidatoIntegracao, tipo: TipoEtapaIntegracao): { situacao: SituacaoEtapa; detalhe?: string | null } {
  const x = c.etapas?.find((et) => et.tipo === tipo);
  if (x) return { situacao: x.situacao, detalhe: x.detalhe };
  return { situacao: tipo === 'nr12' || tipo === 'nr35' ? 'naoExigido' : 'nestaIntegracao' };
}

const tomDa: Record<SituacaoEtapa, Tom> = { nestaIntegracao: 'info', naoExigido: 'neutro', valido: 'ok' };
const rotuloDa: Record<SituacaoEtapa, string> = { nestaIntegracao: 'Nesta integração', naoExigido: 'Não exigido', valido: 'Válido' };

// Tela de início (mockup v4): escolher a obra e conferir, por pessoa, em quais etapas ela entra. Todos
// os admitidos da obra com plano aberto entram; o combo por pessoa mostra Válido, Não exigido ou Nesta
// integração. O instrutor da integração é escolhido aqui (o servidor exige um perfil de instrutor).
export function InicioIntegracao({ obras, obraId, aoMudarObra, entrada, instrutorId, aoMudarInstrutor }: Props) {
  const e = useEstilosIntegracao();
  const navigate = useNavigate();
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);
  const candidatos = entrada?.candidatos ?? [];
  const instrutores = entrada?.instrutores ?? [];
  const selecionado = candidatos.find((c) => c.trabalhadorId === selecionadoId) ?? candidatos[0] ?? null;

  const pontoTom = (s: SituacaoEtapa) => corDoPonto(s === 'valido' ? 'valida' : s === 'naoExigido' ? 'neutra' : 'marca');

  return (
    <Card
      titulo={<TituloSecao numero="1" estado="atual">Obra e pessoas</TituloSecao>}
      subtitulo="Escolha a obra e confira, por pessoa, em quais etapas ela entra."
    >
      <div className={e.campo}>
        <label className={e.campoRotulo} htmlFor="integracao-obra">Obra</label>
        <Select id="integracao-obra" value={obraId} onChange={(_, d) => aoMudarObra(d.value)}>
          {obraId === '' && <option value="">Selecione a obra…</option>}
          {obras.map((o) => (
            <option key={o.id} value={o.id}>{o.nome}</option>
          ))}
        </Select>
      </div>

      {entrada && (
        <div className={e.campo}>
          <label className={e.campoRotulo} htmlFor="integracao-instrutor">Instrutor da integração</label>
          <Select id="integracao-instrutor" value={instrutorId} onChange={(_, d) => aoMudarInstrutor(d.value)} disabled={instrutores.length === 0}>
            {instrutores.length === 0 && <option value="">Nenhum instrutor cadastrado</option>}
            {instrutores.map((i) => (
              <option key={i.trabalhadorId} value={i.trabalhadorId}>
                {i.nome}{i.registro ? ` — ${i.registro}` : ''}
              </option>
            ))}
          </Select>
        </div>
      )}

      {entrada?.bloqueios.map((b) => (
        <FeedbackInline
          key={b.codigo}
          tom="aviso"
          acao={b.rota ? { rotulo: 'Resolver', aoClicar: () => navigate(b.rota!) } : undefined}
        >
          {b.mensagem}
        </FeedbackInline>
      ))}

      {candidatos.length === 0 ? (
        <FeedbackInline tom="info">
          Não há admitidos aguardando integração nesta obra. Quando uma admissão for registrada, a pessoa aparece aqui.
        </FeedbackInline>
      ) : (
        <>
          <div className={e.tituloMini} style={{ marginTop: 0 }}>
            Admitidos desta obra ({candidatos.length} selecionado{candidatos.length > 1 ? 's' : ''})
          </div>
          <div className={e.pessoasGrade}>
            <div className={e.listaPessoas} role="radiogroup" aria-label="Admitidos da obra">
              {candidatos.map((p) => {
                const sel = p.trabalhadorId === selecionado?.trabalhadorId;
                return (
                  <button
                    key={p.trabalhadorId}
                    type="button"
                    role="radio"
                    aria-checked={sel}
                    className={mergeClasses(e.linhaPessoa, sel && e.linhaPessoaSel)}
                    onClick={() => setSelecionadoId(p.trabalhadorId)}
                  >
                    <AvatarIniciais nome={p.nome} selecionado={sel} />
                    <span>
                      <span className={e.nomePessoa}>{p.nome}</span>
                      <span className={e.funcaoPessoa}>{p.funcao}</span>
                      <span className={e.pontos}>
                        {TIPOS_EM_ORDEM.map((t) => {
                          const s = situacaoDa(p, t).situacao;
                          return (
                            <span key={t} className={e.ponto} style={pontoTom(s)} title={`${rotuloEtapa(t).curto}: ${rotuloDa[s]}`}>
                              {rotuloEtapa(t).curto}{s === 'naoExigido' ? ' ·' : s === 'valido' ? ' ✓' : ''}
                            </span>
                          );
                        })}
                      </span>
                    </span>
                    <span className={mergeClasses(e.radio, sel && e.radioSel)} aria-hidden="true" />
                  </button>
                );
              })}
            </div>

            {selecionado && (
              <div className={e.detalhePessoa}>
                <div className={e.detalheTopo}>
                  <AvatarIniciais nome={selecionado.nome} />
                  <div>
                    <b style={{ fontSize: 16 }}>{selecionado.nome}</b>
                    <div className={e.funcaoPessoa}>{selecionado.funcao}</div>
                  </div>
                </div>
                <div className={e.tituloMini} style={{ marginTop: 4 }}>Etapas da integração, em ordem</div>
                {TIPOS_EM_ORDEM.map((t) => {
                  const { situacao, detalhe } = situacaoDa(selecionado, t);
                  const rot = rotuloEtapa(t);
                  const hint =
                    detalhe ??
                    (situacao === 'naoExigido'
                      ? `Não exigido para ${selecionado.funcao}`
                      : situacao === 'valido'
                        ? 'Já tem. Fora desta integração'
                        : t === 'ordemServico'
                          ? `OS própria, só a função de ${primeiroNome(selecionado.nome)}. Assinatura à parte`
                          : 'Assinatura biométrica própria neste curso');
                  return (
                    <div key={t} className={e.linhaEtapaPessoa}>
                      <span className={e.codigoEtapaPessoa}>{rot.curto}</span>
                      <span>
                        <b>{rot.nome}</b>
                        <small style={{ display: 'block' }} className={e.mudo}>{hint}</small>
                      </span>
                      <StatusChip tom={tomDa[situacao]}>{rotuloDa[situacao]}</StatusChip>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
          <div style={{ marginTop: 14 }}>
            <FeedbackInline tom="info">
              Cada curso gera um certificado por pessoa, com a <b>assinatura biométrica dela</b> naquele curso. A <b>Ordem de Serviço tem assinatura própria</b>,
              à parte.
            </FeedbackInline>
          </div>
        </>
      )}
    </Card>
  );
}
