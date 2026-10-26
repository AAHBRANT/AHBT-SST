import { useState, type CSSProperties, type ReactNode } from 'react';
import {
  Avatar,
  BotaoAcao,
  Button,
  Card,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  StatusChip,
  designTokens,
  tokensUi,
  useTipografia,
  type Tom,
} from '@ui';
import {
  ArrowDownload24Regular,
  Eye24Regular,
  EyeOff24Regular,
  ShieldCheckmark24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import { tipoVinculoLabel, type PerfilCompletoTrabalhador } from '../../lib/api';
import { formatarCpf, mascararCpf } from '../../lib/cpf';

const tomAptidao: Record<string, Tom> = {
  Apto: 'ok',
  'Apto com restrição': 'atencao',
  Inapto: 'alerta',
  'ASO vencido': 'alerta',
};

const estiloFato: CSSProperties = {
  display: 'flex',
  justifyContent: 'space-between',
  alignItems: 'center',
  gap: 10,
  padding: '9px 0',
  borderTop: `1px solid ${designTokens.colorCardBorder}`,
};

const estiloValorFato: CSSProperties = {
  margin: 0,
  fontWeight: 600,
  textAlign: 'right',
  display: 'flex',
  alignItems: 'center',
  gap: tokensUi.espaco.xs,
  minWidth: 0,
  fontVariantNumeric: 'tabular-nums',
};

function vencido(data: string | null | undefined): boolean {
  return !!data && new Date(data).getTime() < new Date(new Date().toDateString()).getTime();
}

// Pendências que impedem o trabalhador de receber PT: NR vencida (mesmo critério do "BLOQUEADO" da aba
// Geral), EPI em posse vencido e ASO com resultado Inapto.
function contarPendencias(perfil: PerfilCompletoTrabalhador): number {
  return (
    perfil.treinamentos.filter((t) => vencido(t.dataValidade)).length +
    perfil.episAtivos.filter((e) => vencido(e.dataValidade)).length +
    (perfil.statusAptidao === 'Inapto' ? 1 : 0)
  );
}

export interface FichaTrabalhadorLateralProps {
  perfil: PerfilCompletoTrabalhador;
  fotoUrl: string | null;
  baixandoRelatorio: boolean;
  aoVisualizarRelatorio: () => void;
  aoBaixarRelatorio: () => void;
}

// Identificação fixa do trabalhador (Modelo A, pedido do usuário 21/10): tudo que antes ficava dividido
// entre o PageHeader e o card de foto/CPF/RG fica numa coluna só, que acompanha a rolagem das abas.
export function FichaTrabalhadorLateral({
  perfil,
  fotoUrl,
  baixandoRelatorio,
  aoVisualizarRelatorio,
  aoBaixarRelatorio,
}: FichaTrabalhadorLateralProps) {
  const tipo = useTipografia();
  const [cpfVisivel, setCpfVisivel] = useState(false);
  const [fotoAmpliada, setFotoAmpliada] = useState(false);
  const pendencias = contarPendencias(perfil);
  const tomLiberacao: Tom = pendencias === 0 ? 'ok' : 'alerta';

  const fatos: Array<[string, ReactNode]> = [
    ['Matrícula', perfil.matricula || '—'],
    ['Obra', perfil.obraNome],
    ['Admissão', perfil.dataAdmissao ? perfil.dataAdmissao.slice(0, 10).split('-').reverse().join('/') : '—'],
    [
      'CPF',
      <>
        {cpfVisivel ? formatarCpf(perfil.cpf) : mascararCpf(perfil.cpf)}
        <Button
          appearance="subtle"
          size="small"
          icon={cpfVisivel ? <EyeOff24Regular /> : <Eye24Regular />}
          onClick={() => setCpfVisivel((v) => !v)}
          aria-label={cpfVisivel ? 'Ocultar CPF' : 'Revelar CPF'}
        />
      </>,
    ],
    ['RG', perfil.rg || '—'],
    [
      'Biometria',
      <StatusChip key="biometria" tom={perfil.temBiometria ? 'ok' : 'atencao'}>
        {perfil.temBiometria ? 'Cadastrada' : 'Pendente'}
      </StatusChip>,
    ],
  ];

  return (
    <Card>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', gap: tokensUi.espaco.sm }}>
        {/* Com foto, o avatar abre a foto ampliada (pedido do usuário, 21/10); sem foto fica só as iniciais. */}
        {fotoUrl ? (
          <button
            type="button"
            onClick={() => setFotoAmpliada(true)}
            aria-label={`Ampliar foto de ${perfil.nome}`}
            title="Ampliar foto"
            style={{ border: 0, padding: 0, background: 'none', borderRadius: '50%', cursor: 'zoom-in' }}
          >
            <Avatar
              name={perfil.nome}
              image={{ src: fotoUrl }}
              color="brand"
              size={96}
              active={pendencias === 0 ? 'active' : 'unset'}
              activeAppearance="ring"
            />
          </button>
        ) : (
          <Avatar
            name={perfil.nome}
            color="brand"
            size={96}
            active={pendencias === 0 ? 'active' : 'unset'}
            activeAppearance="ring"
          />
        )}
        <div>
          <h1 className={tipo.titulo} style={{ margin: `${tokensUi.espaco.xs} 0 0`, textWrap: 'balance' }}>
            {perfil.nome}
          </h1>
          <div className={tipo.corpo} style={{ color: designTokens.colorNeutralMedium }}>
            {perfil.funcaoNome}
          </div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'center', marginTop: tokensUi.espaco.sm }}>
            <StatusChip tom={tomAptidao[perfil.statusAptidao] ?? 'neutro'}>{perfil.statusAptidao}</StatusChip>
            <StatusChip tom="neutro">{tipoVinculoLabel[perfil.vinculo]}</StatusChip>
          </div>
        </div>
      </div>

      <div
        role="status"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          marginTop: tokensUi.espaco.lg,
          padding: '10px 12px',
          borderRadius: tokensUi.raio.md,
          fontWeight: 700,
          fontSize: 13,
          backgroundColor: tokensUi.status[tomLiberacao].fundo,
          color: tokensUi.status[tomLiberacao].tinta,
        }}
      >
        {pendencias === 0 ? (
          <ShieldCheckmark24Regular style={{ flexShrink: 0 }} />
        ) : (
          <Warning24Regular style={{ flexShrink: 0 }} />
        )}
        <span>
          {pendencias === 0
            ? 'Liberado para campo'
            : `${pendencias} ${pendencias === 1 ? 'pendência bloqueando' : 'pendências bloqueando'} PT`}
        </span>
      </div>

      <dl style={{ margin: `${tokensUi.espaco.lg} 0 0` }}>
        {fatos.map(([rotulo, valor]) => (
          <div key={rotulo} className={tipo.corpo} style={estiloFato}>
            <dt style={{ color: designTokens.colorNeutralMedium }}>{rotulo}</dt>
            <dd style={estiloValorFato}>{valor}</dd>
          </div>
        ))}
      </dl>

      <div style={{ display: 'grid', gap: tokensUi.espaco.sm, marginTop: tokensUi.espaco.lg }}>
        <BotaoAcao tom="ver" icon={<Eye24Regular />} onClick={aoVisualizarRelatorio} aria-label="Visualizar relatório">
          Visualizar relatório
        </BotaoAcao>
        <BotaoAcao
          tom="baixar"
          icon={<ArrowDownload24Regular />}
          onClick={aoBaixarRelatorio}
          disabled={baixandoRelatorio}
          aria-label="Emitir relatório de fiscalização (PDF)"
        >
          Emitir relatório de fiscalização
        </BotaoAcao>
      </div>

      {fotoUrl && (
        <Dialog open={fotoAmpliada} onOpenChange={(_, dados) => setFotoAmpliada(dados.open)}>
          <DialogSurface style={{ maxWidth: 560 }}>
            <DialogBody>
              <DialogTitle>{perfil.nome}</DialogTitle>
              <DialogContent>
                <img
                  src={fotoUrl}
                  alt={`Foto de ${perfil.nome}`}
                  style={{
                    display: 'block',
                    width: '100%',
                    maxHeight: '70vh',
                    objectFit: 'contain',
                    borderRadius: tokensUi.raio.lg,
                    backgroundColor: designTokens.colorNeutralLight,
                  }}
                />
              </DialogContent>
              <DialogActions>
                <Button appearance="primary" onClick={() => setFotoAmpliada(false)}>
                  Fechar
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      )}
    </Card>
  );
}
