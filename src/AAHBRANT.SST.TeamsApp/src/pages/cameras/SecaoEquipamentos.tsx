import { useState } from 'react';
import { BotaoAcao, Button, EstadoVazio, FeedbackInline, StatusChip, useTipografia } from '@ui';
import { Add20Regular, Camera24Regular, Edit20Regular, Key20Regular } from '@fluentui/react-icons';
import type { AgenteCamera, CameraResumo, EquipamentoCamera } from '../../lib/api';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';
import { FormularioEquipamento } from './FormularioEquipamento';
import { MarcaEstado } from './PecasCameraIp';
import { PainelSenhaEquipamento } from './PainelSenhaEquipamento';
import { formatarHora, situacaoDaCameraIp } from './rotulos';

const LENTES = [1, 2] as const;

type Formulario = { tipo: 'novo' } | { tipo: 'editar'; id: string } | null;

export interface SecaoEquipamentosProps {
  equipamentos: EquipamentoCamera[];
  agentes: AgenteCamera[];
  cameras: CameraResumo[];
  aoNovaCamera: (equipamento: EquipamentoCamera, lente: number) => void;
}

// Equipamento = a câmera física. Cada lente é uma câmera do mesmo equipamento. A senha só é gravada: a tela mostra
// "Senha definida" e o botão de trocar, nunca o valor.
export function SecaoEquipamentos({ equipamentos, agentes, cameras, aoNovaCamera }: SecaoEquipamentosProps) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const [formulario, setFormulario] = useState<Formulario>(null);
  const [senhaDe, setSenhaDe] = useState<string | null>(null);

  const semAgente = agentes.filter((a) => a.ativo).length === 0;
  const emEdicao = formulario?.tipo === 'editar' ? equipamentos.find((q) => q.id === formulario.id) : undefined;

  return (
    <section className={e.secao} aria-label="Equipamentos">
      <div className={e.secaoCabecalho}>
        <div>
          <h2 className={mergeClasses(tipo.titulo, e.secaoTitulo)}>
            <Camera24Regular aria-hidden="true" /> Equipamentos
          </h2>
          <p className={mergeClasses(tipo.corpo, e.secaoApoio)}>
            O equipamento é a câmera física. A senha só é gravada; nunca é mostrada. Uma câmera de duas lentes é um
            equipamento com duas câmeras.
          </p>
        </div>
        {agentes.length > 0 && (
          <Button
            appearance="primary"
            className={e.botao}
            icon={<Add20Regular />}
            disabled={semAgente || formulario?.tipo === 'novo'}
            onClick={() => {
              setSenhaDe(null);
              setFormulario({ tipo: 'novo' });
            }}
          >
            Novo equipamento
          </Button>
        )}
      </div>

      {agentes.length === 0 && (
        <FeedbackInline tom="info">Os equipamentos aparecem aqui depois que existir um agente.</FeedbackInline>
      )}
      {agentes.length > 0 && semAgente && (
        <FeedbackInline tom="aviso">
          Todos os agentes foram revogados. Cadastre um novo agente para cadastrar equipamentos.
        </FeedbackInline>
      )}

      {formulario?.tipo === 'novo' && (
        <FormularioEquipamento
          completo
          agentes={agentes}
          aoSalvar={() => setFormulario(null)}
          aoCancelar={() => setFormulario(null)}
        />
      )}
      {emEdicao && (
        <FormularioEquipamento
          key={emEdicao.id}
          completo
          equipamento={emEdicao}
          agentes={agentes}
          aoSalvar={() => setFormulario(null)}
          aoCancelar={() => setFormulario(null)}
          aoTrocarSenha={() => setSenhaDe(emEdicao.id)}
        />
      )}

      {agentes.length > 0 && equipamentos.length === 0 && formulario === null && (
        <div className={e.vazio}>
          <EstadoVazio
            icone={<Camera24Regular />}
            titulo="Nenhum equipamento cadastrado"
            descricao="O equipamento é a câmera física da obra. Cadastre o primeiro para poder criar câmeras IP."
            acao={semAgente ? undefined : { rotulo: 'Novo equipamento', aoClicar: () => setFormulario({ tipo: 'novo' }) }}
          />
        </div>
      )}

      <div className={e.lista}>
        {equipamentos.map((q) => (
          <div key={q.id} className={e.item}>
            <div className={e.equipTopo}>
              <div>
                <div className={e.equipNome}>
                  <Camera24Regular aria-hidden="true" />
                  {q.nome}
                </div>
                <div className={mergeClasses(tipo.legenda, e.apoio)}>
                  Agente: {q.agenteNome} · {q.obraNome}
                </div>
              </div>
              <BotaoAcao
                tom="ver"
                icon={<Edit20Regular />}
                aria-label={`Editar o equipamento ${q.nome}`}
                onClick={() => {
                  setSenhaDe(null);
                  setFormulario({ tipo: 'editar', id: q.id });
                }}
              >
                Editar
              </BotaoAcao>
            </div>

            <div className={e.dados}>
              <div>
                <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Endereço IP</div>
                <div className={mergeClasses(e.dadoValor, e.mono)}>{q.host}</div>
              </div>
              <div>
                <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Porta</div>
                <div className={mergeClasses(e.dadoValor, e.mono)}>{q.portaRtsp}</div>
              </div>
              <div>
                <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Usuário</div>
                <div className={e.dadoValor}>{q.usuario || '—'}</div>
              </div>
              <div>
                <div className={mergeClasses(tipo.micro, e.rotuloMicro)}>Senha</div>
                <div className={e.senhaLinha}>
                  {q.senhaDefinida ? (
                    <>
                      <StatusChip tom="ok">Senha definida</StatusChip>
                      <BotaoAcao
                        tom="ver"
                        icon={<Key20Regular />}
                        aria-label={`Trocar a senha do equipamento ${q.nome}`}
                        onClick={() => setSenhaDe(q.id)}
                      >
                        Trocar senha
                      </BotaoAcao>
                    </>
                  ) : (
                    <StatusChip tom="atencao">Senha ainda não definida</StatusChip>
                  )}
                </div>
              </div>
            </div>

            {!q.senhaDefinida && senhaDe !== q.id && (
              <FeedbackInline tom="aviso" acao={{ rotulo: 'Definir senha', aoClicar: () => setSenhaDe(q.id) }}>
                <strong>Falta definir a senha.</strong> Enquanto isso, as câmeras deste equipamento ficam desligadas. Defina a
                senha para liberar o monitoramento.
              </FeedbackInline>
            )}
            {senhaDe === q.id && (
              <PainelSenhaEquipamento equipamento={q} aoSalvar={() => setSenhaDe(null)} aoCancelar={() => setSenhaDe(null)} />
            )}

            <div className={mergeClasses(tipo.micro, e.apoio)} style={{ margin: '6px 0 8px' }}>
              Lentes deste equipamento
            </div>
            <div className={e.lentes}>
              {LENTES.map((n) => {
                const camera = cameras.find((c) => c.equipamentoCameraId === q.id && c.canal === n);
                if (!camera) {
                  return (
                    <div key={n} className={mergeClasses(e.lente, e.lenteLivre)}>
                      <span className={mergeClasses(e.lenteNumero, e.lenteNumeroLivre)}>{n}</span>
                      <div className={e.lenteTexto}>
                        <span className={e.lenteNome}>Lente {n} livre</span>
                        <span className={mergeClasses(tipo.legenda, e.apoio)}>Ainda sem câmera cadastrada</span>
                      </div>
                      <Button className={e.botao} icon={<Add20Regular />} onClick={() => aoNovaCamera(q, n)}>
                        Nova câmera
                      </Button>
                    </div>
                  );
                }
                const situacao = situacaoDaCameraIp(camera);
                const apoio =
                  !camera.habilitada && !q.senhaDefinida
                    ? 'Liga depois de definir a senha'
                    : camera.ultimoQuadroRecebidoEmUtc
                      ? `Última foto ${formatarHora(camera.ultimoQuadroRecebidoEmUtc)}`
                      : 'Ainda sem fotos';
                return (
                  <div key={n} className={e.lente}>
                    <span className={e.lenteNumero}>{n}</span>
                    <div className={e.lenteTexto}>
                      <span className={e.lenteNome} title={camera.nome}>
                        {camera.nome}
                      </span>
                      <span className={mergeClasses(tipo.legenda, e.apoio)}>{apoio}</span>
                    </div>
                    <MarcaEstado tom={situacao.tom}>{situacao.rotulo}</MarcaEstado>
                  </div>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
