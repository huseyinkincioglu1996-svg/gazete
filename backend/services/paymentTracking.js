const { calculateCashHandoverTotal } = require('./cashHandovers');
const { parseCashHandoverMonth } = require('./cashHandovers');
const { HttpError } = require('../utils/http');
const {
  hasOwn,
  objectId,
  rejectUnknownFields
} = require('../utils/validation');

function parsePaymentTrackingQuery(query) {
  rejectUnknownFields(query, ['month', 'distributor_id']);
  if (!hasOwn(query, 'month')) {
    throw new HttpError(400, 'month zorunludur');
  }

  const range = parseCashHandoverMonth(query.month);
  const distributorId = hasOwn(query, 'distributor_id')
    ? objectId(query.distributor_id, 'distributor_id')
    : null;

  return {
    ...range,
    distributorId
  };
}

function calculatePaymentTrackingSummary(payments, cashCollections) {
  const paymentTotal = calculateCashHandoverTotal(
    payments.map((payment) => ({ tutar: payment.tutar }))
  );
  const paidTotal = calculateCashHandoverTotal(
    payments
      .filter((payment) => payment.durum === 'Ödendi')
      .map((payment) => ({ tutar: payment.tutar }))
  );
  const pendingTotal = calculateCashHandoverTotal(
    payments
      .filter((payment) => payment.durum === 'Beklemede')
      .map((payment) => ({ tutar: payment.tutar }))
  );
  const cashTotal = calculateCashHandoverTotal(
    cashCollections.map((collection) => ({ tutar: collection.tutar }))
  );

  return {
    dagitici_odeme_toplami: paymentTotal,
    odenen_toplami: paidTotal,
    bekleyen_toplami: pendingTotal,
    nakit_tahsilat_toplami: cashTotal,
    nakit_tahsilat_adedi: cashCollections.length
  };
}

module.exports = {
  calculatePaymentTrackingSummary,
  parsePaymentTrackingQuery
};
