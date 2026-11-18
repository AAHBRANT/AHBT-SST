using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Funcoes;
using AAHBRANT.SST.Application.Funcoes.Commands;
using AAHBRANT.SST.Application.Funcoes.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Funcoes;

public class CorCapaceteFuncaoTests
{
    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(nomeBanco).Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    // Padrão AAHBRANT decidido em 14/11/2026.
    [Theory]
    [InlineData("Carpinteiro", CorCapacete.Amarelo)]
    [InlineData("Pedreiro", CorCapacete.Amarelo)]
    [InlineData("Servente", CorCapacete.Amarelo)]
    [InlineData("Ajudante Geral", CorCapacete.Amarelo)]
    [InlineData("Armador", CorCapacete.Amarelo)]
    [InlineData("Pintor", CorCapacete.Amarelo)]
    [InlineData("Operário", CorCapacete.Amarelo)]
    [InlineData("Técnico de Segurança do Trabalho", CorCapacete.Verde)]
    [InlineData("TÉCNICO EM SEGURANÇA DO TRABALHO", CorCapacete.Verde)]
    [InlineData("Tecnico de seguranca do trabalho", CorCapacete.Verde)]
    [InlineData("Analista de Meio Ambiente", CorCapacete.Verde)]
    [InlineData("Visitante", CorCapacete.Laranja)]
    [InlineData("Mão de obra temporária", CorCapacete.Laranja)]
    [InlineData("Engenheiro Civil", CorCapacete.Branco)]
    [InlineData("Engenheiro de Segurança do Trabalho", CorCapacete.Branco)]
    [InlineData("Arquiteta", CorCapacete.Branco)]
    [InlineData("Mestre de Obras", CorCapacete.Branco)]
    [InlineData("Encarregado de Obras", CorCapacete.Branco)]
    [InlineData("Coordenador de Obras", CorCapacete.Branco)]
    [InlineData("Supervisor de Campo", CorCapacete.Branco)]
    [InlineData("Eletricista", CorCapacete.Azul)]
    [InlineData("Encanador", CorCapacete.Azul)]
    [InlineData("Bombeiro Hidráulico", CorCapacete.Azul)]
    [InlineData("Operador de Máquinas", CorCapacete.Azul)]
    [InlineData("Operador de Retroescavadeira", CorCapacete.Azul)]
    [InlineData("Técnico em Edificações", CorCapacete.Azul)]
    [InlineData("Brigadista", CorCapacete.Vermelho)]
    [InlineData("Bombeiro Civil", CorCapacete.Vermelho)]
    [InlineData("Almoxarife", CorCapacete.SemPadrao)]
    [InlineData("Auxiliar Administrativo", CorCapacete.SemPadrao)]
    [InlineData("Motorista", CorCapacete.SemPadrao)]
    [InlineData("", CorCapacete.SemPadrao)]
    [InlineData(null, CorCapacete.SemPadrao)]
    public void Sugerir_SegueOPadraoAahbrantSemAcentoNemMaiusculas(string? nome, CorCapacete esperada)
        => Assert.Equal(esperada, SugestaoCorCapacete.Sugerir(nome));

    // Empates documentados no serviço: a ordem das regras decide.
    [Theory]
    [InlineData("Ajudante de Eletricista", CorCapacete.Amarelo)]
    [InlineData("Servente de Pedreiro", CorCapacete.Amarelo)]
    public void Sugerir_EmpatesResolvidosPelaOrdemDasRegras(string nome, CorCapacete esperada)
        => Assert.Equal(esperada, SugestaoCorCapacete.Sugerir(nome));

    [Fact]
    public async Task Sugestoes_ListaFuncoesComCorAtualESugerida()
    {
        var db = CriarDb(nameof(Sugestoes_ListaFuncoesComCorAtualESugerida));
        var carpinteiro = new Funcao { Nome = "Carpinteiro", CorCapacete = CorCapacete.Branco };
        var almoxarife = new Funcao { Nome = "Almoxarife" };
        var inativa = new Funcao { Nome = "Visitante", Ativo = false };
        db.Funcoes.AddRange(carpinteiro, almoxarife, inativa);
        await db.SaveChangesAsync();

        var lista = await new ListarSugestoesCorCapaceteQueryHandler(db).Handle(new ListarSugestoesCorCapaceteQuery(), default);

        Assert.Equal(new[] { "Almoxarife", "Carpinteiro" }, lista.Select(s => s.Nome).ToArray());
        Assert.Equal(new SugestaoCorCapaceteDto(carpinteiro.Id, "Carpinteiro", CorCapacete.Branco, CorCapacete.Amarelo,
            "Operários, serventes, ajudantes, execução"), lista[1]);
        Assert.Null(lista[0].Grupo);
        Assert.Equal(CorCapacete.SemPadrao, lista[0].CorSugerida);
        Assert.Equal(CorCapacete.SemPadrao, lista[0].CorAtual);
    }

    [Fact]
    public async Task Sugestoes_NaoGravaNada()
    {
        var db = CriarDb(nameof(Sugestoes_NaoGravaNada));
        db.Funcoes.Add(new Funcao { Nome = "Pedreiro" });
        await db.SaveChangesAsync();

        await new ListarSugestoesCorCapaceteQueryHandler(db).Handle(new ListarSugestoesCorCapaceteQuery(), default);

        Assert.Equal(CorCapacete.SemPadrao, (await db.Funcoes.SingleAsync()).CorCapacete);
    }

    [Fact]
    public async Task Aplicar_GravaACorEscolhidaDeCadaFuncaoEDeixaAsOutrasComoEstavam()
    {
        var db = CriarDb(nameof(Aplicar_GravaACorEscolhidaDeCadaFuncaoEDeixaAsOutrasComoEstavam));
        var pedreiro = new Funcao { Nome = "Pedreiro" };
        var engenheiro = new Funcao { Nome = "Engenheiro", CorCapacete = CorCapacete.Branco };
        var outra = new Funcao { Nome = "Almoxarife", CorCapacete = CorCapacete.Verde };
        db.Funcoes.AddRange(pedreiro, engenheiro, outra);
        await db.SaveChangesAsync();

        await new AplicarCorCapaceteFuncoesCommandHandler(db).Handle(new AplicarCorCapaceteFuncoesCommand(new List<AplicarCorCapaceteItem>
        {
            new(pedreiro.Id, CorCapacete.Amarelo),
            new(engenheiro.Id, CorCapacete.SemPadrao),
        }), default);

        Assert.Equal(CorCapacete.Amarelo, (await db.Funcoes.SingleAsync(f => f.Id == pedreiro.Id)).CorCapacete);
        Assert.Equal(CorCapacete.SemPadrao, (await db.Funcoes.SingleAsync(f => f.Id == engenheiro.Id)).CorCapacete);
        Assert.Equal(CorCapacete.Verde, (await db.Funcoes.SingleAsync(f => f.Id == outra.Id)).CorCapacete);
    }

    [Fact]
    public async Task Aplicar_FuncaoInexistente_LancaENaoGravaNenhuma()
    {
        var db = CriarDb(nameof(Aplicar_FuncaoInexistente_LancaENaoGravaNenhuma));
        var pedreiro = new Funcao { Nome = "Pedreiro" };
        db.Funcoes.Add(pedreiro);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new AplicarCorCapaceteFuncoesCommandHandler(db).Handle(
            new AplicarCorCapaceteFuncoesCommand(new List<AplicarCorCapaceteItem>
            {
                new(pedreiro.Id, CorCapacete.Amarelo),
                new(Guid.NewGuid(), CorCapacete.Azul),
            }), default));

        Assert.Equal(CorCapacete.SemPadrao, (await db.Funcoes.SingleAsync()).CorCapacete);
    }

    [Fact]
    public void Aplicar_Validador_RecusaCorForaDaFaixaFuncaoRepetidaEIdVazio()
    {
        var validador = new AplicarCorCapaceteFuncoesCommandValidator();
        var id = Guid.NewGuid();

        Assert.True(validador.Validate(new AplicarCorCapaceteFuncoesCommand(new() { new(id, CorCapacete.Laranja) })).IsValid);
        Assert.True(validador.Validate(new AplicarCorCapaceteFuncoesCommand(new())).IsValid);
        Assert.False(validador.Validate(new AplicarCorCapaceteFuncoesCommand(new() { new(id, (CorCapacete)7) })).IsValid);
        Assert.False(validador.Validate(new AplicarCorCapaceteFuncoesCommand(new() { new(id, CorCapacete.Azul), new(id, CorCapacete.Verde) })).IsValid);
        Assert.False(validador.Validate(new AplicarCorCapaceteFuncoesCommand(new() { new(Guid.Empty, CorCapacete.Azul) })).IsValid);
    }

    [Fact]
    public async Task Criar_SemCor_FicaSemPadraoEComCorGrava()
    {
        var db = CriarDb(nameof(Criar_SemCor_FicaSemPadraoEComCorGrava));
        var handler = new CriarFuncaoCommandHandler(db);

        var semCor = await handler.Handle(new CriarFuncaoCommand("Almoxarife", null, null), default);
        var comCor = await handler.Handle(new CriarFuncaoCommand("Pedreiro", null, null, CorCapacete.Amarelo), default);

        Assert.Equal(CorCapacete.SemPadrao, (await db.Funcoes.SingleAsync(f => f.Id == semCor)).CorCapacete);
        Assert.Equal(CorCapacete.Amarelo, (await db.Funcoes.SingleAsync(f => f.Id == comCor)).CorCapacete);
    }

    [Fact]
    public async Task Atualizar_CorAusenteNaoMudaEPresenteMuda()
    {
        var db = CriarDb(nameof(Atualizar_CorAusenteNaoMudaEPresenteMuda));
        var funcao = new Funcao { Nome = "Pedreiro", CorCapacete = CorCapacete.Amarelo };
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();
        var handler = new AtualizarFuncaoCommandHandler(db);

        await handler.Handle(new AtualizarFuncaoCommand(funcao.Id, "Pedreiro Oficial", null, null), default);
        Assert.Equal(CorCapacete.Amarelo, (await db.Funcoes.SingleAsync()).CorCapacete);

        await handler.Handle(new AtualizarFuncaoCommand(funcao.Id, "Pedreiro Oficial", null, null, CorCapacete.Azul), default);
        Assert.Equal(CorCapacete.Azul, (await db.Funcoes.SingleAsync()).CorCapacete);

        // 0 é uma cor válida de verdade ("sem padrão") e precisa limpar.
        await handler.Handle(new AtualizarFuncaoCommand(funcao.Id, "Pedreiro Oficial", null, null, CorCapacete.SemPadrao), default);
        Assert.Equal(CorCapacete.SemPadrao, (await db.Funcoes.SingleAsync()).CorCapacete);
    }

    [Fact]
    public void Criar_EAtualizar_ValidadoresRecusamCorForaDaFaixa()
    {
        Assert.False(new CriarFuncaoCommandValidator().Validate(new CriarFuncaoCommand("X", null, null, (CorCapacete)9)).IsValid);
        Assert.False(new AtualizarFuncaoCommandValidator().Validate(new AtualizarFuncaoCommand(Guid.NewGuid(), "X", null, null, (CorCapacete)9)).IsValid);
        Assert.True(new CriarFuncaoCommandValidator().Validate(new CriarFuncaoCommand("X", null, null)).IsValid);
    }

    [Fact]
    public async Task Listar_EObterPorId_DevolvemACor()
    {
        var db = CriarDb(nameof(Listar_EObterPorId_DevolvemACor));
        var funcao = new Funcao { Nome = "Eletricista", CorCapacete = CorCapacete.Azul };
        db.Funcoes.Add(funcao);
        await db.SaveChangesAsync();

        var lista = await new ListarFuncoesQueryHandler(db).Handle(new ListarFuncoesQuery(), default);
        var uma = await new ObterFuncaoPorIdQueryHandler(db).Handle(new ObterFuncaoPorIdQuery(funcao.Id), default);

        Assert.Equal(CorCapacete.Azul, Assert.Single(lista).CorCapacete);
        Assert.Equal(CorCapacete.Azul, uma!.CorCapacete);
    }
}
