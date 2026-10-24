using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Inspecoes;
using AAHBRANT.SST.Application.Inspecoes.Temas;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Veiculos.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Inspecoes;

// Inspeção periódica por item (24/10/2026): ex.: veículo a cada 30 dias desde a última (ou primeira)
// inspeção, com aviso ao responsável daquele item naquela obra.
public class InspecaoPeriodicaTests
{
    [Fact]
    public void ProximaEm_SemPeriodicidade_NaoVence()
        => Assert.Null(PeriodicidadeInspecao.ProximaEm(null, new DateTime(2026, 10, 1), new DateTime(2026, 9, 1)));

    [Fact]
    public void ProximaEm_ContaDaUltimaInspecaoConcluida_ESemEla_DoCadastro()
    {
        var cadastro = new DateTime(2026, 9, 1, 14, 30, 0);
        Assert.Equal(new DateTime(2026, 10, 1), PeriodicidadeInspecao.ProximaEm(30, null, cadastro));
        Assert.Equal(new DateTime(2026, 11, 9), PeriodicidadeInspecao.ProximaEm(30, new DateTime(2026, 10, 10, 8, 0, 0), cadastro));
    }

    [Fact]
    public void Veiculo_ComPeriodicidade_ExigeResponsavelEFaixaValida()
    {
        var validador = new CriarVeiculoCommandValidator();
        CriarVeiculoCommand Comando(int? dias, Guid? responsavel) =>
            new(Guid.NewGuid(), TipoVeiculo.CaminhaoMunck, "ABC1D23", null, null, null, null, null, dias, responsavel);

        Assert.True(validador.Validate(Comando(null, null)).IsValid);
        Assert.False(validador.Validate(Comando(30, null)).IsValid);
        Assert.True(validador.Validate(Comando(30, Guid.NewGuid())).IsValid);
        Assert.False(validador.Validate(Comando(0, Guid.NewGuid())).IsValid);
        Assert.False(validador.Validate(Comando(366, Guid.NewGuid())).IsValid);
    }

    [Fact]
    public async Task Provider_VeiculoVenceNDiasDepoisDaUltimaInspecao_ESemElaDoCadastro_AvisandoOResponsavel()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var gilton = new Usuario { Email = "g@x.com", Nome = "Gilton" };
        var comInspecao = new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "AAA1A11", PeriodicidadeInspecaoDias = 30, ResponsavelInspecaoUsuarioId = gilton.Id };
        var nunca = new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "BBB2B22", PeriodicidadeInspecaoDias = 30, ResponsavelInspecaoUsuarioId = gilton.Id };
        var semRegra = new Veiculo { ObraId = obra.Id, Tipo = TipoVeiculo.CaminhaoMunck, PlacaPrefixo = "CCC3C33" };
        db.AddRange(obra, gilton, comInspecao, nunca, semRegra);
        var ultima = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);
        db.Add(new Inspecao
        {
            ObraId = obra.Id, TipoInspecao = TipoInspecao.Veiculo, VeiculoId = comInspecao.Id, Status = StatusInspecao.Concluida,
            Data = ultima, ChecklistModeloId = Guid.NewGuid(), ResponsavelUsuarioId = gilton.Id,
        });
        // Inspeção ainda em andamento não renova o prazo.
        db.Add(new Inspecao
        {
            ObraId = obra.Id, TipoInspecao = TipoInspecao.Veiculo, VeiculoId = nunca.Id, Status = StatusInspecao.EmAndamento,
            Data = DateTime.UtcNow, ChecklistModeloId = Guid.NewGuid(), ResponsavelUsuarioId = gilton.Id,
        });
        await db.SaveChangesAsync();

        var itens = await new InspecaoPeriodicaAlertaProvider(db).ObterItensAsync();

        Assert.Equal(2, itens.Count); // o veículo sem periodicidade não gera alerta
        var a = itens.Single(i => i.EntidadeOrigemId == comInspecao.Id);
        Assert.Equal(ultima.Date.AddDays(30), a.DataVencimento);
        Assert.Equal(gilton.Id, a.DestinatarioUsuarioId);
        Assert.Equal(obra.Id, a.ObraId);
        Assert.Equal(TipoAlerta.InspecaoPendente, a.TipoAlertaVencendo);
        Assert.Contains("AAA1A11", a.Titulo);
        var b = itens.Single(i => i.EntidadeOrigemId == nunca.Id);
        Assert.Equal(nunca.CreatedAtUtc.Date.AddDays(30), b.DataVencimento);
    }

    [Fact]
    public async Task Provider_ItemDeTema_UsaOItemNaObraEOResponsavelDele()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var tema = new TemaInspecao { Nome = "Andaime" };
        var responsavel = new Usuario { Email = "r@x.com", Nome = "Responsável" };
        var item = new ItemInspecionavel { TemaInspecaoId = tema.Id, ObraId = obra.Id, Identificacao = "A-03", PeriodicidadeInspecaoDias = 7, ResponsavelInspecaoUsuarioId = responsavel.Id };
        var semPeriodo = new ItemInspecionavel { TemaInspecaoId = tema.Id, ObraId = obra.Id, Identificacao = "A-04" };
        db.AddRange(obra, tema, responsavel, item, semPeriodo);
        await db.SaveChangesAsync();

        var itens = await new InspecaoPeriodicaAlertaProvider(db).ObterItensAsync();

        var unico = Assert.Single(itens);
        Assert.Equal(item.Id, unico.EntidadeOrigemId);
        Assert.Equal(responsavel.Id, unico.DestinatarioUsuarioId);
        Assert.Equal(obra.Id, unico.ObraId);
        Assert.Contains("Andaime", unico.Titulo);
        Assert.Contains("A-03", unico.Titulo);
    }

    [Fact]
    public async Task ItemNovo_HerdaAPeriodicidadePadraoDoTema_EExigeResponsavel()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var responsavel = new Usuario { Email = "r@x.com", Nome = "Responsável" };
        db.AddRange(obra, responsavel);
        await db.SaveChangesAsync();
        var temaId = await new CriarTemaInspecaoCommandHandler(db).Handle(
            new CriarTemaInspecaoCommand("Andaime", new() { new CriarTemaInspecaoItemInput("Guarda-corpo") }, 15), default);
        var criar = new CriarItemInspecionavelCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            criar.Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "A-01", null), default));

        var id = await criar.Handle(new CriarItemInspecionavelCommand(temaId, obra.Id, "A-01", null, null, responsavel.Id), default);
        var item = await db.ItensInspecionaveis.SingleAsync(i => i.Id == id);
        Assert.Equal(15, item.PeriodicidadeInspecaoDias);
        Assert.Equal(responsavel.Id, item.ResponsavelInspecaoUsuarioId);

        var resumo = (await new ListarItensInspecionaveisQueryHandler(db).Handle(new ListarItensInspecionaveisQuery(temaId, null), default)).Single();
        Assert.Equal(item.CreatedAtUtc.Date.AddDays(15), resumo.ProximaInspecaoEm);
        Assert.Equal("Responsável", resumo.ResponsavelInspecaoNome);
    }

    [Fact]
    public async Task AtualizarItem_TrocaPeriodicidadeEResponsavel_ESemPeriodicidadeLimpaOResponsavel()
    {
        var db = DbContextFactory.Criar();
        var obra = new Obra { Codigo = "OB1", Nome = "Obra" };
        var r1 = new Usuario { Email = "a@x.com", Nome = "A" };
        var r2 = new Usuario { Email = "b@x.com", Nome = "B" };
        var tema = new TemaInspecao { Nome = "Escada" };
        var item = new ItemInspecionavel { TemaInspecaoId = tema.Id, ObraId = obra.Id, Identificacao = "E-01", PeriodicidadeInspecaoDias = 30, ResponsavelInspecaoUsuarioId = r1.Id };
        db.AddRange(obra, r1, r2, tema, item);
        await db.SaveChangesAsync();
        var atualizar = new AtualizarItemInspecionavelCommandHandler(db);

        await atualizar.Handle(new AtualizarItemInspecionavelCommand(item.Id, "nova descrição", 10, r2.Id), default);
        var salvo = await db.ItensInspecionaveis.SingleAsync();
        Assert.Equal(10, salvo.PeriodicidadeInspecaoDias);
        Assert.Equal(r2.Id, salvo.ResponsavelInspecaoUsuarioId);

        await atualizar.Handle(new AtualizarItemInspecionavelCommand(item.Id, null, null, r2.Id), default);
        salvo = await db.ItensInspecionaveis.SingleAsync();
        Assert.Null(salvo.PeriodicidadeInspecaoDias);
        Assert.Null(salvo.ResponsavelInspecaoUsuarioId);
    }

    [Fact]
    public void AtualizarItem_ComPeriodicidadeSemResponsavel_Recusa()
        => Assert.False(new AtualizarItemInspecionavelCommandValidator()
            .Validate(new AtualizarItemInspecionavelCommand(Guid.NewGuid(), null, 30, null)).IsValid);
}
