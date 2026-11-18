import { useEffect, useRef, type CSSProperties } from 'react';
import { api, type EquipamentoPublicoDto } from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import {
  ADESIVO_GRADIENTE,
  ADESIVO_MARCA,
  ADESIVO_TITULO,
  adesivoEstaAntigo,
  formatarDataAdesivo,
} from '../../components/inspecoes/adesivoVisual';
import { IconeSituacaoAdesivo } from '../../components/inspecoes/IconeSituacaoAdesivo';

// Card público "Equipamento/Local" aberto pelo QR do adesivo (mockup aprovado:
// docs/mockups/adesivo-inspecao-2026-11-17.html, seção 4). Sem login e sempre no tema claro, por isso as
// cores são fixas (mesmas do mockup) e não acompanham o tema do app. Mostra só a situação ATUAL: nunca CPF
// nem o detalhe das não conformidades, e o inspetor aparece só pelo nome.

const TINTA = '#09090b';
const MUDO = '#6f7280';
const BORDA = '#dedbea';

const raiz: CSSProperties = { minHeight: '100vh', backgroundColor: '#f4f4f8', color: TINTA };
const coluna: CSSProperties = { maxWidth: 560, margin: '0 auto', minHeight: '100vh', backgroundColor: '#f4f4f8', display: 'flex', flexDirection: 'column' };
const barra: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  gap: 12,
  padding: '10px 16px',
  backgroundColor: '#fff',
  borderBottom: `3px solid ${ADESIVO_MARCA}`,
};
const caixa: CSSProperties = { padding: 16, display: 'flex', flexDirection: 'column', gap: 12 };
const painel: CSSProperties = { backgroundColor: '#fff', border: `1px solid ${BORDA}`, borderRadius: 14 };

function notaRodape(dto: EquipamentoPublicoDto) {
  if (dto.situacao === 'NaoLiberado') return 'Não utilize até uma nova inspeção. Procure o responsável pela segurança da obra.';
  // O aviso da etiqueta de manutenção só vale para extintor.
  if (/extintor/i.test(dto.tema)) return 'Inspeção interna de SST. Não substitui a etiqueta de manutenção do extintor.';
  return dto.temDocumento
    ? 'Inspeção interna de SST. O documento assinado traz o checklist completo da inspeção.'
    : 'Inspeção interna de SST. Dados pessoais e detalhes da inspeção não são exibidos nesta página.';
}

interface EquipamentoPublicoCardProps {
  dados: EquipamentoPublicoDto;
  /** UID da tag lida (rota /p/{uid}); usado para abrir o PDF assinado da inspeção. */
  uid: string;
  /** ?i= do QR: protocolo da inspeção que gerou o adesivo. */
  protocoloAdesivo?: string | null;
}

export function EquipamentoPublicoCard({ dados, uid, protocoloAdesivo }: EquipamentoPublicoCardProps) {
  const adesivoAntigo = adesivoEstaAntigo(dados);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  // Ao ler o QR, o PDF assinado da inspeção do adesivo abre sozinho (decisão do usuário em 18/11/2026); ao fechar,
  // fica a situação atual e o botão para abrir de novo.
  const abrirDocumento = () =>
    void visualizar({
      titulo: `Inspeção ${dados.protocolo}`,
      nomeArquivo: `${dados.protocolo || 'inspecao'}.pdf`,
      obter: () => api.identificacaoPublica.baixarDocumentoEquipamento(uid, protocoloAdesivo),
    });
  const abriuSozinho = useRef(false);
  useEffect(() => {
    if (!dados.temDocumento || !uid || abriuSozinho.current) return;
    abriuSozinho.current = true;
    abrirDocumento();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dados.temDocumento, uid]);

  const linhas: Array<[string, string]> = [['Última inspeção', formatarDataAdesivo(dados.ultimaInspecaoEm)]];
  if (dados.proximaInspecaoEm) {
    linhas.push(['Próxima inspeção até', formatarDataAdesivo(dados.proximaInspecaoEm)]);
  } else if (dados.situacao === 'NaoLiberado') {
    linhas.push(['Próxima inspeção', 'Reinspeção antes do uso']);
  }
  linhas.push(['Protocolo', dados.protocoloAtual || dados.protocolo], ['Inspetor', dados.inspetorNome]);

  return (
    <div style={raiz} data-theme="light">
      <div style={coluna}>
        {/* Logo pública da obra ainda não vem da rota (decisão de LGPD pendente): o cabeçalho leva o nome da obra. */}
        <div style={barra}>
          <b style={{ fontSize: 13, lineHeight: '16px', fontWeight: 800, color: ADESIVO_MARCA, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            {dados.obraNome}
          </b>
          <span style={{ fontSize: 11, lineHeight: 1, fontWeight: 700, letterSpacing: '0.5px', color: ADESIVO_MARCA, textAlign: 'right' }}>
            Situação atual
          </span>
        </div>

        <div style={caixa}>
          {adesivoAntigo && (
            <div
              style={{
                display: 'flex',
                gap: 10,
                padding: '12px 14px',
                borderRadius: 14,
                backgroundColor: '#fdf3e3',
                border: '1px solid #f0cf94',
                fontSize: 13,
                lineHeight: '18px',
                fontWeight: 600,
              }}
            >
              <svg viewBox="0 0 24 24" width={20} height={20} fill="none" stroke="#d97706" strokeWidth="2" aria-hidden="true" style={{ flex: 'none', marginTop: 1 }}>
                <circle cx="12" cy="12" r="9" />
                <path d="M12 11v6M12 7.5v.5" />
              </svg>
              <span>
                Este adesivo é da inspeção {dados.protocolo}. A mais recente é a {dados.protocoloAtual}. A situação abaixo é a atual.
              </span>
            </div>
          )}

          <div
            role="status"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 12,
              padding: '18px 16px',
              borderRadius: 14,
              color: '#fff',
              backgroundImage: ADESIVO_GRADIENTE[dados.situacao],
            }}
          >
            <IconeSituacaoAdesivo situacao={dados.situacao} tamanho={44} />
            <div>
              <small style={{ display: 'block', fontSize: 11, lineHeight: '14px', fontWeight: 700, letterSpacing: 1, textTransform: 'uppercase', opacity: 0.9 }}>
                Situação atual
              </small>
              <b style={{ display: 'block', fontSize: 28, lineHeight: '32px', fontWeight: 900, letterSpacing: 0.3 }}>
                {ADESIVO_TITULO[dados.situacao]}
              </b>
            </div>
          </div>

          {dados.temDocumento && (
            <button
              type="button"
              onClick={abrirDocumento}
              style={{
                minHeight: 52,
                borderRadius: 10,
                border: 0,
                backgroundColor: ADESIVO_MARCA,
                color: '#fff',
                fontSize: 15,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              Ver documento da inspeção (PDF)
            </button>
          )}
          {dialogoVisualizador}

          <div style={{ ...painel, padding: '14px 16px' }}>
            <small style={{ display: 'block', fontSize: 12, lineHeight: '16px', fontWeight: 600, color: MUDO }}>{dados.tema}</small>
            <b style={{ display: 'block', fontSize: 20, lineHeight: '26px', fontWeight: 800 }}>{dados.objetoIdentificacao}</b>
            <span style={{ fontSize: 13, lineHeight: '18px', fontWeight: 600, color: '#444' }}>
              {dados.situacao === 'NaoLiberado' ? `Uso bloqueado · ${dados.obraNome}` : dados.obraNome}
            </span>
          </div>

          <dl style={{ ...painel, margin: 0, padding: '6px 16px' }}>
            {linhas.map(([rotulo, valor], i) => (
              <div
                key={rotulo}
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  gap: 12,
                  padding: '11px 0',
                  borderBottom: i === linhas.length - 1 ? 0 : '1px solid #eceaf3',
                }}
              >
                <dt style={{ fontSize: 12, lineHeight: '18px', fontWeight: 600, color: MUDO }}>{rotulo}</dt>
                <dd style={{ margin: 0, fontSize: 14, lineHeight: '18px', fontWeight: 600, textAlign: 'right' }}>{valor}</dd>
              </div>
            ))}
          </dl>

          <p style={{ margin: 0, padding: '4px 8px 16px', fontSize: 11, lineHeight: '15px', fontWeight: 500, color: MUDO, textAlign: 'center' }}>
            {adesivoAntigo ? 'Troque o adesivo antigo pelo mais recente.' : notaRodape(dados)}
          </p>
        </div>
      </div>
    </div>
  );
}
