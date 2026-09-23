using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Application.Treinamentos;
using AAHBRANT.SST.Application.Treinamentos.Queries;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Treinamentos;

// Conformidade do lançamento retroativo (22/09): a AAHBRANT não pode emitir certificado no modelo
// próprio — com o Técnico de Segurança assinando como responsável técnico — para um curso que quem
// ministrou foi outra instituição. Este teste existe para que ninguém reative isso por engano.
public class ExportarCertificadoTreinamentoOrigemTests
{
    private class PdfServiceQueNuncaDeveriaSerChamado : ICertificadoTreinamentoPdfService
    {
        public bool Chamado { get; private set; }

        public byte[] Gerar(CertificadoTreinamentoPdfModelo modelo)
        {
            Chamado = true;
            return new byte[] { 0x25, 0x50, 0x44, 0x46 };
        }
    }

    private class RastreabilidadeFalsa : IRegistradorRastreabilidadeService
    {
        public byte[]? ArquivoRegistrado { get; private set; }

        public Task<RastreabilidadeDocumentoResultado> GarantirAsync(string entidadeTipo, Guid entidadeId, CancellationToken ct)
            => Task.FromResult(new RastreabilidadeDocumentoResultado(Guid.NewGuid(), "hash", "https://exemplo/validar", Array.Empty<byte>(), false));

        public Task RegistrarArquivoAsync(Guid documentoId, byte[] pdf, CancellationToken ct)
        {
            ArquivoRegistrado = pdf;
            return Task.CompletedTask;
        }
    }

    private static async Task<Guid> SemearAsync(
        Infrastructure.Persistencia.SstDbContext db,
        OrigemCertificadoTreinamento origem)
    {
        var obra = new Obra { Nome = "Obra Teste", Cnpj = "00000000000191" };
        var funcao = new Funcao { Nome = "Pedreiro" };
        db.Obras.Add(obra);
        db.Funcoes.Add(funcao);

        var trabalhador = new Trabalhador { Nome = "João da Silva", Obra = obra, Funcao = funcao, Cpf = "52998224725" };
        var curso = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        db.Trabalhadores.Add(trabalhador);
        db.CursosTreinamento.Add(curso);

        var treinamento = new Treinamento
        {
            Trabalhador = trabalhador,
            CursoTreinamento = curso,
            DataRealizacao = new DateTime(2025, 3, 10),
            DataValidade = new DateTime(2027, 3, 10),
            CargaHorariaRealizada = 8,
            OrigemCertificado = origem,
        };
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        return treinamento.Id;
    }

    [Fact]
    public async Task Handle_CertificadoExterno_RecusaEmitirModeloAahbrant()
    {
        var db = DbContextFactory.Criar();
        var treinamentoId = await SemearAsync(db, OrigemCertificadoTreinamento.Externo);
        var pdf = new PdfServiceQueNuncaDeveriaSerChamado();
        var handler = new ExportarCertificadoTreinamentoQueryHandler(db, pdf, new RastreabilidadeFalsa());

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExportarCertificadoTreinamentoQuery(treinamentoId), default));

        Assert.Contains("externa", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(pdf.Chamado);
    }

    [Fact]
    public async Task Handle_CertificadoDaAahbrant_ContinuaEmitindoModeloProprio()
    {
        var db = DbContextFactory.Criar();
        var treinamentoId = await SemearAsync(db, OrigemCertificadoTreinamento.Aahbrant);
        var pdf = new PdfServiceQueNuncaDeveriaSerChamado();
        var handler = new ExportarCertificadoTreinamentoQueryHandler(db, pdf, new RastreabilidadeFalsa());

        var resultado = await handler.Handle(new ExportarCertificadoTreinamentoQuery(treinamentoId), default);

        Assert.NotNull(resultado);
        Assert.True(pdf.Chamado);
    }

    // Todo Treinamento cadastrado antes de 22/09 nasceu sem o campo — precisa continuar sendo
    // tratado como interno, senão a migration quebraria a emissão de certificado do acervo inteiro.
    [Fact]
    public void Treinamento_NovoSemInformarOrigem_NasceComoAahbrant()
    {
        var treinamento = new Treinamento();
        Assert.Equal(OrigemCertificadoTreinamento.Aahbrant, treinamento.OrigemCertificado);
    }
}
