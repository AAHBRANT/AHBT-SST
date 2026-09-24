import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Card, designTokens, Spinner, StatusChip, Text } from '@ui';
import { CheckmarkCircle24Regular, ShieldError24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type DocumentoPublico } from '../../lib/api';

const hashEstilo = {
  marginTop: 4,
  wordBreak: 'break-all',
  fontFamily: 'monospace',
  fontSize: 11,
  color: designTokens.colorNeutralMedium,
} as const;

function formatarDataHoraBrasilia(valor: string) {
  return new Date(valor).toLocaleString('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

// Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 11) — página pública
// aberta ao escanear o QR do comprovante. Fica fora do AppShell (sem sidebar/header do Teams), mesmo
// padrão de IdentificacaoPublicaPage (módulo NTAG/Identificação) e template §4.6 "Público" (Onda 2,
// task de pontas soltas fora do escopo numerado): Card centralizado com largura máx. 480px,
// Badge→StatusChip (item 5 do Guia de conversão) — "Válido" é o próprio status do documento (tom
// "ok"), e o método de autenticação de cada signatário é rótulo/categoria sem semântica de estado,
// mesmo julgamento de colapso em tom="neutro" já registrado em RequisitosLegaisTab.tsx.
export function ValidarDocumentoPage() {
  const { token } = useParams<{ token: string }>();
  const [documento, setDocumento] = useState<DocumentoPublico | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [naoEncontrado, setNaoEncontrado] = useState(false);

  useEffect(() => {
    if (!token) return;
    setCarregando(true);
    setNaoEncontrado(false);
    setDocumento(null);
    api.validacaoPublica
      .resolver(token)
      .then(setDocumento)
      .catch(() => setNaoEncontrado(true))
      .finally(() => setCarregando(false));
  }, [token]);

  return (
    // data-theme="light" no wrapper (não só no FluentProvider de App.tsx): os tokens --sst-* que
    // Card/StatusChip consomem (spec §1.6) são custom properties de index.css escopadas por
    // [data-theme] em qualquer elemento, não só :root — mesmo achado documentado em
    // IdentificacaoPublicaPage.tsx.
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'flex-start',
        padding: '32px 16px',
        backgroundColor: designTokens.colorNeutralLight,
      }}
      data-theme="light"
    >
      <div style={{ width: '100%', maxWidth: 480 }}>
        {carregando && (
          <Card>
            <div style={{ display: 'flex', justifyContent: 'center', padding: '48px 0' }}>
              <Spinner label="Validando documento..." />
            </div>
          </Card>
        )}

        {!carregando && naoEncontrado && (
          <Card>
            <ShieldError24Regular />
            {/* `block` obrigatório: o Text do Fluent é display:inline por padrão e `as="p"` só troca
                a tag, não o display — sem isto os parágrafos saem colados um no outro. */}
            <Text block as="p" weight="semibold" style={{ marginTop: 8 }}>
              Documento não encontrado.
            </Text>
            <Text block as="p">
              Verifique se o link ou o QR Code está correto e se o documento foi finalizado.
            </Text>
          </Card>
        )}

        {!carregando && documento && (
          <Card
            titulo={documento.assinado ? 'Documento válido' : 'Documento rastreável'}
            subtitulo={`${documento.entidadeTipoRotulo} · emitido em ${formatarDataHoraBrasilia(documento.emitidoEm)} - Horário de Brasília`}
            acoes={
              <StatusChip tom={documento.assinado ? 'ok' : 'info'} icone={<CheckmarkCircle24Regular />}>
                {documento.assinado ? 'Válido' : 'Rastreável'}
              </StatusChip>
            }
          >
            <div style={{ marginTop: 16 }}>
              <Text block weight="semibold">
                Assinaturas registradas
              </Text>

              {/* Documento consolidado (Ficha de EPI, Ata de Sessão, DDS Semanal): as assinaturas
                  exibidas vêm dos registros que ele agrega, não dele próprio — dizer isso evita a
                  leitura de que o documento estaria sem assinatura. */}
              {documento.consolidado && documento.origemAssinaturas && (
                <Text
                  block
                  as="p"
                  style={{ marginTop: 4, color: designTokens.colorNeutralMedium }}
                >
                  {documento.origemAssinaturas}
                </Text>
              )}

              {documento.signatarios.length === 0 ? (
                <Text block as="p" style={{ marginTop: 4 }}>
                  Nenhuma assinatura eletrônica registrada até o momento.
                </Text>
              ) : (
                <ul style={{ margin: '4px 0 0', paddingLeft: 20 }}>
                  {documento.signatarios.map((s, i) => (
                    <li key={i} style={{ marginTop: 6 }}>
                      <Text>{s.trabalhadorNome} — </Text>
                      <StatusChip tom="neutro">
                        {metodoAutenticacaoAssinaturaLabel[s.metodoAutenticacao] ?? 'Método desconhecido'}
                      </StatusChip>
                      <Text> em {formatarDataHoraBrasilia(s.assinadoEm)} - Horário de Brasília</Text>
                      {(s.trabalhadorCpfMascarado || s.trabalhadorFuncaoNome) && (
                        <Text block style={{ fontSize: 11, color: designTokens.colorNeutralMedium }}>
                          {s.trabalhadorCpfMascarado ? `CPF: ${s.trabalhadorCpfMascarado}` : ''}
                          {s.trabalhadorCpfMascarado && s.trabalhadorFuncaoNome ? ' · ' : ''}
                          {s.trabalhadorFuncaoNome ? `Função: ${s.trabalhadorFuncaoNome}` : ''}
                        </Text>
                      )}
                      {/* Origem de rede mascarada: atesta que a assinatura tem registro de origem
                          sem publicar o endereço de quem assinou numa página anônima. */}
                      {s.origemRede && (
                        <Text block style={{ fontSize: 11, color: designTokens.colorNeutralMedium }}>
                          Origem de rede registrada: {s.origemRede}
                        </Text>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {/* Integridade. São duas provas distintas e a página diz explicitamente o que cada uma
                cobre — antes havia um único "Hash de integridade" que, na verdade, só cobria a lista
                de signatários, o que superestimava o que o QR provava.
                Não há download do PDF aqui por decisão do usuário (23/09): servir o arquivo de forma
                anônima tornaria um QR repassado um acesso indiscriminado ao documento. A conferência
                não precisa disso — quem tem o documento calcula o SHA-256 dele e compara. */}
            <div style={{ marginTop: 20 }}>
              <Text block weight="semibold">
                Integridade do documento
              </Text>

              {documento.hashPdf ? (
                <>
                  <Text block as="p" style={{ marginTop: 4, color: designTokens.colorNeutralMedium }}>
                    Impressão digital do documento emitido
                    {documento.arquivoAtualizadoEm
                      ? ` em ${formatarDataHoraBrasilia(documento.arquivoAtualizadoEm)} - Horário de Brasília`
                      : ''}{' '}
                    (SHA-256). Cobre todo o conteúdo: qualquer alteração de um único caractere muda
                    este código.
                  </Text>
                  <Text block as="p" style={hashEstilo}>
                    {documento.hashPdf}
                  </Text>
                  <Text
                    block
                    as="p"
                    style={{ marginTop: 6, fontSize: 11, color: designTokens.colorNeutralMedium }}
                  >
                    Para conferir o documento que você tem em mãos, calcule o SHA-256 do arquivo e
                    compare com o código acima — <code>certutil -hashfile arquivo.pdf SHA256</code> no
                    Windows, <code>sha256sum arquivo.pdf</code> no Linux. A conferência não depende
                    deste sistema.
                  </Text>
                </>
              ) : (
                <Text block as="p" style={{ marginTop: 4, color: designTokens.colorNeutralMedium }}>
                  A impressão digital do conteúdo passou a ser registrada nas emissões mais recentes.
                  Emita o documento novamente para que ela conste aqui.
                </Text>
              )}

              <Text block as="p" style={{ marginTop: 12, color: designTokens.colorNeutralMedium }}>
                Chave do registro de assinaturas (SHA-256). Cobre quem assinou, quando e por qual
                método — não o conteúdo do documento.
              </Text>
              <Text block as="p" style={hashEstilo}>
                {documento.conteudoHash}
              </Text>
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}
