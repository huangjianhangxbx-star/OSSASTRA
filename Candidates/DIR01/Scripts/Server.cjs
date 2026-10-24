/* DIR01本机只读预览服务：仅白名单资源，不联网、不写模型、不加载生产工程。 */
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const args=process.argv.slice(2);const option=(n,d)=>args.includes(n)?args[args.indexOf(n)+1]:d;
const study=path.resolve(option('--study','')),runtime=path.resolve(option('--runtime','')),port=Number(option('--port','5187'));
for(const p of [path.join(study,'assets/unit.json'),runtime])if(!fs.existsSync(p))throw Error('Missing local prerequisite: '+p);
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.atlas':'text/plain; charset=utf-8','.png':'image/png'};
http.createServer((req,res)=>{let name;try{name=decodeURIComponent(new URL(req.url,'http://localhost').pathname);}catch{return res.writeHead(400).end();}let file;
if(name==='/')file=path.join(__dirname,'Viewer.html');else if(name==='/viewer.js')file=path.join(__dirname,'Viewer.js');else if(name==='/vendor.js')file=runtime;else if(name==='/analysis.json')file=path.join(study,'DIR01-rig-direction-map.json');else if(['/assets/unit.json','/assets/unit.atlas','/assets/小蓝.png'].includes(name))file=path.join(study,name.slice(1));else return res.writeHead(404).end();
fs.readFile(file,(error,data)=>{if(error)return res.writeHead(404).end('Missing research file');res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store','Content-Security-Policy':"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'self'"});res.end(data);});
}).listen(port,'127.0.0.1',()=>console.log('DIR01 preview http://127.0.0.1:'+port));
