import { useState } from 'react';
import { Clock16Regular, Fingerprint24Regular } from '@fluentui/react-icons';
import { useNavigate } from 'react-router-dom';
import { Button, Card, Checkbox, FeedbackInline, Select, StatusChip, type Tom } from '@ui';
import { CadastroBiometriaDialog } from './CadastroBiometriaDialog';
import type { CandidatoIntegracao, EntradaIntegracao, Obra, SituacaoPessoaEtapaIntegracao, TipoEtapaIntegracao } from '../../../lib/api';
import { AvatarIniciais, TituloSecao } from './pecas';
import { corDoPonto, mergeClasses, useEstilosIntegracao } from './estilos';
import { situacaoBiometriaInstrutor } from './helpersInstrutor';
import { TIPOS_EM_ORDEM, elegiveisDaObra, estadoSelecionarTodos, pessoaForaDaIntegracao, primeiroNome, rotuloEtapa, tituloSelecao } from './helpers';

interface Props {
  obras: Obra[];
  obraId: string;
  aoMudarObra: (obraId: string) => void;
  entrada: EntradaIntegracao | null;
  /** Quem entra nesta integração (ids dos elegíveis marcados). */
  marcados: Set<string>;
  aoAlternarPessoa: (trabalhadorId: string) => void;
  aoAlternarTodos: () => void;
  instrutorId: string;
  aoMudarInstrutor: (id: string) => void;
  /** Depois de cadastrar a biometria do instrutor: busca a obra de novo (sem perder quem está marcado). */
  aoAtualizarInstrutores: () => void;
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
export function InicioIntegracao({ obras, obraId, aoMudarObra, entrada, marcados, aoAlternarPessoa, aoAlternarTodos, instrutorId, aoMudarInstrutor, aoAtualizarInstrutores }: Props) {
  const e = useEstilosIntegracao();
  const navigate = useNavigate();
  const [cadastrandoInstrutor, setCadastrandoInstrutor] = useState(false);
  // Detalhe aberto à direita: independe da seleção (marcar não muda o detalhe; abrir o detalhe não marca).
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);
  const candidatos = entrada?.candidatos ?? [];
  const elegiveis = elegiveisDaObra(candidatos);
  const nMarcados = elegiveis.filter((c) => marcados.has(c.trabalhadorId)).length;
  const instrutores = entrada?.instrutores ?? [];
  const instrutorEscolhido = instrutores.find((i) => i.trabalhadorId === instrutorId) ?? null;
  const verificacaoInstrutor = situacaoBiometriaInstrutor(instrutorEscolhido);
  const selecionado = candidatos.find((c) => c.trabalhadorId === selecionadoId) ?? candidatos[0] ?? null;
  const foraDaIntegracao = !!selecionado && pessoaForaDaIntegracao(selecionado, marcados);

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

      {/* Pré-checagem (decisão 2, 16/11): na hora em que o instrutor é escolhido. Só a biometria dele; o ASO só barra na OS. */}
      {entrada && instrutorEscolhido && verificacaoInstrutor !== 'desconhecida' && (
        <div className={e.campo} role="status" aria-live="polite">
          <span className={e.campoRotulo}>Antes de iniciar</span>
          {verificacaoInstrutor === 'sem_biometria' ? (
            <>
              <FeedbackInline tom="erro">
                <b>{instrutorEscolhido.nome} não tem digital nem facial cadastrada.</b> Ele precisa assinar cada etapa por biometria. Sem isso, nenhuma etapa avança.
              </FeedbackInline>
              <div>
                <Button appearance="primary" size="large" className={e.btnGrande} icon={<Fingerprint24Regular />} onClick={() => setCadastrandoInstrutor(true)}>
                  Cadastrar biometria do instrutor
                </Button>
              </div>
            </>
          ) : (
            <FeedbackInline tom="sucesso">
              <b>Instrutor com biometria cadastrada.</b>{' '}
              {instrutorEscolhido.temDigital && instrutorEscolhido.temFacial
                ? `Digital e facial de ${instrutorEscolhido.nome} prontas para assinar.`
                : instrutorEscolhido.temDigital
                  ? `Digital de ${instrutorEscolhido.nome} pronta para assinar (sem facial cadastrada).`
                  : `Facial de ${instrutorEscolhido.nome} pronta para assinar (sem digital cadastrada).`}
            </FeedbackInline>
          )}
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
            Admitidos desta obra ({tituloSelecao(nMarcados, elegiveis.length)})
          </div>
          <div className={e.pessoasGrade}>
            <div className={e.listaRolagem}>
              <div className={e.selecionarTodosFixo}>
                <div className={e.selecionarTodosLinha}>
                  <Checkbox
                    className={e.todosCaixa}
                    indicator={{ className: e.caixaVisivel }}
                    checked={estadoSelecionarTodos(nMarcados, elegiveis.length)}
                    disabled={elegiveis.length === 0}
                    onChange={aoAlternarTodos}
                    label="Selecionar todos"
                  />
                  <span className={e.selecionarTodosContador}>{nMarcados} de {elegiveis.length}</span>
                </div>
              </div>
              <div className={e.listaPessoas} role="group" aria-label="Admitidos da obra">
                {candidatos.map((p) => {
                  const aberto = p.trabalhadorId === selecionado?.trabalhadorId;
                  const marcado = !pessoaForaDaIntegracao(p, marcados);
                  return (
                    <div
                      key={p.trabalhadorId}
                      className={mergeClasses(
                        e.linhaSelecao,
                        aberto && e.linhaSelecaoAberta,
                        !marcado && e.linhaSelecaoFora,
                        !marcado && aberto && e.linhaSelecaoForaAberta,
                      )}
                    >
                      <Checkbox
                        className={e.caixaAlvo}
                        indicator={{ className: e.caixaVisivel }}
                        checked={marcado}
                        disabled={p.jaParticipa}
                        onChange={() => aoAlternarPessoa(p.trabalhadorId)}
                        aria-label={`Incluir ${p.nome} nesta integração`}
                      />
                      <button
                        type="button"
                        className={e.cartaoPessoa}
                        aria-pressed={aberto}
                        aria-current={aberto ? 'true' : undefined}
                        onClick={() => setSelecionadoId(p.trabalhadorId)}
                      >
                        <span className={marcado ? undefined : e.esmaecido}>
                          <AvatarIniciais nome={p.nome} selecionado={aberto} />
                        </span>
                        <span>
                          <span className={mergeClasses(e.nomePessoa, !marcado && e.esmaecido)}>{p.nome}</span>
                          <span className={mergeClasses(e.funcaoPessoa, !marcado && e.esmaecido)}>{p.funcao}</span>
                          {marcado ? (
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
                          ) : (
                            <span className={e.fica}>
                              <Clock16Regular aria-hidden="true" />
                              {p.jaParticipa ? 'Já está na integração em andamento' : 'Fica para a próxima integração'}
                            </span>
                          )}
                        </span>
                      </button>
                    </div>
                  );
                })}
              </div>
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
                {foraDaIntegracao && (
                  <FeedbackInline tom="aviso">Fora desta integração. As etapas abaixo ficam para a próxima.</FeedbackInline>
                )}
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
                      {foraDaIntegracao && situacao === 'nestaIntegracao' ? (
                        <StatusChip tom="neutro">Na próxima integração</StatusChip>
                      ) : (
                        <StatusChip tom={tomDa[situacao]}>{rotuloDa[situacao]}</StatusChip>
                      )}
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

      {cadastrandoInstrutor && instrutorEscolhido && (
        <CadastroBiometriaDialog
          trabalhadorId={instrutorEscolhido.trabalhadorId}
          trabalhadorNome={instrutorEscolhido.nome}
          aoFechar={() => setCadastrandoInstrutor(false)}
          aoAlterar={aoAtualizarInstrutores}
        />
      )}
    </Card>
  );
}
