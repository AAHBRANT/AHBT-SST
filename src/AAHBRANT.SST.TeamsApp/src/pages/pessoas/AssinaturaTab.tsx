import { useEffect, useState } from 'react';
import { Button, Card, FeedbackInline, Legenda } from '@ui';
import { CheckmarkCircle24Regular } from '@fluentui/react-icons';
import { api, type StatusCadastroBiometrico } from '../../lib/api';
import { RevogarConsentimentoBiometriaDialog } from '../../components/pessoas/RevogarConsentimentoBiometriaDialog';

interface AssinaturaTabProps {
  trabalhadorId: string;
}

function extrairMensagemErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trechoJson = e.message.match(/\{.*\}$/);
  if (trechoJson) {
    try {
      const corpo = JSON.parse(trechoJson[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON — cai no fallback abaixo
    }
  }
  return e.message || fallback;
}

// Aba de configuração do Motor de Assinatura Eletrônica para este trabalhador (docs/Motor-Assinatura-
// Eletronica.md §3/§5). Decisão do usuário (31/08): único método de cadastro nesta tela passa a ser a
// digital via leitor local Futronic FS80H — os cartões de PIN de assinatura (crachá/QR) e credencial
// WebAuthn/FIDO2 foram removidos daqui (o backend/API deles continua existindo, só não é mais
// oferecido nesta tela de cadastro por trabalhador).
//
// O Termo de Aceite de Assinatura Eletrônica e o consentimento LGPD de biometria são registros
// separados no backend. O texto abaixo orienta o operador, mas a captura continua bloqueada pelo
// backend caso esses registros ainda não existam para o trabalhador.
export function AssinaturaTab({ trabalhadorId }: AssinaturaTabProps) {
  // Regra do usuário (30/09): digital e facial são cadastrados uma única vez. Depois de concluído com
  // sucesso o botão de cadastro some e fica só a confirmação — o backend também recusa um 2º cadastro.
  const [statusCadastro, setStatusCadastro] = useState<StatusCadastroBiometrico | null>(null);
  const [revogandoConsentimento, setRevogandoConsentimento] = useState(false);
  const [versaoStatus, setVersaoStatus] = useState(0);

  useEffect(() => {
    let cancelado = false;
    setStatusCadastro(null);
    api.trabalhadores
      .obterStatusCadastroBiometrico(trabalhadorId)
      .then((s) => !cancelado && setStatusCadastro(s))
      .catch(() => undefined);
    return () => {
      cancelado = true;
    };
  }, [trabalhadorId, versaoStatus]);

  const [confirmandoAceite, setConfirmandoAceite] = useState(false);
  const [erroAceite, setErroAceite] = useState<string | null>(null);
  const [aceiteConfirmado, setAceiteConfirmado] = useState(false);

  // Pedido do usuário (22/09): botão único que registra os dois consentimentos de uma vez, enquanto
  // não existe tela própria com o texto jurídico definitivo (ver comentário acima da função do
  // componente). RegistrarTermoAceiteAssinaturaCommand/RegistrarConsentimentoBiometriaCommand não são
  // idempotentes por design (cada chamada é um novo evento de aceite com timestamp atual) — clicar de
  // novo só reafirma, não dá erro. Em sequência, não em paralelo (testado e corrigido em 22/09): os
  // dois comandos escrevem no mesmo Trabalhador, e a leitura+gravação concorrente das duas chamadas
  // batia no RowVersion (concorrência otimista do EF Core) e devolvia 409 Conflict.
  async function confirmarAceite() {
    try {
      setConfirmandoAceite(true);
      setErroAceite(null);
      setAceiteConfirmado(false);
      await api.trabalhadores.registrarTermoAceiteAssinatura(trabalhadorId);
      await api.trabalhadores.registrarConsentimentoBiometria(trabalhadorId);
      setAceiteConfirmado(true);
      setVersaoStatus((v) => v + 1);
    } catch (e) {
      setErroAceite(extrairMensagemErro(e, 'Falha ao confirmar o aceite.'));
    } finally {
      setConfirmandoAceite(false);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card densidade="compacta" titulo="Termos e consentimento">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <Legenda>
            Antes de cadastrar digital ou reconhecimento facial, o trabalhador deve ter Termo de Aceite de
            Assinatura Eletrônica e Termo de Consentimento LGPD para Biometria registrados.
          </Legenda>
          <Legenda>
            O consentimento biométrico cobre dado pessoal sensível usado para identificação e autenticação
            em documentos de SST, incluindo digital no leitor local e reconhecimento facial via Azure Face
            API quando a obra permitir esse método.
          </Legenda>
          <Legenda>
            <strong>Atenção:</strong> o texto jurídico definitivo do Termo de Aceite e do Consentimento LGPD
            ainda está pendente de revisão do jurídico. O botão abaixo só registra a data/hora da
            confirmação — use com o trabalhador ciente do que está aceitando, até o texto final substituir
            este aviso.
          </Legenda>
        </div>
        {erroAceite && (
          <FeedbackInline tom="erro" aoFechar={() => setErroAceite(null)}>
            {erroAceite}
          </FeedbackInline>
        )}
        {aceiteConfirmado && (
          <FeedbackInline tom="sucesso" aoFechar={() => setAceiteConfirmado(false)}>
            Termo de Aceite e Consentimento Biométrico confirmados para este trabalhador.
          </FeedbackInline>
        )}
        {statusCadastro?.consentimentoBiometriaEm && (
          <Legenda>
            Consentimento de biometria ativo desde {new Date(statusCadastro.consentimentoBiometriaEm).toLocaleString('pt-BR')}
            {statusCadastro.consentimentoBiometriaVersao ? ` (termo versão ${statusCadastro.consentimentoBiometriaVersao})` : ''}.
          </Legenda>
        )}
        {!statusCadastro?.consentimentoBiometriaEm && statusCadastro?.consentimentoBiometriaRevogadoEm && (
          <Legenda>
            Consentimento de biometria revogado em {new Date(statusCadastro.consentimentoBiometriaRevogadoEm).toLocaleString('pt-BR')}.
          </Legenda>
        )}
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8 }}>
          <Button
            icon={<CheckmarkCircle24Regular />}
            onClick={confirmarAceite}
            disabled={confirmandoAceite}
          >
            Confirmar aceite
          </Button>
          {statusCadastro?.consentimentoBiometriaEm && (
            <Button appearance="secondary" onClick={() => setRevogandoConsentimento(true)}>
              Revogar consentimento
            </Button>
          )}
        </div>
      </Card>
      <RevogarConsentimentoBiometriaDialog
        trabalhadorId={trabalhadorId}
        aberto={revogandoConsentimento}
        aoFechar={() => setRevogandoConsentimento(false)}
        aoRevogar={() => {
          setRevogandoConsentimento(false);
          setVersaoStatus((v) => v + 1);
        }}
      />

      <Legenda>O cadastro de digital e facial fica na aba Geral, no card Biometria.</Legenda>
    </div>
  );
}
