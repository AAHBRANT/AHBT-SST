namespace AAHBRANT.SST.Application.Acidentes;

// Relatório de Investigação de Ocorrência (pedido de 23/10, desenho aprovado em
// docs/mockups/relatorio-investigacao-ocorrencia-v1.html): tudo o que já está no registro da
// ocorrência — identificação, descrição, consequências, análise/causas, plano de ação, reunião e
// fotos — num PDF com o cabeçalho/rodapé padrão. Os textos já chegam traduzidos (rótulos dos enums),
// o serviço de PDF só desenha.
public record InvestigacaoOcorrenciaPdfAcaoModelo(
    string Descricao,
    string? Fundamentacao,
    string Tipo,
    string? ResponsavelNome,
    DateTime? Prazo,
    string Situacao,
    bool Concluida);

public record InvestigacaoOcorrenciaPdfFotoModelo(
    byte[] Conteudo,
    DateTime? CapturadaEmUtc,
    double? Latitude,
    double? Longitude,
    // Foto enviada de arquivo (18/11/2026): data/GPS lidos da própria foto, não comprovados.
    bool DeArquivo = false,
    DateTime? RecebidaEmUtc = null);

// Reunião realizada: sai data, presentes e ata. Só agendada: sai o horário e os convidados.
public record InvestigacaoOcorrenciaPdfReuniaoModelo(
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<string> Participantes,
    bool NoTeams,
    DateTime? RealizadaEm = null,
    IReadOnlyList<string>? Presentes = null,
    string? Ata = null);

// Pessoa externa no PDF: CPF sempre mascarado (o relatório circula fora do sistema); telefone não vai.
public record InvestigacaoOcorrenciaPdfPessoaExternaModelo(
    string Nome,
    string Vinculo,
    string? Empresa,
    string? CpfMascarado,
    int? Idade,
    string Resultado,
    string? Lesao,
    string? Atendimento);

public record InvestigacaoOcorrenciaPdfModelo(
    string? ObraNome,
    byte[]? ObraLogoConteudo,
    string Tipo,
    DateTime Data,
    TimeSpan? Hora,
    string Local,
    string? AtividadeNome,
    IReadOnlyList<string> Envolvidos,
    string Situacao,
    DateTime? DataConclusaoInvestigacao,
    string Descricao,
    string Gravidade,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    int DiasDebitados,
    string? Lesao,
    string? Atendimento,
    string? Consequencia,
    string? Metodologia,
    string? Causas,
    IReadOnlyList<InvestigacaoOcorrenciaPdfAcaoModelo> Acoes,
    InvestigacaoOcorrenciaPdfReuniaoModelo? Reuniao,
    IReadOnlyList<InvestigacaoOcorrenciaPdfFotoModelo> Fotos,
    string ConteudoHash,
    string UrlValidacaoPublica,
    byte[] QrCodePng,
    bool TemAssinatura,
    // Códigos do mapa corporal (RegioesCorpoAcidente): o serviço desenha o mapa e lista os rótulos.
    IReadOnlyList<string>? RegioesCorpo = null,
    IReadOnlyList<string>? Testemunhas = null,
    IReadOnlyList<InvestigacaoOcorrenciaPdfPessoaExternaModelo>? PessoasExternas = null,
    string? ConclusaoInvestigacao = null,
    // Quem assinou eletronicamente a ocorrência (DocumentoSignatarios do documento finalizado).
    // Vazio = seção 8 continua com as linhas em branco para assinatura à mão.
    IReadOnlyList<InvestigacaoOcorrenciaPdfSignatarioModelo>? Signatarios = null,
    string? HashAssinatura = null,
    // Certificado de Assinaturas Eletrônicas (evidência de cada assinatura), anexado no fim do PDF.
    AAHBRANT.SST.Application.Assinatura.CertificadoAssinaturasModelo? Certificado = null);

public record InvestigacaoOcorrenciaPdfSignatarioModelo(
    string Nome,
    string? Funcao,
    DateTime AssinadoEm,
    AAHBRANT.SST.Domain.Enums.MetodoAutenticacaoAssinatura Metodo);

public interface IInvestigacaoOcorrenciaPdfService
{
    byte[] Gerar(InvestigacaoOcorrenciaPdfModelo modelo);
}
