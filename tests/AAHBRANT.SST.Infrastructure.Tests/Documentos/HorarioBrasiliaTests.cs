using AAHBRANT.SST.Infrastructure.Documentos;

namespace AAHBRANT.SST.Infrastructure.Tests.Documentos;

// O bug que estes testes travam: em container Linux (sem TZ definida) `DateTime.Now` devolve UTC, e
// o rodapé de todo documento oficial saía três horas à frente do relógio do canteiro. Os valores
// abaixo são fixos de propósito — o resultado não pode depender do fuso da máquina que roda o teste.
public class HorarioBrasiliaTests
{
    [Fact]
    public void De_HorarioDeInverno_SubtraiTresHoras()
    {
        var utc = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc);

        var brasilia = HorarioBrasilia.De(utc);

        Assert.Equal(new DateTime(2026, 9, 23, 14, 0, 0), brasilia);
    }

    // O Brasil não tem mais horário de verão desde 2019: janeiro também é UTC-3. Se algum dia voltar,
    // este teste quebra e avisa que a conversão passou a variar no ano.
    [Fact]
    public void De_Janeiro_TambemSubtraiTresHoras()
    {
        var utc = new DateTime(2026, 1, 15, 12, 30, 0, DateTimeKind.Utc);

        var brasilia = HorarioBrasilia.De(utc);

        Assert.Equal(new DateTime(2026, 1, 15, 9, 30, 0), brasilia);
    }

    // Vira o dia para trás: 00:30 UTC ainda é o dia anterior no canteiro — o caso que fazia um
    // documento emitido à noite aparecer com a data do dia seguinte.
    [Fact]
    public void De_MadrugadaUtc_VoltaParaODiaAnterior()
    {
        var utc = new DateTime(2026, 9, 24, 0, 30, 0, DateTimeKind.Utc);

        var brasilia = HorarioBrasilia.De(utc);

        Assert.Equal(new DateTime(2026, 9, 23, 21, 30, 0), brasilia);
    }

    [Fact]
    public void De_DataSemKindDefinido_TratadaComoUtc()
    {
        var semKind = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Unspecified);

        var brasilia = HorarioBrasilia.De(semKind);

        Assert.Equal(new DateTime(2026, 9, 23, 14, 0, 0), brasilia);
    }

    [Fact]
    public void Agora_NaoUsaORelogioDoServidor_EFicaTresHorasAtrasDoUtc()
    {
        var diferenca = DateTime.UtcNow - HorarioBrasilia.Agora;

        Assert.Equal(3, Math.Round(diferenca.TotalHours));
    }
}
