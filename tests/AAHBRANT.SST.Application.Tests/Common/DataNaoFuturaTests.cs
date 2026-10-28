using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.EntregasEpi.Commands;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Treinamentos.Commands;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Common;

// 28/10: treinamento e entrega de EPI são fatos já ocorridos — data futura é recusada.
public class DataNaoFuturaTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static DateTime Hoje => DataNaoFuturaExtensions.HojeBrasilia();

    private static CriarTreinamentoCommand Treinamento(DateTime realizacao) =>
        new(Id, Id, realizacao, realizacao.AddYears(2), 4, null, null, null, null);

    private static CriarEntregaEpiCommand Entrega(DateTime entrega) =>
        new(Id, Id, entrega, null, null, 1, null, null, null, default(MotivoEntregaEpi), null, null);

    private static CriarSessaoTreinamentoCommand Sessao(DateTime realizacao) =>
        new(Id, Id, realizacao, 4, null, new List<Guid> { Id });

    [Fact]
    public void Treinamento_hoje_e_passado_sao_aceitos_e_amanha_e_recusado()
    {
        var v = new CriarTreinamentoCommandValidator();
        Assert.True(v.Validate(Treinamento(Hoje)).IsValid);
        Assert.True(v.Validate(Treinamento(Hoje.AddDays(-30))).IsValid);
        Assert.False(v.Validate(Treinamento(Hoje.AddDays(1))).IsValid);
    }

    [Fact]
    public void Entrega_epi_amanha_e_recusada()
    {
        var v = new CriarEntregaEpiCommandValidator();
        Assert.True(v.Validate(Entrega(Hoje)).IsValid);
        Assert.False(v.Validate(Entrega(Hoje.AddDays(1))).IsValid);
    }

    [Fact]
    public void Turma_amanha_e_recusada()
    {
        var v = new CriarSessaoTreinamentoCommandValidator();
        Assert.True(v.Validate(Sessao(Hoje)).IsValid);
        Assert.False(v.Validate(Sessao(Hoje.AddDays(1))).IsValid);
    }
}
