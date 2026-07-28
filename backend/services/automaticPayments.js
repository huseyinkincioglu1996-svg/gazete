const Payment = require('../models/Payment');
const { startOfDay } = require('../utils/date');
const { isDuplicateKeyError } = require('../utils/http');

function calculatePaymentTotals(deliveries, newspaperPrice) {
  const price = Number(newspaperPrice);
  if (!Number.isFinite(price) || price < 0) {
    throw new RangeError('Dağıtıcı gazete fiyatı geçerli ve negatif olmayan bir sayı olmalıdır');
  }

  const totalGazete = deliveries.reduce((total, delivery) => {
    const quantity = Number(delivery.gazeteSayisi);
    if (!Number.isFinite(quantity) || quantity < 0) {
      throw new RangeError('Tamamlanan dağıtımdaki gazete sayısı geçersiz');
    }
    return total + quantity;
  }, 0);

  const tutar = totalGazete * price;
  if (!Number.isFinite(tutar) || tutar < 0) {
    throw new RangeError('Hesaplanan ödeme tutarı geçersiz');
  }

  return { totalGazete, tutar, price };
}

function paymentPeriodIdentity({ distributorId, odemeTuru, donemBaslangic, donemBitis }) {
  return [
    String(distributorId),
    odemeTuru,
    startOfDay(donemBaslangic).toISOString(),
    startOfDay(donemBitis).toISOString()
  ].join('|');
}

/**
 * Uses the unique payment-period index as the final concurrency guard. Repeated
 * cron executions leave the first payment intact instead of creating another.
 */
async function createPaymentIfAbsent({
  distributor,
  odemeTuru,
  donemBaslangic,
  donemBitis,
  tarih = new Date(),
  deliveries,
  aciklamaPrefix = ''
}) {
  const periodStart = startOfDay(donemBaslangic);
  const periodEnd = startOfDay(donemBitis);
  const paymentDate = startOfDay(tarih);
  if (periodEnd < periodStart) {
    throw new RangeError('Ödeme dönemi bitişi başlangıçtan önce olamaz');
  }
  const { totalGazete, tutar, price } = calculatePaymentTotals(deliveries, distributor.gazete_fiyat);
  const prefix = aciklamaPrefix ? `${aciklamaPrefix}: ` : '';
  const now = new Date();
  const filter = {
    distributor_id: distributor._id,
    odeme_turu: odemeTuru,
    donem_baslangic: periodStart,
    donem_bitis: periodEnd
  };

  const payment = {
    ...filter,
    tutar,
    tarih: paymentDate,
    aciklama: `${prefix}${totalGazete} gazete × ${price}₺ = ${tutar}₺`,
    durum: 'Beklemede',
    createdAt: now,
    updatedAt: now
  };

  try {
    const result = await Payment.updateOne(
      filter,
      { $setOnInsert: payment },
      { upsert: true, timestamps: false }
    );

    return {
      created: result.upsertedCount === 1,
      totalGazete,
      tutar,
      identity: paymentPeriodIdentity({
        distributorId: distributor._id,
        odemeTuru,
        donemBaslangic: periodStart,
        donemBitis: periodEnd
      })
    };
  } catch (error) {
    if (isDuplicateKeyError(error)) {
      return { created: false, totalGazete, tutar };
    }
    throw error;
  }
}

module.exports = {
  calculatePaymentTotals,
  paymentPeriodIdentity,
  createPaymentIfAbsent
};
