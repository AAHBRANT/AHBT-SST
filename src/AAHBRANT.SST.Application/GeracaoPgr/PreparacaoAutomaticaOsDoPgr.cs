using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Quando o PGR é criado e aprovado dentro do sistema (ou já está vigente na obra), prepara automaticamente
// o que a Ordem de Serviço exige da SST por atividade/função:
// 1. Instrução de Trabalho (IT) vigente (MaterialApoio + AtividadeDocumento) gerada a partir dos riscos e medidas da atividade;
// 2. Confirmação das atividades do GHE para as funções daquele GHE (AtividadeFuncao = Validada), preservando decisões anteriores;
// 3. Validação de sugestões da IA eventualmente pendentes nas atividades do PGR vigente.
public static class PreparacaoAutomaticaOsDoPgr
{
    private static readonly Regex CodigoEapInicioRegex = new(@"^([A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*)\s+", RegexOptions.Compiled);

    public static async Task AplicarNaAprovacaoAsync(
        IAppDbContext db,
        Guid obraId,
        IReadOnlyList<Ghe> novosGhes,
        Guid? usuarioId,
        string? aprovador,
        DateTime agoraUtc,
        CancellationToken ct)
    {
        var nomeAprovador = string.IsNullOrWhiteSpace(aprovador) ? "Engenheiro de Segurança (PGR)" : aprovador.Trim();
        var materiaisExistentes = await db.MateriaisApoio
            .Where(m => (m.Tipo == TipoDocumentoSst.InstrucaoTrabalho || m.Tipo == TipoDocumentoSst.Procedimento)
                && m.Situacao == SituacaoDocumentoSst.Vigente)
            .ToListAsync(ct);

        var porCodigo = materiaisExistentes
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => m.Codigo!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var indice = 1;
        foreach (var ghe in novosGhes.OrderBy(g => g.Numero))
        {
            var funcoesDoGhe = ghe.Funcoes
                .Select(gf => gf.Funcao?.Id ?? gf.FuncaoId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            foreach (var atividade in ghe.Atividades)
            {
                // 1. Garante que toda medida eventualmente pendente do risco saia validada na aprovação do PGR.
                foreach (var risco in atividade.Riscos)
                {
                    foreach (var m in risco.Medidas.Where(m => m.Status == StatusMedidaPrevencao.Pendente))
                    {
                        m.Status = StatusMedidaPrevencao.Validada;
                        m.ValidadoEmUtc = agoraUtc;
                        m.ValidadoPorUsuarioId = usuarioId;
                        m.ValidadoPorNome = nomeAprovador;
                    }
                }

                // 2. Se a atividade ainda não tem procedimento/IT vinculado (ex.: não herdou de revisão anterior),
                // cria ou reutiliza a Instrução de Trabalho (IT) vigente da atividade e vincula.
                if (atividade.Documentos.Count == 0)
                {
                    var codigo = GerarCodigoIt(atividade.Nome, ghe.Numero, indice);
                    if (!porCodigo.TryGetValue(codigo, out var material))
                    {
                        var titulo = Limitar($"IT — {atividade.Nome}", 200);
                        material = new MaterialApoio
                        {
                            Nome = titulo,
                            Categoria = "Instruções de Trabalho (PGR)",
                            NomeArquivo = $"{NormalizarNomeArquivo(codigo)}.pdf",
                            ContentType = "application/pdf",
                            Conteudo = GerarPdfInstrucaoTrabalho(codigo, titulo, ghe, atividade),
                            Tipo = TipoDocumentoSst.InstrucaoTrabalho,
                            Codigo = codigo,
                            Revisao = "01",
                            Situacao = SituacaoDocumentoSst.Vigente,
                            FormaConsulta = "Disponível no G-SST e com a liderança da frente de serviço",
                        };
                        db.MateriaisApoio.Add(material);
                        porCodigo[codigo] = material;
                    }

                    db.AtividadeDocumentos.Add(new AtividadeDocumento
                    {
                        Atividade = atividade,
                        AtividadeId = atividade.Id,
                        MaterialApoio = material,
                        MaterialApoioId = material.Id,
                    });
                }

                // 3. Confirma a atividade para todas as funções do GHE que ainda não tenham decisão preservada.
                foreach (var funcaoId in funcoesDoGhe)
                {
                    var jaTemDecisao = db is DbContext ef
                        && ef.ChangeTracker.Entries<AtividadeFuncao>()
                            .Any(e => e.Entity.AtividadeId == atividade.Id && e.Entity.FuncaoId == funcaoId);
                    if (jaTemDecisao) continue;

                    db.AtividadesFuncoes.Add(new AtividadeFuncao
                    {
                        ObraId = obraId,
                        AtividadeId = atividade.Id,
                        FuncaoId = funcaoId,
                        Status = StatusMedidaPrevencao.Validada,
                        OrigemVinculo = OrigemMedidaPrevencao.Manual,
                        JustificativaIa = "Confirmada na aprovação do PGR",
                        ValidadoEmUtc = agoraUtc,
                        ValidadoPorUsuarioId = usuarioId,
                        ValidadoPorNome = nomeAprovador,
                    });
                }

                indice++;
            }
        }
    }

    // Backfill idempotente para obras que já têm PGR vigente aprovado no banco (ex.: Obra Beira Rio).
    public static async Task GarantirParaObraComPgrVigenteAsync(IAppDbContext db, Guid obraId, CancellationToken ct)
    {
        var pgr = await db.Pgrs
            .Where(p => p.ObraId == obraId && p.Status == StatusPgr.Vigente)
            .OrderByDescending(p => p.DataElaboracao)
            .FirstOrDefaultAsync(ct);
        if (pgr is null) return;

        var geracao = await db.GeracoesPgrEap
            .Where(g => g.ObraId == obraId && g.Status == StatusGeracaoPgrEap.Aprovado)
            .OrderByDescending(g => g.AprovadoEmUtc)
            .FirstOrDefaultAsync(ct);

        var agora = geracao?.AprovadoEmUtc ?? DateTime.UtcNow;
        var usuarioId = geracao?.AprovadoPorUsuarioId ?? pgr.ResponsavelUsuarioId;
        var aprovador = !string.IsNullOrWhiteSpace(geracao?.AprovadoPorNome)
            ? geracao!.AprovadoPorNome!
            : "Engenheiro de Segurança (PGR)";

        var ghes = await db.Ghes
            .Where(g => g.ObraId == obraId)
            .Include(g => g.Funcoes)
            .OrderBy(g => g.Numero)
            .ToListAsync(ct);
        if (ghes.Count == 0) return;

        var gheIds = ghes.Select(g => g.Id).ToHashSet();
        var atividades = await db.Atividades.EmUso(db)
            .Where(a => a.ObraId == obraId && a.GheId != null && gheIds.Contains(a.GheId.Value))
            .Include(a => a.Documentos).ThenInclude(d => d.MaterialApoio)
            .Include(a => a.Riscos).ThenInclude(r => r.Perigo)
            .Include(a => a.Riscos).ThenInclude(r => r.Medidas)
            .Include(a => a.ItensOs)
            .OrderBy(a => a.Nome)
            .ToListAsync(ct);
        if (atividades.Count == 0) return;

        var materiaisVigentes = await db.MateriaisApoio
            .Where(m => (m.Tipo == TipoDocumentoSst.InstrucaoTrabalho || m.Tipo == TipoDocumentoSst.Procedimento)
                && m.Situacao == SituacaoDocumentoSst.Vigente)
            .ToListAsync(ct);
        var porCodigo = materiaisVigentes
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => m.Codigo!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var atividadeIds = atividades.Select(a => a.Id).ToList();
        var decisoesExistentes = await db.AtividadesFuncoes
            .Where(af => af.ObraId == obraId && atividadeIds.Contains(af.AtividadeId))
            .ToListAsync(ct);
        var porPar = decisoesExistentes.ToDictionary(x => (x.AtividadeId, x.FuncaoId));

        var alterou = false;
        var indice = 1;

        foreach (var ghe in ghes)
        {
            var funcoesDoGhe = ghe.Funcoes.Select(f => f.FuncaoId).Distinct().ToList();
            var atividadesDoGhe = atividades.Where(a => a.GheId == ghe.Id).ToList();

            foreach (var atividade in atividadesDoGhe)
            {
                // 1. Valida sugestões pendentes da IA (medidas e itens da OS) e garante avaliação dos riscos
                foreach (var risco in atividade.Riscos)
                {
                    if (risco.AvaliadoEmUtc is null)
                    {
                        risco.AvaliadoEmUtc = agora;
                        risco.AvaliadoPorUsuarioId = usuarioId;
                        risco.AvaliadoPorNome = aprovador;
                        alterou = true;
                    }

                    foreach (var m in risco.Medidas.Where(m => m.Status == StatusMedidaPrevencao.Pendente))
                    {
                        m.Status = StatusMedidaPrevencao.Validada;
                        m.ValidadoEmUtc = agora;
                        m.ValidadoPorUsuarioId = usuarioId;
                        m.ValidadoPorNome = aprovador;
                        alterou = true;
                    }

                    if (!risco.Medidas.Any(m => m.Status == StatusMedidaPrevencao.Validada))
                    {
                        db.MedidasPrevencao.Add(new MedidaPrevencao
                        {
                            RiscoId = risco.Id,
                            Fase = FaseMedidaPrevencao.Durante,
                            Texto = "Cumprir a Instrução de Trabalho da atividade e utilizar os EPIs obrigatórios da função.",
                            OrigemMedida = OrigemMedidaPrevencao.SugestaoIa,
                            Status = StatusMedidaPrevencao.Validada,
                            Ordem = 0,
                            ValidadoEmUtc = agora,
                            ValidadoPorUsuarioId = usuarioId,
                            ValidadoPorNome = aprovador,
                        });
                        alterou = true;
                    }
                }

                foreach (var item in atividade.ItensOs.Where(i => i.Status == StatusMedidaPrevencao.Pendente))
                {
                    item.Status = StatusMedidaPrevencao.Validada;
                    item.ValidadoEmUtc = agora;
                    item.ValidadoPorUsuarioId = usuarioId;
                    item.ValidadoPorNome = aprovador;
                    alterou = true;
                }

                // 2. Garante IT vigente vinculada à atividade
                var temDocVigente = atividade.Documentos.Any(d =>
                    d.MaterialApoio is { Situacao: SituacaoDocumentoSst.Vigente } m
                    && (m.Tipo == TipoDocumentoSst.InstrucaoTrabalho || m.Tipo == TipoDocumentoSst.Procedimento));

                if (!temDocVigente)
                {
                    var codigo = GerarCodigoIt(atividade.Nome, ghe.Numero, indice);
                    if (!porCodigo.TryGetValue(codigo, out var material))
                    {
                        var titulo = Limitar($"IT — {atividade.Nome}", 200);
                        material = new MaterialApoio
                        {
                            Nome = titulo,
                            Categoria = "Instruções de Trabalho (PGR)",
                            NomeArquivo = $"{NormalizarNomeArquivo(codigo)}.pdf",
                            ContentType = "application/pdf",
                            Conteudo = GerarPdfInstrucaoTrabalho(codigo, titulo, ghe, atividade),
                            Tipo = TipoDocumentoSst.InstrucaoTrabalho,
                            Codigo = codigo,
                            Revisao = "01",
                            Situacao = SituacaoDocumentoSst.Vigente,
                            FormaConsulta = "Disponível no G-SST e com a liderança da frente de serviço",
                        };
                        db.MateriaisApoio.Add(material);
                        porCodigo[codigo] = material;
                        alterou = true;
                    }

                    if (atividade.Documentos.All(d => d.MaterialApoioId != material.Id))
                    {
                        db.AtividadeDocumentos.Add(new AtividadeDocumento
                        {
                            AtividadeId = atividade.Id,
                            MaterialApoioId = material.Id,
                        });
                        alterou = true;
                    }
                }

                // 3. Garante confirmação da atividade para as funções do GHE
                foreach (var funcaoId in funcoesDoGhe)
                {
                    if (!porPar.TryGetValue((atividade.Id, funcaoId), out var decisao))
                    {
                        decisao = new AtividadeFuncao
                        {
                            ObraId = obraId,
                            AtividadeId = atividade.Id,
                            FuncaoId = funcaoId,
                            Status = StatusMedidaPrevencao.Validada,
                            OrigemVinculo = OrigemMedidaPrevencao.Manual,
                            JustificativaIa = "Confirmada na aprovação do PGR",
                            ValidadoEmUtc = agora,
                            ValidadoPorUsuarioId = usuarioId,
                            ValidadoPorNome = aprovador,
                        };
                        db.AtividadesFuncoes.Add(decisao);
                        porPar[(atividade.Id, funcaoId)] = decisao;
                        alterou = true;
                    }
                    else if (decisao.Status == StatusMedidaPrevencao.Pendente)
                    {
                        decisao.Status = StatusMedidaPrevencao.Validada;
                        decisao.ValidadoEmUtc = agora;
                        decisao.ValidadoPorUsuarioId = usuarioId;
                        decisao.ValidadoPorNome = aprovador;
                        alterou = true;
                    }
                }

                indice++;
            }
        }

        if (alterou)
            await db.SaveChangesAsync(ct);
    }

    public static string GerarCodigoIt(string nomeAtividade, int gheNumero, int indice)
    {
        var texto = (nomeAtividade ?? string.Empty).Trim();
        var match = CodigoEapInicioRegex.Match(texto);
        if (match.Success)
        {
            var eap = match.Groups[1].Value.ToUpperInvariant();
            return Limitar($"IT-SSO-{eap}", 60);
        }

        var slug = FuncaoSstClassifier.Normalizar(texto)
            .Replace(' ', '-')
            .ToUpperInvariant();
        slug = Regex.Replace(slug, @"[^A-Z0-9\-]", "");
        if (slug.Length > 24) slug = slug[..24].TrimEnd('-');
        return string.IsNullOrWhiteSpace(slug)
            ? $"IT-SSO-GHE{gheNumero:00}-{indice:00}"
            : Limitar($"IT-SSO-{slug}", 60);
    }

    private static string NormalizarNomeArquivo(string codigo) =>
        Regex.Replace(codigo.ToLowerInvariant(), @"[^a-z0-9\-\._]", "-");

    private static string Limitar(string s, int max) => s.Length <= max ? s : s[..max];

    // Gera um PDF 1.4 válido e enxuto com os dados da Instrução de Trabalho (código, atividade, ambiente, riscos e medidas).
    public static byte[] GerarPdfInstrucaoTrabalho(string codigo, string titulo, Ghe ghe, Atividade atividade)
    {
        var linhas = new List<string>
        {
            $"INSTRUCAO DE TRABALHO - {SemAcento(codigo)} (REV. 01)",
            $"Atividade: {SemAcento(atividade.Nome)}",
            $"GHE {ghe.Numero:00}: {SemAcento(ghe.Setor ?? "Geral")} | Ambiente: {SemAcento(ghe.DescricaoAmbiente ?? "Canteiro de obras")}",
            "------------------------------------------------------------------------",
            "RISCOS E MEDIDAS DE PREVENCAO APROVADOS NO PGR:",
        };

        foreach (var r in atividade.Riscos.OrderByDescending(x => x.NivelRisco))
        {
            var perigo = SemAcento(r.Perigo?.Nome ?? "Risco operacional");
            linhas.Add($"* Perigo: {perigo} (Nivel: {r.NivelRisco})");
            foreach (var m in r.Medidas.Where(x => x.Status != StatusMedidaPrevencao.Descartada).OrderBy(x => x.Fase).ThenBy(x => x.Ordem).Take(6))
            {
                var fase = m.Fase switch
                {
                    FaseMedidaPrevencao.Antes => "Antes",
                    FaseMedidaPrevencao.Durante => "Durante",
                    FaseMedidaPrevencao.Apos => "Apos",
                    _ => "Controle",
                };
                linhas.Add($"  - [{fase}] {SemAcento(Limitar(m.Texto, 95))}");
            }
        }

        var streamSb = new StringBuilder();
        streamSb.AppendLine("BT");
        streamSb.AppendLine("/F1 10 Tf");
        var y = 800;
        foreach (var linha in linhas.Take(48))
        {
            var escapada = linha.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            streamSb.AppendLine($"1 0 0 1 40 {y} Tm ({escapada}) Tj");
            y -= 15;
        }
        streamSb.AppendLine("ET");
        var conteudoStream = Encoding.ASCII.GetBytes(streamSb.ToString());

        using var ms = new MemoryStream();
        void WriteAscii(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        WriteAscii("%PDF-1.4\n");
        var offsets = new int[6];

        offsets[1] = (int)ms.Position;
        WriteAscii("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = (int)ms.Position;
        WriteAscii("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = (int)ms.Position;
        WriteAscii("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        offsets[4] = (int)ms.Position;
        WriteAscii("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        offsets[5] = (int)ms.Position;
        WriteAscii($"5 0 obj\n<< /Length {conteudoStream.Length} >>\nstream\n");
        ms.Write(conteudoStream);
        WriteAscii("\nendstream\nendobj\n");

        var xrefPos = (int)ms.Position;
        WriteAscii("xref\n0 6\n0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++)
            WriteAscii($"{offsets[i]:0000000000} 00000 n \n");

        WriteAscii($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        return ms.ToArray();
    }

    private static string SemAcento(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var norm = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var c in norm)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c <= 126 && c >= 32 ? c : ' ');
        }
        return sb.ToString();
    }
}
