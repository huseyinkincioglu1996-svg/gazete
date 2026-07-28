const test = require('node:test');
const assert = require('node:assert/strict');
const mongoose = require('mongoose');

const PaymentPeriod = require('../models/PaymentPeriod');
const Payment = require('../models/Payment');
const Subscriber = require('../models/Subscriber');
const Distributor = require('../models/Distributor');
const SubscriberDailyDelivery = require('../models/SubscriberDailyDelivery');
const { app } = require('../server');

let server;
let baseUrl;
const PAYMENT_PERIOD_ID = '507f1f77bcf86cd799439012';
const SUBSCRIBER_ID = '507f1f77bcf86cd799439011';
const DISTRIBUTOR_ID = '507f1f77bcf86cd799439013';

function leanQuery(result) {
  return {
    sort() {
      return this;
    },
    populate() {
      return this;
    },
    async lean() {
      return result;
    }
  };
}

test.before(async () => {
  await new Promise((resolve) => {
    server = app.listen(0, '127.0.0.1', () => {
      const address = server.address();
      baseUrl = `http://127.0.0.1:${address.port}`;
      resolve();
    });
  });
});

test.after(async () => {
  if (server) {
    await new Promise((resolve) => server.close(resolve));
  }
});

async function api(path, options) {
  const response = await fetch(`${baseUrl}${path}`, options);
  const body = await response.json();
  return { response, body };
}

test('payment period list API applies the active filter and returns a direct array', async (context) => {
  let receivedFilter;
  context.mock.method(PaymentPeriod, 'find', (filter) => {
    receivedFilter = filter;
    return {
      sort: async () => []
    };
  });

  const { response, body } = await api('/api/payment-periods?aktif=true');
  assert.equal(response.status, 200);
  assert.deepEqual(body, []);
  assert.deepEqual(receivedFilter, { aktif: true });
});

test('payment period create API returns 201 for a validated payload', async (context) => {
  context.mock.method(PaymentPeriod.prototype, 'save', async function savePeriod() {
    await this.validate();
    return this;
  });

  const { response, body } = await api('/api/payment-periods', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      ad: 'Aylık',
      gun_sayisi: 30,
      aciklama: '',
      aktif: true
    })
  });

  assert.equal(response.status, 201);
  assert.equal(body.ad, 'Aylık');
  assert.equal(body.gun_sayisi, 30);
  assert.equal(body.aktif, true);
});

test('subscriber create API accepts a referenced period and complete coordinates', async (context) => {
  context.mock.method(PaymentPeriod, 'exists', async () => ({ _id: PAYMENT_PERIOD_ID }));
  context.mock.method(Subscriber.prototype, 'save', async function saveSubscriber() {
    await this.validate();
    return this;
  });

  const { response, body } = await api('/api/subscribers', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      isim: 'Konumlu Abone',
      odeme_periyodu_id: PAYMENT_PERIOD_ID,
      konum: { enlem: 41.0082, boylam: 28.9784 }
    })
  });

  assert.equal(response.status, 201);
  assert.equal(body.odeme_periyodu_id, PAYMENT_PERIOD_ID);
  assert.deepEqual(body.konum, { enlem: 41.0082, boylam: 28.9784 });
});

test('payment period and subscriber APIs reject invalid query and nested location payloads', async () => {
  const invalidFilter = await api('/api/payment-periods?aktif=evet');
  assert.equal(invalidFilter.response.status, 400);
  assert.match(invalidFilter.body.hata, /true veya false/);

  const invalidLocation = await api('/api/subscribers', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      isim: 'Eksik Konumlu Abone',
      konum: { enlem: 41 }
    })
  });
  assert.equal(invalidLocation.response.status, 400);
  assert.match(invalidLocation.body.hata, /birlikte gönderilmelidir/);
});

test('subscriber first distributor assignment backfills only collected legacy null snapshots', async (context) => {
  const subscriber = new Subscriber({
    _id: SUBSCRIBER_ID,
    isim: 'Legacy Abone',
    distributor_id: null
  });
  let receivedFilter;
  let receivedUpdate;
  let receivedUpdateOptions;
  let receivedSaveSession;
  const session = { id: 'subscriber-assignment-session' };

  context.mock.method(mongoose.connection, 'transaction', async (operation) => operation(session));
  context.mock.method(Subscriber, 'findById', async () => subscriber);
  context.mock.method(Subscriber.prototype, 'save', async function saveSubscriber(options) {
    receivedSaveSession = options.session;
    await this.validate();
    return this;
  });
  context.mock.method(Distributor, 'findById', () => ({
    select: async () => ({ _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' })
  }));
  context.mock.method(SubscriberDailyDelivery, 'updateMany', async (filter, update, options) => {
    receivedFilter = filter;
    receivedUpdate = update;
    receivedUpdateOptions = options;
    return { modifiedCount: 2 };
  });

  const { response, body } = await api(`/api/subscribers/${SUBSCRIBER_ID}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ distributor_id: DISTRIBUTOR_ID })
  });

  assert.equal(response.status, 200);
  assert.equal(body.distributor_id, DISTRIBUTOR_ID);
  assert.equal(String(receivedFilter.subscriber_id), SUBSCRIBER_ID);
  assert.equal(receivedFilter.tahsil_edildi, true);
  assert.equal(receivedFilter.distributor_id, null);
  assert.deepEqual(receivedUpdate.$set, {
    distributor_id: DISTRIBUTOR_ID,
    distributor_adi: 'Merkez Dağıtıcı'
  });
  assert.equal(receivedSaveSession, session);
  assert.equal(receivedUpdateOptions.session, session);
});

test('subscriber reassignment never rewrites filled or null historical snapshots', async (context) => {
  const subscriber = new Subscriber({
    _id: SUBSCRIBER_ID,
    isim: 'Atanmış Abone',
    distributor_id: '507f1f77bcf86cd799439014'
  });
  let backfillCalls = 0;

  context.mock.method(
    mongoose.connection,
    'transaction',
    async (operation) => operation({ id: 'reassignment-session' })
  );
  context.mock.method(Subscriber, 'findById', async () => subscriber);
  context.mock.method(Subscriber.prototype, 'save', async function saveSubscriber() {
    await this.validate();
    return this;
  });
  context.mock.method(Distributor, 'findById', () => ({
    select: async () => ({ _id: DISTRIBUTOR_ID, isim: 'Yeni Dağıtıcı' })
  }));
  context.mock.method(SubscriberDailyDelivery, 'updateMany', async () => {
    backfillCalls += 1;
    return { modifiedCount: 0 };
  });

  const { response } = await api(`/api/subscribers/${SUBSCRIBER_ID}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ distributor_id: DISTRIBUTOR_ID })
  });

  assert.equal(response.status, 200);
  assert.equal(backfillCalls, 0);
});

test('subscriber assignment transaction propagates backfill failure for rollback', async (context) => {
  const subscriber = new Subscriber({
    _id: SUBSCRIBER_ID,
    isim: 'Rollback Abone',
    distributor_id: null
  });
  let rollbackObserved = false;

  context.mock.method(mongoose.connection, 'transaction', async (operation) => {
    try {
      return await operation({ id: 'rollback-session' });
    } catch (error) {
      rollbackObserved = true;
      throw error;
    }
  });
  context.mock.method(Subscriber, 'findById', async () => subscriber);
  context.mock.method(Subscriber.prototype, 'save', async function saveSubscriber() {
    await this.validate();
    return this;
  });
  context.mock.method(Distributor, 'findById', () => ({
    select: async () => ({ _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' })
  }));
  context.mock.method(SubscriberDailyDelivery, 'updateMany', async () => {
    const error = new Error('Legacy backfill başarısız');
    error.statusCode = 409;
    throw error;
  });

  const { response, body } = await api(`/api/subscribers/${SUBSCRIBER_ID}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ distributor_id: DISTRIBUTOR_ID })
  });

  assert.equal(response.status, 409);
  assert.match(body.hata, /backfill başarısız/);
  assert.equal(rollbackObserved, true);
});

test('payment tracking API returns separate monthly outgoing and cash totals', async (context) => {
  let receivedCollectionFilter;
  context.mock.method(Payment, 'find', () => leanQuery([
    { _id: 'payment-1', tarih: '2026-07-10', tutar: 100, durum: 'Ödendi' },
    { _id: 'payment-2', tarih: '2026-07-20', tutar: 50, durum: 'Beklemede' }
  ]));
  context.mock.method(SubscriberDailyDelivery, 'find', (filter) => {
    receivedCollectionFilter = filter;
    return leanQuery([
      {
        _id: 'collection-1',
        subscriber_id: { _id: SUBSCRIBER_ID, isim: 'Nakit Abone' },
        distributor_id: { _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' },
        distributor_adi: 'Merkez Dağıtıcı',
        tarih: '2026-07-15',
        tahsil_edildi: true,
        tutar: 75,
        odeme_yontemi: 'Nakit'
      }
    ]);
  });

  const { response, body } = await api('/api/payments/tracking?month=2026-07');

  assert.equal(response.status, 200);
  assert.equal(body.ay, '2026-07');
  assert.equal(body.distributor_id, null);
  assert.deepEqual(body.ozet, {
    dagitici_odeme_toplami: 150,
    odenen_toplami: 100,
    bekleyen_toplami: 50,
    nakit_tahsilat_toplami: 75,
    nakit_tahsilat_adedi: 1
  });
  assert.equal(receivedCollectionFilter.tahsil_edildi, true);
  assert.equal(receivedCollectionFilter.odeme_yontemi, 'Nakit');
  assert.equal(body.odemeler.length, 2);
  assert.equal(body.nakit_tahsilatlar.length, 1);
});

test('payment tracking route validates month before the generic payment id route', async () => {
  const { response, body } = await api('/api/payments/tracking?month=2026-13');
  assert.equal(response.status, 400);
  assert.match(body.hata, /geçerli bir ay/);
});

test('payment tracking distributor filter includes matching snapshots and legacy subscriber fallback only', async (context) => {
  const otherDistributorId = '507f1f77bcf86cd799439014';
  let paymentFilter;
  let collectionFilter;

  context.mock.method(Distributor, 'findById', () => ({
    select: async () => ({ _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' })
  }));
  context.mock.method(Payment, 'find', (filter) => {
    paymentFilter = filter;
    return leanQuery([]);
  });
  context.mock.method(SubscriberDailyDelivery, 'find', (filter) => {
    collectionFilter = filter;
    return leanQuery([
      {
        _id: 'legacy-matching',
        subscriber_id: {
          _id: SUBSCRIBER_ID,
          isim: 'Legacy Eşleşen',
          distributor_id: { _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' }
        },
        distributor_id: null,
        tarih: '2026-07-05',
        tahsil_edildi: true,
        tutar: 20,
        odeme_yontemi: 'Nakit'
      },
      {
        _id: 'snapshot-matching',
        subscriber_id: {
          _id: '507f1f77bcf86cd799439015',
          isim: 'Snapshot Eşleşen',
          distributor_id: { _id: otherDistributorId, isim: 'Başka Dağıtıcı' }
        },
        distributor_id: { _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' },
        distributor_adi: 'Merkez Dağıtıcı',
        tarih: '2026-07-06',
        tahsil_edildi: true,
        tutar: 30,
        odeme_yontemi: 'Nakit'
      },
      {
        _id: 'legacy-other',
        subscriber_id: {
          _id: '507f1f77bcf86cd799439016',
          isim: 'Legacy Başka',
          distributor_id: { _id: otherDistributorId, isim: 'Başka Dağıtıcı' }
        },
        distributor_id: null,
        tarih: '2026-07-07',
        tahsil_edildi: true,
        tutar: 40,
        odeme_yontemi: 'Nakit'
      }
    ]);
  });

  const { response, body } = await api(
    `/api/payments/tracking?month=2026-07&distributor_id=${DISTRIBUTOR_ID}`
  );

  assert.equal(response.status, 200);
  assert.equal(body.distributor_id, DISTRIBUTOR_ID);
  assert.equal(paymentFilter.distributor_id, DISTRIBUTOR_ID);
  assert.deepEqual(collectionFilter.$or, [
    { distributor_id: DISTRIBUTOR_ID },
    { distributor_id: null }
  ]);
  assert.equal(body.nakit_tahsilatlar.length, 2);
  assert.equal(body.ozet.nakit_tahsilat_toplami, 50);
  assert.deepEqual(
    body.nakit_tahsilatlar.map((collection) => collection._id).sort(),
    ['legacy-matching', 'snapshot-matching']
  );
});
