const test = require('node:test');
const assert = require('node:assert/strict');

const SubscriberDailyDelivery = require('../models/SubscriberDailyDelivery');
const {
  buildSubscriberDeliveryRow,
  planSubscriberDelivery,
  planSubscriberDeliveryDistributorSnapshot,
  resolveSubscriberDeliveryDistributor,
  serializeSubscriberCollection
} = require('../services/subscriberDeliveries');
const { buildSubscriberDailyDeliveryPayload } = require('../utils/payloads');
const { dateKey } = require('../utils/date');

const SUBSCRIBER_ID = '507f1f77bcf86cd799439011';
const DISTRIBUTOR_ID = '507f1f77bcf86cd799439013';
const OTHER_DISTRIBUTOR_ID = '507f1f77bcf86cd799439014';

test('subscriber delivery scheduling handles standard and combined Sunday-Monday plans', () => {
  const monday = '2026-07-27';
  const sunday = '2026-07-26';
  const combinedSubscriber = { gazete_gunleri: ['pazar_pazartesi'] };

  const combinedMonday = planSubscriberDelivery(combinedSubscriber, monday);
  assert.equal(combinedMonday.planli, true);
  assert.equal(combinedMonday.gazete_adedi, 2);
  assert.deepEqual(
    combinedMonday.kapsanan_tarihler.map(dateKey),
    ['2026-07-26', '2026-07-27']
  );
  assert.equal(planSubscriberDelivery(combinedSubscriber, sunday), null);

  const standardSunday = planSubscriberDelivery({ gazete_gunleri: ['pazar'] }, sunday);
  assert.equal(standardSunday.gazete_adedi, 1);
  assert.deepEqual(standardSunday.kapsanan_tarihler.map(dateKey), ['2026-07-26']);

  const general = planSubscriberDelivery({ gazete_gunleri: [] }, monday);
  assert.equal(general.planli, false);
  assert.equal(general.gazete_adedi, 1);
});

test('combined Sunday-Monday coverage crosses month boundaries in the Istanbul calendar', () => {
  const schedule = planSubscriberDelivery(
    { gazete_gunleri: ['pazar_pazartesi'] },
    '2026-06-01'
  );

  assert.deepEqual(
    schedule.kapsanan_tarihler.map(dateKey),
    ['2026-05-31', '2026-06-01']
  );
});

test('subscriber delivery payload is strict and enforces collection invariants', () => {
  assert.deepEqual(
    buildSubscriberDailyDeliveryPayload({
      kayitlar: [{
        subscriber_id: SUBSCRIBER_ID,
        teslim_edildi: true,
        tahsil_edildi: true,
        tutar: '125.50',
        odeme_yontemi: 'Nakit'
      }]
    }),
    {
      kayitlar: [{
        subscriber_id: SUBSCRIBER_ID,
        teslim_edildi: true,
        tahsil_edildi: true,
        tutar: 125.5,
        odeme_yontemi: 'Nakit'
      }]
    }
  );

  assert.throws(
    () => buildSubscriberDailyDeliveryPayload({
      kayitlar: [{
        subscriber_id: SUBSCRIBER_ID,
        teslim_edildi: true,
        tahsil_edildi: true,
        tutar: 0,
        odeme_yontemi: 'Nakit'
      }]
    }),
    /tutar 0 değerinden büyük/
  );
  assert.throws(
    () => buildSubscriberDailyDeliveryPayload({
      kayitlar: [{
        subscriber_id: SUBSCRIBER_ID,
        teslim_edildi: false,
        tahsil_edildi: false,
        tutar: 0,
        odeme_yontemi: 'Çek'
      }]
    }),
    /odeme_yontemi geçerli/
  );
  assert.throws(
    () => buildSubscriberDailyDeliveryPayload({
      kayitlar: [
        {
          subscriber_id: SUBSCRIBER_ID,
          teslim_edildi: false,
          tahsil_edildi: false,
          tutar: 0,
          odeme_yontemi: 'Kart'
        },
        {
          subscriber_id: SUBSCRIBER_ID,
          teslim_edildi: true,
          tahsil_edildi: false,
          tutar: 0,
          odeme_yontemi: 'Kart'
        }
      ]
    }),
    /aynı subscriber_id/
  );
  assert.throws(
    () => buildSubscriberDailyDeliveryPayload({
      kayitlar: [{
        subscriber_id: SUBSCRIBER_ID,
        distributor_id: DISTRIBUTOR_ID,
        teslim_edildi: true,
        tahsil_edildi: true,
        tutar: 100,
        odeme_yontemi: 'Nakit'
      }]
    }),
    /İzin verilmeyen alanlar: distributor_id/
  );
});

test('subscriber delivery model normalizes dates and rejects inconsistent or zero-value collections', async () => {
  const valid = new SubscriberDailyDelivery({
    subscriber_id: SUBSCRIBER_ID,
    tarih: '2026-07-27',
    kapsanan_tarihler: ['2026-07-26', '2026-07-27'],
    gazete_adedi: 2,
    teslim_edildi: true,
    tahsil_edildi: true,
    tutar: 100,
    odeme_yontemi: 'Havale/EFT'
  });
  await valid.validate();
  assert.equal(dateKey(valid.tarih), '2026-07-27');
  assert.deepEqual(valid.kapsanan_tarihler.map(dateKey), ['2026-07-26', '2026-07-27']);

  const zeroCollection = new SubscriberDailyDelivery({
    subscriber_id: SUBSCRIBER_ID,
    tarih: '2026-07-27',
    kapsanan_tarihler: ['2026-07-27'],
    gazete_adedi: 1,
    tahsil_edildi: true,
    tutar: 0
  });
  await assert.rejects(() => zeroCollection.validate(), /tutar 0 değerinden büyük/);

  const inconsistentCoverage = new SubscriberDailyDelivery({
    subscriber_id: SUBSCRIBER_ID,
    tarih: '2026-07-27',
    kapsanan_tarihler: ['2026-07-27'],
    gazete_adedi: 2
  });
  await assert.rejects(
    () => inconsistentCoverage.validate(),
    /gazete_adedi ile aynı/
  );
});

test('daily and collection serializers expose stable frontend-friendly shapes', () => {
  const subscriber = {
    _id: SUBSCRIBER_ID,
    isim: 'Ayşe Abone',
    aylik_ucret: 300,
    gazete_gunleri: []
  };
  const schedule = planSubscriberDelivery(subscriber, '2026-07-27');
  const row = buildSubscriberDeliveryRow({
    subscriber,
    schedule,
    tarih: '2026-07-27'
  });

  assert.equal(row.abone, 'Ayşe Abone');
  assert.equal(row.planli, false);
  assert.equal(row.tutar, 300);
  assert.equal(row.odeme_yontemi, 'Nakit');

  assert.deepEqual(
    serializeSubscriberCollection({
      _id: 'delivery-id',
      subscriber_id: subscriber,
      tarih: '2026-07-27',
      tutar: 300,
      odeme_yontemi: 'Kart'
    }),
    {
      _id: 'delivery-id',
      subscriber_id: SUBSCRIBER_ID,
      abone: 'Ayşe Abone',
      tarih: '2026-07-27',
      tutar: 300,
      odeme_yontemi: 'Kart',
      distributor_id: null,
      dagitici: '',
      durum: 'Tahsil Edildi',
      kaynak: 'Dağıtımlar'
    }
  );
});

test('delivery distributor snapshots are server-derived and preserve filled history', () => {
  const subscriber = {
    _id: SUBSCRIBER_ID,
    isim: 'Ayşe Abone',
    distributor_id: { _id: DISTRIBUTOR_ID, isim: 'Merkez Dağıtıcı' }
  };

  assert.deepEqual(
    planSubscriberDeliveryDistributorSnapshot({
      existing: null,
      nextTahsilEdildi: false,
      subscriber
    }),
    {
      apply: 'insert',
      distributor_id: DISTRIBUTOR_ID,
      distributor_adi: 'Merkez Dağıtıcı'
    }
  );
  assert.deepEqual(
    planSubscriberDeliveryDistributorSnapshot({
      existing: {
        tahsil_edildi: false,
        distributor_id: null
      },
      nextTahsilEdildi: true,
      subscriber
    }),
    {
      apply: 'update',
      distributor_id: DISTRIBUTOR_ID,
      distributor_adi: 'Merkez Dağıtıcı'
    }
  );
  assert.equal(
    planSubscriberDeliveryDistributorSnapshot({
      existing: {
        tahsil_edildi: false,
        distributor_id: OTHER_DISTRIBUTOR_ID,
        distributor_adi: 'Eski Dağıtıcı'
      },
      nextTahsilEdildi: true,
      subscriber
    }).apply,
    null
  );
  assert.deepEqual(
    resolveSubscriberDeliveryDistributor(
      {
        distributor_id: OTHER_DISTRIBUTOR_ID,
        distributor_adi: 'Eski Dağıtıcı'
      },
      subscriber
    ),
    {
      distributor_id: OTHER_DISTRIBUTOR_ID,
      distributor_adi: 'Eski Dağıtıcı'
    }
  );
});

test('legacy null delivery distributor falls back to the subscriber current assignment', () => {
  const subscriber = {
    _id: SUBSCRIBER_ID,
    isim: 'Legacy Abone',
    distributor_id: { _id: DISTRIBUTOR_ID, isim: 'Güncel Dağıtıcı' }
  };
  const collection = serializeSubscriberCollection({
    _id: 'legacy-delivery',
    subscriber_id: subscriber,
    distributor_id: null,
    distributor_adi: '',
    tarih: '2026-07-27',
    tutar: 125,
    odeme_yontemi: 'Nakit'
  });

  assert.equal(collection.distributor_id, DISTRIBUTOR_ID);
  assert.equal(collection.dagitici, 'Güncel Dağıtıcı');
});

test('subscriber delivery model exposes the distributor tracking index', () => {
  const indexes = SubscriberDailyDelivery.schema.indexes();
  assert.ok(indexes.some(([fields]) => (
    fields.distributor_id === 1 &&
    fields.tarih === -1 &&
    fields.tahsil_edildi === 1 &&
    fields.odeme_yontemi === 1
  )));
});
