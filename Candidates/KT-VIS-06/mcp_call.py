"""Bounded local MCP transport; never retries an unknown mutation.

Uses the configured per-project endpoint and exposed tools only.
"""
import argparse
import json
from pathlib import Path
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('tool')
parser.add_argument('--port', type=int, required=True, help='Use only the project endpoint verified from Editor.log and owning process')
parser.add_argument('--args', default='{}')
parser.add_argument('--code-file')
parser.add_argument('--output')
options = parser.parse_args()
arguments = json.loads(options.args)
if options.code_file:
    arguments.update(code=Path(options.code_file).read_text(encoding='utf-8-sig'),
                     skip_refresh=True, safety_checks=False)
payload = {'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
           'params': {'name': options.tool, 'arguments': arguments}}
request = urllib.request.Request(f'http://127.0.0.1:{options.port}/',
    json.dumps(payload).encode(), {'Content-Type': 'application/json',
                                 'Accept': 'application/json, text/event-stream'})
with urllib.request.urlopen(request, timeout=55) as response:
    result = json.load(response)
if options.output:
    Path(options.output).write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
inner = result.get('result', result)
if isinstance(inner, dict) and len(inner.get('content', [])) == 1:
    block = inner['content'][0]
    if block.get('type') == 'text':
        try:
            inner = json.loads(block['text'])
        except ValueError:
            pass
print(json.dumps(inner, ensure_ascii=False))
