// Keep the installer socket alive through Node's IPv6-to-IPv4 connection fallback.
// Playwright supplies its own request timeout; this default must not expire first.
const configuredTimeout = Number(process.env.PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT);
const timeout = Number.isFinite(configuredTimeout) && configuredTimeout > 0
    ? configuredTimeout
    : 30000;

for (const protocol of ['node:http', 'node:https']) {
    const agent = require(protocol).globalAgent;
    agent.options.timeout = Math.max(Number(agent.options.timeout) || 0, timeout);
}
