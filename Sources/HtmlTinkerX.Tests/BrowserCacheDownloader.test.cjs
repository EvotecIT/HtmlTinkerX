'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const { once } = require('node:events');
const modulePath = process.env.HTMLTINKERX_TEST_DOWNLOADER;
const { download, verifyArchive } = require(modulePath);
const packageRoot = process.env.HTMLTINKERX_TEST_PACKAGE;
require(path.join(packageRoot, 'lib', 'bootstrap'));
const utils = require(path.join(packageRoot, 'lib', 'coreBundle')).utils;
const yauzl = require(path.join(packageRoot, 'lib', 'utilsBundle')).yauzl;
const root = path.dirname(modulePath);
const payload = Buffer.alloc(1024 * 512, 0x61);

async function serve(handler, action) {
    const server = http.createServer(handler);
    server.listen(0, '127.0.0.1');
    await once(server, 'listening');
    try { await action(`http://127.0.0.1:${server.address().port}`); }
    finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
}

test('interrupted transfer resumes with Range and validates bytes', async () => {
    let count = 0;
    let range;
    const file = path.join(root, 'interrupted.partial');
    await serve((request, response) => {
        count++;
        if (count === 1) {
            response.writeHead(200, { 'content-length': payload.length, etag: '"immutable"' });
            response.write(payload.subarray(0, 65536));
            // Abort only once bytes reached the destination, rather than racing the file stream's open.
            fs.watchFile(file, { interval: 10 }, stat => {
                if (stat.size > 0) { fs.unwatchFile(file); response.destroy(); }
            });
        } else {
            range = Number(request.headers.range?.match(/\d+/)?.[0]);
            assert.ok(range > 0);
            assert.equal(request.headers['if-range'], '"immutable"');
            response.writeHead(206, { 'content-length': payload.length - range, 'content-range': `bytes ${range}-${payload.length - 1}/${payload.length}`, etag: '"immutable"' });
            response.end(payload.subarray(range));
        }
    }, async url => {
        const sha = await download([url], file, utils, { backoff: 0 });
        assert.equal(sha, crypto.createHash('sha256').update(payload).digest('hex'));
        assert.deepEqual(fs.readFileSync(file), payload);
        assert.equal(count, 2);
    });
});

test('killed downloader resumes its persisted partial in another process', async () => {
    const file = path.join(root, 'killed.partial');
    let resumed = false;
    await serve((request, response) => {
        const offset = Number(request.headers.range?.match(/\d+/)?.[0] || 0);
        if (offset) {
            resumed = true;
            response.writeHead(206, { 'content-length': payload.length - offset, 'content-range': `bytes ${offset}-${payload.length - 1}/${payload.length}`, etag: '"immutable"' });
            response.end(payload.subarray(offset));
        } else {
            response.writeHead(200, { 'content-length': payload.length, etag: '"immutable"' });
            response.write(payload.subarray(0, 65536));
        }
    }, async url => {
        const child = spawn(process.execPath, ['-e', `require(${JSON.stringify(modulePath)}).download([${JSON.stringify(url)}], ${JSON.stringify(file)}, require(${JSON.stringify(path.join(packageRoot, 'lib', 'coreBundle'))}).utils).catch(()=>process.exit(2))`], { stdio: 'ignore' });
        try {
            await new Promise((resolve, reject) => {
                const timeout = setTimeout(() => { fs.unwatchFile(file); reject(new Error('partial transfer not written')); }, 10000);
                fs.watchFile(file, { interval: 20 }, stat => {
                    if (stat.size > 0) { clearTimeout(timeout); fs.unwatchFile(file); resolve(); }
                });
            });
        } finally { child.kill(); await once(child, 'exit'); }
        await download([url], file, utils, { backoff: 0 });
        assert.ok(resumed);
        assert.deepEqual(fs.readFileSync(file), payload);
    });
});

test('official fallback URL is tried after host failures', async () => {
    let mirrors = 0;
    await serve((request, response) => {
        if (request.url === '/bad') { response.writeHead(503); response.end(); }
        else { mirrors++; response.writeHead(200, { 'content-length': payload.length }); response.end(payload); }
    }, async url => {
        const file = path.join(root, 'mirror.partial');
        await download([url + '/bad', url + '/mirror'], file, utils, { backoff: 0 });
        assert.equal(mirrors, 1);
        assert.deepEqual(fs.readFileSync(file), payload);
    });
});

test('digest mismatch is rejected before extraction', async () => {
    await serve((request, response) => {
        response.writeHead(200, { 'content-length': payload.length, digest: 'sha-256=' + Buffer.alloc(32).toString('base64') });
        response.end(payload);
    }, async url => {
        const file = path.join(root, 'checksum.partial');
        await assert.rejects(download([url], file, utils, { attempts: 2, backoff: 0 }), /checksum mismatch/);
        assert.equal(fs.existsSync(file), false);
    });
});

test('corrupt archive checksum is rejected before extraction', async () => {
    const valid = process.env.HTMLTINKERX_TEST_ZIP;
    await verifyArchive(valid, yauzl);
    const corrupt = path.join(root, 'corrupt.zip');
    const bytes = fs.readFileSync(valid);
    const centralDirectory = bytes.indexOf(Buffer.from([0x50, 0x4b, 0x01, 0x02]));
    assert.ok(centralDirectory >= 0);
    bytes[centralDirectory + 16] ^= 1;
    fs.writeFileSync(corrupt, bytes);
    await assert.rejects(verifyArchive(corrupt, yauzl), /CRC mismatch/);
});
