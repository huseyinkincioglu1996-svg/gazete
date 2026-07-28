require('dotenv').config();

const express = require('express');
const mongoose = require('mongoose');
const cors = require('cors');
const cron = require('node-cron');
const dns = require('node:dns');
const { HttpError, isDuplicateKeyError } = require('./utils/http');

mongoose.set('strictQuery', true);
mongoose.set('bufferCommands', false);

const app = express();
app.disable('x-powered-by');

function parseAllowedOrigins(value) {
  return String(value || '')
    .split(',')
    .map((origin) => origin.trim())
    .filter((origin) => origin && origin !== '*');
}

const developmentOrigins = [
  'http://localhost:3000',
  'http://127.0.0.1:3000',
  'http://localhost:5173',
  'http://127.0.0.1:5173'
];
const configuredOrigins = parseAllowedOrigins(process.env.CORS_ORIGINS || process.env.CORS_ORIGIN);
const allowedOrigins = configuredOrigins.length > 0 ? configuredOrigins : developmentOrigins;
app.locals.allowedOrigins = allowedOrigins;

app.use(cors({
  origin(origin, callback) {
    // Non-browser callers (health checks, server-to-server, CRA's proxy) do
    // not send Origin. Browser requests must come from the allowlist.
    if (!origin || allowedOrigins.includes(origin)) {
      return callback(null, true);
    }
    return callback(new HttpError(403, 'Bu kaynağın API erişimine izin verilmiyor'));
  },
  methods: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS'],
  allowedHeaders: ['Content-Type'],
  optionsSuccessStatus: 204
}));
app.use(express.json({ limit: process.env.JSON_BODY_LIMIT || '3mb' }));

app.use('/api/company-settings', require('./routes/companySettings'));
app.use('/api/distributors', require('./routes/distributors'));
app.use('/api/deliveries', require('./routes/deliveries'));
app.use('/api/payments', require('./routes/payments'));
app.use('/api/reports', require('./routes/reports'));
app.use('/api/cash-handovers', require('./routes/cashHandovers'));
app.use('/api/subscribers', require('./routes/subscribers'));
app.use('/api/subscriber-deliveries', require('./routes/subscriberDeliveries'));
app.use('/api/payment-periods', require('./routes/paymentPeriods'));

app.get('/api/test', (req, res) => {
  res.json({ mesaj: 'Server çalışıyor!' });
});

app.get('/api/health', (req, res) => {
  const states = ['disconnected', 'connected', 'connecting', 'disconnecting'];
  const readyState = mongoose.connection.readyState;
  const connected = readyState === 1;
  res.status(connected ? 200 : 503).json({
    durum: connected ? 'hazır' : 'veritabanı_bekleniyor',
    database: states[readyState] || 'bilinmiyor'
  });
});

app.use((req, res, next) => {
  next(new HttpError(404, 'İstenen API yolu bulunamadı'));
});

app.use((error, req, res, next) => { // eslint-disable-line no-unused-vars
  let statusCode = error.statusCode || 500;
  let message = error.message || 'Beklenmeyen bir sunucu hatası oluştu';
  let details;

  if (isDuplicateKeyError(error)) {
    statusCode = 409;
    message = 'Bu kayıt veya ödeme dönemi zaten mevcut';
  } else if (error.name === 'ValidationError' || error.name === 'CastError') {
    statusCode = 400;
    message = 'Gönderilen veriler geçersiz';
    details = Object.values(error.errors || {}).map((item) => item.message);
  } else if (error instanceof SyntaxError && error.status === 400 && 'body' in error) {
    statusCode = 400;
    message = 'JSON gövdesi geçersiz';
  }

  if (statusCode >= 500) {
    console.error('İşlenmeyen API hatası:', error);
    if (process.env.NODE_ENV === 'production') {
      message = 'Beklenmeyen bir sunucu hatası oluştu';
    }
  }

  const response = { hata: message };
  if (details?.length) {
    response.detaylar = details;
  }
  res.status(statusCode).json(response);
});

async function connectDatabase() {
  const uri = process.env.MONGODB_URI || 'mongodb://127.0.0.1:27017/gazete-dagitim';
  const timeout = Number(process.env.MONGODB_SERVER_SELECTION_TIMEOUT_MS || 10000);
  const dnsServers = String(process.env.DNS_SERVERS || '')
    .split(',')
    .map((server) => server.trim())
    .filter(Boolean);

  if (dnsServers.length > 0) {
    dns.setServers(dnsServers);
  }

  try {
    await mongoose.connect(uri, { serverSelectionTimeoutMS: timeout });
    console.log('MongoDB bağlantısı kuruldu');
  } catch (error) {
    console.error('MongoDB bağlantısı kurulamadı:', error.message);
    throw error;
  }
}

function cronDisabled() {
  return /^(1|true|yes)$/i.test(String(process.env.DISABLE_CRON || ''));
}

function registerCronJobs() {
  if (cronDisabled()) {
    console.log('DISABLE_CRON=true: otomatik cron işleri planlanmadı');
    return [];
  }

  const timezone = process.env.CRON_TIMEZONE || 'Europe/Istanbul';
  const scheduleOptions = { timezone };
  const dailyDeliveryCron = require('./cron/dailyDelivery');
  const dailyPaymentCron = require('./cron/dailyPayment');
  const weeklyPaymentCron = require('./cron/weeklyPayment');
  const monthlyPaymentCron = require('./cron/monthlyPayment');

  return [
    cron.schedule('0 0 * * *', () => dailyDeliveryCron(), scheduleOptions),
    cron.schedule('59 23 * * *', () => dailyPaymentCron(), scheduleOptions),
    cron.schedule('59 23 * * *', () => weeklyPaymentCron(), scheduleOptions),
    cron.schedule('59 23 * * *', () => monthlyPaymentCron(), scheduleOptions)
  ];
}

function startServer() {
  const port = Number(process.env.PORT || 5000);
  const host = String(process.env.HOST || '127.0.0.1').trim() || '127.0.0.1';
  const server = app.listen(port, host, () => {
    console.log(`Server http://${host}:${port} adresinde çalışıyor`);
  });

  connectDatabase()
    .then(() => registerCronJobs())
    .catch(() => {
      // The health endpoint remains available and database-backed routes fail
      // immediately because Mongoose command buffering is disabled. Cron jobs
      // are deliberately not scheduled until a database connection succeeds.
    });

  const shutdown = async () => {
    server.close(() => {});
    await mongoose.connection.close(false);
  };
  process.once('SIGINT', shutdown);
  process.once('SIGTERM', shutdown);

  return server;
}

if (require.main === module) {
  startServer();
}

module.exports = {
  app,
  connectDatabase,
  registerCronJobs,
  startServer,
  cronDisabled
};
