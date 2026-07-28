const Distributor = require('../models/Distributor');
const { startOfDay, getTurkishBusinessDay } = require('../utils/date');
const { createPaymentsForDistributors } = require('./dailyPayment');

// Her gün 23:59'da kontrol edilir. 0=Pazartesi ... 6=Pazar eşlemesi kullanılır.
async function weeklyPaymentCron(now = new Date()) {
  const today = startOfDay(now);
  const businessDay = getTurkishBusinessDay(today);

  try {
    const distributors = await Distributor.find({
      aktif: true,
      odeme_tipi: 'Haftalık',
      odeme_gunleri_hafta: businessDay
    });

    return await createPaymentsForDistributors(distributors, 'Haftalık', today, 'Haftalık');
  } catch (error) {
    console.error('Haftalık ödeme kontrolü başarısız:', error.message);
    return { created: 0, existing: 0, skipped: 0, failed: 1 };
  }
}

module.exports = weeklyPaymentCron;
