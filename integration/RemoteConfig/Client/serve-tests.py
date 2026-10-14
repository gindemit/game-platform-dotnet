"""Two OS-assigned owned loopback origins for real shipping browser adapter tests."""
import argparse
import gzip
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading
import time
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[3]

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--receipt',required=True);parser.add_argument('--log',required=True);args=parser.parse_args()
    corpus=json.loads((ROOT/'docs/implementation/remote-config/service-vectors-v1.json').read_text(encoding='utf-8'))
    canonical=lambda value:json.dumps(value,ensure_ascii=False,separators=(',',':'),sort_keys=True).encode('utf-8')
    documents={'current.json':corpus['fixtureRawObservations']['pointer'].encode(),'control.json':corpus['fixtureRawObservations']['control'].encode()}
    pointer=corpus['examples']['current'];manifest=corpus['examples']['manifest'];documents[pointer['manifest']['path']]=canonical(manifest);documents[manifest['selector']['path']]=canonical(corpus['fixtureDocuments']['selector'])
    for asset in corpus['fixtureDocuments']['assets'].values(): documents['assets/'+hashlib.sha256(canonical([asset['key'],asset['revision']])).hexdigest()+'.json']=canonical(asset)
    lock=threading.Lock();entries=[];stop=threading.Event();setup={};log=Path(args.log);log.parent.mkdir(parents=True,exist_ok=True)
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*unused): pass
        def respond(self,status,body,mime='application/json',immutable=False):
            decoded=len(body);encoding='gzip' if 'gzip' in self.headers.get('Accept-Encoding','') and body else None
            if encoding:body=gzip.compress(body,mtime=0)
            validator='"'+hashlib.sha256(body).hexdigest()+'"'
            if status==200 and urlsplit(self.path).path.startswith('/config/') and self.headers.get('If-None-Match')==validator:status=304;body=b'';decoded=0;encoding=None
            self.send_response(status);self.send_header('Content-Type',mime);self.send_header('Content-Length',str(len(body)));self.send_header('Access-Control-Allow-Origin','*');self.send_header('Access-Control-Expose-Headers','ETag,Date');self.send_header('Cache-Control','public,max-age=31536000,immutable' if immutable else 'public,max-age=0,must-revalidate');self.send_header('Vary','Accept-Encoding')
            if encoding:self.send_header('Content-Encoding',encoding)
            self.send_header('ETag',validator);self.end_headers()
            try:self.wfile.write(body)
            except (BrokenPipeError,ConnectionResetError):pass
            entry={'port':self.server.server_port,'method':self.command,'path':urlsplit(self.path).path,'status':status,'encodedBodyBytes':len(body),'decodedBodyBytes':decoded,'cookieHeaderPresent':bool(self.headers.get('Cookie')),'authorizationHeaderPresent':bool(self.headers.get('Authorization'))}
            with lock:entries.append(entry);log.open('a',encoding='utf-8').write(json.dumps(entry)+'\n')
        def do_GET(self):
            path=urlsplit(self.path).path
            if path in ('/','/browser-tests.html'):self.respond(200,(Path(__file__).parent/'browser-tests.html').read_bytes(),'text/html');return
            if path in ('/configuration-client.jslib','/browser-tests.js'):self.respond(200,(Path(__file__).parent/path[1:]).read_bytes(),'application/javascript');return
            if path=='/harness-config.json':self.respond(200,canonical(setup));return
            if path=='/fixture.json':self.respond(200,canonical(corpus));return
            if path=='/status.json':
                with lock:value={'options':sum(e['method']=='OPTIONS' for e in entries),'network304':sum(e['status']==304 for e in entries),'credentialsSent':any(e['cookieHeaderPresent'] or e['authorizationHeaderPresent'] for e in entries),'requests':len(entries)}
                self.respond(200,canonical(value));return
            if path.startswith('/config/') and path[len('/config/'):] in documents:self.respond(200,documents[path[len('/config/'):]],immutable=path not in ('/config/current.json','/config/control.json'));return
            self.respond(404,b'{}')
        def do_OPTIONS(self):self.respond(404,b'{}')
        def do_POST(self):
            if urlsplit(self.path).path=='/shutdown':self.respond(200,b'{}');stop.set()
            else:self.respond(405,b'{}')
    a=ThreadingHTTPServer(('127.0.0.1',0),Handler);b=ThreadingHTTPServer(('127.0.0.1',0),Handler)
    setup.update(normalRoot='http://127.0.0.1:'+str(b.server_port)+'/config/',controlRoot='http://127.0.0.1:'+str(b.server_port)+'/config/')
    receipt=Path(args.receipt);receipt.parent.mkdir(parents=True,exist_ok=True);state={'page':'http://127.0.0.1:'+str(a.server_port)+'/browser-tests.html','config':setup['normalRoot'],'ports':[a.server_port,b.server_port],'scope':'shipping browser adapter only; C# Unity lifecycle separately qualified','startedAtUnixSeconds':int(time.time()),'stopped':False};receipt.write_text(json.dumps(state,indent=2)+'\n',encoding='utf-8');print(json.dumps(state),flush=True)
    threads=[threading.Thread(target=server.serve_forever,daemon=True) for server in (a,b)]
    for thread in threads:thread.start()
    try:stop.wait()
    except KeyboardInterrupt:pass
    finally:
        for server in (a,b):server.shutdown();server.server_close()
        for thread in threads:thread.join()
        state.update(stopped=True,stoppedAtUnixSeconds=int(time.time()));receipt.write_text(json.dumps(state,indent=2)+'\n',encoding='utf-8')

if __name__=='__main__':main()
