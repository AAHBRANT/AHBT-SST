import { useState } from 'react';
import { Button, Card, FeedbackInline, Legenda } from '@ui';
import { CheckmarkCircle24Regular, Fingerprint24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { capturarDigitalBrutaLocal } from '../../lib/agenteBiometricoLocal';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';

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
  const [cadastrandoBiometriaLocal, setCadastrandoBiometriaLocal] = useState(false);
  const [erroBiometriaLocal, setErroBiometriaLocal] = useState<string | null>(null);
  const [biometriaLocalCadastrada, setBiometriaLocalCadastrada] = useState(false);

  const [erroFacial, setErroFacial] = useState<string | null>(null);
  const [facialCadastrada, setFacialCadastrada] = useState(false);

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
    } catch (e) {
      setErroAceite(extrairMensagemErro(e, 'Falha ao confirmar o aceite.'));
    } finally {
      setConfirmandoAceite(false);
    }
  }

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
        <Button
          icon={<CheckmarkCircle24Regular />}
          onClick={confirmarAceite}
          disabled={confirmandoAceite}
          style={{ marginTop: 8 }}
        >
          Confirmar aceite
        </Button>
      </Card>

      <Card densidade="compacta" titulo="Digital (leitor local — Futronic FS80H)">
        <div style={{ marginBottom: 12 }}>
          <Legenda>
            Exige aceite de assinatura eletrônica e consentimento biométrico já registrados para este
            trabalhador.
          </Legenda>
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

      <Card densidade="compacta" titulo="Reconhecimento Facial (Azure)">
        <div style={{ marginBottom: 12 }}>
          <Legenda>
            Método adicional ao leitor de digital. A foto facial é enviada ao Azure Face API para cadastro
            e autenticação, usando o consentimento biométrico LGPD já registrado para este trabalhador.
          </Legenda>
        </div>
        {/* Esta é a foto de referência: é contra ela que toda assinatura futura vai ser comparada.
            Foto ruim aqui vira "baixa confiança" na hora de assinar, semanas depois, e ninguém liga
            uma coisa à outra. Por isso o servidor recusa foto fraca (ver ValidarQualidadeParaCadastro)
            e a orientação vem antes da captura, não só no erro. */}
        <div style={{ marginBottom: 12 }}>
          <Legenda>
            <strong>Para a foto ser aceita:</strong> rosto de frente ocupando boa parte do quadro, luz vindo
            da frente (nunca contra janela), sem boné, capacete na testa ou óculos escuros, olhando direto
            para a câmera e sem tremer. Só o trabalhador no enquadramento.
          </Legenda>
        </div>
        {erroFacial && (
          <FeedbackInline tom="erro" aoFechar={() => setErroFacial(null)}>
            {erroFacial}
          </FeedbackInline>
        )}
        {facialCadastrada && <FeedbackInline tom="sucesso">Face cadastrada com sucesso.</FeedbackInline>}
        <SeletorFotoCamera
          aoSelecionarArquivo={cadastrarFacial}
          aoErroValidacao={setErroFacial}
          rotulo="Capturar foto do rosto"
          modoCamera="user"
          exigirCamera
        />
      </Card>
    </div>
  );
}
