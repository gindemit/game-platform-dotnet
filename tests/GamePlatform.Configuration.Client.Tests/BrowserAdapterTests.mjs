import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { webcrypto } from 'node:crypto';
import test from 'node:test';

function adapter(fetch) {
  const library = {}, callbacks = [];
  const context = vm.createContext({ LibraryManager: { library }, mergeInto: Object.assign,
    URL, TextEncoder, TextDecoder, Uint8Array, crypto: webcrypto, fetch,
    AbortController, setTimeout, clearTimeout, btoa, atob,
    SendMessage: (...args) => callbacks.push(args), UTF8ToString: value => value });
  vm.runInContext(readFileSync(new URL('../../integration/RemoteConfig/Client/configuration-client.jslib', import.meta.url), 'utf8'), context);
  context.GPConfigurationClient = library.$GPConfigurationClient;
  const value = library.$GPConfigurationClient.create('a'.repeat(32),'https://normal.example/app/','https://control.example/app/',false);
  return { value, factory: library.$GPConfigurationClient, context, library, callbacks };
}
function response(bytes, options = {}) {
  const body = new ReadableStream({ start(controller) { controller.enqueue(bytes); controller.close(); } });
  return { url: options.url || 'https://normal.example/app/current.json', status: options.status || 200, body,
    headers: new Headers({ 'Content-Type': options.mime || 'application/json', 'ETag': '"fixture"' }) };
}
test('fetch adapter uses direct credential-free CORS revalidation and bounded decoded streaming', async () => {
  let observed;
  const { value } = adapter(async (url, options) => { observed = options; return response(new TextEncoder().encode('{}')); });
  const result = await value.fetch('https://normal.example/app/current.json', 10, false, new AbortController().signal);
  assert.equal(result.status, 200); assert.equal(result.bytes.length, 2);
  assert.equal(observed.credentials, 'omit'); assert.equal(observed.redirect, 'error'); assert.equal(observed.mode, 'cors'); assert.equal(observed.cache, 'no-cache'); assert.equal(observed.headers, undefined);
  await assert.rejects(value.fetch('https://normal.example/app/current.json', 1, false, new AbortController().signal), /bounds/);
});
test('origin, redirects, mime, immutable path and base64 bounds reject before host transfer', async () => {
  const { value } = adapter(async () => response(new Uint8Array(2), { mime: 'text/html' }));
  assert.throws(() => value.allowed('https://other.example/app/current.json'));
  assert.throws(() => value.allowed('https://normal.example/app/current.json?subject=private'));
  assert.throws(() => value.allowed('https://normal.example/app/../escape'));
  assert.throws(() => value.allowed('https://normal.example/app/assets/arbitrary.json'));
  assert.throws(() => value.decode('AAAA', 1), /bounds/);
  await assert.rejects(value.fetch('https://normal.example/app/current.json', 10, false, new AbortController().signal), /mime/);
  const redirected = adapter(async () => response(new Uint8Array(0), { url: 'https://other.example/app/current.json' })).value;
  await assert.rejects(redirected.fetch('https://normal.example/app/current.json', 10, false, new AbortController().signal), /redirect/);
});
test('cancel fences late callbacks and rejects duplicate bridge work', async () => {
  const { value, callbacks } = adapter(async () => response(new Uint8Array(0)));
  let release;
  const pending = new Promise(resolve => { release = resolve; });
  value.start('0000000000000001', 'owned-host', 'OnConfigurationStore', () => pending);
  value.cancel('0000000000000001'); release('201|');
  await new Promise(resolve => setTimeout(resolve, 0)); assert.equal(callbacks.length, 0);
});
test('storage denial is explicit and namespace validates immutable owner tuple before IndexedDB', async () => {
  const { value } = adapter(async () => response(new Uint8Array(0)));
  await assert.rejects(value.open(), /storage_denied/);
  const corpus = JSON.parse(readFileSync(new URL('../../docs/implementation/remote-config/service-vectors-v1.json', import.meta.url), 'utf8'));
  const metadata = new TextEncoder().encode(JSON.stringify(corpus.examples.cache));
  const sha = await value.sha(new TextEncoder().encode(JSON.stringify(['synthetic','local',corpus.examples.cache.owner.ownerId,corpus.examples.cache.owner.subjectId])));
  assert.equal((await value.namespace(metadata, sha)).owner.ownerId, corpus.examples.cache.owner.ownerId);
  await assert.rejects(value.namespace(metadata, '0'.repeat(64)), /owner_changed/);
  const ownerless = { ...corpus.examples.cache, owner: null };
  await assert.rejects(value.namespace(new TextEncoder().encode(JSON.stringify(ownerless)), sha), /owner_changed/);
});
test('two explicit hosts isolate roots and same request IDs, cancellation and disposal', async () => {
  const { value, factory, callbacks } = adapter(async () => response(new Uint8Array(0)));
  const other = factory.create('b'.repeat(32),'https://other.example/app/','https://other-control.example/app/',false);
  assert.throws(()=>value.allowed('https://other.example/app/current.json'));
  assert.equal(other.allowed('https://other.example/app/current.json').hostname,'other.example');
  let releaseA,releaseB; const a=new Promise(resolve=>releaseA=resolve),b=new Promise(resolve=>releaseB=resolve);
  value.start('0000000000000001','host-a','OnConfigurationStore',()=>a);
  other.start('0000000000000001','host-b','OnConfigurationStore',()=>b);
  factory.dispose('a'.repeat(32)); releaseA('201|'); releaseB('201|');
  await new Promise(resolve=>setTimeout(resolve,0)); assert.equal(callbacks.length,1); assert.equal(callbacks[0][0],'host-b'); assert.throws(()=>factory.host('a'.repeat(32)));
  factory.dispose('b'.repeat(32));
});

test('dispose before queued storage work prevents IndexedDB opening and callbacks', async () => {
  const { value, factory, context, callbacks } = adapter(async () => response(new Uint8Array(0)));
  let opens=0;context.indexedDB={open(){opens++;throw Error('must_not_open');}};
  value.start('0000000000000001','owned-host','OnConfigurationStore',signal=>value.read('0'.repeat(64),null,1048576,signal));
  factory.dispose('a'.repeat(32));await new Promise(resolve=>setTimeout(resolve,0));
  assert.equal(opens,0);assert.equal(callbacks.length,0);await assert.rejects(value.open(),/host_disposed/);
  await assert.rejects(value.read('0'.repeat(64),null,1048576),/host_disposed/);
  await assert.rejects(value.commit('0'.repeat(64),'0',new Uint8Array(),{},null),/host_disposed/);
  assert.throws(()=>value.start('0000000000000002','owned-host','OnConfigurationStore',()=>{}),/host_disposed/);
});

test('dispose while IndexedDB opening closes late connection and never starts transaction', async () => {
  const { value, factory, context } = adapter(async () => response(new Uint8Array(0)));
  let request,closed=0,transactions=0;context.indexedDB={open(){return request={result:{close(){closed++;},transaction(){transactions++;}}};}};
  const read=value.read('0'.repeat(64),null,1048576);const rejected=assert.rejects(read,/host_disposed/);
  factory.dispose('a'.repeat(32));request.onsuccess();await rejected;await new Promise(resolve=>setTimeout(resolve,0));
  assert.equal(transactions,0);assert.ok(closed>=1);
});

test('shipping transaction rejects private owner generation regression before object writes', async () => {
  const { value }=adapter(async()=>response(new Uint8Array(0)));
  const corpus=JSON.parse(readFileSync(new URL('../../docs/implementation/remote-config/service-vectors-v1.json',import.meta.url),'utf8'));
  const next=structuredClone(corpus.examples.cache);next.storeGeneration='2';next.owner.generation='1';
  const accepted=structuredClone(next);accepted.storeGeneration='1';accepted.owner.generation='2';
  const namespace=await value.sha(new TextEncoder().encode(JSON.stringify([next.app,next.environment,next.owner.ownerId,next.owner.subjectId])));
  let writes=0;value.open=async()=>({transaction(){const tx={objectStore(){return {get(){const request={};queueMicrotask(()=>{request.result={metadata:new TextEncoder().encode(JSON.stringify(accepted)),generation:'1'};request.onsuccess();});return request;},put(){writes++;}};},abort(){queueMicrotask(()=>tx.onabort());}};return tx;}});
  await assert.rejects(value.commit(namespace,'1',new TextEncoder().encode(JSON.stringify(next)),{},null),/owner_changed/);assert.equal(writes,0);
});
