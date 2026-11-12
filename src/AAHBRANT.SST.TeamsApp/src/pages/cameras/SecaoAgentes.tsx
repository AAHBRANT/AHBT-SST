import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  BotaoAcao,
  Button,
  Campo,
  Checkbox,
  FeedbackInline,
  Field,
  FormGrid,
  Input,
  EstadoVazio,
  Select,
  useConfirmar,
  useTipografia,
} from '@ui';
import { Add20Regular, Checkmark20Regular, Copy20Regular, Desktop24Regular, PlugDisconnected20Regular } from '@fluentui/react-icons';
import { api, type AgenteCamera, type EquipamentoCamera, type Obra } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { escolherObraInicial } from '../../hooks/useObraLembrada';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';
import { MarcaEstado } from './PecasCameraIp';
import { descreverHa, formatarDiaHora, situacaoDoAgente } from './rotulos';

interface SegredoGerado {
  nome: string;
  segredo: string;
}

export interface SecaoAgentesProps {
  agentes: AgenteCamera[];
  equipamentos: EquipamentoCamera[];
  obras: Obra[];
  souAdministrador: boolean;
}

// Agentes = o programa instalado no PC da obra que lê as câmeras pela rede local. O segredo do agente só existe na
// resposta do cadastro: a tela mostra uma única vez, com "Copiar" e a confirmação de que foi guardado.
export function SecaoAgentes({ agentes, equipamentos, obras, souAdministrador }: SecaoAgentesProps) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const queryClient = useQueryClient();
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();

  const [criando, setCriando] = useState(false);
  const [nome, setNome] = useState('');
  const [obraId, setObraId] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // O segredo vive só nesta memória: sai quando o usuário conclui ou deixa a tela.
  const [segredo, setSegredo] = useState<SegredoGerado | null>(null);
  const [copiado, setCopiado] = useState(false);
  const [copiaFalhou, setCopiaFalhou] = useState(false);
  const [guardei, setGuardei] = useState(false);

  const vazio = agentes.length === 0 && !criando && !segredo;

  function abrirNovo() {
    setNome('');
    setObraId(escolherObraInicial(obras));
    setErro(null);
    setCriando(true);
  }

  async function criar() {
    const nomeLimpo = nome.trim();
    if (!nomeLimpo) return setErro('Informe o nome do agente.');
    if (nomeLimpo.length > 100) return setErro('O nome do agente pode ter no máximo 100 caracteres.');
    if (!obraId) return setErro('Escolha a obra do agente.');
    try {
      setSalvando(true);
      setErro(null);
      const registro = await api.cameras.criarAgente(obraId, nomeLimpo);
      setSegredo({ nome: nomeLimpo, segredo: registro.segredo });
      setCopiado(false);
      setCopiaFalhou(false);
      setGuardei(false);
      setCriando(false);
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'agentes'] });
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível criar o agente.');
    } finally {
      setSalvando(false);
    }
  }

  async function copiar() {
    if (!segredo) return;
    try {
      await navigator.clipboard.writeText(segredo.segredo);
      setCopiado(true);
      setCopiaFalhou(false);
    } catch {
      // Sem permissão de área de transferência: o texto segue selecionável para copiar à mão.
      setCopiaFalhou(true);
    }
  }

  function concluir() {
    setSegredo(null);
    setCopiado(false);
    setGuardei(false);
  }

  async function revogar(agente: AgenteCamera) {
    const cameras = equipamentos
      .filter((q) => q.agenteCameraId === agente.id)
      .reduce((soma, q) => soma + q.quantidadeCameras, 0);
    const dele = cameras === 0 ? 'Nenhuma câmera usa este agente hoje' : cameras === 1 ? 'A câmera dele deixa de receber imagem' : `As ${cameras} câmeras dele deixam de receber imagem`;
    const ok = await confirmar({
      titulo: `Revogar o agente "${agente.nome}"?`,
      mensagem: `${dele}${cameras === 0 ? '' : ' até você instalar e cadastrar um novo agente'}. As ocorrências já registradas continuam guardadas.`,
      rotuloConfirmar: 'Revogar agente',
    });
    if (!ok) return;
    try {
      setErro(null);
      await api.cameras.revogarAgente(agente.id);
      await queryClient.invalidateQueries({ queryKey: ['cameras'] });
      sucessoToast('Agente revogado com sucesso.');
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível revogar o agente.');
    }
  }

  return (
    <section className={e.secao} aria-label="Agentes">
      {dialogElement}
      <div className={e.secaoCabecalho}>
        <div>
          <h2 className={mergeClasses(tipo.titulo, e.secaoTitulo)}>
            <Desktop24Regular aria-hidden="true" /> Agentes
          </h2>
          <p className={mergeClasses(tipo.corpo, e.secaoApoio)}>
            O agente é o PC da obra que lê as câmeras pela rede local. Cada obra tem o seu.
          </p>
        </div>
        {!vazio && (
          <Button appearance="primary" className={e.botao} icon={<Add20Regular />} disabled={criando} onClick={abrirNovo}>
            Novo agente
          </Button>
        )}
      </div>

      {erro && !criando && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {criando && (
        <div className={e.subPainel}>
          <div className={e.subPainelTitulo}>
            <Add20Regular aria-hidden="true" /> Novo agente
          </div>
          {erro && (
            <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
              {erro}
            </FeedbackInline>
          )}
          <FormGrid>
            <Campo span={6}>
              <Field
                label="Nome do agente"
                required
                dica="Use o nome que a equipe da obra reconhece, como o do computador."
                hint="Use o nome que a equipe da obra reconhece, como o do computador."
              >
                <Input value={nome} maxLength={100} placeholder="Ex.: PC da guarita" onChange={(_, d) => setNome(d.value)} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Obra" required dica="A obra onde o PC do agente está instalado.">
                <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                  <option value="">Selecione</option>
                  {obras.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          </FormGrid>
          <div className={e.acoesLinha}>
            <Button appearance="primary" className={e.botao} onClick={() => void criar()} disabled={salvando}>
              Criar agente
            </Button>
            <Button className={e.botao} onClick={() => setCriando(false)} disabled={salvando}>
              Cancelar
            </Button>
          </div>
        </div>
      )}

      {segredo && (
        <div className={mergeClasses(e.subPainel, e.subPainelOk)}>
          <div className={e.subPainelTitulo}>
            <Checkmark20Regular aria-hidden="true" /> Agente "{segredo.nome}" criado
          </div>
          <FeedbackInline tom="aviso">
            <strong>Guarde este segredo agora.</strong> Ele aparece só desta vez e não dá para ver depois. Se perder, revogue o
            agente e crie outro.
          </FeedbackInline>
          <div className={mergeClasses(tipo.micro, e.apoio)}>Segredo do agente</div>
          <div className={e.segredoLinha}>
            <code className={e.segredo} aria-label="Segredo do agente">
              {segredo.segredo}
            </code>
            <Button appearance="primary" className={e.botao} icon={<Copy20Regular />} onClick={() => void copiar()}>
              {copiado ? 'Copiado' : 'Copiar'}
            </Button>
          </div>
          {copiado && (
            <div className={mergeClasses(tipo.legenda, e.copiado)} role="status">
              Copiado. Cole o segredo no instalador do agente de câmeras, no PC da obra.
            </div>
          )}
          {copiaFalhou && (
            <div className={mergeClasses(tipo.legenda, e.dicaAtencao)} role="status">
              Não foi possível copiar automaticamente. Selecione o segredo acima e copie à mão.
            </div>
          )}
          <Checkbox
            checked={guardei}
            label="Já copiei e guardei o segredo"
            onChange={(_, d) => setGuardei(d.checked === true)}
          />
          <div className={e.acoesLinha} style={{ marginTop: 10 }}>
            <Button appearance="primary" className={e.botao} disabled={!guardei} onClick={concluir}>
              Concluir
            </Button>
          </div>
        </div>
      )}

      {vazio ? (
        <div className={e.vazio}>
          <EstadoVazio
            icone={<Desktop24Regular />}
            titulo="Nenhum agente cadastrado"
            descricao="O agente é o programa instalado no PC da obra que lê a câmera pela rede local e envia as imagens. Cadastre o primeiro para começar."
            acao={{ rotulo: 'Novo agente', aoClicar: abrirNovo }}
          />
        </div>
      ) : (
        <div className={e.lista}>
          {agentes.map((a) => {
            const s = situacaoDoAgente(a);
            return (
              <div key={a.id} className={mergeClasses(e.item, !a.ativo && e.itemRevogado)}>
                <div className={e.agente}>
                  <div>
                    <div className={e.agenteNome}>
                      <Desktop24Regular aria-hidden="true" />
                      {a.nome}
                    </div>
                    <div className={mergeClasses(tipo.legenda, e.apoio)}>{a.obraNome}</div>
                  </div>
                  <div>
                    <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Situação</div>
                    <MarcaEstado tom={s.tom}>{s.rotulo}</MarcaEstado>
                  </div>
                  <div>
                    <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Último contato</div>
                    {a.ultimoContatoEmUtc ? (
                      <>
                        <div style={{ fontWeight: 600 }}>{descreverHa(a.ultimoContatoEmUtc)}</div>
                        <div className={mergeClasses(tipo.legenda, e.apoio)}>{formatarDiaHora(a.ultimoContatoEmUtc)}</div>
                      </>
                    ) : (
                      <div style={{ fontWeight: 600 }}>Ainda não conectou</div>
                    )}
                  </div>
                  <div>
                    <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Versão</div>
                    <div className={e.mono}>{a.versao ?? '—'}</div>
                  </div>
                  <div className={e.agenteAcao}>
                    {souAdministrador && a.ativo && (
                      <BotaoAcao
                        tom="excluir"
                        className={e.botao}
                        icon={<PlugDisconnected20Regular />}
                        aria-label={`Revogar o agente ${a.nome}`}
                        onClick={() => void revogar(a)}
                      >
                        Revogar
                      </BotaoAcao>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
