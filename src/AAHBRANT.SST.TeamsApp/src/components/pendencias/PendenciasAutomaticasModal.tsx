import { useNavigate } from 'react-router-dom';
import {
  makeStyles,
  tokens,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
} from '@fluentui/react-components';
import { Fingerprint24Regular, PersonFeedback24Regular } from '@fluentui/react-icons';
import { TipoPendenciaAutomatica, type PendenciaAutomatica } from '../../lib/api';

// Pop-up de pendências automáticas por obra (pedido do usuário, 03/11; mockup aprovado em
// docs/mockups/popup-pendencias-v1.html). A lista vem pronta do servidor, já no escopo de obras de
// quem está logado. "Cadastrar" leva ao perfil do funcionário, onde ficam a câmera e o leitor.

const useStyles = makeStyles({
  superficie: { maxWidth: '620px', width: '100%' },
  cabecalho: { display: 'flex', alignItems: 'center', gap: '10px' },
  icone: {
    width: '32px',
    height: '32px',
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  subtitulo: { margin: 0, fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 },
  progresso: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    fontVariantNumeric: 'tabular-nums',
    marginTop: '8px',
  },
  barra: {
    flex: 1,
    height: '6px',
    borderRadius: '3px',
    backgroundColor: tokens.colorNeutralBackground5,
    overflow: 'hidden',
  },
  barraCheia: { height: '100%', backgroundColor: tokens.colorPaletteGreenBackground3 },
  lista: { display: 'flex', flexDirection: 'column', marginTop: '12px', maxHeight: '50vh', overflowY: 'auto' },
  grupo: {
    display: 'flex',
    justifyContent: 'space-between',
    fontSize: tokens.fontSizeBase100,
    fontWeight: tokens.fontWeightSemibold,
    textTransform: 'uppercase',
    letterSpacing: '0.06em',
    color: tokens.colorNeutralForeground3,
    padding: '12px 0 6px',
    position: 'sticky',
    top: 0,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  linha: {
    display: 'grid',
    gridTemplateColumns: '36px 1fr auto',
    alignItems: 'center',
    gap: '12px',
    padding: '8px 0',
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  avatar: {
    width: '36px',
    height: '36px',
    borderRadius: '50%',
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    display: 'grid',
    placeItems: 'center',
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
  },
  nome: { minWidth: 0 },
  nomeTexto: { display: 'block', fontWeight: tokens.fontWeightSemibold, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  detalhe: { fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 },
  falta: { color: tokens.colorBrandForeground1, fontWeight: tokens.fontWeightSemibold },
  acoes: { justifyContent: 'space-between', flexWrap: 'wrap' },
  nota: { fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3, alignSelf: 'center' },
});

const TEXTOS: Record<TipoPendenciaAutomatica, { titulo: string; subtitulo: string; botao: string; concluidos: string }> = {
  [TipoPendenciaAutomatica.RecadastroFacial]: {
    titulo: 'precisam de novo cadastro facial',
    subtitulo: 'As biometrias faciais foram renovadas. Cadastre o rosto de cada um para liberar assinaturas e presença.',
    botao: 'Cadastrar agora',
    concluidos: 'já cadastrados',
  },
  [TipoPendenciaAutomatica.DigitaisIncompletas]: {
    titulo: 'precisam completar as digitais',
    subtitulo: 'Cada funcionário precisa de 2 digitais, uma de cada mão. Se um dedo estiver machucado ou seco, ele assina com o outro.',
    botao: 'Cadastrar digital',
    concluidos: 'com as 2 mãos cadastradas',
  },
};

function iniciais(nome: string) {
  const partes = nome.trim().split(/\s+/).filter((p) => p.length > 2);
  return partes.slice(0, 2).map((p) => p[0]).join('').toUpperCase() || nome.slice(0, 2).toUpperCase();
}

interface Props {
  pendencia: PendenciaAutomatica;
  aoFechar: () => void;
  aoAbrirCadastro: () => void;
}

export function PendenciasAutomaticasModal({ pendencia, aoFechar, aoAbrirCadastro }: Props) {
  const estilos = useStyles();
  const navigate = useNavigate();
  const { configuracao, obras } = pendencia;
  const textos = TEXTOS[configuracao.tipo];
  const ehFacial = configuracao.tipo === TipoPendenciaAutomatica.RecadastroFacial;

  const pendentes = obras.reduce((soma, o) => soma + o.pendentes.length, 0);
  const total = obras.reduce((soma, o) => soma + o.totalTrabalhadores, 0);
  const feitos = total - pendentes;
  const onde = obras.length === 1 ? `da ${obras[0].obraNome}` : `em ${obras.length} obras`;

  function abrirCadastro(trabalhadorId: string) {
    aoAbrirCadastro();
    // O cadastro facial fica na aba Assinaturas do perfil; a digital, no cartão lateral (qualquer aba).
    navigate(`/pessoas/${trabalhadorId}`, ehFacial ? { state: { aba: 'cofre' } } : undefined);
  }

  return (
    <Dialog
      open
      modalType={configuracao.podeFechar ? 'modal' : 'alert'}
      onOpenChange={(_, data) => !data.open && configuracao.podeFechar && aoFechar()}
    >
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>
            <div className={estilos.cabecalho}>
              <span className={estilos.icone}>{ehFacial ? <PersonFeedback24Regular /> : <Fingerprint24Regular />}</span>
              <span>
                {pendentes} {pendentes === 1 ? 'funcionário' : 'funcionários'} {onde} {textos.titulo}
              </span>
            </div>
          </DialogTitle>
          <DialogContent>
            <p className={estilos.subtitulo}>{textos.subtitulo}</p>
            <div className={estilos.progresso}>
              <div className={estilos.barra}>
                <div className={estilos.barraCheia} style={{ width: `${total > 0 ? (feitos / total) * 100 : 0}%` }} />
              </div>
              <span>
                {feitos} de {total} {textos.concluidos}
              </span>
            </div>
            <div className={estilos.lista}>
              {obras.map((obra) => (
                <div key={obra.obraId}>
                  <div className={estilos.grupo}>
                    <span>{obra.obraNome}</span>
                    <span>{obra.pendentes.length} pendentes</span>
                  </div>
                  {obra.pendentes.map((t) => (
                    <div key={t.trabalhadorId} className={estilos.linha}>
                      <span className={estilos.avatar} aria-hidden>{iniciais(t.nome)}</span>
                      <div className={estilos.nome}>
                        <span className={estilos.nomeTexto}>{t.nome}</span>
                        <span className={estilos.detalhe}>
                          {t.funcao}
                          {!ehFacial && (
                            <>
                              {' · '}
                              <span className={estilos.falta}>{t.falta}</span>
                            </>
                          )}
                        </span>
                      </div>
                      <Button appearance="primary" size="small" onClick={() => abrirCadastro(t.trabalhadorId)}>
                        {textos.botao}
                      </Button>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </DialogContent>
          <DialogActions className={estilos.acoes}>
            <span className={estilos.nota}>Este aviso volta enquanto houver pendência.</span>
            {configuracao.podeFechar && (
              <Button appearance="secondary" onClick={aoFechar}>
                Lembrar mais tarde
              </Button>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
