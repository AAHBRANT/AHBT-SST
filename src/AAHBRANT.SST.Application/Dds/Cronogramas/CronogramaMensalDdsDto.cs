using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;

namespace AAHBRANT.SST.Application.Dds.Cronogramas;

public record TemaCronogramaDdsDto(Guid CatalogoTemaDdsId, string Nome, string? Descricao);
public record DiaCronogramaDdsDto(DateOnly Data, List<TemaCronogramaDdsDto> Temas);
public record DiaCronogramaDdsEntrada(DateOnly Data, List<Guid> TemasIds);
public record RevisaoCronogramaDdsDto(Guid Id, int Numero, int VersaoOrigem, DateTime EmitidoEmUtc);
public record CronogramaMensalDdsDto(
    Guid? Id, Guid ObraId, int Ano, int Mes, int Versao, string? NumeroDocumento,
    List<DiaCronogramaDdsDto> Dias, List<RevisaoCronogramaDdsDto> Revisoes);
public record CronogramaDdsPdfModelo(
    string ObraNome, string? Cliente, byte[] Logo, int Ano, int Mes, string Responsavel,
    string NumeroDocumento, bool SemanaCompleta, List<DiaCronogramaDdsDto> Dias);
public record ArquivoCronogramaDds(byte[] Conteudo, string NomeArquivo);

public static class ConteudoCronogramaDds
{
    public static List<DiaCronogramaDdsDto> Ler(string json) =>
        JsonSerializer.Deserialize<List<DiaCronogramaDdsDto>>(json) ?? [];

    public static void ValidarCompetencia(int ano, int mes)
    {
        if (ano is < 2000 or > 2100 || mes is < 1 or > 12)
            throw new InvalidOperationException("Informe um mês válido entre os anos 2000 e 2100.");
    }
}

public interface ICronogramaMensalDdsPdfService
{
    byte[] Gerar(CronogramaDdsPdfModelo modelo, int? revisao = null, RastreabilidadeDocumentoResultado? rastreio = null);
}

public interface IEmissorCronogramaMensalDds
{
    Task<ArquivoCronogramaDds> GerarAsync(Guid id, int versao, bool semanaCompleta, bool emitir, CancellationToken ct);
}
