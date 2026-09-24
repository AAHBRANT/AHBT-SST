using System.Globalization;
using System.Text;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Movido de IAutenticacaoAssinaturaService.cs (31/08) quando PIN/crachá-QR e WebAuthn/FIDO2 foram
// removidos do sistema (decisão do usuário: único método de assinatura é o Futronic FS80H) — o
// record continua compartilhado pelas estratégias que restaram (Futronic, sessão logada).
public record ResultadoAutenticacaoAssinatura(Guid TrabalhadorId, MetodoAutenticacaoAssinatura Metodo);

// Extraído de RegistrarAssinaturaCommandHandler na etapa 13: gravar o DocumentoSignatario + trilha de
// auditoria é idêntico não importa qual estratégia autenticou o trabalhador, só muda como o
// ResultadoAutenticacaoAssinatura foi obtido — cada estratégia tem sua própria cerimônia, mas o que
// acontece depois de autenticado é sempre o mesmo.
public interface IRegistradorAssinaturaService
{
    Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, string? ipAddress, CancellationToken ct);
}

public class RegistradorAssinaturaService : IRegistradorAssinaturaService
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public RegistradorAssinaturaService(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, string? ipAddress, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.Id == documentoAssinaturaId, ct);
        if (documento is null)
            throw new KeyNotFoundException("Documento de assinatura não encontrado.");
        if (documento.Status != StatusDocumentoAssinatura.EmAndamento)
            throw new InvalidOperationException("Este documento não está mais aceitando assinaturas.");

        var jaAssinou = await _db.DocumentoSignatarios.AnyAsync(
            s => s.DocumentoAssinaturaId == documento.Id
                && s.TrabalhadorId == resultado.TrabalhadorId
                && MesmoPapelDeAssinatura(s.MetodoAutenticacao, resultado.Metodo), ct);
        if (jaAssinou)
            throw new InvalidOperationException("Este trabalhador já assinou este documento.");

        var trabalhador = await _db.Trabalhadores.Include(t => t.Funcao).FirstAsync(t => t.Id == resultado.TrabalhadorId, ct);

        await GarantirQueEhOSignatarioEsperadoAsync(documento, resultado, trabalhador, ct);

        // Regras específicas de Inspeção/Patrulha de Segurança (pedido do usuário, 01/09) — o Motor
        // de Assinatura é genérico (qualquer trabalhador assina DDS/PT/EPI), então essas duas
        // restrições só valem quando a origem é uma Inspecao: (1) assinatura única — a primeira
        // assinatura já fecha o documento pra novas assinaturas; (2) só Técnico ou Engenheiro de
        // Segurança podem ser o signatário, verificado pela Função cadastrada do trabalhador.
        if (documento.EntidadeTipo == nameof(Inspecao))
        {
            var jaTemAssinatura = await _db.DocumentoSignatarios.AnyAsync(s => s.DocumentoAssinaturaId == documento.Id, ct);
            if (jaTemAssinatura)
                throw new InvalidOperationException("Esta inspeção já foi assinada — a Patrulha de Segurança aceita apenas uma assinatura.");

            if (!FuncaoPodeAssinarInspecao(trabalhador.Funcao?.Nome))
                throw new InvalidOperationException("Apenas Técnico de Segurança ou Engenheiro de Segurança podem assinar inspeções.");
        }

        var signatario = new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = resultado.TrabalhadorId,
            MetodoAutenticacao = resultado.Metodo,
            AssinadoEm = DateTime.UtcNow,
            IpAddress = ipAddress,
        };
        _db.DocumentoSignatarios.Add(signatario);

        await _auditoria.RegistrarAsync(
            "Assinatura.Registrada",
            documento.EntidadeTipo,
            documento.EntidadeId,
            usuarioId: null,
            trabalhadorId: trabalhador.Id,
            dadosDepois: new { DocumentoAssinaturaId = documento.Id, TrabalhadorId = trabalhador.Id, TrabalhadorNome = trabalhador.Nome, signatario.MetodoAutenticacao, signatario.AssinadoEm },
            ct);

        await _db.SaveChangesAsync(ct);

        return new DocumentoSignatarioDto(trabalhador.Id, trabalhador.Nome, signatario.MetodoAutenticacao, signatario.AssinadoEm, signatario.IpAddress);
    }

    // Nome da Função é texto livre no cadastro (não existe um "tipo de função" fechado no sistema —
    // ver Funcao.cs) — comparação por prefixo normalizado (sem acento/caixa) pra tolerar variações
    // como "Técnico de Segurança do Trabalho" ou "Engenheiro de Segurança Sênior", sem depender de
    // um nome cadastrado byte a byte igual. Lista fechada às duas funções indicadas pelo usuário
    // (01/09); qualquer outra função (mesmo "de segurança") fica de fora até ele pedir para incluir.
    private static readonly string[] FuncoesHabilitadasAssinarInspecao =
    {
        "tecnico de seguranca",
        "engenheiro de seguranca",
    };

    // Confere se quem a biometria identificou é mesmo a pessoa de quem o documento trata.
    //
    // Sem isto, o registro aceitava qualquer trabalhador identificado: se o reconhecimento facial
    // devolvesse um colega parecido acima do limiar, a entrega de EPI do Gabriel ficava assinada em
    // nome desse colega — registro errado numa peça com valor juridico. O risco ficou concreto em
    // 24/09, quando o Azure devolveu um candidato para alguem que sequer tinha cadastro facial (o
    // identify sempre retorna o mais parecido acima de 50%); quem barrou foi o limiar de confianca,
    // que e a ultima linha, nao a unica que deveria existir.
    //
    // Vale so para os metodos que identificam a PESSOA (facial e digital). SessaoLogada fica de
    // fora de proposito: ali quem assina é o responsavel/instrutor logado, que legitimamente nao é
    // o trabalhador do documento.
    //
    // Tipos sem dono unico (SessaoTreinamento, onde cada participante assina; Inspecao, que tem
    // regra propria por Funcao logo acima; APR/PT, assinadas pela equipe) nao entram: para eles nao
    // existe "o signatario esperado", e restringir quebraria fluxo legitimo.
    private async Task GarantirQueEhOSignatarioEsperadoAsync(
        DocumentoAssinatura documento, ResultadoAutenticacaoAssinatura resultado, Trabalhador identificado, CancellationToken ct)
    {
        if (resultado.Metodo == MetodoAutenticacaoAssinatura.SessaoLogada)
            return;

        var trabalhadorEsperadoId = documento.EntidadeTipo switch
        {
            "EntregaEpi" or "DevolucaoEpi" => await _db.EntregasEpi
                .Where(e => e.Id == documento.EntidadeId).Select(e => (Guid?)e.TrabalhadorId).FirstOrDefaultAsync(ct),
            "EntregaUniforme" => await _db.EntregasUniforme
                .Where(e => e.Id == documento.EntidadeId).Select(e => (Guid?)e.TrabalhadorId).FirstOrDefaultAsync(ct),
            "Treinamento" => await _db.Treinamentos
                .Where(t => t.Id == documento.EntidadeId).Select(t => (Guid?)t.TrabalhadorId).FirstOrDefaultAsync(ct),
            // A ficha consolidada é do próprio trabalhador: a entidade É ele.
            "FichaEpiTrabalhador" => documento.EntidadeId,
            _ => null,
        };

        if (trabalhadorEsperadoId is null || trabalhadorEsperadoId == identificado.Id)
            return;

        var nomeEsperado = await _db.Trabalhadores
            .Where(t => t.Id == trabalhadorEsperadoId.Value)
            .Select(t => t.Nome)
            .FirstOrDefaultAsync(ct);

        var comoFoiIdentificado = resultado.Metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial
            ? "O rosto reconhecido"
            : "A digital lida";

        throw new InvalidOperationException(
            $"{comoFoiIdentificado} é de {identificado.Nome}, mas este documento é de {nomeEsperado ?? "outro trabalhador"} — " +
            "a assinatura não foi registrada. Confirme que quem está assinando é a pessoa certa; " +
            "se for, o cadastro biométrico de um dos dois precisa ser refeito.");
    }

    private static bool FuncaoPodeAssinarInspecao(string? nomeFuncao)
    {
        if (string.IsNullOrWhiteSpace(nomeFuncao)) return false;
        var normalizado = RemoverAcentos(nomeFuncao.Trim().ToLowerInvariant());
        return FuncoesHabilitadasAssinarInspecao.Any(f => normalizado.StartsWith(f, StringComparison.Ordinal));
    }

    private static bool MesmoPapelDeAssinatura(MetodoAutenticacaoAssinatura existente, MetodoAutenticacaoAssinatura novo)
    {
        return EhAssinaturaDeResponsavel(existente) == EhAssinaturaDeResponsavel(novo);
    }

    private static bool EhAssinaturaDeResponsavel(MetodoAutenticacaoAssinatura metodo)
    {
        return metodo == MetodoAutenticacaoAssinatura.SessaoLogada;
    }

    private static string RemoverAcentos(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder();
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                semAcento.Append(c);
        }
        return semAcento.ToString().Normalize(NormalizationForm.FormC);
    }
}
