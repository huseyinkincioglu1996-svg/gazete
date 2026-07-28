const test = require('node:test');
const assert = require('node:assert/strict');

const {
  addDays,
  addMonthsClamped,
  dateKey,
  getTurkishBusinessDay,
  startOfWeek
} = require('../utils/date');
const { initialPeriodStart } = require('../services/paymentPeriods');
const { paymentPeriodIdentity, calculatePaymentTotals } = require('../services/automaticPayments');
const { monthlyScheduledDayFilter } = require('../cron/monthlyPayment');

test('Turkish business-day mapping is Monday-first', () => {
  assert.equal(getTurkishBusinessDay(new Date(2026, 6, 27)), 0); // Monday
  assert.equal(getTurkishBusinessDay(new Date(2026, 7, 2)), 6); // Sunday
  assert.equal(getTurkishBusinessDay(new Date(2026, 6, 29)), 2); // Wednesday
});

test('weekly start is Monday and a weekly initial period contains seven inclusive days', () => {
  const sunday = new Date(2026, 7, 2);
  assert.equal(dateKey(startOfWeek(sunday)), '2026-07-27');
  assert.equal(dateKey(initialPeriodStart('Haftalık', sunday)), '2026-07-27');
  assert.equal(dateKey(addDays(initialPeriodStart('Haftalık', sunday), 6)), '2026-08-02');
});

test('monthly initial period ends on the payment date and never uses a future boundary', () => {
  const paymentDay = new Date(2026, 2, 30);
  const start = initialPeriodStart('Aylık', paymentDay);

  assert.equal(dateKey(start), '2026-02-28');
  assert.ok(start <= paymentDay);
  assert.equal(dateKey(addMonthsClamped(new Date(2026, 2, 31), -1)), '2026-02-28');
});

test('short months run 29th-31st monthly schedules on the last day', () => {
  assert.deepEqual(monthlyScheduledDayFilter(new Date('2026-02-28T12:00:00Z')), { $gte: 28 });
  assert.equal(monthlyScheduledDayFilter(new Date('2026-04-29T12:00:00Z')), 29);
});

test('payment identity is stable for an exact distributor/type/period and totals reject negatives', () => {
  const first = paymentPeriodIdentity({
    distributorId: '507f1f77bcf86cd799439011',
    odemeTuru: 'Haftalık',
    donemBaslangic: new Date(2026, 6, 27),
    donemBitis: new Date(2026, 7, 2)
  });
  const second = paymentPeriodIdentity({
    distributorId: '507f1f77bcf86cd799439011',
    odemeTuru: 'Haftalık',
    donemBaslangic: '2026-07-27',
    donemBitis: '2026-08-02'
  });

  assert.equal(first, second);
  assert.deepEqual(
    calculatePaymentTotals([{ gazeteSayisi: 2 }, { gazeteSayisi: 3 }], 5),
    { totalGazete: 5, tutar: 25, price: 5 }
  );
  assert.throws(() => calculatePaymentTotals([{ gazeteSayisi: -1 }], 5), /geçersiz/);
});
