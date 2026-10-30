using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Dds.Cronogramas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class EmissorCronogramaMensalDds(SstDbContext db, ICronogramaMensalDdsPdfService pdf,
    IRegistradorRastreabilidadeService rastreabilidade) : IEmissorCronogramaMensalDds
{
    public async Task<ArquivoCronogramaDds> GerarAsync(Guid id, int versao, bool semanaCompleta, bool emitir, CancellationToken ct)
    {
        // Uma emissão guarda revisão + token + arquivo na mesma transação. Uma falha de PDF
        // não deixa revisão incompleta nem muda os bytes de uma emissão anterior.
        await using var transacao = emitir && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var cronograma = await db.CronogramasMensaisDds.AsNoTracking()
            .Include(c => c.Obra).Include(c => c.ResponsavelUsuario)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Cronograma não encontrado ou sem acesso.");
        if (cronograma.Versao != versao)
            throw new DbUpdateConcurrencyException("O cronograma mudou. Recarregue antes de gerar o documento.");
        if (cronograma.Obra?.LogoConteudo is not { Length: > 0 } logo)
            throw new InvalidOperationException("Cadastre a logo desta obra em Administração → Obras antes de gerar o cronograma.");
        var dias = ConteudoCronogramaDds.Ler(cronograma.ProgramacaoJson);
        // Nunca omitir temas de sábado/domingo, mesmo se o cliente pedir só os dias úteis.
        semanaCompleta |= dias.Any(d => d.Temas.Count > 0 && d.Data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        var modelo = new CronogramaDdsPdfModelo(cronograma.Obra.Nome, cronograma.Obra.Cliente, logo,
            cronograma.Ano, cronograma.Mes, cronograma.ResponsavelUsuario?.Nome ?? "",
            cronograma.NumeroDocumento, semanaCompleta, dias);
        var nome = $"cronograma-dds-{cronograma.Ano}-{cronograma.Mes:00}";
        if (!emitir) return new(pdf.Gerar(modelo), $"{nome}-previa.pdf");

        var json = JsonSerializer.Serialize(modelo);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var existente = await db.CronogramasMensaisDdsRevisoes.AsNoTracking()
            .Include(r => r.DocumentoAssinatura)
            .FirstOrDefaultAsync(r => r.CronogramaId == id && r.HashPlanejamento == hash, ct);
        if (existente?.DocumentoAssinatura?.PdfConteudo is { Length: > 0 } arquivo)
        {
            if (transacao is not null) await transacao.CommitAsync(ct);
            return new(arquivo, $"{nome}-r{existente.Numero:00}.pdf");
        }
        var numero = (await db.CronogramasMensaisDdsRevisoes.Where(r => r.CronogramaId == id)
            .MaxAsync(r => (int?)r.Numero, ct) ?? 0) + 1;
        var revisao = new CronogramaMensalDdsRevisao
        {
            CronogramaId = id, Numero = numero, VersaoOrigem = versao,
            HashPlanejamento = hash, ConteudoJson = json,
        };
        db.CronogramasMensaisDdsRevisoes.Add(revisao);
        await db.SaveChangesAsync(ct);
        var rastreio = await rastreabilidade.GarantirAsync(nameof(CronogramaMensalDdsRevisao), revisao.Id, ct);
        var bytes = pdf.Gerar(modelo, numero, rastreio);
        await rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, bytes, ct);
        revisao.DocumentoAssinaturaId = rastreio.DocumentoId;
        var documento = await db.DocumentosAssinatura.SingleAsync(d => d.Id == rastreio.DocumentoId, ct);
        // Documento emitido sem signatários: congelado, sem abrir coleta de assinaturas.
        documento.Status = StatusDocumentoAssinatura.Finalizado;
        documento.FinalizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (transacao is not null) await transacao.CommitAsync(ct);
        return new(bytes, $"{nome}-r{numero:00}.pdf");
    }
}
