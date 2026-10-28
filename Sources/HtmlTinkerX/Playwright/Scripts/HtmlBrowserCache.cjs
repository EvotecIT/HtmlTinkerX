'use strict';

// The loaded Playwright package remains the authority for platform layouts, URLs, proxies and extraction.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { pipeline } = require('node:stream/promises');
const { Transform } = require('node:stream');

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const hash = file => new Promise((resolve, reject) => {
    const digest = crypto.createHash('sha256');
    fs.createReadStream(file).on('error', reject).on('data', chunk => digest.update(chunk))
        .on('end', () => resolve(digest.digest('hex')));
});

// Resume only the same immutable URL and validator. A mirror or ignored Range starts a new file.
async function download(urls, destination, utils, options = {}) {
    const timeout = options.timeout || Math.max(180000, Number(process.env.PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT) || 0);
    const attempts = options.attempts || 6;
    for (const protocol of ['node:http', 'node:https']) {
        const agent = require(protocol).globalAgent;
        agent.options.timeout = Math.max(Number(agent.options.timeout) || 0, timeout);
    }
    const maximum = options.maximum || 2 * 1024 * 1024 * 1024;
    const metadataFile = destination + '.resume.json';
    let lastError;
    for (let attempt = 0; attempt < attempts; attempt++) {
        // Retry once on the same host before rotating to the registry's official fallback hosts.
        const url = urls[Math.floor(attempt / 2) % urls.length];
        let metadata = {};
        try { metadata = JSON.parse(fs.readFileSync(metadataFile, 'utf8')); } catch { }
        let offset = fs.existsSync(destination) && metadata.url === url && metadata.validator ? fs.statSync(destination).size : 0;
        try {
            await new Promise((resolve, reject) => {
                let deadline;
                const finish = callback => value => { clearTimeout(deadline); callback(value); };
                const fail = finish(reject);
                let responseStarted = false;
                const headers = { 'accept-encoding': 'identity' };
                if (offset) { headers.Range = `bytes=${offset}-`; headers['If-Range'] = metadata.validator; }
                const request = utils.httpRequest({ url, headers, socketTimeout: timeout }, async response => {
                    responseStarted = true;
                    try {
                        const code = response.statusCode;
                        if (code !== 200 && code !== 206) {
                            response.resume();
                            throw new Error(`Download HTTP ${code}`);
                        }
                        const range = /^bytes (\d+)-(\d+)\/(\d+)$/.exec(response.headers['content-range'] || '');
                        if (code === 206 && (!range || Number(range[1]) !== offset)) {
                            response.destroy();
                            throw new Error('Download returned an inconsistent Content-Range');
                        }
                        if (code === 200) offset = 0;
                        const total = code === 206 ? Number(range[3]) : Number(response.headers['content-length']);
                        if (!Number.isSafeInteger(total) || total <= offset || total > maximum) {
                            response.destroy();
                            throw new Error('Download has no valid bounded Content-Length');
                        }
                        const validator = response.headers.etag || response.headers['last-modified'];
                        fs.writeFileSync(metadataFile, JSON.stringify({ url, validator, total }));
                        let received = offset;
                        const limit = new Transform({ transform(chunk, encoding, callback) {
                            received += chunk.length;
                            callback(received > total || received > maximum ? new Error('Download exceeds declared size') : null, chunk);
                        }});
                        await pipeline(response, limit, fs.createWriteStream(destination, { flags: offset ? 'a' : 'w' }));
                        if (received !== total) throw new Error(`Interrupted download (${received}/${total} bytes)`);
                        const digest = response.headers['content-digest'] || response.headers.digest;
                        const expected = /sha-256[=:]+:?([A-Za-z0-9+/=]+):?/i.exec(digest || '');
                        if (expected && Buffer.from(await hash(destination), 'hex').toString('base64') !== expected[1])
                            throw new Error('Download SHA-256 checksum mismatch');
                        finish(resolve)();
                    } catch (error) { fail(error); }
                }, error => { if (!responseStarted) fail(error); });
                // Connection and read inactivity are bounded by Playwright's proxy-aware socket timeout.
                // This total deadline also bounds a server that continuously dribbles bytes.
                deadline = setTimeout(() => request.cancel(new Error('Browser download deadline exceeded')), timeout * 10);
                deadline.unref();
            });
            fs.rmSync(metadataFile, { force: true });
            return await hash(destination);
        } catch (error) {
            lastError = error;
            if (/checksum|Content-Range|HTTP 416/.test(error.message)) {
                fs.rmSync(destination, { force: true });
                fs.rmSync(metadataFile, { force: true });
            }
            process.stderr.write(`Browser download attempt ${attempt + 1}/${attempts}: ${error.message}\n`);
            if (attempt + 1 < attempts) await sleep(options.backoff === undefined ? Math.min(30000, 1000 * 2 ** attempt) : options.backoff);
        }
    }
    throw lastError;
}

// ZIP central-directory CRCs validate every decompressed entry, including valid ZIPs with corrupt payloads.
async function verifyArchive(archive, yauzl) {
    const table = Array.from({ length: 256 }, (_, value) => {
        for (let bit = 0; bit < 8; bit++) value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
        return value >>> 0;
    });
    await new Promise((resolve, reject) => yauzl.open(archive, { lazyEntries: true }, (error, zip) => {
        if (error) return reject(error);
        const fail = error => { zip.close(); reject(error); };
        zip.on('error', fail).on('end', resolve).on('entry', entry => {
            if (entry.fileName.endsWith('/')) return zip.readEntry();
            zip.openReadStream(entry, (error, stream) => {
                if (error) return fail(error);
                let crc = 0xffffffff;
                stream.on('error', fail).on('data', chunk => {
                    for (const value of chunk) crc = table[(crc ^ value) & 0xff] ^ (crc >>> 8);
                }).on('end', () => {
                    if (((crc ^ 0xffffffff) >>> 0) !== entry.crc32) return fail(new Error(`Archive CRC mismatch: ${entry.fileName}`));
                    zip.readEntry();
                });
            });
        });
        zip.readEntry();
    }));
}

async function run(packageRoot, root, engine, mode, downloadRoot) {
    process.env.PLAYWRIGHT_BROWSERS_PATH = root;
    process.env.PLAYWRIGHT_SKIP_BROWSER_GC = '1';
    require(path.join(packageRoot, 'lib', 'bootstrap.js'));
    const core = require(path.join(packageRoot, 'lib', 'coreBundle.js'));
    if (mode === 'install') {
        const names = engine === 'chromium' ? ['chromium', 'chromium-headless-shell', 'ffmpeg'] : [engine, 'ffmpeg'];
        if (process.platform === 'win32') names.push('winldd');
        const runtimes = [];
        for (const name of names) {
            const executable = core.registry.registry.findExecutable(name);
            if (!executable?.downloadURLs?.length) throw new Error(`Playwright has no official download plan for ${name}`);
            fs.mkdirSync(downloadRoot, { recursive: true });
            const archive = path.join(downloadRoot, `${name}-${executable.revision}.zip.partial`);
            const checksum = archive + '.sha256';
            let sha256;
            if (fs.existsSync(archive) && fs.existsSync(checksum) && await hash(archive) === fs.readFileSync(checksum, 'utf8'))
                sha256 = fs.readFileSync(checksum, 'utf8');
            else {
                sha256 = await download(executable.downloadURLs, archive, core.utils);
                fs.writeFileSync(checksum, sha256);
            }
            try { await verifyArchive(archive, require(path.join(packageRoot, 'lib', 'utilsBundle.js')).yauzl); }
            catch (error) { fs.rmSync(archive, { force: true }); fs.rmSync(checksum, { force: true }); throw error; }
            await core.utils.extractZip(archive, { dir: executable.directory });
            const binary = executable.executablePath();
            if (binary) {
                if (!fs.existsSync(binary) || !fs.statSync(binary).size) throw new Error(`Missing launch binary ${binary}`);
                if (process.platform !== 'win32') fs.chmodSync(binary, 0o755);
            }
            fs.writeFileSync(path.join(executable.directory, 'INSTALLATION_COMPLETE'), '');
            fs.writeFileSync(path.join(executable.directory, 'archive-sha256.txt'), sha256);
            fs.rmSync(archive);
            fs.rmSync(checksum, { force: true });
            runtimes.push({
                Name: name, Directory: path.relative(root, executable.directory), Executable: path.relative(root, binary)
            });
        }
        fs.writeFileSync(path.join(root, 'runtime-plan.json'), JSON.stringify(runtimes));
    } else if (mode === 'smoke') {
        // Test each launch binary, including full Chromium and its separately shipped headless shell.
        const manifest = JSON.parse(fs.readFileSync(path.join(root, 'runtime-plan.json'), 'utf8'));
        const playwright = require(packageRoot);
        for (const runtime of manifest) {
            if (runtime.Name !== engine && runtime.Name !== 'chromium-headless-shell') continue;
            const browser = await playwright[engine].launch({ executablePath: path.join(root, runtime.Executable), headless: true, timeout: 30000 });
            try { const page = await browser.newPage(); await page.goto('about:blank'); }
            finally { await browser.close(); }
        }
    } else throw new Error(`Unknown cache command ${mode}`);
}

module.exports = { download, verifyArchive, run };
if (require.main === module) {
    const start = process.env.HTMLTINKERX_PARENT_PIPE === '1' ? new Promise(resolve => {
        process.stdin.once('data', resolve);
        process.stdin.on('end', () => process.exit(3));
        process.stdin.resume();
    }) : Promise.resolve();
    start.then(() => run(...process.argv.slice(2))).catch(error => {
        console.error(error.message); process.exitCode = 2;
    }).finally(() => process.stdin.destroy());
}
