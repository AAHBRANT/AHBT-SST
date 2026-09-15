using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Trabalhadores.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.Identificacao;

// Regressão do bug: a rota pública de crachá (IdentificacaoPublicaController, [AllowAnonymous])
// nunca resolvia nenhum trabalhador em produção, mesmo com a tag corretamente vinculada — o filtro
// global de RBAC (SstDbContext, Camada 3, docs/RBAC-Matrix.md §4) nega acesso por padrão quando não
// há usuário autenticado (EscopoPorObraMiddleware: TemAcessoGlobal=false, ObrasPermitidas=[] para
// requisição anônima). Estes testes fixam TemAcessoGlobal=false explicitamente para reproduzir
// exatamente esse cenário — o padrão do DbContextFactory.Criar() compartilhado é acesso global
// (não pega este bug de propósito, ver comentário lá).
public class ResolverTrabalhadorPublicoQueryTests
{
    static ResolverTrabalhadorPublicoQueryTests()
    {
        CpfCriptografiaContexto.Configurar(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
    }

    private static IAppDbContext CriarDbSemAcessoGlobal(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        var usuarioAtual = new CurrentUserService();
        usuarioAtual.DefinirEscopo(temAcessoGlobal: false, Array.Empty<Guid>());
        return new SstDbContext(options, usuarioAtual);
    }

    private static (Obra obra, Funcao funcao, Trabalhador trabalhador, TagIdentificacao tag) CriarCenario(bool trabalhadorAtivo = true)
    {
        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var funcao = new Funcao { Nome = "Soldador" };
        var trabalhador = new Trabalhador
        {
            Obra = obra,
            Funcao = funcao,
            Nome = "Augusto Inácio Felipe",
            Matricula = "MAT-042",
            Cpf = "12345678901",
            DataAdmissao = new DateTime(2024, 1, 10),
            Turno = "Diurno",
            Ativo = trabalhadorAtivo,
        };
        var tag = new TagIdentificacao
        {
            Uid = "04:E2:AD:3E:C5:2A:81",
            Tipo = TipoTag.Ntag215,
            Status = StatusTag.Vinculada,
            EntidadeVinculadaTipo = TipoEntidadeVinculada.Trabalhador,
            EntidadeVinculadaId = trabalhador.Id,
        };
        return (obra, funcao, trabalhador, tag);
    }

    [Fact]
    public async Task Handle_SemAcessoGlobalRBAC_AindaResolveTrabalhadorPelaTag()
    {
        var db = CriarDbSemAcessoGlobal(nameof(Handle_SemAcessoGlobalRBAC_AindaResolveTrabalhadorPelaTag));
        var (obra, funcao, trabalhador, tag) = CriarCenario();

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.TagsIdentificacao.Add(tag);
        await db.SaveChangesAsync();

        var handler = new ResolverTrabalhadorPublicoQueryHandler(db);
        var resultado = await handler.Handle(new ResolverTrabalhadorPublicoQuery(tag.Uid), default);

        Assert.NotNull(resultado);
        Assert.Equal("Augusto Inácio Felipe", resultado!.Nome);
        Assert.Equal("MAT-042", resultado.Matricula);
        Assert.Equal("Soldador", resultado.FuncaoNome);
        Assert.Equal("Obra Central", resultado.ObraNome);
    }

    [Fact]
    public async Task Handle_TrabalhadorDesativado_NaoResolveMesmoComTagVinculada()
    {
        var db = CriarDbSemAcessoGlobal(nameof(Handle_TrabalhadorDesativado_NaoResolveMesmoComTagVinculada));
        var (obra, funcao, trabalhador, tag) = CriarCenario(trabalhadorAtivo: false);

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.TagsIdentificacao.Add(tag);
        await db.SaveChangesAsync();

        var handler = new ResolverTrabalhadorPublicoQueryHandler(db);
        var resultado = await handler.Handle(new ResolverTrabalhadorPublicoQuery(tag.Uid), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_UidSemTagVinculada_RetornaNull()
    {
        var db = CriarDbSemAcessoGlobal(nameof(Handle_UidSemTagVinculada_RetornaNull));

        var handler = new ResolverTrabalhadorPublicoQueryHandler(db);
        var resultado = await handler.Handle(new ResolverTrabalhadorPublicoQuery("uid-inexistente"), default);

        Assert.Null(resultado);
    }

    // Regressão específica do histórico de DDS (mesma classe de bug do RBAC acima, mas em Dds/
    // DdsParticipante — Dds também tem o filtro global de Camada 3, sem IgnoreQueryFilters() a
    // consulta voltaria vazia numa requisição anônima mesmo com participação real).
    [Fact]
    public async Task Handle_SemAcessoGlobalRBAC_AindaTraHistoricoDdsOrdenadoPorDataDesc()
    {
        var db = CriarDbSemAcessoGlobal(nameof(Handle_SemAcessoGlobalRBAC_AindaTraHistoricoDdsOrdenadoPorDataDesc));
        var (obra, funcao, trabalhador, tag) = CriarCenario();

        var usuarioResponsavel = new Usuario { Nome = "Encarregado", Email = "encarregado@exemplo.com" };

        var ddsAntigo = new AAHBRANT.SST.Domain.Entidades.Dds { Obra = obra, ResponsavelUsuario = usuarioResponsavel, Data = new DateTime(2026, 9, 1), TemaLivreNome = "Uso de EPI" };
        var ddsRecente = new AAHBRANT.SST.Domain.Entidades.Dds { Obra = obra, ResponsavelUsuario = usuarioResponsavel, Data = new DateTime(2026, 9, 8), TemaLivreNome = "Trabalho em altura" };
        var ddsCancelado = new AAHBRANT.SST.Domain.Entidades.Dds { Obra = obra, ResponsavelUsuario = usuarioResponsavel, Data = new DateTime(2026, 9, 5), TemaLivreNome = "Não deve aparecer", Ativo = false };
        var ddsSemParticipacao = new AAHBRANT.SST.Domain.Entidades.Dds { Obra = obra, ResponsavelUsuario = usuarioResponsavel, Data = new DateTime(2026, 9, 9), TemaLivreNome = "Outro trabalhador" };

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.TagsIdentificacao.Add(tag);
        db.Usuarios.Add(usuarioResponsavel);
        db.Dds.AddRange(ddsAntigo, ddsRecente, ddsCancelado, ddsSemParticipacao);
        await db.SaveChangesAsync();

        db.DdsParticipantes.AddRange(
            new DdsParticipante { Dds = ddsAntigo, TrabalhadorId = trabalhador.Id },
            new DdsParticipante { Dds = ddsRecente, TrabalhadorId = trabalhador.Id },
            new DdsParticipante { Dds = ddsCancelado, TrabalhadorId = trabalhador.Id });
        await db.SaveChangesAsync();

        var handler = new ResolverTrabalhadorPublicoQueryHandler(db);
        var resultado = await handler.Handle(new ResolverTrabalhadorPublicoQuery(tag.Uid), default);

        Assert.NotNull(resultado);
        Assert.Equal(2, resultado!.HistoricoDds.Count);
        Assert.Equal("Trabalho em altura", resultado.HistoricoDds[0].Tema);
        Assert.Equal("Uso de EPI", resultado.HistoricoDds[1].Tema);
        Assert.All(resultado.HistoricoDds, d => Assert.Equal("Obra Central", d.ObraNome));
    }
}
