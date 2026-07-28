const Distributor = require('../models/Distributor');
const { startOfDay, getBusinessDateParts } = require('../utils/date');
const { createPaymentsForDistributors } = require('./dailyPayment');

function monthlyScheduledDayFilter(now = new Date()) {
  const today = startOfDay(now);
  const { year, month, day: dayOfMonth } = getBusinessDateParts(today);
  const lastDayOfMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
  // A distributor choosing 29/30/31 is paid on the last available calendar
  // day of short months instead of silently being skipped.
  return dayOfMonth === lastDayOfMonth
    ? { $gte: dayOfMonth }
    : dayOfMonth;
}

// Her gün 23:59'da kontrol edilir. Dönem bitişi ödeme gününün kendisidir.
async function monthlyPaymentCron(now = new Date()) {
  const today = startOfDay(now);
  const scheduledDayFilter = monthlyScheduledDayFilter(today);

  try {
    const distributors = await Distributor.find({
      aktif: true,
      odeme_tipi: 'Aylık',
      odeme_gunleri_ay: scheduledDayFilter
    });

    return await createPaymentsForDistributors(distributors, 'Aylık', today, 'Aylık');
  } catch (error) {
    console.error('Aylık ödeme kontrolü başarısız:', error.message);
    return { created: 0, existing: 0, skipped: 0, failed: 1 };
  }
}

module.exports = monthlyPaymentCron;
module.exports.monthlyScheduledDayFilter = monthlyScheduledDayFilter;
