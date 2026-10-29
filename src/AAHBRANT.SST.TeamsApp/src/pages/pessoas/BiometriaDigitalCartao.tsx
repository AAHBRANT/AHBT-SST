import { useEffect, useState } from 'react';
import { Card, Legenda, StatusChip, Text, designTokens, tokensUi, type Tom } from '@ui';
import { Checkmark16Regular, Fingerprint24Regular } from '@fluentui/react-icons';
import { api, DedoBiometria, type StatusCadastroBiometrico } from '../../lib/api';
import { CadastroDigitalDialog } from '../../components/pessoas/CadastroDigitalDialog';

function dataBr(iso?: string | null): string {
  return iso ? iso.slice(0, 10).split('-').reverse().join('/') : '';
}

// 29/10: o dedo cadastrado passou a ser o indicador (pedido do usuário). Só o nome mudou — o enum
// mantém PolegarEsquerdo/PolegarDireito porque o valor gravado no banco é o número. Ordem: esquerdo na
// coluna da esquerda, direito na da direita, como as mãos do funcionário.
const INDICADORES: Array<{ dedo: DedoBiometria; rotulo: string }> = [
  { dedo: DedoBiometria.PolegarEsquerdo, rotulo: 'Indicador esquerdo' },
  { dedo: DedoBiometria.PolegarDireito, rotulo: 'Indicador direito' },
];

// Cadastro da digital na ficha do trabalhador (pedido do usuário, 21/10): um botão por indicador, logo
// abaixo da identificação. Cada indicador é cadastrado uma vez (a trava vale por dedo no backend, ver
// CadastrarTemplateBiometricoCommand); depois do cadastro o botão vira só a confirmação com a data.
// A leitura em si é o mesmo fluxo do CadastroDigitalDialog (duas leituras do mesmo dedo no Futronic).
export function BiometriaDigitalCartao({
  trabalhadorId,
  trabalhadorNome,
  aoAlterar,
}: {
  trabalhadorId: string;
  trabalhadorNome: string;
  aoAlterar?: () => void;
}) {
  const [status, setStatus] = useState<StatusCadastroBiometrico | null>(null);
  const [erro, setErro] = useState(false);
  const [dedoAlvo, setDedoAlvo] = useState<DedoBiometria | null>(null);

  async function carregar() {
    try {
      setErro(false);
      setStatus(await api.trabalhadores.obterStatusCadastroBiometrico(trabalhadorId));
    } catch {
      setErro(true);
    }
  }

  useEffect(() => {
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  const digitais = status?.digitais ?? [];
  const legado = digitais.find((d) => d.dedo === DedoBiometria.NaoInformado);
  const consentimentoRevogado = !!status?.consentimentoBiometriaRevogadoEm;
  const termosRegistrados = !!status?.consentimentoBiometriaEm && !consentimentoRevogado;

  return (
    <Card titulo="Biometria digital" subtitulo="Leitor de digital · cada indicador é cadastrado uma vez">
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: tokensUi.espaco.sm }}>
        {INDICADORES.map(({ dedo, rotulo }) => {
          const cadastro = digitais.find((d) => d.dedo === dedo);
          const tom: Tom = cadastro ? 'ok' : 'atencao';
          const bloqueado = !status || !!cadastro || consentimentoRevogado;
          return (
            <button
              key={dedo}
              type="button"
              onClick={() => setDedoAlvo(dedo)}
              disabled={bloqueado}
              aria-label={cadastro ? `${rotulo} cadastrado em ${dataBr(cadastro.cadastradaEm)}` : `Cadastrar ${rotulo.toLowerCase()}`}
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 6,
                padding: `${tokensUi.espaco.md} ${tokensUi.espaco.sm}`,
                borderRadius: tokensUi.raio.md,
                border: `1px solid ${cadastro ? tokensUi.status.ok.tinta : designTokens.colorCardBorder}`,
                backgroundColor: cadastro ? tokensUi.status.ok.fundo : designTokens.colorSurface,
                color: 'inherit',
                font: 'inherit',
                textAlign: 'center',
                cursor: bloqueado ? 'default' : 'pointer',
                opacity: !status || (consentimentoRevogado && !cadastro) ? 0.6 : 1,
                transition: `border-color ${tokensUi.duracao.rapido}, transform ${tokensUi.duracao.rapido}`,
              }}
            >
              <span
                aria-hidden="true"
                style={{
                  position: 'relative',
                  width: 44,
                  height: 44,
                  borderRadius: '50%',
                  display: 'grid',
                  placeItems: 'center',
                  backgroundColor: tokensUi.status[tom].fundo,
                  color: tokensUi.status[tom].tinta,
                }}
              >
                <Fingerprint24Regular />
                {cadastro && (
                  <span
                    style={{
                      position: 'absolute',
                      right: -2,
                      bottom: -2,
                      width: 18,
                      height: 18,
                      borderRadius: '50%',
                      display: 'grid',
                      placeItems: 'center',
                      backgroundColor: tokensUi.status.ok.tinta,
                      color: designTokens.colorWhite,
                    }}
                  >
                    <Checkmark16Regular style={{ width: 12, height: 12 }} />
                  </span>
                )}
              </span>
              <Text weight="semibold" size={200}>
                {rotulo}
              </Text>
              {cadastro ? (
                <Legenda>Cadastrado em {dataBr(cadastro.cadastradaEm)}</Legenda>
              ) : (
                <StatusChip tom="atencao">{status ? 'Cadastrar' : 'Verificando…'}</StatusChip>
              )}
            </button>
          );
        })}
      </div>

      {erro && (
        <div style={{ marginTop: tokensUi.espaco.sm }}>
          <Legenda>Não foi possível consultar as digitais cadastradas. Recarregue a página.</Legenda>
        </div>
      )}
      {legado && (
        <div style={{ marginTop: tokensUi.espaco.sm }}>
          <Legenda>
            Há uma digital cadastrada em {dataBr(legado.cadastradaEm)}, antes da separação por dedo. Ela continua
            valendo para assinar.
          </Legenda>
        </div>
      )}
      {consentimentoRevogado && (
        <div style={{ marginTop: tokensUi.espaco.sm }}>
          <Legenda>
            Consentimento de biometria revogado em {dataBr(status?.consentimentoBiometriaRevogadoEm)}: novos cadastros
            ficam bloqueados.
          </Legenda>
        </div>
      )}

      <CadastroDigitalDialog
        trabalhadorId={dedoAlvo !== null ? trabalhadorId : null}
        trabalhadorNome={trabalhadorNome}
        dedo={dedoAlvo ?? undefined}
        termosJaRegistrados={termosRegistrados}
        aoFechar={() => setDedoAlvo(null)}
        aoConcluir={() => {
          void carregar();
          aoAlterar?.();
        }}
      />
    </Card>
  );
}
