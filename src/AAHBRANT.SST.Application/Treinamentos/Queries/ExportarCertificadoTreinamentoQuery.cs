using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Instrutores;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

public record ExportarCertificadoTreinamentoQuery(Guid TreinamentoId) : IRequest<byte[]?>;

public class ExportarCertificadoTreinamentoQueryHandler : IRequestHandler<ExportarCertificadoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly ICertificadoTreinamentoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarCertificadoTreinamentoQueryHandler(IAppDbContext db, ICertificadoTreinamentoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarCertificadoTreinamentoQuery request, CancellationToken ct)
    {
        var treinamento = await _db.Treinamentos
            .Include(t => t.CursoTreinamento)
            .Include(t => t.Trabalhador)
                .ThenInclude(t => t!.Obra)
            .Include(t => t.Trabalhador)
                .ThenInclude(t => t!.Funcao)
            .FirstOrDefaultAsync(t => t.Id == request.TreinamentoId, ct);
        if (treinamento is null || treinamento.Trabalhador is null || treinamento.CursoTreinamento is null) return null;

        // Certificado externo (lançamento retroativo, 22/09): o curso foi ministrado por terceiro e
        // só está registrado aqui. Emitir o modelo AAHBRANT nesse caso seria a empresa atestar um
        // treinamento que não deu, com o Técnico de Segurança assinando como responsável técnico —
        // exposição real em fiscalização e em ação trabalhista. O que se baixa é o arquivo original
        // anexado (ObterArquivoCertificadoTreinamentoQuery), não este PDF.
        if (treinamento.OrigemCertificado == OrigemCertificadoTreinamento.Externo)
            throw new InvalidOperationException(
                "Este treinamento foi realizado por instituição externa. Baixe o certificado original anexado — a AAHBRANT não emite certificado de treinamento que não ministrou.");

        // Um DocumentoAssinatura por treinamento (EntidadeTipo="Treinamento", EntidadeId=Treinamento.Id) —
        // ver docs/Motor-Assinatura-Eletronica.md. Signatários vêm direto da tabela (não do resultado
        // de GarantirAsync) para exibir nome+método+data no corpo do certificado.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
                .ThenInclude(s => s.Trabalhador).ThenInclude(t => t!.Funcao)
            .Where(d => d.EntidadeTipo == "Treinamento" && d.EntidadeId == request.TreinamentoId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        // Rastreabilidade sempre disponível a partir do primeiro export (Motor de Assinatura Task 2) —
        // antes disto, o QR só existia depois de uma finalização que, para Treinamento, nada nunca dispara.
        var rastreio = await _rastreabilidade.GarantirAsync("Treinamento", request.TreinamentoId, ct);

        // Instrutor escolhido na turma (perfil de instrutor): nome, cargo pela formação e registro. IgnoreQueryFilters
        // porque o instrutor pode ser de outra obra e o escopo de obra o esconderia.
        var instrutorNome = treinamento.InstituicaoInstrutor;
        var instrutorRegistro = treinamento.InstrutorRegistroProfissional;
        string? instrutorCargo = null;
        if (treinamento.SessaoTreinamentoId is not null)
        {
            var instrutorId = await _db.SessoesTreinamento.IgnoreQueryFilters()
                .Where(s => s.Id == treinamento.SessaoTreinamentoId)
                .Select(s => s.InstrutorTrabalhadorId)
                .FirstOrDefaultAsync(ct);
            if (instrutorId is not null)
            {
                var nome = await _db.Trabalhadores.IgnoreQueryFilters()
                    .Where(t => t.Id == instrutorId).Select(t => t.Nome).FirstOrDefaultAsync(ct);
                var perfil = await _db.PerfisInstrutor.FirstOrDefaultAsync(p => p.TrabalhadorId == instrutorId, ct);
                if (!string.IsNullOrWhiteSpace(nome)) instrutorNome = nome;
                if (perfil is not null)
                {
                    instrutorCargo = DescricaoInstrutor.Formacao(perfil.Formacao);
                    instrutorRegistro = DescricaoInstrutor.Registro(perfil.Conselho, perfil.NumeroRegistro, perfil.UfRegistro) ?? instrutorRegistro;
                }
            }
        }

        // O selo do instrutor é achado pelo nome. Quando o instrutor da turma assinou, a assinatura automática
        // antiga (sessão logada de quem encerrou a turma) deixa de aparecer no certificado; ela continua no cofre.
        var assinouComoInstrutor = documento?.Signatarios.FirstOrDefault(s => s.Papel == PapelAssinatura.Instrutor);
        var signatarios = documento?.Signatarios
            .Where(s => !(assinouComoInstrutor is not null && s.Papel is null && s.MetodoAutenticacao == MetodoAutenticacaoAssinatura.SessaoLogada))
            .Select(s => new CertificadoTreinamentoPdfSignatarioModelo(
                s.Papel == PapelAssinatura.Instrutor ? instrutorNome ?? string.Empty : s.Trabalhador?.Nome ?? string.Empty, s.AssinadoEm, s.MetodoAutenticacao, s.Trabalhador?.Funcao?.Nome,
                HashAssinatura(s.Id, s.TrabalhadorId, s.AssinadoEm, s.MetodoAutenticacao, rastreio.ConteudoHash)))
            .ToList() ?? new List<CertificadoTreinamentoPdfSignatarioModelo>();
        var qrCodePng = rastreio.QrCodePng;

        // Foto da turma (item 6 da proposta) — só existe quando este Treinamento foi gerado pelo
        // encerramento de uma SessaoTreinamento (fluxo antigo, criado manualmente por trabalhador,
        // continua sem foto). Usa a primeira das 3 fotos obrigatórias (Ordem = 1) como representativa.
        byte[]? fotoTurma = null;
        if (treinamento.SessaoTreinamentoId is not null)
        {
            fotoTurma = await _db.FotosEvidenciaSessaoTreinamento
                .Where(f => f.SessaoTreinamentoId == treinamento.SessaoTreinamentoId && f.Ativo)
                .OrderBy(f => f.Ordem)
                .Select(f => f.FotoConteudo)
                .FirstOrDefaultAsync(ct);
        }

        // Padrão da NR do curso (19/10): sem conteúdo programático próprio cadastrado, o verso usa o
        // conteúdo mínimo da norma do catálogo.
        var padraoNr = CatalogoNormasRegulamentadoras.Obter(treinamento.CursoTreinamento.NormaReferencia);
        // Ordem (19/10): o conteúdo gravado no próprio treinamento (o que foi ministrado na turma),
        // depois o do curso, por fim o padrão da NR.
        var conteudoProgramatico = !string.IsNullOrWhiteSpace(treinamento.ConteudoProgramatico)
            ? treinamento.ConteudoProgramatico
            : string.IsNullOrWhiteSpace(treinamento.CursoTreinamento.ConteudoProgramatico)
                ? padraoNr?.ConteudoProgramaticoTexto
                : treinamento.CursoTreinamento.ConteudoProgramatico;

        var modelo = new CertificadoTreinamentoPdfModelo(
            treinamento.Trabalhador.Obra?.Nome ?? string.Empty,
            treinamento.Trabalhador.Obra?.LogoConteudo,
            treinamento.Trabalhador.Obra?.Cnpj,
            treinamento.Trabalhador.Obra?.Endereco,
            treinamento.Trabalhador.Obra?.Cidade,
            treinamento.Trabalhador.Obra?.Uf,
            treinamento.Trabalhador.Nome,
            CpfMascarador.Mascarar(treinamento.Trabalhador.Cpf),
            treinamento.Trabalhador.Rg,
            treinamento.Trabalhador.Matricula ?? string.Empty,
            treinamento.Trabalhador.Funcao?.Nome ?? string.Empty,
            treinamento.CursoTreinamento.Nome,
            treinamento.CursoTreinamento.NormaReferencia,
            treinamento.CursoTreinamento.CargaHorariaMinima,
            treinamento.CargaHorariaRealizada,
            treinamento.DataRealizacao,
            treinamento.DataValidade,
            instrutorNome,
            instrutorRegistro,
            treinamento.NumeroCertificado,
            treinamento.Local,
            conteudoProgramatico,
            signatarios,
            qrCodePng,
            fotoTurma,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            rastreio.TemAssinatura,
            CatalogoNormasRegulamentadoras.NormalizarNumero(treinamento.CursoTreinamento.NormaReferencia),
            padraoNr?.Titulo,
            instrutorCargo);

        var pdf = _pdf.Gerar(modelo);
        // Guarda a cópia exata emitida e o SHA-256 dela — é o que permite conferir, depois,
        // que o arquivo em mãos não foi adulterado (ver HashArquivoCalculador).
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

    // Hash determinístico da assinatura: recalculável a partir dos dados gravados no cofre, então
    // serve de prova de que o selo corresponde àquela assinatura daquele documento.
    private static string HashAssinatura(Guid signatarioId, Guid trabalhadorId, DateTime assinadoEm, MetodoAutenticacaoAssinatura metodo, string hashDocumento)
    {
        var entrada = $"{signatarioId:N}|{trabalhadorId:N}|{assinadoEm:O}|{(int)metodo}|{hashDocumento}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(entrada)));
    }
}
