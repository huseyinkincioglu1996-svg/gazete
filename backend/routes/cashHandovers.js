const express = require('express');
const router = express.Router();
const CashHandover = require('../models/CashHandover');
const SubscriberDailyDelivery = require('../models/SubscriberDailyDelivery');
const { asyncHandler } = require('../utils/http');
const { hasOwn } = require('../utils/validation');
const { dateKey } = require('../utils/date');
const {
  buildCashHandoverPayload,
  buildAutomaticCashItems,
  calculateCashHandoverComponents,
  calculateCashHandoverTotal,
  parseCashHandoverDateKey,
  parseCashHandoverMonth
} = require('../services/cashHandovers');

function serializeCashHandover(record, requestedDate, automaticItems = []) {
  let serialized;
  if (!record) {
    serialized = {
      tarih: dateKey(requestedDate),
      kalemler: [],
      durum: 'Taslak',
      teslim_tarihi: null
    };
  } else {
    serialized = typeof record.toObject === 'function'
      ? record.toObject()
      : { ...record };
    delete serialized.__v;
    serialized.tarih = dateKey(serialized.tarih);
  }

  const totals = calculateCashHandoverComponents(
    serialized.kalemler || [],
    automaticItems
  );
  return {
    ...serialized,
    otomatik_kalemler: automaticItems,
    ...totals
  };
}

async function automaticCashItemsForDate(tarih) {
  const deliveries = await SubscriberDailyDelivery.find({
    tarih,
    tahsil_edildi: true,
    odeme_yontemi: 'Nakit'
  })
    .populate({ path: 'subscriber_id', select: { isim: 1 } })
    .lean();

  return buildAutomaticCashItems(deliveries);
}

router.get('/daily/:tarih', asyncHandler(async (req, res) => {
  const tarih = parseCashHandoverDateKey(req.params.tarih);
  const [handover, automaticItems] = await Promise.all([
    CashHandover.findOne({ tarih }),
    automaticCashItemsForDate(tarih)
  ]);
  res.json(serializeCashHandover(handover, tarih, automaticItems));
}));

router.put('/daily/:tarih', asyncHandler(async (req, res) => {
  const tarih = parseCashHandoverDateKey(req.params.tarih);
  const payload = buildCashHandoverPayload(req.body);
  const current = await CashHandover.findOne({ tarih })
    .select({ durum: 1, teslim_tarihi: 1 })
    .lean();

  if (hasOwn(payload, 'durum')) {
    if (payload.durum === 'Taslak') {
      payload.teslim_tarihi = null;
    } else {
      payload.teslim_tarihi =
        current?.durum === 'Teslim Edildi' && current.teslim_tarihi
          ? current.teslim_tarihi
          : new Date();
    }
  }

  const handover = await CashHandover.findOneAndUpdate(
    { tarih },
    {
      $set: payload,
      $setOnInsert: { tarih }
    },
    {
      new: true,
      upsert: true,
      runValidators: true,
      setDefaultsOnInsert: true
    }
  );

  const automaticItems = await automaticCashItemsForDate(tarih);
  res.json(serializeCashHandover(handover, tarih, automaticItems));
}));

router.get('/monthly/:month', asyncHandler(async (req, res) => {
  const range = parseCashHandoverMonth(req.params.month);
  const handovers = await CashHandover.find({
    tarih: { $gte: range.start, $lt: range.endExclusive },
    durum: 'Teslim Edildi'
  })
    .sort({ tarih: 1 })
    .select({
      tarih: 1,
      toplam: 1,
      teslim_tarihi: 1,
      kalemler: 1
    })
    .lean();

  const handoverDates = handovers.map((handover) => handover.tarih);
  const deliveries = handoverDates.length > 0
    ? await SubscriberDailyDelivery.find({
        tarih: { $in: handoverDates },
        tahsil_edildi: true,
        odeme_yontemi: 'Nakit'
      })
        .populate({ path: 'subscriber_id', select: { isim: 1 } })
        .lean()
    : [];
  const automaticItemsByDate = new Map();

  for (const delivery of deliveries) {
    const key = dateKey(delivery.tarih);
    const items = automaticItemsByDate.get(key) || [];
    items.push(...buildAutomaticCashItems([delivery]));
    automaticItemsByDate.set(key, items);
  }

  const summaries = handovers.map((handover) => {
    const automaticItems = automaticItemsByDate.get(dateKey(handover.tarih)) || [];
    const totals = calculateCashHandoverComponents(
      handover.kalemler || [],
      automaticItems
    );
    return { handover, automaticItems, totals };
  });
  const total = calculateCashHandoverTotal(
    summaries.map((summary) => ({ tutar: summary.totals.toplam }))
  );

  res.json({
    ay: range.month,
    baslangic: range.startKey,
    bitis: range.endKey,
    toplam: total,
    teslim_edilen_gun_sayisi: handovers.length,
    kayitlar: summaries.map(({ handover, automaticItems, totals }) => ({
      _id: handover._id,
      tarih: dateKey(handover.tarih),
      ...totals,
      kalem_sayisi: handover.kalemler.length + automaticItems.length,
      manuel_kalem_sayisi: handover.kalemler.length,
      otomatik_kalem_sayisi: automaticItems.length,
      teslim_tarihi: handover.teslim_tarihi
    }))
  });
}));

module.exports = router;
module.exports.serializeCashHandover = serializeCashHandover;
