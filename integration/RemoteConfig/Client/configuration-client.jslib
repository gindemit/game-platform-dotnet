mergeInto(LibraryManager.library, {
  $GPConfigurationClient: {
    hosts: {},
    create: function (handle, normal, control, loopback) {
      if (!/^[0-9a-f]{32}$/.test(handle) || this.hosts[handle]) throw Error('host_handle_invalid');
      var instance = Object.create(this); instance.pending = {}; instance.database = null; instance.roots = null; instance.disposed = false; instance.initialize(normal, control, loopback); this.hosts[handle] = instance; return instance;
    },
    host: function (handle) { if (!Object.prototype.hasOwnProperty.call(this.hosts, handle)) throw Error('host_unavailable'); return this.hosts[handle]; },
    dispose: function (handle) {
      var instance = this.hosts[handle]; if (!instance) return; instance.disposed = true; delete this.hosts[handle];
      Object.keys(instance.pending).forEach(function (key) { instance.cancel(key); });
      if (instance.database) instance.database.then(function (db) { db.close(); }).catch(function () {}); instance.database = null; instance.roots = null;
    },
    roots: null,
    pending: {},
    database: null,
    check: function (signal) { if (this.disposed) throw Error('host_disposed'); if (signal && signal.aborted) throw Error('storage_abort'); },
    initialize: function (normal, control, loopback) {
      function root(text) {
        var url = new URL(text);
        if (text.length > 4096 || url.username || url.password || url.search || url.hash || !url.pathname.endsWith('/') || url.pathname.indexOf('%') >= 0 || url.pathname.indexOf('..') >= 0 || (url.protocol !== 'https:' && !(loopback && url.protocol === 'http:' && /^(localhost|127\.0\.0\.1|\[::1\])$/.test(url.hostname)))) throw Error('origin_invalid');
        return url;
      }
      this.roots = [root(normal), root(control)];
    },
    allowed: function (text) {
      if (!this.roots) throw Error('not_initialized');
      var url = new URL(text);
      if (text.length > 4096 || url.username || url.password || url.hash || url.search) throw Error('url_invalid');
      for (var i = 0; i < this.roots.length; i++) {
        var root = this.roots[i];
        if (url.origin === root.origin && url.pathname.indexOf(root.pathname) === 0) {
          var path = url.pathname.substring(root.pathname.length);
          if ((i === 0 && (path === 'current.json' || /^(assets|selectors|manifests)\/[0-9a-f]{64}\.json$/.test(path))) || (i === 1 && path === 'control.json')) return url;
        }
      }
      throw Error('url_scope');
    },
    encode: function (bytes) {
      var binary = ''; for (var i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 8192)); return btoa(binary);
    },
    decode: function (text, maximum) {
      if (text.length > Math.ceil(maximum / 3) * 4 || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(text)) throw Error('decoded_bounds');
      var binary = atob(text); if (binary.length > maximum) throw Error('decoded_bounds'); var bytes = new Uint8Array(binary.length); for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i); return bytes;
    },
    sha: async function (bytes) {
      var result = new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)); var text = ''; for (var i = 0; i < result.length; i++) text += result[i].toString(16).padStart(2, '0'); return text;
    },
    fetch: async function (address, maximum, immutable, signal, expectedValidator) {
      this.check(signal);
      var url = this.allowed(address);
      if (!Number.isInteger(maximum) || maximum < 1 || maximum > 1048576) throw Error('decoded_bounds');
      // Browser HTTP cache performs revalidation without custom If-None-Match/preflight.
      var response = await fetch(url.href, { credentials: 'omit', redirect: 'error', mode: 'cors', cache: immutable ? 'force-cache' : 'no-cache', signal: signal });
      if (response.url !== url.href) throw Error('redirect_invalid');
      var etag = response.headers.get('ETag'); if (etag && (etag.length > 4096 || /[\r\n]/.test(etag))) throw Error('validator_invalid');
      if (expectedValidator && (expectedValidator.length > 4096 || /[\r\n]/.test(expectedValidator))) throw Error('validator_invalid');
      if (response.status === 304 && (!expectedValidator || (etag && etag !== expectedValidator))) throw Error('validator_invalid');
      var retry = response.headers.get('Retry-After'); retry = retry && /^[0-9]{1,6}$/.test(retry) ? Math.min(900, Number(retry)) : null;
      var mime = (response.headers.get('Content-Type') || '').split(';')[0].trim().toLowerCase();
      if (response.status !== 200) { if (response.body) await response.body.cancel(); return { status: response.status, bytes: new Uint8Array(0), etag: etag, retry: retry, mime: mime }; }
      if (mime !== 'application/json') { if (response.body) await response.body.cancel(); throw Error('mime_invalid'); }
      if (!response.body || typeof response.body.getReader !== 'function') throw Error('stream_unavailable');
      var reader = response.body.getReader(), chunks = [], count = 0;
      try {
        while (true) { var piece = await reader.read(); if (piece.done) break; count += piece.value.byteLength; if (count > maximum) throw Error('decoded_bounds'); chunks.push(piece.value); }
      } catch (error) { await reader.cancel(); throw error; }
      finally { reader.releaseLock(); }
      var bytes = new Uint8Array(count), offset = 0; for (var i = 0; i < chunks.length; i++) { bytes.set(chunks[i], offset); offset += chunks[i].length; }
      return { status: response.status, bytes: bytes, etag: etag, retry: retry, mime: mime };
    },
    open: function () {
      if (this.disposed) return Promise.reject(Error('host_disposed'));
      if (this.database) return this.database;
      var self = this;
      this.database = new Promise(function (resolve, reject) {
        if (typeof indexedDB === 'undefined') { reject(Error('storage_denied')); return; }
        var request, failed = false;
        try { request = indexedDB.open('game-platform.configuration.client.v1', 1); } catch (error) { reject(error); return; }
        request.onupgradeneeded = function () { if (self.disposed) { request.transaction.abort(); return; } var db = request.result; db.createObjectStore('indexes'); db.createObjectStore('backups'); db.createObjectStore('objects'); };
        request.onerror = function () { failed = true; reject(request.error || Error('storage_denied')); };
        request.onblocked = function () { failed = true; reject(Error('storage_blocked')); };
        request.onsuccess = function () { var db = request.result; if (failed || self.disposed) { db.close(); reject(Error('host_disposed')); return; } db.onversionchange = function () { db.close(); self.database = null; }; resolve(db); };
      });
      return this.database;
    },
    namespace: async function (metadata, expected) {
      if (metadata.byteLength < 1 || metadata.byteLength > 1048576 || !/^[0-9a-f]{64}$/.test(expected)) throw Error('index_bounds');
      var value = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(metadata));
      var fields = ['schema','app','environment','storeGeneration','owner','normal','control','objects','activations','pins'];
      if (!value || Object.keys(value).length !== fields.length || fields.some(function (field) { return !Object.prototype.hasOwnProperty.call(value, field); }) || value.schema !== 'platform.remote-config.cache-commit.v1' || typeof value.app !== 'string' || typeof value.environment !== 'string' || !/^(0|[1-9][0-9]{0,18})$/.test(value.storeGeneration) || BigInt(value.storeGeneration) > 9223372036854775807n || !Array.isArray(value.objects) || value.objects.length > 4096 || !Array.isArray(value.activations) || !Array.isArray(value.pins)) throw Error('index_invalid');
      if (value.owner === null && (value.activations.length || value.pins.length)) throw Error('owner_changed');
      if (value.owner !== null && (!value.owner || Object.keys(value.owner).sort().join(',') !== 'generation,ownerId,subjectId' || typeof value.owner.ownerId !== 'string' || typeof value.owner.subjectId !== 'string')) throw Error('owner_changed');
      if (value.owner !== null && (typeof value.owner.generation !== 'string' || !/^(0|[1-9][0-9]{0,18})$/.test(value.owner.generation) || BigInt(value.owner.generation) > 9223372036854775807n)) throw Error('owner_changed');
      var tuple = [value.app, value.environment, value.owner === null ? null : value.owner.ownerId, value.owner === null ? null : value.owner.subjectId];
      if (await this.sha(new TextEncoder().encode(JSON.stringify(tuple))) !== expected) throw Error('owner_changed');
      var seen = {};
      value.objects.forEach(function (item) { if (!item || Object.keys(item).sort().join(',') !== 'decodedBytes,lastUsedAtSeconds,representationSha' || !/^[0-9a-f]{64}$/.test(item.representationSha) || seen[item.representationSha] || !Number.isInteger(item.decodedBytes) || item.decodedBytes < 1 || item.decodedBytes > 1048576) throw Error('object_invalid'); seen[item.representationSha] = item; });
      if (value.normal && !seen[value.normal.pointerRepresentationSha]) throw Error('pointer_raw_missing');
      if (value.control && (!seen[value.control.representationSha] || seen[value.control.representationSha].decodedBytes > 16384)) throw Error('control_raw_missing');
      return value;
    },
    read: async function (space, sha, maximum, signal) {
      this.check(signal);
      if (!/^[0-9a-f]{64}$/.test(space) || (sha !== null && !/^[0-9a-f]{64}$/.test(sha)) || !Number.isInteger(maximum) || maximum < 1 || maximum > 1048576) throw Error('read_invalid');
      var db = await this.open(), value; this.check(signal);
      await new Promise(function (resolve, reject) { var tx = db.transaction(sha === null ? ['indexes'] : ['objects'], 'readonly'); var request = tx.objectStore(sha === null ? 'indexes' : 'objects').get(sha === null ? space : space + '/' + sha); request.onsuccess = function () { value = request.result; }; tx.oncomplete = resolve; tx.onerror = function () { reject(tx.error); }; tx.onabort = function () { reject(tx.error || Error('storage_abort')); }; });
      if (!value) return null; var bytes = sha === null ? value.metadata : value;
      if (!(bytes instanceof Uint8Array) || bytes.byteLength > maximum) throw Error('stored_bounds'); if (sha !== null && await this.sha(bytes) !== sha) throw Error('stored_integrity'); if (sha === null) { var index = await this.namespace(bytes, space); if (value.generation !== index.storeGeneration) throw Error('store_generation'); } return bytes;
    },
    commit: async function (space, expectedGeneration, metadata, objects, signal, fault) {
      this.check(signal);
      if (!/^(0|[1-9][0-9]{0,18})$/.test(expectedGeneration) || BigInt(expectedGeneration) >= 9223372036854775807n) throw Error('sequence_invalid');
      var index = await this.namespace(metadata, space); if (BigInt(index.storeGeneration) !== BigInt(expectedGeneration) + 1n) throw Error('store_generation');
      var descriptors = {}; index.objects.forEach(function (item) { descriptors[item.representationSha] = item; });
      var keys = Object.keys(objects), total = 0; if (keys.length > 72) throw Error('added_bounds');
      for (var i = 0; i < keys.length; i++) { var key = keys[i], bytes = objects[key]; if (!descriptors[key] || !(bytes instanceof Uint8Array) || bytes.byteLength !== descriptors[key].decodedBytes || await this.sha(bytes) !== key) throw Error('object_invalid'); total += bytes.byteLength; }
      if (total > 12582912) throw Error('added_bounds'); if (signal && signal.aborted) throw Error('storage_abort');
      this.check(signal); var db = await this.open(); this.check(signal);
      return await new Promise(function (resolve, reject) {
        var tx; try { tx = db.transaction(['indexes', 'backups', 'objects'], 'readwrite', { durability: 'strict' }); } catch (error) { reject(error); return; }
        var outcome = 'committed', failure = null;
        function abort(reason) { failure = Error(reason); try { tx.abort(); } catch (_) { } }
        function cancelled() { abort('storage_abort'); }
        if (signal) signal.addEventListener('abort', cancelled, { once: true });
        tx.oncomplete = function () { if (signal) signal.removeEventListener('abort', cancelled); resolve(outcome); };
        tx.onabort = function () { if (signal) signal.removeEventListener('abort', cancelled); reject(failure || tx.error || Error('storage_abort')); };
        tx.onerror = function () { if (!failure) failure = tx.error; };
        var request = tx.objectStore('indexes').get(space);
        request.onsuccess = function () {
          var prior = request.result;
          if ((prior ? prior.generation : '0') !== expectedGeneration) { outcome = 'conflict'; return; }
          if (prior && index.owner !== null) {
            try { var accepted = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(prior.metadata)); if (!accepted.owner || accepted.app !== index.app || accepted.environment !== index.environment || accepted.owner.ownerId !== index.owner.ownerId || accepted.owner.subjectId !== index.owner.subjectId || typeof accepted.owner.generation !== 'string' || !/^(0|[1-9][0-9]{0,18})$/.test(accepted.owner.generation) || BigInt(accepted.owner.generation) > BigInt(index.owner.generation)) { abort('owner_changed'); return; } }
            catch (_) { abort('index_invalid'); return; }
          }
          if (fault === 'before_objects') { abort('injected_abort'); return; }
          var waiting = index.objects.length, seen = {};
          function ready() {
            if (--waiting > 0 || failure) return;
            if (fault === 'before_commit') { abort('injected_abort'); return; }
            if (fault === 'quota') { failure = new DOMException('injected_qualification_quota', 'QuotaExceededError'); tx.abort(); return; }
            if (prior) tx.objectStore('backups').put(prior, space);
            tx.objectStore('indexes').put({ metadata: metadata, generation: index.storeGeneration }, space);
            // Per-namespace object keys preserve retired owners; protect current+backup indexes.
            var protectedKeys = {}; index.objects.forEach(function (item) { protectedKeys[space + '/' + item.representationSha] = true; });
            if (prior) { var oldIndex = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(prior.metadata)); oldIndex.objects.forEach(function (item) { protectedKeys[space + '/' + item.representationSha] = true; }); }
            var cursor = tx.objectStore('objects').openCursor(); cursor.onsuccess = function () { var item = cursor.result; if (!item) return; if (typeof item.key === 'string' && item.key.indexOf(space + '/') === 0 && !protectedKeys[item.key]) item.delete(); item.continue(); };
          }
          if (waiting === 0) { waiting = 1; ready(); return; }
          index.objects.forEach(function (descriptor) {
            var key = descriptor.representationSha, objectKey = space + '/' + key, lookup = tx.objectStore('objects').get(objectKey);
            lookup.onsuccess = function () {
              var existing = lookup.result, added = objects[key];
              if (existing && (!(existing instanceof Uint8Array) || existing.byteLength !== descriptor.decodedBytes)) { abort('immutable_replacement'); return; }
              if (existing && added) { for (var j = 0; j < existing.length; j++) if (existing[j] !== added[j]) { abort('immutable_replacement'); return; } }
              if (!existing && !added) { abort('object_missing'); return; }
              if (added && !existing) tx.objectStore('objects').put(added, objectKey);
              seen[key] = true; ready();
            };
          });
        };
      });
    },
    start: function (id, target, method, execute) {
      if (this.disposed) throw Error('host_disposed');
      if (!/^[0-9a-f]{16}$/.test(id) || !target || target.length > 128 || this.pending[id]) return;
      var self = this, state = { controller: new AbortController(), timer: null }; this.pending[id] = state;
      state.timer = setTimeout(function () { if (self.pending[id] !== state) return; delete self.pending[id]; state.controller.abort(); SendMessage(target, method, id + '|' + (method === 'OnConfigurationHttp' ? '-5||||' : '202|')); }, 5000);
      Promise.resolve().then(function () { self.check(state.controller.signal); if (self.pending[id] !== state) throw Error('storage_abort'); return execute(state.controller.signal); }).then(function (payload) { if (self.pending[id] === state) SendMessage(target, method, id + '|' + payload); }).catch(function (error) { if (self.pending[id] === state) SendMessage(target, method, id + '|' + (error && error.name === 'QuotaExceededError' ? '507' : '500') + '|'); }).finally(function () { clearTimeout(state.timer); if (self.pending[id] === state) delete self.pending[id]; });
    },
    cancel: function (id) { var state = this.pending[id]; if (state) { delete this.pending[id]; clearTimeout(state.timer); state.controller.abort(); } }
  },
  GPConfigInitialize__deps: ['$GPConfigurationClient'],
  GPConfigInitialize: function (handle, normal, control, loopback) { GPConfigurationClient.create(UTF8ToString(handle), UTF8ToString(normal), UTF8ToString(control), !!loopback); },
  GPConfigDispose__deps: ['$GPConfigurationClient'],
  GPConfigDispose: function (handle) { GPConfigurationClient.dispose(UTF8ToString(handle)); },
  GPConfigFetch__deps: ['$GPConfigurationClient'],
  GPConfigFetch: function (handle, address, id, target, maximum, immutable, validator) {
    var api = GPConfigurationClient.host(UTF8ToString(handle)), url = UTF8ToString(address), key = UTF8ToString(id), receiver = UTF8ToString(target), etag = UTF8ToString(validator);
    api.start(key, receiver, 'OnConfigurationHttp', async function (signal) { var result = await api.fetch(url, maximum, !!immutable, signal, etag); return result.status + '|' + api.encode(new TextEncoder().encode(result.etag || '')) + '|' + (result.retry === null ? '' : result.retry) + '|' + api.encode(new TextEncoder().encode(result.mime)) + '|' + api.encode(result.bytes); });
  },
  GPConfigStoreRead__deps: ['$GPConfigurationClient'],
  GPConfigStoreRead: function (handle, space, sha, id, target, maximum) {
    var api = GPConfigurationClient.host(UTF8ToString(handle)), namespace = UTF8ToString(space), digest = UTF8ToString(sha), key = UTF8ToString(id), receiver = UTF8ToString(target);
    api.start(key, receiver, 'OnConfigurationStore', async function (signal) { var bytes = await api.read(namespace, digest || null, maximum, signal); return bytes === null ? '204|' : '200|' + api.encode(bytes); });
  },
  GPConfigStoreCommit__deps: ['$GPConfigurationClient'],
  GPConfigStoreCommit: function (handle, space, generation, packed, id, target) {
    var api = GPConfigurationClient.host(UTF8ToString(handle)), namespace = UTF8ToString(space), expected = UTF8ToString(generation), data = UTF8ToString(packed), key = UTF8ToString(id), receiver = UTF8ToString(target);
    api.start(key, receiver, 'OnConfigurationStore', async function (signal) {
      if (data.length > 18000000) throw Error('added_bounds'); var parts = data.split('~'); if (parts.length > 73) throw Error('added_bounds'); var metadata = api.decode(parts.shift(), 1048576), objects = {};
      parts.forEach(function (part) { var colon = part.indexOf(':'); var sha = part.substring(0, colon); if (!/^[0-9a-f]{64}$/.test(sha) || objects[sha]) throw Error('object_invalid'); objects[sha] = api.decode(part.substring(colon + 1), 1048576); });
      var outcome = await api.commit(namespace, expected, metadata, objects, signal); return outcome === 'committed' ? '201|' : '409|';
    });
  },
  GPConfigCancel__deps: ['$GPConfigurationClient'],
  GPConfigCancel: function (handle, id) { GPConfigurationClient.host(UTF8ToString(handle)).cancel(UTF8ToString(id)); }
});
