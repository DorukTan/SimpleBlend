const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const directory = path.resolve(__dirname, '../docs/prototypes');
const files = {
  '/': ['workspace.html', 'text/html; charset=utf-8'],
  '/workspace.html': ['workspace.html', 'text/html; charset=utf-8'],
  '/workspace.css': ['workspace.css', 'text/css; charset=utf-8'],
  '/workspace.js': ['workspace.js', 'text/javascript; charset=utf-8']
};
const server = http.createServer((request, response) => {
  const entry = files[new URL(request.url, 'http://127.0.0.1').pathname];
  if (!entry || !['GET', 'HEAD'].includes(request.method)) {
    response.writeHead(404); response.end('Not found'); return;
  }
  fs.readFile(path.join(directory, entry[0]), (error, data) => {
    if (error) { response.writeHead(500); response.end('Prototype unavailable'); return; }
    response.writeHead(200, {
      'Content-Type': entry[1], 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff',
      'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'none'; frame-ancestors 'none'"
    });
    response.end(request.method === 'HEAD' ? undefined : data);
  });
});
server.listen(0, '127.0.0.1', () => console.log(`Workspace prototype: http://127.0.0.1:${server.address().port}`));
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
