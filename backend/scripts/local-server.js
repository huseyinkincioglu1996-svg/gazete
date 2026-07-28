// Yerel geliştirme sunucusu, kişisel .env içindeki uzak veritabanı ayarını
// değiştirmeden uygulamayı çalışma alanındaki yerel MongoDB'ye bağlar.
process.env.MONGODB_URI = 'mongodb://127.0.0.1:27017/gazete-dagitim';
process.env.PORT = process.env.PORT || '5001';
process.env.HOST = process.env.HOST || '127.0.0.1';
process.env.DISABLE_CRON = process.env.DISABLE_CRON || 'true';
process.env.CORS_ORIGINS = process.env.CORS_ORIGINS
  || 'http://127.0.0.1:3001,http://localhost:3001';

const { startServer } = require('../server');

startServer();
