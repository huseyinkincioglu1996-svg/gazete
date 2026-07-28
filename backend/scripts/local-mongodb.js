const path = require('path');
const fs = require('fs');

const localRoot = path.resolve(__dirname, '..', '.local-mongodb');
const dataPath = path.join(localRoot, 'data');
const downloadPath = path.join(localRoot, 'binaries');

fs.mkdirSync(dataPath, { recursive: true });
fs.mkdirSync(downloadPath, { recursive: true });

process.env.MONGOMS_DOWNLOAD_DIR = downloadPath;

const { MongoMemoryServer } = require('mongodb-memory-server');

const HOST = '127.0.0.1';
const PORT = 27017;

let server;
let stopping = false;

async function stop(exitCode = 0) {
  if (stopping) return;
  stopping = true;

  if (server) {
    await server.stop();
  }

  process.exit(exitCode);
}

async function start() {
  server = await MongoMemoryServer.create({
    instance: {
      ip: HOST,
      port: PORT,
      dbPath: dataPath,
      storageEngine: 'wiredTiger',
    },
  });

  console.log(`Yerel MongoDB hazır: ${server.getUri()}`);
  console.log(`Kalıcı veri dizini: ${dataPath}`);
}

process.on('SIGINT', () => {
  stop(0).catch((error) => {
    console.error('Yerel MongoDB kapatılamadı:', error);
    process.exit(1);
  });
});

process.on('SIGTERM', () => {
  stop(0).catch((error) => {
    console.error('Yerel MongoDB kapatılamadı:', error);
    process.exit(1);
  });
});

start().catch((error) => {
  console.error('Yerel MongoDB başlatılamadı:', error);
  process.exit(1);
});
