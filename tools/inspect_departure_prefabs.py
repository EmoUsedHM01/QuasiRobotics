import UnityPy, json, re
from pathlib import Path
p=Path(r'C:/Program Files (x86)/Steam/steamapps/common/Quasimorph/Quasimorph_Data')
out=Path(__file__).resolve().parents[1] / 'game-reference' / 'departure-prefabs.json'
found=[]
for filename in ['resources.assets','sharedassets0.assets','sharedassets1.assets']:
 e=UnityPy.load(str(p/filename))
 for o in e.objects:
  if o.type.name!='GameObject': continue
  d=o.read()
  if d.m_Name not in ['PrepareRaidScreen']: continue
  print(filename,o.path_id,d.m_Name)
  def walk(go,depth=0):
   data=go.read_typetree(); result={'name':data.get('m_Name'),'components':[],'children':[]}
   for c in go.read().m_Component:
    co=c.component.deref()
    try: tree=co.read_typetree()
    except ValueError: tree={'strings':[s.decode('ascii') for s in re.findall(rb'[ -~]{5,}',co.get_raw_data())]}
    result['components'].append({'type':co.type.name,'data':tree})
    if co.type.name in ['Transform','RectTransform']:
     for ch in co.read().m_Children:
      tr=ch.read(); result['children'].append(walk(tr.m_GameObject.deref(),depth+1))
   return result
  found.append(walk(o))
out.write_text(json.dumps(found,indent=2))
print('saved',out,len(found))

