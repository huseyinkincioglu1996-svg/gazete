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
mongoose.connect(process.env.MONGODB_URI || 'mongodb://localhost:27017/gazete-dagitim', {
  useNewUrlParser: true,
  useUnifiedTopology: true
}).then(() => {
  console.log('✅ MongoDB bağlandı!');
}).catch(err => {
  console.error('❌ MongoDB bağlantı hatası:', err);
});

// Routes
app.use('/api/distributors', require('./routes/distributors'));
app.use('/api/deliveries', require('./routes/deliveries'));
app.use('/api/payments', require('./routes/payments'));
app.use('/api/reports', require('./routes/reports'));

// Test Route
app.get('/api/test', (req, res) => {
  res.json({ mesaj: 'Server çalışıyor! 🚀' });
});

// Error Handler
app.use((err, req, res, next) => {
  console.error(err.stack);
  res.status(500).json({ hata: err.message });
});

// Cron Jobs
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

const PORT = process.env.PORT || 5000;
app.listen(PORT, () => {
  console.log(`🚀 Server ${PORT} portunda çalışıyor!`);
});
