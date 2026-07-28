const test = require('node:test');
const assert = require('node:assert/strict');

const {
  buildSubscriberPayload,
  buildSubscriberStatusPayload,
  validateSubscriberNewspaperDays
} = require('../utils/payloads');
const Subscriber = require('../models/Subscriber');
const PAYMENT_PERIOD_ID = '507f1f77bcf86cd799439012';
const DISTRIBUTOR_ID = '507f1f77bcf86cd799439013';

test('subscriber create payload requires and trims a name while normalizing optional fields', () => {
  assert.deepEqual(
    buildSubscriberPayload({
      isim: '  Ayşe Yılmaz  ',
      telefon: '  0555 000 00 00  ',
      adres: null,
      aylik_ucret: '250.50',
      notlar: '  Sabah teslimatı  ',
      aktif: false,
      gazete_gunleri: ['carsamba', 'pazar_pazartesi']
    }),
    {
      isim: 'Ayşe Yılmaz',
      telefon: '0555 000 00 00',
      adres: '',
      aylik_ucret: 250.5,
      notlar: 'Sabah teslimatı',
      aktif: false,
      gazete_gunleri: ['carsamba', 'pazar_pazartesi']
    }
  );

  assert.throws(() => buildSubscriberPayload({}), /isim zorunludur/);
  assert.throws(() => buildSubscriberPayload({ isim: '   ' }), /isim boş olamaz/);
  assert.throws(
    () => buildSubscriberPayload({ isim: 'Abone', aylik_ucret: -1 }),
    /aylik_ucret.*küçük olamaz/
  );
});

test('subscriber update payload accepts boolean status, is allowlisted, and must contain a change', () => {
  assert.deepEqual(
    buildSubscriberPayload(
      { telefon: '', aylik_ucret: 0, aktif: true },
      { partial: true }
    ),
    { telefon: '', aylik_ucret: 0, aktif: true }
  );
  assert.throws(
    () => buildSubscriberPayload({ aktif: 'false' }, { partial: true }),
    /doğru veya yanlış/
  );
  assert.throws(
    () => buildSubscriberPayload({ bilinmeyen: true }, { partial: true }),
    /İzin verilmeyen alanlar: bilinmeyen/
  );
  assert.throws(
    () => buildSubscriberPayload({}, { partial: true }),
    /Güncellenecek en az bir alan/
  );
});

test('subscriber status payload accepts only a required boolean aktif field', () => {
  assert.deepEqual(buildSubscriberStatusPayload({ aktif: false }), { aktif: false });
  assert.throws(() => buildSubscriberStatusPayload({}), /aktif zorunludur/);
  assert.throws(
    () => buildSubscriberStatusPayload({ aktif: 'false' }),
    /doğru veya yanlış/
  );
  assert.throws(
    () => buildSubscriberStatusPayload({ aktif: true, isim: 'Abone' }),
    /İzin verilmeyen alanlar: isim/
  );
});

test('subscriber newspaper days preserve order and reject duplicates or Sunday/Monday conflicts', () => {
  assert.deepEqual(
    validateSubscriberNewspaperDays(['cuma', 'sali', 'pazar_pazartesi']),
    ['cuma', 'sali', 'pazar_pazartesi']
  );
  assert.throws(
    () => validateSubscriberNewspaperDays(['sali', 'sali']),
    /tekrar eden/
  );
  assert.throws(
    () => validateSubscriberNewspaperDays(['pazar_pazartesi', 'pazar']),
    /birlikte seçilemez/
  );
  assert.throws(
    () => validateSubscriberNewspaperDays(['pazar_pazartesi', 'pazartesi']),
    /birlikte seçilemez/
  );
  assert.throws(
    () => validateSubscriberNewspaperDays(['çarşamba']),
    /geçerli bir değer/
  );
});

test('subscriber model defaults newspaper days and enforces the same invariants', async () => {
  const legacyCompatibleSubscriber = new Subscriber({ isim: 'Varsayılan Abone' });
  await legacyCompatibleSubscriber.validate();
  assert.deepEqual(legacyCompatibleSubscriber.gazete_gunleri, []);

  const duplicateDays = new Subscriber({
    isim: 'Tekrarlı Abone',
    gazete_gunleri: ['cuma', 'cuma']
  });
  await assert.rejects(() => duplicateDays.validate(), /tekrar eden/);

  const conflictingDays = new Subscriber({
    isim: 'Çakışan Abone',
    gazete_gunleri: ['pazar_pazartesi', 'pazartesi']
  });
  await assert.rejects(() => conflictingDays.validate(), /birlikte seçilemez/);
});

test('subscriber payload accepts a nullable payment period and an all-or-nothing valid location', () => {
  assert.deepEqual(
    buildSubscriberPayload(
      {
        odeme_periyodu_id: PAYMENT_PERIOD_ID,
        konum: { enlem: '41.0082', boylam: '28.9784' }
      },
      { partial: true }
    ),
    {
      odeme_periyodu_id: PAYMENT_PERIOD_ID,
      konum: { enlem: 41.0082, boylam: 28.9784 }
    }
  );
  assert.deepEqual(
    buildSubscriberPayload(
      { odeme_periyodu_id: null, konum: null },
      { partial: true }
    ),
    { odeme_periyodu_id: null, konum: null }
  );

  assert.throws(
    () => buildSubscriberPayload(
      { konum: { enlem: 41 } },
      { partial: true }
    ),
    /birlikte gönderilmelidir/
  );
  assert.throws(
    () => buildSubscriberPayload(
      { konum: { enlem: 91, boylam: 29 } },
      { partial: true }
    ),
    /enlem -90 ile 90/
  );
  assert.throws(
    () => buildSubscriberPayload(
      { konum: { enlem: 41, boylam: -181 } },
      { partial: true }
    ),
    /boylam.*küçük olamaz/
  );
  assert.throws(
    () => buildSubscriberPayload(
      { odeme_periyodu_id: 'geçersiz' },
      { partial: true }
    ),
    /geçerli bir kimlik/
  );
});

test('subscriber model validates both coordinates and their ranges', async () => {
  const located = new Subscriber({
    isim: 'Konumlu Abone',
    odeme_periyodu_id: PAYMENT_PERIOD_ID,
    konum: { enlem: 41.0082, boylam: 28.9784 }
  });
  await located.validate();
  assert.equal(String(located.odeme_periyodu_id), PAYMENT_PERIOD_ID);
  assert.deepEqual(located.konum.toObject(), { enlem: 41.0082, boylam: 28.9784 });

  const incomplete = new Subscriber({
    isim: 'Eksik Konum',
    konum: { enlem: 41 }
  });
  await assert.rejects(() => incomplete.validate(), /boylam.*required|boylam.*zorunlu/);

  const outOfRange = new Subscriber({
    isim: 'Geçersiz Konum',
    konum: { enlem: -91, boylam: 29 }
  });
  await assert.rejects(
    () => outOfRange.validate(),
    /less than minimum allowed|minimum izin verilen/
  );
});

test('subscriber payload and model accept a nullable distributor reference', async () => {
  assert.deepEqual(
    buildSubscriberPayload(
      { distributor_id: DISTRIBUTOR_ID },
      { partial: true }
    ),
    { distributor_id: DISTRIBUTOR_ID }
  );
  assert.deepEqual(
    buildSubscriberPayload(
      { distributor_id: null },
      { partial: true }
    ),
    { distributor_id: null }
  );
  assert.throws(
    () => buildSubscriberPayload(
      { distributor_id: 'geçersiz' },
      { partial: true }
    ),
    /geçerli bir kimlik/
  );

  const subscriber = new Subscriber({
    isim: 'Dağıtıcılı Abone',
    distributor_id: DISTRIBUTOR_ID
  });
  await subscriber.validate();
  assert.equal(String(subscriber.distributor_id), DISTRIBUTOR_ID);
});
