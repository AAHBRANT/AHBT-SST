import { Info16Regular, Key16Regular, Video20Regular, Video24Regular, Warning16Regular } from '@fluentui/react-icons';
import { BotaoAcao, SstTooltip, useTipografia } from '@ui';
import { TEXTOS_BLOQUEIO_AO_VIVO, type BloqueioAoVivo } from './aoVivoRegras';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';

const ICONE_NOTA = { agente: Warning16Regular, versao: Info16Regular, senha: Key16Regular } as const;

export interface BotaoVerAoVivoProps {
  nomeCamera: string;
  // Motivo que impede de abrir; nulo = pronto. O botão fica apagado e o motivo aparece escrito (nota ou balão).
  bloqueio: BloqueioAoVivo | null;
  // "texto" = botão do bloco do Mosaico; "icone" = botão da lista (ocupa o espaço reservado das câmeras IP).
  variante: 'texto' | 'icone';
  className?: string;
  aoAbrir: () => void;
}

// Botão "Ver ao vivo", só para câmera IP (quem chama decide).
export function BotaoVerAoVivo({ nomeCamera, bloqueio, variante, className, aoAbrir }: BotaoVerAoVivoProps) {
  const e = useEstilosCameraIp();
  const rotulo = `Ver ao vivo a câmera ${nomeCamera}`;

  if (variante === 'texto') {
    return (
      <BotaoAcao
        tom="ver"
        icon={<Video20Regular />}
        aria-label={rotulo}
        className={mergeClasses(e.botao, className)}
        disabled={!!bloqueio}
        onClick={aoAbrir}
      >
        Ver ao vivo
      </BotaoAcao>
    );
  }

  if (!bloqueio) {
    return <BotaoAcao tom="ver" icon={<Video24Regular />} aria-label={rotulo} className={className} onClick={aoAbrir} />;
  }
  // Apagado na lista: o motivo aparece num balão ao tocar, focar ou passar o mouse (celular não tem "passar o mouse").
  const balao = TEXTOS_BLOQUEIO_AO_VIVO[bloqueio].balao;
  return (
    <SstTooltip conteudo={balao} posicao="before" relacao="description">
      <span
        className={e.aoVivoInvolucro}
        role="button"
        tabIndex={0}
        aria-disabled="true"
        aria-label={`${rotulo}, indisponível. ${balao}`}
      >
        <BotaoAcao
          tom="ver"
          icon={<Video24Regular />}
          aria-label={rotulo}
          className={mergeClasses(className, e.aoVivoApagado)}
          disabled
          title={balao}
        />
      </span>
    </SstTooltip>
  );
}

// Motivo escrito logo abaixo do botão apagado, no bloco do Mosaico.
export function NotaBloqueioAoVivo({ bloqueio }: { bloqueio: BloqueioAoVivo }) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const Icone = ICONE_NOTA[bloqueio];
  const tom = { agente: e.notaAtencao, versao: e.notaInfo, senha: e.notaNeutra }[bloqueio];
  return (
    <div role="note" className={mergeClasses(e.nota, tipo.legenda, tom)}>
      <Icone className={e.notaIcone} aria-hidden="true" />
      <span>{TEXTOS_BLOQUEIO_AO_VIVO[bloqueio].nota}</span>
    </div>
  );
}
