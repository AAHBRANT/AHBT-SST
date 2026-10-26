import json, urllib.request, urllib.parse, collections, sys
BASE="https://comunicaapi.pje.jus.br/api/v1/comunicacao"
def get(params):
    url=BASE+"?"+urllib.parse.urlencode(params)
    req=urllib.request.Request(url,headers={"Accept":"application/json","User-Agent":"Mozilla/5.0"})
    with urllib.request.urlopen(req,timeout=60) as r: return json.loads(r.read())
# 1. estrutura de um item
d=get({"nomeParte":"CONSORCIO PARQUE ROGER FASE II","siglaTribunal":"TJPB","pagina":1,"itensPorPagina":1})
it=d["items"][0]; it2=dict(it); it2["texto"]=it2["texto"][:120]
print("KEYS:",sorted(it.keys())); print(json.dumps(it2,ensure_ascii=False,indent=1)[:3000])
# 2. descoberta por nome (todos tribunais)
for nome in ["AAHBRANT ENGENHARIA","AAHBRANT","CONSORCIO PARQUE ROGER FASE II","CONSORCIO PONTE RIO CUIA","CONSÓRCIO PONTE RIO CUIÁ"]:
    procs=collections.OrderedDict(); pag=1; total=None; datas=[]
    while True:
        d=get({"nomeParte":nome,"pagina":pag,"itensPorPagina":100})
        total=d.get("count"); items=d.get("items",[])
        if not items: break
        for i in items:
            n=i.get("numero_processo") or i.get("numeroprocessocommascara")
            procs.setdefault(n,{"trib":i.get("siglaTribunal"),"orgao":i.get("nomeOrgao"),"n":0,"ult":None,"mask":i.get("numeroprocessocommascara")})
            procs[n]["n"]+=1; procs[n]["ult"]=max(procs[n]["ult"] or "",i.get("data_disponibilizacao") or "")
            datas.append(i.get("data_disponibilizacao"))
        if len(items)<100 or pag>=40: break
        pag+=1
    print(f"\n### {nome}: comunicacoes={total} processos_distintos={len(procs)} periodo={min(datas) if datas else None}..{max(datas) if datas else None}")
    bytrib=collections.Counter(v["trib"] for v in procs.values()); print("  por tribunal:",dict(bytrib))
    for n,v in list(procs.items())[:60]: print(f"  {v['mask'] or n} | {v['trib']} | {v['orgao']} | com={v['n']} | ult={v['ult']}")
# 3. filtro por CNPJ suportado?
for k in ["numeroCnpj","cnpj","numeroDocumento","documento"]:
    try:
        d=get({k:"55980337000102","siglaTribunal":"TJPB","pagina":1,"itensPorPagina":1}); print("\nparam",k,"count=",d.get("count"))
    except Exception as e: print("param",k,"ERR",e)
