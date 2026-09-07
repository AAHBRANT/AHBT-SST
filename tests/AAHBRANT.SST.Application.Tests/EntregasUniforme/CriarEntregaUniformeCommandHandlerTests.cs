using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasUniforme.Commands;
using AAHBRANT.SST.Application.EstoquesUniforme.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasUniforme;

public class CriarEntregaUniformeCommandHandlerTests
{
    // O conversor de criptografia do CPF (Trabalhador.Cpf) exige chaves configuradas em
    // CpfCriptografiaContexto — normalmente feito por DependencyInjection.AddInfrastructure a partir
    // de appsettings, que este projeto de testes não executa. Mesmo padrão usado em
    // DefinirTamanhosUniformeTrabalhadorCommandHandlerTests (Task 6).
    static CriarEntregaUniformeCommandHandlerTests()
    {
        CpfCriptografiaContexto.Configurar(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
    }

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    private static async Task<(Trabalhador Trabalhador, CatalogoUniforme Camisa, Obra Obra, Funcao Funcao)> SemearBaseAsync(IAppDbContext db)
    {
        var obra = new Obra { Nome = "Obra Teste" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        var camisa = new CatalogoUniforme { Nome = "Camisa" };
        var trabalhador = new Trabalhador
        {
            Nome = "Bruno Silva Santos",
            Matricula = "00427",
            Cpf = "12345678900",
            Obra = obra,
            Funcao = funcao,
            DataAdmissao = DateTime.UtcNow,
        };

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.CatalogoUniformes.Add(camisa);
        db.Trabalhadores.Add(trabalhador);
        await db.SaveChangesAsync();

        return (trabalhador, camisa, obra, funcao);
    }

    [Fact]
    public async Task Handle_MatrizDaFuncaoVazia_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_MatrizDaFuncaoVazia_LancaInvalidOperationException));
        var (trabalhador, camisa, _, _) = await SemearBaseAsync(db);
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("ainda não foi cadastrada", ex.Message);
    }

    [Fact]
    public async Task Handle_PecaForaDaMatrizDaFuncao_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_PecaForaDaMatrizDaFuncao_LancaInvalidOperationException));
        var (trabalhador, camisa, _, funcao) = await SemearBaseAsync(db);
        var calca = new CatalogoUniforme { Nome = "Calça" };
        db.CatalogoUniformes.Add(calca);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = calca.Id });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("não faz parte da matriz", ex.Message);
    }

    [Fact]
    public async Task Handle_TrabalhadorSemTamanhoCadastrado_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorSemTamanhoCadastrado_LancaInvalidOperationException));
        var (trabalhador, camisa, _, funcao) = await SemearBaseAsync(db);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id });
        await db.SaveChangesAsync();
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("tamanho cadastrado", ex.Message);
    }

    [Fact]
    public async Task Handle_EstoqueInsuficienteNoTamanhoResolvido_LancaInvalidOperationException()
    {
        var db = CriarDb(nameof(Handle_EstoqueInsuficienteNoTamanhoResolvido_LancaInvalidOperationException));
        var (trabalhador, camisa, obra, funcao) = await SemearBaseAsync(db);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id });
        db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme { TrabalhadorId = trabalhador.Id, CatalogoUniformeId = camisa.Id, Tamanho = "M" });
        // Estoque só existe no tamanho "G", não no "M" resolvido pelo cadastro do trabalhador —
        // confirma que a trava verifica o bucket (peça+tamanho) certo, não a peça em qualquer tamanho.
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(camisa.Id, obra.Id, "G", 10, null), default);
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 1, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, null), default));
        Assert.Contains("Estoque insuficiente", ex.Message);
    }

    [Fact]
    public async Task Handle_TudoAutorizadoComEstoque_RegistraEntregaEBaixaEstoqueDoTamanhoCorreto()
    {
        var db = CriarDb(nameof(Handle_TudoAutorizadoComEstoque_RegistraEntregaEBaixaEstoqueDoTamanhoCorreto));
        var (trabalhador, camisa, obra, funcao) = await SemearBaseAsync(db);
        db.MatrizUniformeFuncoes.Add(new MatrizUniformeFuncao { FuncaoId = funcao.Id, CatalogoUniformeId = camisa.Id });
        db.TrabalhadorTamanhosUniforme.Add(new TrabalhadorTamanhoUniforme { TrabalhadorId = trabalhador.Id, CatalogoUniformeId = camisa.Id, Tamanho = "M" });
        await db.SaveChangesAsync();
        await new RegistrarEntradaEstoqueUniformeCommandHandler(db)
            .Handle(new RegistrarEntradaEstoqueUniformeCommand(camisa.Id, obra.Id, "M", 10, null), default);
        var handler = new CriarEntregaUniformeCommandHandler(db);

        var id = await handler.Handle(new CriarEntregaUniformeCommand(trabalhador.Id, camisa.Id, 2, DateTime.UtcNow, MotivoEntregaUniforme.Inicial, "Kit inicial"), default);

        var entrega = await db.EntregasUniforme.SingleAsync(e => e.Id == id);
        Assert.Equal("M", entrega.Tamanho);
        Assert.Equal(2, entrega.Quantidade);
        var estoque = await db.EstoquesUniforme.SingleAsync(e => e.CatalogoUniformeId == camisa.Id && e.ObraId == obra.Id && e.Tamanho == "M");
        Assert.Equal(8, estoque.Saldo);
        var movimentacao = await db.MovimentacoesEstoqueUniforme.OrderByDescending(m => m.CreatedAtUtc).FirstAsync(m => m.EstoqueUniformeId == estoque.Id);
        Assert.Equal(TipoMovimentacaoEstoqueUniforme.SaidaEntrega, movimentacao.Tipo);
        Assert.Equal(id, movimentacao.EntregaUniformeId);
    }
}
