const test = require('node:test');
const assert = require('node:assert/strict');

const {
  buildAutomaticCashItems,
  buildCashHandoverPayload,
  calculateCashHandoverComponents,
  calculateCashHandoverTotal,
  parseCashHandoverDateKey,
  parseCashHandoverMonth
} = require('../services/cashHandovers');
const { dateKey } = require('../utils/date');
const CashHandover = require('../models/CashHandover');

test('cash handover total is calculated on the server and rounded as currency', () => {
  assert.equal(
    calculateCashHandoverTotal([
      { tutar: 10.1 },
      { tutar: 0.2 },
      { tutar: 4.555 }
    ]),
    14.86
  );
  assert.throws(
    () => calculateCashHandoverTotal([{ tutar: -0.01 }]),
    /negatif olmayan/
  );
});

test('cash handover payload trims values, permits an optional note, and rejects client totals', () => {
  assert.deepEqual(
    buildCashHandoverPayload({
      kalemler: [
        { abone: '  Ayşe Yılmaz  ', tutar: '25.50', aciklama: '  Temmuz  ' },
        { abone: 'Mehmet Kaya', tutar: 10 }
      ],
      durum: 'Teslim Edildi'
    }),
    {
      kalemler: [
        { abone: 'Ayşe Yılmaz', tutar: 25.5, aciklama: 'Temmuz' },
        { abone: 'Mehmet Kaya', tutar: 10, aciklama: '' }
      ],
      toplam: 35.5,
      durum: 'Teslim Edildi'
    }
  );

  assert.throws(
    () => buildCashHandoverPayload({ kalemler: [{ abone: ' ', tutar: 1 }] }),
    /kalemler\[0\].*abone/
  );
  assert.throws(
    () => buildCashHandoverPayload({ kalemler: [{ abone: 'A', tutar: -1 }] }),
    /kalemler\[0\].*tutar/
  );
  assert.throws(
    () => buildCashHandoverPayload({ kalemler: [], toplam: 999 }),
    /İzin verilmeyen alanlar: toplam/
  );
});

test('cash handover date and month ranges follow the Istanbul business calendar', () => {
  assert.equal(dateKey(parseCashHandoverDateKey('2026-07-27')), '2026-07-27');
  assert.throws(
    () => parseCashHandoverDateKey('2026-02-30'),
    /geçerli bir tarih|geçerli bir takvim/
  );

  const february = parseCashHandoverMonth('2028-02');
  assert.equal(dateKey(february.start), '2028-02-01');
  assert.equal(dateKey(february.endExclusive), '2028-03-01');
  assert.equal(february.endKey, '2028-02-29');
  assert.throws(() => parseCashHandoverMonth('2026-13'), /geçerli bir ay/);
});

test('cash handover model preserves the server-side total invariant', async () => {
  const handover = new CashHandover({
    tarih: '2026-07-27',
    kalemler: [
      { abone: 'Abone 1', tutar: 12.5 },
      { abone: 'Abone 2', tutar: 7.25 }
    ],
    toplam: 999,
    durum: 'Teslim Edildi'
  });

  await handover.validate();

  assert.equal(dateKey(handover.tarih), '2026-07-27');
  assert.equal(handover.toplam, 19.75);
  assert.ok(handover.teslim_tarihi instanceof Date);
});

test('cash handover automatic items include only collected cash without mutating manual totals', () => {
  const automaticItems = buildAutomaticCashItems([
    {
      _id: 'cash-delivery',
      subscriber_id: { isim: 'Nakit Abone' },
      tahsil_edildi: true,
      tutar: 25,
      odeme_yontemi: 'Nakit'
    },
    {
      _id: 'card-delivery',
      subscriber_id: { isim: 'Kart Abone' },
      tahsil_edildi: true,
      tutar: 50,
      odeme_yontemi: 'Kart'
    },
    {
      _id: 'uncollected-delivery',
      subscriber_id: { isim: 'Bekleyen Abone' },
      tahsil_edildi: false,
      tutar: 75,
      odeme_yontemi: 'Nakit'
    }
  ]);

  assert.deepEqual(automaticItems, [{
    abone: 'Nakit Abone',
    tutar: 25,
    aciklama: 'Günlük abone tahsilatı',
    otomatik: true,
    kaynak_id: 'cash-delivery',
    odeme_yontemi: 'Nakit'
  }]);
  assert.deepEqual(
    calculateCashHandoverComponents([{ tutar: 10 }], automaticItems),
    {
      manuel_toplam: 10,
      otomatik_toplam: 25,
      toplam: 35
    }
  );
});
