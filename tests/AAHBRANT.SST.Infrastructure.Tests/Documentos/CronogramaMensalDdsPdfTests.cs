using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Dds.Cronogramas;
using AAHBRANT.SST.Infrastructure.Documentos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

public class CronogramaMensalDdsPdfTests
{
    [Theory]
    [InlineData(2026, 10, false)]
    [InlineData(2026, 11, true)]
    [InlineData(2028, 2, false)]
    public void MesCompletoComLogoETemasCabeEmUmaPagina(int ano, int mes, bool semanaCompleta)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        using var imagem = new Image<Rgb24>(140, 70, new Rgb24(130, 20, 20));
        using var logo = new MemoryStream();
        imagem.SaveAsPng(logo);
        var dias = Enumerable.Range(1, DateTime.DaysInMonth(ano, mes))
            .Where(d => semanaCompleta || new DateOnly(ano, mes, d).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(d => new DiaCronogramaDdsDto(new(ano, mes, d), [new(Guid.NewGuid(), "Organização da frente de trabalho", null)])).ToList();
        var modelo = new CronogramaDdsPdfModelo("Obra de demonstração", "Fiscalização", logo.ToArray(), ano, mes, "Responsável de SST", "DDS-CRON-2026-0001", semanaCompleta, dias);
        var pdf = new CronogramaMensalDdsPdfService().Gerar(modelo);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal(1, Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Count (\d+)").Select(m => int.Parse(m.Groups[1].Value)).Max());
        var pasta = Environment.GetEnvironmentVariable("CRONOGRAMA_PDF_AMOSTRA");
        if (pasta is not null) { Directory.CreateDirectory(pasta); File.WriteAllBytes(Path.Combine(pasta, $"cronograma-{ano}-{mes}.pdf"), pdf); }
    }
}
