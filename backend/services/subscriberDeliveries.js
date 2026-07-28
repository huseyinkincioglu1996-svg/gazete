const {
  addDays,
  dateKey,
  getTurkishBusinessDay,
  startOfDay
} = require('../utils/date');

const STANDARD_DAY_KEYS = Object.freeze([
  'pazartesi',
  'sali',
  'carsamba',
  'persembe',
  'cuma',
  'cumartesi',
  'pazar'
]);

function planSubscriberDelivery(subscriber, value) {
  const tarih = startOfDay(value, 'tarih');
  const newspaperDays = Array.isArray(subscriber?.gazete_gunleri)
    ? subscriber.gazete_gunleri
    : [];

  if (newspaperDays.length === 0) {
    return {
      planli: false,
      kapsanan_tarihler: [tarih],
      gazete_adedi: 1
    };
  }

  const businessDay = getTurkishBusinessDay(tarih);
  if (businessDay === 0 && newspaperDays.includes('pazar_pazartesi')) {
    return {
      planli: true,
      kapsanan_tarihler: [addDays(tarih, -1), tarih],
      gazete_adedi: 2
    };
  }

  if (!newspaperDays.includes(STANDARD_DAY_KEYS[businessDay])) {
    return null;
  }

  return {
    planli: true,
    kapsanan_tarihler: [tarih],
    gazete_adedi: 1
  };
}

function populatedSubscriber(value) {
  return value && typeof value === 'object' && value.isim !== undefined
    ? value
    : null;
}

function populatedDistributor(value) {
  return value && typeof value === 'object' && value.isim !== undefined
    ? value
    : null;
}

function currentSubscriberDistributor(subscriber) {
  const distributor = subscriber?.distributor_id;
  if (!distributor) {
    return {
      distributor_id: null,
      distributor_adi: ''
    };
  }

  const populated = populatedDistributor(distributor);
  return {
    distributor_id: populated?._id || distributor?._id || distributor,
    distributor_adi: populated?.isim || ''
  };
}

function resolveSubscriberDeliveryDistributor(record, subscriber) {
  const storedDistributor = record?.distributor_id;
  if (storedDistributor) {
    const populated = populatedDistributor(storedDistributor);
    return {
      distributor_id: populated?._id || storedDistributor?._id || storedDistributor,
      distributor_adi:
        record.distributor_adi ||
        populated?.isim ||
        ''
    };
  }

  const sourceSubscriber = subscriber || populatedSubscriber(record?.subscriber_id);
  return currentSubscriberDistributor(sourceSubscriber);
}

function planSubscriberDeliveryDistributorSnapshot({
  existing,
  nextTahsilEdildi,
  subscriber
}) {
  const snapshot = currentSubscriberDistributor(subscriber);

  if (!existing) {
    return { apply: 'insert', ...snapshot };
  }
  if (existing.distributor_id) {
    return { apply: null, distributor_id: null, distributor_adi: '' };
  }
  if (!existing.tahsil_edildi && nextTahsilEdildi && snapshot.distributor_id) {
    return { apply: 'update', ...snapshot };
  }

  return { apply: null, distributor_id: null, distributor_adi: '' };
}

function buildSubscriberDeliveryRow({
  record,
  subscriber,
  schedule,
  tarih
}) {
  const sourceSubscriber = subscriber || populatedSubscriber(record?.subscriber_id);
  const subscriberId = sourceSubscriber?._id || record?.subscriber_id;
  const coverage =
    record?.kapsanan_tarihler ||
    schedule?.kapsanan_tarihler ||
    [startOfDay(tarih, 'tarih')];
  const distributor = resolveSubscriberDeliveryDistributor(record, sourceSubscriber);

  const row = {
    subscriber_id: subscriberId,
    abone: sourceSubscriber?.isim || 'Bilinmeyen abone',
    gazete_gunleri: sourceSubscriber?.gazete_gunleri || [],
    planli: Boolean(schedule?.planli),
    kapsanan_tarihler: coverage.map((date) => dateKey(date)),
    gazete_adedi: record?.gazete_adedi || schedule?.gazete_adedi || 1,
    teslim_edildi: record?.teslim_edildi || false,
    tahsil_edildi: record?.tahsil_edildi || false,
    tutar: record?.tutar ?? sourceSubscriber?.aylik_ucret ?? 0,
    odeme_yontemi: record?.odeme_yontemi || 'Nakit',
    distributor_id: distributor.distributor_id,
    dagitici: distributor.distributor_adi
  };

  if (record?._id) {
    row._id = record._id;
  }

  return row;
}

function serializeSubscriberCollection(record) {
  const subscriber = populatedSubscriber(record.subscriber_id);
  const distributor = resolveSubscriberDeliveryDistributor(record, subscriber);

  return {
    _id: record._id,
    subscriber_id: subscriber?._id || record.subscriber_id,
    abone: subscriber?.isim || 'Bilinmeyen abone',
    tarih: dateKey(record.tarih),
    tutar: record.tutar,
    odeme_yontemi: record.odeme_yontemi,
    distributor_id: distributor.distributor_id,
    dagitici: distributor.distributor_adi,
    durum: 'Tahsil Edildi',
    kaynak: 'Dağıtımlar'
  };
}

module.exports = {
  STANDARD_DAY_KEYS,
  buildSubscriberDeliveryRow,
  currentSubscriberDistributor,
  planSubscriberDelivery,
  planSubscriberDeliveryDistributorSnapshot,
  resolveSubscriberDeliveryDistributor,
  serializeSubscriberCollection
};
