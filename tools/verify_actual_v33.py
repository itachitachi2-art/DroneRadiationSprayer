#!/usr/bin/env python3
"""Supplementary production compile against user-provided actual v3.3 DLLs.

This script copies no reference binaries. It never runs Unity or the game.
Keep the user-game-validated shipping DLL; this output is build verification
unless intentionally preparing a new repair candidate.
"""
import argparse,hashlib,json,os,subprocess
from pathlib import Path

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for n in ['managed','harmony','mono','compiler','mono-config','assembly-name','output']:
        p.add_argument('--'+n,required=True)
    p.add_argument('--repo-root',type=Path,default=Path.cwd())
    p.add_argument('--source-dir',required=True)
    p.add_argument('--additional-source-dir',action='append',default=[])
    a=p.parse_args();root=a.repo_root.resolve();managed=Path(a.managed).resolve();harmony=Path(a.harmony).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
    assert (managed/'Assembly-CSharp.dll').is_file() and harmony.is_file()
    refs=sorted(managed.glob('*.dll'))+[harmony]
    sources=sorted((root/a.source_dir).glob('*.cs'))
    for extra in a.additional_source_dir:sources+=sorted((root/extra).glob('*.cs'))
    assert sources
    dll=out/(a.assembly_name+'.dll');rsp=out/'actual-v33.rsp'
    rsp.write_text('\n'.join(['/nologo','/noconfig','/nostdlib+','/target:library','/optimize+','/debug-','/langversion:latest','/out:"'+str(dll)+'"']+['/reference:"'+str(f)+'"' for f in refs]+['"'+str(f)+'"' for f in sources])+'\n')
    command=[a.mono,'--config',a.mono_config,a.compiler,'/noconfig','@'+str(rsp)]
    result=subprocess.run(command,capture_output=True,text=True);(out/'compile.log').write_text(result.stdout+result.stderr)
    sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
    report={'status':'PASS' if result.returncode==0 and dll.is_file() else 'FAIL','command':command,'source_sha256':{f.relative_to(root).as_posix():sha(f) for f in sources},'reference_sha256':{f.name:sha(f) for f in refs},'dll_sha256':sha(dll) if dll.exists() else None,'game_executed':False,'windows_build_executed':False,'production_references':'Only supplied actual Managed and Harmony DLLs; no game/API doubles'}
    (out/'verification.json').write_text(json.dumps(report,indent=2)+'\n');print(report['status']);raise SystemExit(result.returncode)

if __name__=='__main__':main()
