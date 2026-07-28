const Distributor = require('../models/Distributor');
const Delivery = require('../models/Delivery');
const { startOfDay } = require('../utils/date');
const { createPaymentIfAbsent } = require('../services/automaticPayments');
const { nextPaymentPeriod } = require('../services/paymentPeriods');

async function createPaymentsForDistributors(distributors, odemeTuru, today, aciklamaPrefix) {
  const summary = { created: 0, existing: 0, skipped: 0, failed: 0 };

  for (const distributor of distributors) {
    try {
      const period = await nextPaymentPeriod({
        distributorId: distributor._id,
        odemeTuru,
        periodEnd: today
      });

      if (!period) {
        summary.existing += 1;
        continue;
      }

      const deliveries = await Delivery.find({
        distributor_id: distributor._id,
        tarih: { $gte: period.start, $lte: period.end },
        durum: 'Tamamlandı'
      });

      if (deliveries.length === 0) {
        summary.skipped += 1;
        continue;
      }

      const result = await createPaymentIfAbsent({
        distributor,
        odemeTuru,
        donemBaslangic: period.start,
        donemBitis: period.end,
        tarih: today,
        deliveries,
        aciklamaPrefix
      });

      if (result.created) {
        summary.created += 1;
        console.log(`${odemeTuru} ödeme oluşturuldu: ${distributor.isim} (${result.tutar}₺)`);
      } else {
        summary.existing += 1;
      }
    } catch (error) {
      summary.failed += 1;
      console.error(`${odemeTuru} ödeme oluşturulamadı (${distributor.isim}):`, error.message);
    }
  }

  return summary;
}

// Her gün 23:59'da çalışır; dönem başlangıcı ve bitişi bugündür.
async function dailyPaymentCron(now = new Date()) {
  const today = startOfDay(now);

  try {
    const distributors = await Distributor.find({ aktif: true, odeme_tipi: 'Günlük' });
    return await createPaymentsForDistributors(distributors, 'Günlük', today, 'Günlük');
  } catch (error) {
    console.error('Günlük ödeme kontrolü başarısız:', error.message);
    return { created: 0, existing: 0, skipped: 0, failed: 1 };
  }
}

module.exports = dailyPaymentCron;
module.exports.createPaymentsForDistributors = createPaymentsForDistributors;
