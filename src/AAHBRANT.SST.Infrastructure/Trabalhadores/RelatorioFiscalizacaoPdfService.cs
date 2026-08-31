using System.Security.Cryptography;
using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Documentos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Trabalhadores;

// Relatório único de fiscalização (MTE) — mesmo padrão visual de DocumentoAssinaturaPdfService
// (branding AAHBRANT #670000). O hash SHA-256 do rodapé não pode se auto-referenciar, então o PDF é
// gerado em duas passadas: a primeira sem o rodapé de hash (para poder calculá-lo sobre o conteúdo),
// a segunda já com o hash calculado impresso no rodapé.
public class RelatorioFiscalizacaoPdfService : IRelatorioFiscalizacaoPdfService
{
    private const string CorMarca = "#670000";

    public byte[] Gerar(PerfilCompletoTrabalhadorDto perfil)
    {
        var semHash = CriarDocumento(perfil, hashConteudo: null).GeneratePdf();
        var hash = Convert.ToHexString(SHA256.HashData(semHash));
        return CriarDocumento(perfil, hashConteudo: hash).GeneratePdf();
    }

    private static IDocument CriarDocumento(PerfilCompletoTrabalhadorDto perfil, string? hashConteudo)
    {
        return Document.Create(container =>
        {
            container.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Column(coluna =>
                    CabecalhoDocumentoPadrao.Desenhar(coluna, "Relatório de Fiscalização — Perfil de Vida do Trabalhador", perfil.ObraNome, perfil.ObraLogoConteudo));

                pagina.Content().PaddingVertical(12).Column(coluna =>
                {
                    coluna.Spacing(10);

                    coluna.Item().Text("Dados gerais").FontSize(13).Bold().FontColor(CorMarca);
                    coluna.Item().Text(t => { t.Span("Nome: ").SemiBold(); t.Span(perfil.Nome); });
                    coluna.Item().Text(t => { t.Span("Matrícula: ").SemiBold(); t.Span(perfil.Matricula); });
                    coluna.Item().Text(t => { t.Span("CPF: ").SemiBold(); t.Span(perfil.Cpf); });
                    if (!string.IsNullOrWhiteSpace(perfil.Rg))
                        coluna.Item().Text(t => { t.Span("RG: ").SemiBold(); t.Span(perfil.Rg); });
                    coluna.Item().Text(t => { t.Span("Obra: ").SemiBold(); t.Span(perfil.ObraNome); });
                    coluna.Item().Text(t => { t.Span("Função: ").SemiBold(); t.Span(perfil.FuncaoNome); });
                    coluna.Item().Text(t => { t.Span("Admissão: ").SemiBold(); t.Span(perfil.DataAdmissao.ToString("dd/MM/yyyy")); });
                    coluna.Item().Text(t => { t.Span("Situação de aptidão: ").SemiBold(); t.Span(perfil.StatusAptidao); });

                    coluna.Item().PaddingTop(6).Text("ASO — Atestado de Saúde Ocupacional").FontSize(13).Bold().FontColor(CorMarca);
                    if (perfil.Asos.Count == 0)
                        coluna.Item().Text("Nenhum ASO registrado.").Italic();
                    else
                    {
                        var vigente = perfil.Asos[0];
                        coluna.Item().Text(t =>
                        {
                            t.Span($"{DescreverTipoAso(vigente.Tipo)} — ").SemiBold();
                            t.Span($"exame em {vigente.DataExame:dd/MM/yyyy}, válido até {vigente.DataValidade:dd/MM/yyyy} — {DescreverResultadoAso(vigente.ResultadoStatus)}");
                        });
                        if (!string.IsNullOrWhiteSpace(vigente.MedicoNome))
                            coluna.Item().Text($"Médico responsável: {vigente.MedicoNome}{(string.IsNullOrWhiteSpace(vigente.MedicoCrm) ? "" : $" (CRM {vigente.MedicoCrm})")}").FontSize(9);
                    }

                    coluna.Item().PaddingTop(6).Text("EPIs em posse do trabalhador").FontSize(13).Bold().FontColor(CorMarca);
                    if (perfil.EpisAtivos.Count == 0)
                        coluna.Item().Text("Nenhum EPI ativo registrado.").Italic();
                    else
                        foreach (var epi in perfil.EpisAtivos)
                            coluna.Item().Text($"• Entregue em {epi.DataEntrega:dd/MM/yyyy}" + (epi.DataValidade.HasValue ? $", válido até {epi.DataValidade:dd/MM/yyyy}" : "") + $" — quantidade {epi.Quantidade}");

                    coluna.Item().PaddingTop(6).Text("Treinamentos válidos").FontSize(13).Bold().FontColor(CorMarca);
                    var treinamentosValidos = perfil.Treinamentos.Where(t => t.DataValidade >= DateTime.Today).ToList();
                    if (treinamentosValidos.Count == 0)
                        coluna.Item().Text("Nenhum treinamento válido registrado.").Italic();
                    else
                        foreach (var treinamento in treinamentosValidos)
                            coluna.Item().Text($"• Realizado em {treinamento.DataRealizacao:dd/MM/yyyy}, válido até {treinamento.DataValidade:dd/MM/yyyy} ({treinamento.CargaHorariaRealizada}h)" + (string.IsNullOrWhiteSpace(treinamento.InstituicaoInstrutor) ? "" : $" — {treinamento.InstituicaoInstrutor}"));

                    coluna.Item().PaddingTop(6).Text("Riscos expostos (PGR)").FontSize(13).Bold().FontColor(CorMarca);
                    if (perfil.Riscos.Count == 0)
                        coluna.Item().Text("Nenhum risco vinculado.").Italic();
                    else
                        foreach (var risco in perfil.Riscos)
                            coluna.Item().Text($"• {risco.PerigoNome} ({risco.AtividadeNome}) — nível {risco.NivelRisco} (P{risco.Probabilidade}×S{risco.Severidade})");

                    coluna.Item().PaddingTop(6).Text("Ocorrências").FontSize(13).Bold().FontColor(CorMarca);
                    if (perfil.Ocorrencias.Count == 0)
                        coluna.Item().Text("Nenhuma ocorrência registrada.").Italic();
                    else
                        foreach (var ocorrencia in perfil.Ocorrencias)
                            coluna.Item().Text($"• {ocorrencia.Data:dd/MM/yyyy} — {ocorrencia.Tipo}, gravidade {ocorrencia.Gravidade}" + (ocorrencia.HouveAfastamento ? $", {ocorrencia.DiasAfastamento ?? 0} dia(s) de afastamento" : ""));

                    coluna.Item().PaddingTop(6).Text("Cofre de assinaturas").FontSize(13).Bold().FontColor(CorMarca);
                    if (perfil.Assinaturas.Count == 0)
                        coluna.Item().Text("Nenhuma assinatura registrada.").Italic();
                    else
                        foreach (var assinatura in perfil.Assinaturas)
                            coluna.Item().Text($"• {assinatura.EntidadeTipo} — {assinatura.AssinadoEm:dd/MM/yyyy HH:mm}, IP {assinatura.IpAddress ?? "não registrado"}").FontSize(9);

                    coluna.Item().PaddingTop(12).Text("Documento gerado para fins de fiscalização, consolidando os registros de SST do trabalhador na data de emissão.").FontSize(9).Italic();

                    if (hashConteudo is not null)
                    {
                        coluna.Item().PaddingTop(8).Text(t =>
                        {
                            t.Span("Hash de integridade (SHA-256): ").SemiBold().FontSize(9);
                            t.Span(hashConteudo).FontSize(9);
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Gerado em ").FontSize(9);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        });
    }

    private static string DescreverTipoAso(TipoExameAso tipo) => tipo switch
    {
        TipoExameAso.Admissional => "ASO Admissional",
        TipoExameAso.Periodico => "ASO Periódico",
        TipoExameAso.MudancaDeFuncao => "ASO de Mudança de Função",
        TipoExameAso.RetornoAoTrabalho => "ASO de Retorno ao Trabalho",
        TipoExameAso.Demissional => "ASO Demissional",
        _ => tipo.ToString(),
    };

    private static string DescreverResultadoAso(ResultadoAso resultado) => resultado switch
    {
        ResultadoAso.Apto => "Apto",
        ResultadoAso.AptoComRestricao => "Apto com restrição",
        ResultadoAso.Inapto => "Inapto",
        ResultadoAso.Pendente => "Pendente",
        _ => resultado.ToString(),
    };
}
