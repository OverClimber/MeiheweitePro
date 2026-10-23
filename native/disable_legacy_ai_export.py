"""Mark the unsupported legacy AI entry unavailable in a pinned IL2CPP export."""
import hashlib, pathlib, sys
project=pathlib.Path(sys.argv[1])
marker=b'IL2CPP_EXTERN_C IL2CPP_METHOD_ATTR bool smallYgopro_startAI_m766B3EA8315E2F51DC1DB777B41DEB113B0C8771 '
candidates=[]
for path in (project/'Classes/Native').glob('Assembly-CSharp*.cpp'):
    content=path.read_bytes()
    start=content.find(marker)
    if start<0: continue
    brace=content.find(b'{',start)
    semicolon=content.find(b';',start)
    if brace>=0 and brace-start<500 and (semicolon<0 or brace<semicolon): candidates.append(path)
if len(candidates)!=1: raise SystemExit('AI definition missing or ambiguous')
source=candidates[0]
data=source.read_bytes()
pos=data.find(marker)
if pos<0 or data.find(marker,pos+1)>=0: raise SystemExit('AI entry missing or ambiguous')
brace=data.find(b'{',pos)
if brace<0 or brace-pos>500: raise SystemExit('AI entry layout changed')
insert=b'\n\t// iOS: the legacy native AI mode has no compatible implementation.\n\treturn false;\n'
if insert in data: raise SystemExit('AI entry already patched')
source.write_bytes(data[:brace+1]+insert+data[brace+1:])
print('Legacy AI disabled in exported IL2CPP source; original sha256',hashlib.sha256(data).hexdigest())
