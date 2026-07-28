const express = require('express');
const router = express.Router();
const CashHandover = require('../models/CashHandover');
const Subscriber = require('../models/Subscriber');
const SubscriberDailyDelivery = require('../models/SubscriberDailyDelivery');
const { parseCashHandoverDateKey } = require('../services/cashHandovers');
const {
  buildSubscriberDeliveryRow,
  planSubscriberDelivery,
  planSubscriberDeliveryDistributorSnapshot,
  serializeSubscriberCollection
} = require('../services/subscriberDeliveries');
const { asyncHandler, HttpError } = require('../utils/http');
const { buildSubscriberDailyDeliveryPayload } = require('../utils/payloads');
const { dateKey } = require('../utils/date');

function idKey(value) {
  return String(value?._id || value);
}

async function dailySubscriberDeliveries(tarih) {
  const [subscribers, deliveries] = await Promise.all([
    Subscriber.find({ aktif: true })
      .populate({ path: 'distributor_id', select: { isim: 1 } })
      .sort({ isim: 1 })
      .lean(),
    SubscriberDailyDelivery.find({ tarih })
      .populate([
        {
          path: 'subscriber_id',
          select: {
            isim: 1,
            gazete_gunleri: 1,
            aylik_ucret: 1,
            aktif: 1,
            distributor_id: 1
          },
          populate: { path: 'distributor_id', select: { isim: 1 } }
        },
        {
          path: 'distributor_id',
          select: { isim: 1 }
        }
      ])
      .lean()
  ]);

  const deliveryBySubscriber = new Map(
    deliveries
      .filter((delivery) => delivery.subscriber_id)
      .map((delivery) => [idKey(delivery.subscriber_id), delivery])
  );
  const usedSubscriberIds = new Set();
  const records = [];

  for (const subscriber of subscribers) {
    const subscriberId = idKey(subscriber);
    const existing = deliveryBySubscriber.get(subscriberId);
    const schedule = planSubscriberDelivery(subscriber, tarih);
    if (!schedule && !existing) {
      continue;
    }

    records.push(buildSubscriberDeliveryRow({
      record: existing,
      subscriber,
      schedule,
      tarih
    }));
    usedSubscriberIds.add(subscriberId);
  }

  for (const delivery of deliveries) {
    if (!delivery.subscriber_id) {
      continue;
    }
    const subscriberId = idKey(delivery.subscriber_id);
    if (usedSubscriberIds.has(subscriberId)) {
      continue;
    }

    const subscriber = delivery.subscriber_id;
    const schedule = subscriber.aktif
      ? planSubscriberDelivery(subscriber, tarih)
      : null;
    records.push(buildSubscriberDeliveryRow({
      record: delivery,
      subscriber,
      schedule,
      tarih
    }));
  }

  records.sort((left, right) => left.abone.localeCompare(right.abone, 'tr'));
  return {
    tarih: dateKey(tarih),
    kayitlar: records
  };
}

router.get('/daily/:tarih', asyncHandler(async (req, res) => {
  const tarih = parseCashHandoverDateKey(req.params.tarih);
  res.json(await dailySubscriberDeliveries(tarih));
}));

router.put('/daily/:tarih', asyncHandler(async (req, res) => {
  const tarih = parseCashHandoverDateKey(req.params.tarih);
  const { kayitlar } = buildSubscriberDailyDeliveryPayload(req.body);

  const closedHandover = await CashHandover.exists({
    tarih,
    durum: 'Teslim Edildi'
  });
  if (closedHandover) {
    throw new HttpError(409, 'Teslim edilmiş günlük kasa kaydı değiştirilemez');
  }

  const subscriberIds = kayitlar.map((record) => record.subscriber_id);
  const [subscribers, existingDeliveries] = await Promise.all([
    Subscriber.find({ _id: { $in: subscriberIds } })
      .populate({ path: 'distributor_id', select: { isim: 1 } })
      .lean(),
    SubscriberDailyDelivery.find({
      tarih,
      subscriber_id: { $in: subscriberIds }
    }).lean()
  ]);
  const subscriberById = new Map(
    subscribers.map((subscriber) => [idKey(subscriber), subscriber])
  );
  const existingBySubscriber = new Map(
    existingDeliveries.map((delivery) => [idKey(delivery.subscriber_id), delivery])
  );

  const preparedUpdates = kayitlar.map((record) => {
    const subscriber = subscriberById.get(record.subscriber_id);
    if (!subscriber) {
      throw new HttpError(404, `Abone bulunamadı: ${record.subscriber_id}`);
    }

    const existing = existingBySubscriber.get(record.subscriber_id);
    const schedule = subscriber.aktif
      ? planSubscriberDelivery(subscriber, tarih)
      : null;
    if (!schedule && !existing) {
      throw new HttpError(409, `${subscriber.isim} bu tarih için planlı değildir`);
    }

    const kapsananTarihler =
      schedule?.kapsanan_tarihler || existing.kapsanan_tarihler;
    const gazeteAdedi = schedule?.gazete_adedi || existing.gazete_adedi;
    const distributorSnapshot = planSubscriberDeliveryDistributorSnapshot({
      existing,
      nextTahsilEdildi: record.tahsil_edildi,
      subscriber
    });

    return {
      record,
      subscriber,
      kapsananTarihler,
      gazeteAdedi,
      distributorSnapshot
    };
  });

  const updates = preparedUpdates.map(({
    record,
    subscriber,
    kapsananTarihler,
    gazeteAdedi,
    distributorSnapshot
  }) => {
    const setFields = {
      kapsanan_tarihler: kapsananTarihler,
      gazete_adedi: gazeteAdedi,
      teslim_edildi: record.teslim_edildi,
      tahsil_edildi: record.tahsil_edildi,
      tutar: record.tutar,
      odeme_yontemi: record.odeme_yontemi
    };
    const setOnInsertFields = {
      subscriber_id: subscriber._id,
      tarih
    };

    if (distributorSnapshot.apply === 'insert') {
      setOnInsertFields.distributor_id = distributorSnapshot.distributor_id;
      setOnInsertFields.distributor_adi = distributorSnapshot.distributor_adi;
    } else if (distributorSnapshot.apply === 'update') {
      setFields.distributor_id = distributorSnapshot.distributor_id;
      setFields.distributor_adi = distributorSnapshot.distributor_adi;
    }

    return SubscriberDailyDelivery.findOneAndUpdate(
      { subscriber_id: subscriber._id, tarih },
      {
        $set: setFields,
        $setOnInsert: setOnInsertFields
      },
      {
        new: true,
        upsert: true,
        runValidators: true,
        setDefaultsOnInsert: true
      }
    );
  }
  );

  await Promise.all(updates);
  res.json(await dailySubscriberDeliveries(tarih));
}));

router.get('/collections', asyncHandler(async (req, res) => {
  const collections = await SubscriberDailyDelivery.find({ tahsil_edildi: true })
    .sort({ tarih: -1, createdAt: -1 })
    .populate([
      {
        path: 'subscriber_id',
        select: { isim: 1, distributor_id: 1 },
        populate: { path: 'distributor_id', select: { isim: 1 } }
      },
      {
        path: 'distributor_id',
        select: { isim: 1 }
      }
    ])
    .lean();

  res.json(collections.map(serializeSubscriberCollection));
}));

module.exports = router;
module.exports.dailySubscriberDeliveries = dailySubscriberDeliveries;
