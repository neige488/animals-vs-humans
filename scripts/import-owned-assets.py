#!/usr/bin/env python3
"""Import selected owned prefab dependency closure into ignored Assets/ThirdParty only."""
import argparse, pathlib, tarfile, re, subprocess
parser=argparse.ArgumentParser();parser.add_argument('packages',nargs='+');args=parser.parse_args()
root=pathlib.Path(__file__).resolve().parents[1];dest=root/'game/Assets/ThirdParty'
if subprocess.run(['git','check-ignore','-q',str(dest/'probe')],cwd=root).returncode!=0:
 raise RuntimeError('ThirdParty must be ignored')
records={};archives=[]
for name in args.packages:
 archive=tarfile.open(name,'r:gz');archives.append(archive);members={m.name:m for m in archive.getmembers()}
 for member in archive.getmembers():
  if not member.name.endswith('/pathname'):continue
  raw=archive.extractfile(member).read().decode();parts=raw.splitlines();path=pathlib.PurePosixPath(parts[0])
  if not str(path).startswith('Assets/') or '..' in path.parts or path.is_absolute():raise ValueError('Unsafe asset path')
  if any(x not in ('00','') for x in parts[1:]):raise ValueError('Unexpected pathname suffix')
  guid=member.name.split('/')[0];records[guid]=(archive,members,path,guid)
seeds=[g for g,(_,_,p,_) in records.items() if str(p).endswith(('/Fox.prefab','/man_casual.prefab','/SM_Bld_Village_01.prefab','/SM_Bld_Village_02.prefab','/SM_Bld_Wall_01.prefab','/SM_Bld_Stall_01.prefab','/SM_Bld_Base_Floor_01.prefab','/SM_Bld_Base_Stairs_02.prefab','/SM_Env_Tree_05.prefab','/SM_Bld_Village_03.prefab','/SM_Bld_Village_04.prefab','/SM_Bld_Village_Top_01.prefab','/SM_Bld_Stall_Cover_01.prefab','/SM_Prop_Crate_01.prefab','/SM_Prop_Barrel_01.prefab','/SM_Prop_Cart_01.prefab','/SM_Bld_Well_01.prefab','/SM_Bld_Fence_01.prefab'))]
if len(seeds)<5:raise ValueError('Required owned prefabs not found')
seen=set();pending=list(seeds);files=0;total=0
# Game behavior uses our own C#. Vendor demos/scripts are never compiled.
excluded={'.cs','.asmdef','.asmref','.dll','.shadergraph','.shadersubgraph','.unity','.uxml','.uss'}
while pending:
 guid=pending.pop()
 if guid in seen or guid not in records:continue
 seen.add(guid);archive,members,path,_=records[guid]
 if path.suffix.lower() in excluded:continue
 target=dest/path.relative_to('Assets');target.parent.mkdir(parents=True,exist_ok=True)
 for item,suffix in [('asset',''),('asset.meta','.meta')]:
  key=guid+'/'+item
  if key not in members:continue
  if not members[key].isfile():raise ValueError('Non-file asset record')
  data=archive.extractfile(members[key]).read()
  # Binary models may reference external materials in their metadata.
  pending.extend(x.decode().lower() for x in re.findall(rb'guid: ([0-9a-fA-F]{32})',data))
  output=pathlib.Path(str(target)+suffix)
  if output.exists() and output.read_bytes()!=data:raise ValueError('Refusing differing existing asset: '+str(output))
  output.write_bytes(data);files+=1;total+=len(data)
print(f'Imported {files} files ({total//1024//1024} MiB), {len(seen)} dependency GUIDs, only inside ignored ThirdParty')
