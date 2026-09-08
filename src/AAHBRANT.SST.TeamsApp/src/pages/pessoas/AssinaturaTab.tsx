import { useState } from 'react';
import { Button, Card, FeedbackInline, Legenda } from '@ui';
import { Fingerprint24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { capturarDigitalBrutaLocal } from '../../lib/agenteBiometricoLocal';

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
// O Termo de Aceite (MP 2.200-2/2001) e o consentimento LGPD de biometria ainda não têm tela própria
// — o texto jurídico depende de revisão do jurídico e não deve ser aproximado por um texto provisório
// aqui. Até lá, essas duas confirmações são registradas via Swagger (POST /api/trabalhadores/{id}/
// assinatura/termo-aceite e .../consentimento-biometria) e o cadastro de biometria abaixo vai
// simplesmente devolver o erro real do backend se elas ainda não existirem.
export function AssinaturaTab({ trabalhadorId }: AssinaturaTabProps) {
  const [cadastrandoBiometriaLocal, setCadastrandoBiometriaLocal] = useState(false);
  const [erroBiometriaLocal, setErroBiometriaLocal] = useState<string | null>(null);
  const [biometriaLocalCadastrada, setBiometriaLocalCadastrada] = useState(false);

  async function cadastrarBiometriaLocal() {
    try {
      setCadastrandoBiometriaLocal(true);
      setErroBiometriaLocal(null);
      setBiometriaLocalCadastrada(false);
      const templateBase64 = await capturarDigitalBrutaLocal();
      await api.trabalhadores.cadastrarBiometriaLocal(trabalhadorId, templateBase64);
      setBiometriaLocalCadastrada(true);
    } catch (e) {
      setErroBiometriaLocal(extrairMensagemErro(e, 'Falha ao cadastrar a digital.'));
    } finally {
      setCadastrandoBiometriaLocal(false);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card densidade="compacta" titulo="Digital (leitor local — Futronic FS80H)">
        <div style={{ marginBottom: 12 }}>
          <Legenda>Exige Termo de Aceite e consentimento de uso de biometria já registrados para este funcionário.</Legenda>
        </div>
        {erroBiometriaLocal && (
          <FeedbackInline tom="erro" aoFechar={() => setErroBiometriaLocal(null)}>
            {erroBiometriaLocal}
          </FeedbackInline>
        )}
        {biometriaLocalCadastrada && (
          <FeedbackInline tom="sucesso" aoFechar={() => setBiometriaLocalCadastrada(false)}>
            Digital cadastrada com sucesso.
          </FeedbackInline>
        )}
        <Button
          icon={<Fingerprint24Regular />}
          onClick={cadastrarBiometriaLocal}
          disabled={cadastrandoBiometriaLocal}
        >
          Capturar digital
        </Button>
      </Card>
    </div>
  );
}
