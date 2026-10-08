using AAHBRANT.SST.Application.Trabalhadores;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AAHBRANT.SST.Infrastructure.Trabalhadores;

// Grade "Identificação do Trabalhador" do Relatório de Fiscalização, no mesmo desenho da Ficha de EPI
// (caixas com rótulo pequeno em cima e valor embaixo), com a foto de cadastro à direita. Sem Turno.
// Substitui a lista simples "Dados gerais" que o relatório tinha antes.
public static class GradeIdentificacaoFiscalizacaoPdf
{
    private const string CorMarca = "#670000";

    public static void Desenhar(IContainer container, PerfilCompletoTrabalhadorDto perfil, IdentificacaoRelatorioFiscalizacao? identificacao)
    {
        var contratante = !string.IsNullOrWhiteSpace(identificacao?.ObraCliente) ? identificacao!.ObraCliente! : perfil.ObraNome;
        var cpf = string.IsNullOrWhiteSpace(perfil.Rg) ? perfil.Cpf : perfil.Cpf + "   ·   RG " + perfil.Rg;

        container.Column(coluna =>
        {
            coluna.Item().Text("Identificação do Trabalhador").FontSize(13).Bold().FontColor(CorMarca);

            coluna.Item().PaddingTop(3).Row(linha =>
            {
                linha.RelativeItem().Table(tabela =>
                {
                    tabela.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    Celula(tabela, "Nome completo", perfil.Nome, 2);
                    Celula(tabela, "CPF", cpf);
                    Celula(tabela, "Matrícula", string.IsNullOrWhiteSpace(perfil.Matricula) ? "—" : perfil.Matricula);
                    Celula(tabela, "Função", perfil.FuncaoNome);
                    Celula(tabela, "Data de admissão", perfil.DataAdmissao.ToString("dd/MM/yyyy"));
                    Celula(tabela, "Obra / Frente de trabalho", perfil.ObraNome, 2);
                    Celula(tabela, "Situação de aptidão", perfil.StatusAptidao);
                    Celula(tabela, "Empresa contratante", contratante, 2);
                    Celula(tabela, "CNPJ da contratada", identificacao?.ObraCnpj ?? "não informado");
                });

                linha.ConstantItem(86).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(foto =>
                {
                    foto.Item().Text("Foto de cadastro").FontSize(7.5f).SemiBold().FontColor(CorMarca);
                    if (identificacao?.Foto is { Length: > 0 } bytes && FotoValida(bytes))
                        foto.Item().PaddingTop(2).Height(96).Image(bytes).FitArea();
                    else
                        foto.Item().PaddingTop(2).Height(96).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                            .AlignCenter().AlignMiddle().Text("Sem foto cadastrada").FontSize(7.5f).FontColor(Colors.Grey.Darken1).AlignCenter();
                });
            });
        });
    }

    private static void Celula(TableDescriptor tabela, string rotulo, string valor, uint colSpan = 1)
    {
        tabela.Cell().ColumnSpan(colSpan).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(c =>
        {
            c.Item().Text(rotulo).FontSize(7.5f).SemiBold().FontColor(CorMarca);
            c.Item().Text(valor).FontSize(10);
        });
    }

    private static bool FotoValida(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            return SixLabors.ImageSharp.Image.Identify(ms) is not null;
        }
        catch
        {
            return false;
        }
    }
}
