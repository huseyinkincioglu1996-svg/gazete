const Payment = require('../models/Payment');
const { addDays, addMonthsClamped, startOfDay } = require('../utils/date');

function initialPeriodStart(odemeTuru, periodEnd) {
  const end = startOfDay(periodEnd);

  switch (odemeTuru) {
    case 'Günlük':
      return end;
    case 'Haftalık':
      // Inclusive seven-day period: today and the preceding six days.
      return addDays(end, -6);
    case 'Aylık':
      // Inclusive monthly cycle ending today. For example 27 July starts 28 June.
      return addMonthsClamped(addDays(end, 1), -1);
    default:
      throw new RangeError('Geçersiz ödeme türü');
  }
}

/**
 * Calculates an inclusive period that ends on the current payment day. Later
 * runs continue immediately after the latest closed period for the same
 * distributor/type, so changing a scheduled weekday cannot create overlap.
 */
async function nextPaymentPeriod({ distributorId, odemeTuru, periodEnd }) {
  const end = startOfDay(periodEnd);
  const alreadyClosed = await Payment.exists({
    distributor_id: distributorId,
    odeme_turu: odemeTuru,
    donem_bitis: end
  });

  if (alreadyClosed) {
    return null;
  }

  if (odemeTuru === 'Günlük') {
    return { start: end, end };
  }

  const previousPayment = await Payment.findOne({
    distributor_id: distributorId,
    odeme_turu: odemeTuru,
    donem_bitis: { $lt: end }
  })
    .sort({ donem_bitis: -1 })
    .select({ donem_bitis: 1 })
    .lean();

  const start = previousPayment
    ? addDays(previousPayment.donem_bitis, 1)
    : initialPeriodStart(odemeTuru, end);

  if (start > end) {
    return null;
  }

  return { start, end };
}

module.exports = {
  initialPeriodStart,
  nextPaymentPeriod
};
