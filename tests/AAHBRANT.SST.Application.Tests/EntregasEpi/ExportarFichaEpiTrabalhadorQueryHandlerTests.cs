using System.Security.Cryptography;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Application.EntregasEpi.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Assinatura;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.EntregasEpi;

public class ExportarFichaEpiTrabalhadorQueryHandlerTests
{
    // O conversor de criptografia do CPF (Trabalhador.Cpf) exige chaves configuradas em
    // CpfCriptografiaContexto — normalmente feito por DependencyInjection.AddInfrastructure a partir
    // de appsettings, que este projeto de testes não executa. Configura uma chave só para o processo
    // de teste, igual ao que qualquer outro teste que grave um Trabalhador precisaria fazer.
    static ExportarFichaEpiTrabalhadorQueryHandlerTests()
    {
        CpfCriptografiaContexto.Configurar(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
    }

    private class FichaEpiPdfServiceFake : IFichaEpiPdfService
    {
        public FichaEpiPdfModelo? UltimoModelo { get; private set; }

        public byte[] Gerar(FichaEpiPdfModelo modelo)
        {
            UltimoModelo = modelo;
            return new byte[] { 1 };
        }
    }

    private class QrCodeDocumentoServiceFalso : IQrCodeDocumentoService
    {
        public QrCodeDocumentoResultado Gerar(string token) => new(new byte[] { 9 }, $"https://fake/#/validar/{token}");
    }

    private static IAppDbContext CriarDb(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<SstDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new SstDbContext(options, new CurrentUserService());
    }

    [Fact]
    public async Task Handle_TrabalhadorInexistente_RetornaNull()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorInexistente_RetornaNull));
        var pdf = new FichaEpiPdfServiceFake();
        var handler = new ExportarFichaEpiTrabalhadorQueryHandler(db, pdf, new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso()));

        var resultado = await handler.Handle(new ExportarFichaEpiTrabalhadorQuery(Guid.NewGuid()), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Handle_TrabalhadorComEntregasEDevolucoes_AgregaAssinaturasCorretamente()
    {
        var db = CriarDb(nameof(Handle_TrabalhadorComEntregasEDevolucoes_AgregaAssinaturasCorretamente));

        var obra = new Obra { Codigo = "OB1", Nome = "Obra Central", Cliente = "Consórcio Exemplo", Cnpj = "12.345.678/0001-90" };
        var funcao = new Funcao { Nome = "Soldador" };
        var trabalhador = new Trabalhador
        {
            Obra = obra,
            Funcao = funcao,
            Nome = "João da Silva",
            Matricula = "MAT-001",
            Cpf = "12345678901",
            DataAdmissao = new DateTime(2024, 1, 10),
            Turno = "Diurno",
        };
        var epi = new CatalogoEpi { Nome = "Capacete", VidaUtilEmMeses = 12, CertificadoAprovacaoNumero = "CA-123" };

        var entregaComDevolucao = new EntregaEpi
        {
            Trabalhador = trabalhador,
            CatalogoEpi = epi,
            DataEntrega = new DateTime(2024, 2, 1),
            DataDevolucao = new DateTime(2024, 6, 1),
            Quantidade = 1,
            QuantidadeDevolucao = 1,
            MotivoTipo = MotivoEntregaEpi.Inicial,
            VistoConsorcioResponsavel = "Visto do encarregado",
        };
        var entregaSemDevolucao = new EntregaEpi
        {
            Trabalhador = trabalhador,
            CatalogoEpi = epi,
            DataEntrega = new DateTime(2024, 7, 1),
            Quantidade = 1,
            MotivoTipo = MotivoEntregaEpi.Vencimento,
        };

        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);
        db.Trabalhadores.Add(trabalhador);
        db.CatalogoEpis.Add(epi);
        db.EntregasEpi.AddRange(entregaComDevolucao, entregaSemDevolucao);
        await db.SaveChangesAsync();

        var docEntrega = new DocumentoAssinatura { EntidadeTipo = "EntregaEpi", EntidadeId = entregaComDevolucao.Id };
        docEntrega.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = trabalhador.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow });
        docEntrega.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = Guid.NewGuid(), MetodoAutenticacao = MetodoAutenticacaoAssinatura.SessaoLogada, AssinadoEm = DateTime.UtcNow });

        var docDevolucao = new DocumentoAssinatura { EntidadeTipo = "DevolucaoEpi", EntidadeId = entregaComDevolucao.Id };
        docDevolucao.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = trabalhador.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria, AssinadoEm = DateTime.UtcNow });

        db.DocumentosAssinatura.AddRange(docEntrega, docDevolucao);
        await db.SaveChangesAsync();

        var pdf = new FichaEpiPdfServiceFake();
        var handler = new ExportarFichaEpiTrabalhadorQueryHandler(db, pdf, new RegistradorRastreabilidadeService(db, new QrCodeDocumentoServiceFalso()));

        var resultado = await handler.Handle(new ExportarFichaEpiTrabalhadorQuery(trabalhador.Id), default);

        Assert.NotNull(resultado);
        var modelo = pdf.UltimoModelo!;

        Assert.Equal("***.***.***-01", modelo.TrabalhadorCpfMascarado);
        Assert.Equal("Consórcio Exemplo", modelo.ObraCliente);
        Assert.Equal(2, modelo.Entregas.Count);

        var linhaComDevolucao = modelo.Entregas.Single(e => e.Numero == 1);
        Assert.True(linhaComDevolucao.AssinadoPeloEmpregado);
        Assert.True(linhaComDevolucao.AssinadoPeloResponsavel);

        var linhaSemDevolucao = modelo.Entregas.Single(e => e.Numero == 2);
        Assert.False(linhaSemDevolucao.AssinadoPeloEmpregado);
        Assert.False(linhaSemDevolucao.AssinadoPeloResponsavel);

        var devolucao = Assert.Single(modelo.Devolucoes);
        Assert.Equal(1, devolucao.NumeroReferenciaEntrega);
        Assert.True(devolucao.AssinadoPeloEmpregado);
        Assert.Equal("Visto do encarregado", devolucao.VistoResponsavel);
    }
}
