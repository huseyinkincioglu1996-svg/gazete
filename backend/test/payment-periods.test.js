const test = require('node:test');
const assert = require('node:assert/strict');

const PaymentPeriod = require('../models/PaymentPeriod');
const {
  buildPaymentPeriodPayload,
  buildPaymentPeriodStatusPayload
} = require('../utils/payloads');
const { activeFilter } = require('../routes/paymentPeriods');

test('payment period create payload trims text and validates a positive integer day count', () => {
  assert.deepEqual(
    buildPaymentPeriodPayload({
      ad: '  Aylık  ',
      gun_sayisi: '30',
      aciklama: '  Her ay  ',
      aktif: false
    }),
    {
      ad: 'Aylık',
      gun_sayisi: 30,
      aciklama: 'Her ay',
      aktif: false
    }
  );

  assert.throws(() => buildPaymentPeriodPayload({}), /ad zorunludur/);
  assert.throws(
    () => buildPaymentPeriodPayload({ ad: 'Haftalık', gun_sayisi: 0 }),
    /gun_sayisi.*küçük olamaz/
  );
  assert.throws(
    () => buildPaymentPeriodPayload({ ad: 'Özel', gun_sayisi: 1.5 }),
    /tam sayı/
  );
  assert.throws(
    () => buildPaymentPeriodPayload({ ad: 'Çok Uzun', gun_sayisi: 366 }),
    /365 değerinden büyük/
  );
});

test('payment period updates and status changes use strict allowlists', () => {
  assert.deepEqual(
    buildPaymentPeriodPayload({ gun_sayisi: 14 }, { partial: true }),
    { gun_sayisi: 14 }
  );
  assert.throws(
    () => buildPaymentPeriodPayload({}, { partial: true }),
    /Güncellenecek en az bir alan/
  );
  assert.deepEqual(buildPaymentPeriodStatusPayload({ aktif: true }), { aktif: true });
  assert.throws(
    () => buildPaymentPeriodStatusPayload({ aktif: 'true' }),
    /doğru veya yanlış/
  );
  assert.throws(
    () => buildPaymentPeriodStatusPayload({ aktif: true, ad: 'Aylık' }),
    /İzin verilmeyen alanlar: ad/
  );
});

test('payment period active query filter accepts only explicit booleans', () => {
  assert.deepEqual(activeFilter({}), {});
  assert.deepEqual(activeFilter({ aktif: 'true' }), { aktif: true });
  assert.deepEqual(activeFilter({ aktif: 'false' }), { aktif: false });
  assert.throws(() => activeFilter({ aktif: '1' }), /true veya false/);
  assert.throws(() => activeFilter({ sayfa: '1' }), /İzin verilmeyen alanlar/);
});

test('payment period model enforces required, integer, and positive values', async () => {
  const period = new PaymentPeriod({
    ad: '  On Beş Gün  ',
    gun_sayisi: 15
  });
  await period.validate();
  assert.equal(period.ad, 'On Beş Gün');
  assert.equal(period.aktif, true);

  await assert.rejects(
    () => new PaymentPeriod({ ad: 'Kesirli', gun_sayisi: 2.5 }).validate(),
    /tam sayı/
  );
  await assert.rejects(
    () => new PaymentPeriod({ ad: 'Sıfır', gun_sayisi: 0 }).validate(),
    /less than minimum allowed|minimum izin verilen/
  );
  await assert.rejects(
    () => new PaymentPeriod({ ad: 'Çok Uzun', gun_sayisi: 366 }).validate(),
    /more than maximum allowed|maksimum izin verilen/
  );
});
