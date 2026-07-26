require('dotenv').config();
const express = require('express');
const mongoose = require('mongoose');
const cors = require('cors');
const cron = require('node-cron');

const app = express();

// Middleware
app.use(cors());
app.use(express.json());

// MongoDB Connection
const mongoURI = process.env.MONGODB_URI || 'mongodb://localhost:27017/gazete-dagitim';
console.log(`📡 MongoDB'ye bağlanılıyor: ${mongoURI}`);

mongoose.connect(mongoURI, {
  useNewUrlParser: true,
  useUnifiedTopology: true,
  serverSelectionTimeoutMS: 5000,
  socketTimeoutMS: 45000,
}).then(() => {
  console.log('✅ MongoDB bağlandı!');
}).catch(err => {
  console.error('❌ MongoDB bağlantı hatası:', err.message);
  console.log('⚠️ MongoDB olmadan devam ediliyor...');
});

// Routes
app.use('/api/distributors', require('./routes/distributors'));
app.use('/api/deliveries', require('./routes/deliveries'));
app.use('/api/payments', require('./routes/payments'));
app.use('/api/reports', require('./routes/reports'));

// Test Route
app.get('/api/test', (req, res) => {
  res.json({ mesaj: 'Server çalışıyor! 🚀', mongodb: mongoose.connection.readyState === 1 ? '✅ Bağlı' : '❌ Bağlı Değil' });
});

// Error Handler
app.use((err, req, res, next) => {
  console.error(err.stack);
  res.status(500).json({ hata: err.message });
});

// Cron Jobs
try {
  const dailyDeliveryCron = require('./cron/dailyDelivery');
  const dailyPaymentCron = require('./cron/dailyPayment');
  const weeklyPaymentCron = require('./cron/weeklyPayment');
  const monthlyPaymentCron = require('./cron/monthlyPayment');

  // Her gün 00:00'de dağıtım oluştur
  cron.schedule('0 0 * * *', dailyDeliveryCron);

  // Her gün 23:59'de ödeme oluştur
  cron.schedule('59 23 * * *', dailyPaymentCron);
  cron.schedule('59 23 * * *', weeklyPaymentCron);
  cron.schedule('59 23 * * *', monthlyPaymentCron);
  
  console.log('✅ Cron jobs başlatıldı');
} catch (err) {
  console.warn('⚠️ Cron jobs yüklenirken hata:', err.message);
}

const PORT = process.env.PORT || 5000;
app.listen(PORT, () => {
  console.log(`🚀 Server ${PORT} portunda çalışıyor!`);
});
