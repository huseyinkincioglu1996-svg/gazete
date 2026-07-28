const test = require('node:test');
const assert = require('node:assert/strict');

const {
  calculatePaymentTrackingSummary,
  parsePaymentTrackingQuery
} = require('../services/paymentTracking');
const { dateKey } = require('../utils/date');

const DISTRIBUTOR_ID = '507f1f77bcf86cd799439013';

test('payment tracking query requires a strict month and optional valid distributor id', () => {
  const parsed = parsePaymentTrackingQuery({
    month: '2026-07',
    distributor_id: DISTRIBUTOR_ID
  });
  assert.equal(parsed.month, '2026-07');
  assert.equal(parsed.distributorId, DISTRIBUTOR_ID);
  assert.equal(dateKey(parsed.start), '2026-07-01');
  assert.equal(dateKey(parsed.endExclusive), '2026-08-01');

  assert.throws(() => parsePaymentTrackingQuery({}), /month zorunludur/);
  assert.throws(
    () => parsePaymentTrackingQuery({ month: '2026-13' }),
    /geçerli bir ay/
  );
  assert.throws(
    () => parsePaymentTrackingQuery({ month: '2026-07', distributor_id: 'x' }),
    /geçerli bir kimlik/
  );
  assert.throws(
    () => parsePaymentTrackingQuery({ month: '2026-07', durum: 'Ödendi' }),
    /İzin verilmeyen alanlar: durum/
  );
});

test('payment tracking keeps outgoing payments and cash collections as separate rounded totals', () => {
  assert.deepEqual(
    calculatePaymentTrackingSummary(
      [
        { tutar: 10.105, durum: 'Ödendi' },
        { tutar: 20.2, durum: 'Beklemede' }
      ],
      [
        { tutar: 5.555 },
        { tutar: 4.4 }
      ]
    ),
    {
      dagitici_odeme_toplami: 30.31,
      odenen_toplami: 10.11,
      bekleyen_toplami: 20.2,
      nakit_tahsilat_toplami: 9.96,
      nakit_tahsilat_adedi: 2
    }
  );
});
