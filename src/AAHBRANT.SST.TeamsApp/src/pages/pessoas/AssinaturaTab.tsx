import { useState } from 'react';
import { Button, Text } from '@fluentui/react-components';
import { Fingerprint24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { capturarDigitalBrutaLocal } from '../../lib/agenteBiometricoLocal';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { usePageStyles } from '../pageStyles';

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
  const estilos = usePageStyles();

  const [cadastrandoBiometriaLocal, setCadastrandoBiometriaLocal] = useState(false);
  const [erroBiometriaLocal, setErroBiometriaLocal] = useState<string | null>(null);
  const [biometriaLocalCadastrada, setBiometriaLocalCadastrada] = useState(false);

  const [erroFacial, setErroFacial] = useState<string | null>(null);
  const [facialCadastrada, setFacialCadastrada] = useState(false);

  async function cadastrarFacial(arquivo: File) {
    try {
      setErroFacial(null);
      setFacialCadastrada(false);
      await api.trabalhadores.cadastrarFacial(trabalhadorId, arquivo);
      setFacialCadastrada(true);
    } catch (e) {
      setErroFacial(extrairMensagemErro(e, 'Falha ao cadastrar a face.'));
    }
  }

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
      <div className={estilos.card} style={{ maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
          Digital (leitor local — Futronic FS80H)
        </Text>
        <Text style={{ display: 'block', marginBottom: 12, color: 'var(--colorNeutralForeground3)' }}>
          Exige Termo de Aceite e consentimento de uso de biometria já registrados para este funcionário.
        </Text>
        {erroBiometriaLocal && <Text className={estilos.erro}>{erroBiometriaLocal}</Text>}
        {biometriaLocalCadastrada && (
          <Text style={{ display: 'block', marginBottom: 8 }}>Digital cadastrada com sucesso.</Text>
        )}
        <Button
          icon={<Fingerprint24Regular />}
          onClick={cadastrarBiometriaLocal}
          disabled={cadastrandoBiometriaLocal}
        >
          Capturar digital
        </Button>
      </div>

      <div className={estilos.card} style={{ maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
          Reconhecimento Facial (Azure)
        </Text>
        <Text style={{ display: 'block', marginBottom: 12, color: 'var(--colorNeutralForeground3)' }}>
          Método adicional ao leitor de digital — exige Termo de Aceite e consentimento de biometria já
          registrados para este trabalhador.
        </Text>
        {erroFacial && <Text className={estilos.erro}>{erroFacial}</Text>}
        {facialCadastrada && <Text style={{ display: 'block', marginBottom: 8 }}>Face cadastrada com sucesso.</Text>}
        <SeletorFotoCamera aoSelecionarArquivo={cadastrarFacial} rotulo="Capturar foto do rosto" />
      </div>
    </div>
  );
}
