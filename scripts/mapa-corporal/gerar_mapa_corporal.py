"""Gera o mapa corporal (frente e costas) usado no registro de lesão das ocorrências.

Saída: src/AAHBRANT.SST.TeamsApp/src/components/mapaCorporal/mapaCorporal.json — fonte única do
desenho: a tela (MapaCorporal.tsx) importa o JSON e a API embute o mesmo arquivo para desenhar o mapa
no relatório PDF da ocorrência (MapaCorporalSvg.cs). Depois de alterar este script, rode-o de novo e
faça commit do JSON gerado.

Desenho: uma silhueta anatômica contínua (proporção de ~8 cabeças, curvas de Bézier) recorta as
regiões clicáveis. As regiões são polígonos simples por faixa do corpo; o recorte pela silhueta dá a
elas o contorno realista. Só o lado direito da tela é desenhado — o esquerdo é espelhado em x = 100.

    python scripts/mapa-corporal/gerar_mapa_corporal.py
"""
import json
import re
from pathlib import Path

CENTRO = 100
SAIDA = Path(__file__).resolve().parents[2] / "src/AAHBRANT.SST.TeamsApp/src/components/mapaCorporal/mapaCorporal.json"


def esp(x):
    return round(2 * CENTRO - x, 2)


def num(v):
    return f"{v:g}"


def pt(p):
    return f"{num(p[0])},{num(p[1])}"


# ---------------------------------------------------------------- silhueta
# Metade direita da tela, do topo da cabeça até o períneo: (controle 1, controle 2, ponto final).
INICIO = (100, 8)
METADE = [
    ((113, 8), (122, 18), (122, 33)),          # crânio
    ((125.5, 31), (127, 41), (122.5, 45)),     # orelha
    ((121, 52), (118, 58), (113, 62)),         # mandíbula
    ((112.5, 66), (113, 70), (113.5, 74)),     # pescoço
    ((119, 80), (130, 82), (138, 85)),         # trapézio
    ((147, 88), (151.5, 96), (151.5, 107)),    # deltoide
    ((152.5, 119), (154, 134), (152.5, 148)),  # braço (face externa, tríceps)
    ((152.5, 154), (153.5, 160), (154, 166)),  # cotovelo
    ((158.5, 177), (158, 194), (155, 210)),    # antebraço (volume do braquiorradial)
    ((157.5, 218), (159.5, 228), (159, 238)),  # mão (borda externa)
    ((158.5, 246), (153, 250), (148.5, 247)),  # pontas dos dedos
    ((145, 240), (143, 230), (143.5, 220)),    # mão (borda interna)
    ((144, 216), (144, 214), (144, 212)),      # punho
    ((143, 200), (141, 185), (140, 170)),      # antebraço (face interna)
    ((139.5, 165), (139, 162), (139, 158)),    # dobra do cotovelo
    ((137.5, 145), (137, 128), (135, 117)),    # braço (face interna, bíceps) até a axila
    ((133, 115), (131, 117), (131.5, 120)),    # axila
    ((133, 135), (128, 150), (127, 165)),      # flanco / cintura
    ((126.5, 178), (134, 190), (136, 205)),    # quadril
    ((139.5, 222), (139.5, 252), (134, 282)),  # coxa (face externa, vasto lateral)
    ((131.5, 296), (132, 306), (131, 318)),    # joelho
    ((135, 333), (135.5, 351), (129.5, 372)),  # panturrilha
    ((126, 386), (125.5, 397), (126, 404)),    # tornozelo
    ((128.5, 412), (130, 420), (127, 426)),    # pé (borda externa)
    ((122, 430.5), (112, 430.5), (109.5, 426)),  # dedos do pé
    ((107.5, 420), (108.5, 410), (110, 404)),  # pé (borda interna)
    ((111, 388), (106.5, 366), (108.5, 345)),  # perna (face interna, gastrocnêmio medial)
    ((109.5, 330), (110, 320), (109, 310)),    # joelho (face interna)
    ((106, 282), (104, 246), (103, 228)),      # coxa (face interna, adutores)
    ((102.5, 221), (101, 217), (100, 216)),    # períneo
]


def silhueta():
    partes = [f"M{pt(INICIO)}"]
    for c1, c2, fim in METADE:
        partes.append(f"C{pt(c1)} {pt(c2)} {pt(fim)}")
    # volta pelo lado esquerdo: segmentos espelhados, em ordem inversa, com os controles trocados
    inicios = [INICIO] + [s[2] for s in METADE[:-1]]
    for (c1, c2, _), ini in reversed(list(zip(METADE, inicios))):
        partes.append(f"C{num(esp(c2[0]))},{num(c2[1])} {num(esp(c1[0]))},{num(c1[1])} {num(esp(ini[0]))},{num(ini[1])}")
    return " ".join(partes) + " Z"


# ------------------------------------------------------------------ traços
def espelhar_traco(d):
    """Espelha um traço escrito com comandos absolutos M/C/L (pares x,y)."""
    saida = re.findall(r"[A-Za-z]|-?\d*\.?\d+", d)
    resultado, i = [], 0
    while i < len(saida):
        t = saida[i]
        if t.isalpha():
            resultado.append(t)
            i += 1
            continue
        x, y = float(saida[i]), float(saida[i + 1])
        resultado.append(f"{num(esp(x))},{num(y)}")
        i += 2
    return " ".join(resultado).replace("M ", "M").replace("C ", "C").replace("L ", "L")


def elipse(cx, cy, rx, ry):
    return (f"M{num(cx - rx)},{num(cy)} A{num(rx)},{num(ry)} 0 1,0 {num(cx + rx)},{num(cy)} "
            f"A{num(rx)},{num(ry)} 0 1,0 {num(cx - rx)},{num(cy)} Z")


def pares(*tracos):
    return [d for t in tracos for d in (t, espelhar_traco(t))]


DETALHES_FRENTE = [
    "M100,82 L100,120",                                  # esterno
    "M100,128 L100,186",                                 # linha alba
    elipse(100, 178, 1.6, 2),                            # umbigo
    "M100,40 C99,46 97,50 98,52 C99.5,53.2 100.5,53.2 102,52",  # nariz
    "M94,57.5 C98,59 102,59 106,57.5",                   # boca
] + pares(
    "M113,78 C122,80 130,80 138,84",                     # clavícula
    "M101.5,119 C109,124.5 122,125 134,113",            # peitoral (borda inferior)
    "M118,89 C124,93 129,100 133,108",                   # peitoral (borda com o deltoide)
    "M101,140 C108,139 115,140 119,142",                 # abdominais
    "M101,156 C108,155 114,156 118,158",
    "M101,171 C107,170 113,171 117,172",
    "M119,128 C121,150 120,170 116,188",
    "M127,182 C122,192 113,203 106,212",                 # crista ilíaca / "V"
    "M138,86 C136,95 136,104 140,112",                   # deltoide
    "M146,118 C144,130 144,142 147,152",                 # bíceps
    "M147,172 C148,185 149,197 150,208",                 # antebraço
    "M112,64 C109,70 105,76 102,80",                     # esternocleidomastoideo
    "M120,228 C123,250 126,272 126,292",                 # quadríceps
    "M108,276 C112,290 116,298 120,300",                 # vasto medial
    "M118,330 C118,350 117,370 117,395",                 # tíbia
    "M150,238 L150,246.5", "M154,237 L154.5,245", "M146.5,236.5 L146.5,244",  # dedos
    "M114,424 L114,429.3", "M118.5,424 L118.5,429.8", "M123,423 L123,428.5",  # dedos do pé
    "M88,36 C91,34 95,34 97,36",                         # sobrancelha
) + [
    elipse(121, 312, 5.5, 7), elipse(79, 312, 5.5, 7),   # patelas
    elipse(91.5, 41, 4, 2), elipse(108.5, 41, 4, 2),     # olhos
]

DETALHES_COSTAS = [
    "M100,80 L100,196",                                  # coluna
    "M100,200 L100,226",                                 # sulco interglúteo
] + pares(
    "M106,92 C116,90 126,94 128,104 C126,118 118,126 108,128",   # escápula
    "M113,76 C116,96 110,116 100,132",                   # trapézio
    "M131,122 C126,140 118,158 106,168",                 # grande dorsal
    "M104,192 C115,188 128,190 135,200",                 # crista ilíaca
    "M102,230 C110,236 124,236 134,226",                 # prega glútea
    "M138,86 C134,96 135,106 139,113",                   # deltoide posterior
    "M141,160 C144,163 148,163 151,160",                 # olécrano
    "M120,240 C120,262 120,282 119,300",                 # isquiotibiais
    "M110,312 C116,314 124,314 130,312",                 # fossa poplítea
    "M119,330 C114,342 113,356 117,368",                 # gastrocnêmio medial
    "M121,330 C127,342 129,356 125,368",                 # gastrocnêmio lateral
    "M118,372 C118,385 118,395 118,402",                 # tendão calcâneo
    "M150,238 L150,246.5", "M154,237 L154.5,245", "M146.5,236.5 L146.5,244",
) + [
    "M86,52 C95,56 105,56 114,52",                       # nuca
    elipse(108, 198, 1.3, 1.3), elipse(92, 198, 1.3, 1.3),  # fossetas lombares
]


# ------------------------------------------------------------------ regiões
def poli(pontos):
    return "M" + " L".join(pt(p) for p in pontos) + " Z"


def poli_esp(pontos):
    return "M" + " L".join(f"{num(esp(x))},{num(y)}" for x, y in reversed(pontos)) + " Z"


def faixa(x0, x1, y0, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def regioes(vista, centrais, lateralizadas, lado_da_direita_da_tela):
    """lado_da_direita_da_tela: na frente, o lado direito da tela é o lado esquerdo do corpo."""
    lado_esp = "dir" if lado_da_direita_da_tela == "esq" else "esq"
    nomes = {"esq": ("esquerdo", "esquerda"), "dir": ("direito", "direita")}
    saida = []
    for codigo, rotulo, d in centrais:
        saida.append({"codigo": f"{vista}:{codigo}", "rotulo": rotulo, "d": d})
    for codigo, rotulo, pontos in lateralizadas:
        for lado, d in ((lado_esp, poli_esp(pontos)), (lado_da_direita_da_tela, poli(pontos))):
            o, a = nomes[lado]
            saida.append({"codigo": f"{vista}:{codigo}-{lado}", "rotulo": rotulo.format(o=o, a=a), "d": d})
    return saida


# Faixas compartilhadas pelas duas vistas (metade direita da tela).
OMBRO = [(117, 79), (165, 79), (165, 116), (137.5, 116), (131, 97)]
TRONCO_ALTO = [(100, 79), (117, 79), (131, 97), (137.5, 116), (137.5, 150), (100, 150)]
TRONCO_BAIXO = faixa(100, 137.5, 150, 192)
BRACO = [
    ("braco", "Braço {o}", faixa(137.5, 165, 116, 157)),
    ("cotovelo", "Cotovelo {o}", faixa(137.5, 165, 157, 170)),
    ("antebraco", "Antebraço {o}", faixa(137.8, 165, 170, 206)),
    ("punho", "Punho {o}", faixa(139.5, 165, 206, 217)),
]
PERNA = [
    ("joelho", "Joelho {o}", faixa(100, 150, 300, 330)),
    ("tornozelo", "Tornozelo {o}", faixa(100, 150, 392, 408)),
]

FRENTE = regioes("F", [
    ("cabeca", "Cabeça (crânio)", poli(faixa(70, 130, 0, 28))),
    ("face", "Face", poli(faixa(79.5, 120.5, 28, 64))),
    ("pescoco", "Pescoço", poli(faixa(70, 130, 64, 79))),
], [
    ("orelha", "Orelha {a}", faixa(120.5, 132, 28, 50)),
    ("ombro", "Ombro {o}", OMBRO),
    ("torax", "Tórax (lado {o})", TRONCO_ALTO),
    ("abdome", "Abdome (lado {o})", TRONCO_BAIXO),
    ("quadril", "Quadril / virilha (lado {o})", [(100, 192), (137.8, 192), (137.8, 212), (124, 230), (100, 230)]),
    *BRACO,
    ("mao", "Mão {a} (palma)", faixa(139.5, 165, 217, 238)),
    ("dedos-mao", "Dedos da mão {a}", faixa(139.5, 165, 238, 256)),
    ("coxa", "Coxa {a}", [(100, 230), (124, 230), (137.8, 212), (139.5, 212), (139.5, 252), (150, 252), (150, 300), (100, 300)]),
    *PERNA,
    ("perna", "Perna {a} (canela)", faixa(100, 150, 330, 392)),
    ("pe", "Pé {o}", faixa(100, 150, 408, 422)),
    ("dedos-pe", "Dedos do pé {o}", faixa(100, 150, 422, 440)),
], "esq")
# Olhos logo depois da face — a ordem da lista é a de leitura (cabeça → pés). Desenhados por cima da
# face, que vem antes (parte do corpo frequente em obra: projeção de partículas).
_depois_da_face = next(i for i, r in enumerate(FRENTE) if r["codigo"] == "F:face") + 1
FRENTE[_depois_da_face:_depois_da_face] = [
    {"codigo": "F:olho-dir", "rotulo": "Olho direito", "d": elipse(91.5, 41, 4.8, 2.8)},
    {"codigo": "F:olho-esq", "rotulo": "Olho esquerdo", "d": elipse(108.5, 41, 4.8, 2.8)},
]

COSTAS = regioes("C", [
    ("cabeca", "Cabeça (parte posterior)", poli(faixa(70, 130, 0, 64))),
    ("pescoco", "Pescoço (nuca / cervical)", poli(faixa(70, 130, 64, 79))),
], [
    ("ombro", "Ombro / escápula {o}", OMBRO),
    ("dorso", "Costas – região dorsal (lado {o})", TRONCO_ALTO),
    ("lombar", "Região lombar (lado {o})", TRONCO_BAIXO),
    ("gluteo", "Glúteo {o}", [(100, 192), (137.8, 192), (137.8, 212), (139.5, 212), (139.5, 236), (100, 236)]),
    *BRACO,
    ("mao", "Mão {a} (dorso)", faixa(139.5, 165, 217, 238)),
    ("dedos-mao", "Dedos da mão {a}", faixa(139.5, 165, 238, 256)),
    ("coxa", "Coxa {a} (posterior)", [(100, 236), (139.5, 236), (139.5, 252), (150, 252), (150, 300), (100, 300)]),
    *PERNA,
    ("perna", "Panturrilha {a}", faixa(100, 150, 330, 392)),
    ("pe", "Calcanhar / pé {o}", faixa(100, 150, 408, 440)),
], "dir")

# Nas costas, partes que existem nas duas vistas levam "(costas)" para o rótulo não ficar ambíguo.
for r in COSTAS:
    if not re.search(r"posterior|costas|dorsal|lombar|Glúteo|nuca|Calcanhar|Panturrilha|dorso", r["rotulo"]):
        r["rotulo"] += " (costas)"

mapa = {
    "_gerado_por": "scripts/mapa-corporal/gerar_mapa_corporal.py — não edite à mão",
    "viewBox": "40 0 120 440",
    "silhueta": silhueta(),
    "vistas": [
        {"id": "F", "titulo": "Vista anterior (frente)", "detalhes": DETALHES_FRENTE, "regioes": FRENTE},
        {"id": "C", "titulo": "Vista posterior (costas)", "detalhes": DETALHES_COSTAS, "regioes": COSTAS},
    ],
}

codigos = [r["codigo"] for v in mapa["vistas"] for r in v["regioes"]]
assert len(codigos) == len(set(codigos)), "código de região repetido"
SAIDA.parent.mkdir(parents=True, exist_ok=True)
SAIDA.write_text(json.dumps(mapa, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
print(f"{SAIDA} — {len(codigos)} regiões")
