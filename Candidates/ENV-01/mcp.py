"""Bounded recovery client for the verified current project MCP port.
Native connector still targets dead28229 after Editor restart; same exposed tools only.
Never replays a mutation; use durable request keys/readback after transport failure.
"""
import argparse,json,urllib.request
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('tool');p.add_argument('--code');p.add_argument('--args',default='{}');p.add_argument('--save');a=p.parse_args()
args=json.loads(a.args)
if a.code:args.update(code=Path(a.code).read_text(encoding='utf-8-sig'),skip_refresh=True)
payload={'jsonrpc':'2.0','id':1,'method':'tools/call','params':{'name':a.tool,'arguments':args}}
req=urllib.request.Request('http://127.0.0.1:28230/mcp',data=json.dumps(payload).encode(),headers={'Content-Type':'application/json','Accept':'application/json, text/event-stream'})
with urllib.request.urlopen(req,timeout=55) as res:data=json.loads(res.read().decode())
if a.save:Path(a.save).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
try:
    result=json.loads(data['result']['content'][0]['text'])
    value=result.get('data',{}).get('returnValue')
    if isinstance(value,str):
        try:value=json.loads(value)
        except json.JSONDecodeError:pass
    details=result.get('data') if value is None else None
    if isinstance(details,dict) and 'operation' in details:
        op=details['operation']
        details={'operation':{k:op.get(k) for k in ['operation_id','request_key','status','phase','error_code','message','compilation_errors']},'current_editor':details.get('current_editor')}
    print(json.dumps({'success':result.get('success'),'code':result.get('code'),'value':value,'data':details,'message':result.get('message') if details is None and value is None else None},ensure_ascii=False))
except (KeyError,TypeError,IndexError,json.JSONDecodeError):print(json.dumps(data,ensure_ascii=False))
