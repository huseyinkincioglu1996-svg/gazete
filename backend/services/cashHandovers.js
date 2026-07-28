const { CASH_HANDOVER_STATUSES } = require('../utils/constants');
const {
  addDays,
  dateKey,
  startOfDay,
  startOfNextMonth
} = require('../utils/date');
const { HttpError } = require('../utils/http');
const {
  ensureBodyObject,
  enumValue,
  finiteNumber,
  hasOwn,
  rejectUnknownFields,
  requiredString
} = require('../utils/validation');

const DATE_KEY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const MONTH_KEY_PATTERN = /^(\d{4})-(\d{2})$/;

function roundCurrency(value) {
  const floatingPointCorrection =
    Number.EPSILON * Math.max(1, Math.abs(value));
  return Math.round((value + floatingPointCorrection) * 100) / 100;
}

function calculateCashHandoverTotal(items) {
  if (!Array.isArray(items)) {
    throw new TypeError('kalemler bir dizi olmalıdır');
  }

  const total = items.reduce((sum, item) => {
    const amount = item && Number(item.tutar);
    if (!Number.isFinite(amount) || amount < 0) {
      throw new RangeError('Kalem tutarı negatif olmayan geçerli bir sayı olmalıdır');
    }
    return sum + amount;
  }, 0);

  if (!Number.isFinite(total)) {
    throw new RangeError('Kasa toplamı geçerli sayı sınırını aşıyor');
  }

  return roundCurrency(total);
}

function buildAutomaticCashItems(deliveries) {
  if (!Array.isArray(deliveries)) {
    throw new TypeError('teslimatlar bir dizi olmalıdır');
  }

  return deliveries
    .filter(
      (delivery) =>
        delivery?.tahsil_edildi === true &&
        delivery?.odeme_yontemi === 'Nakit'
    )
    .map((delivery) => {
      const subscriber =
        delivery.subscriber_id &&
        typeof delivery.subscriber_id === 'object' &&
        delivery.subscriber_id.isim !== undefined
          ? delivery.subscriber_id
          : null;

      return {
        abone: subscriber?.isim || 'Bilinmeyen abone',
        tutar: delivery.tutar,
        aciklama: 'Günlük abone tahsilatı',
        otomatik: true,
        kaynak_id: delivery._id,
        odeme_yontemi: delivery.odeme_yontemi
      };
    });
}

function calculateCashHandoverComponents(manualItems, automaticItems) {
  const manuelToplam = calculateCashHandoverTotal(manualItems);
  const otomatikToplam = calculateCashHandoverTotal(automaticItems);

  return {
    manuel_toplam: manuelToplam,
    otomatik_toplam: otomatikToplam,
    toplam: roundCurrency(manuelToplam + otomatikToplam)
  };
}

function parseCashHandoverDateKey(value) {
  if (typeof value !== 'string' || !DATE_KEY_PATTERN.test(value)) {
    throw new HttpError(400, 'tarih YYYY-MM-DD biçiminde olmalıdır');
  }

  let date;
  try {
    date = startOfDay(value, 'tarih');
  } catch (error) {
    throw new HttpError(400, error.message);
  }

  if (dateKey(date) !== value) {
    throw new HttpError(400, 'tarih geçerli bir takvim günü olmalıdır');
  }

  return date;
}

function parseCashHandoverMonth(value) {
  const match = typeof value === 'string' && MONTH_KEY_PATTERN.exec(value);
  if (!match) {
    throw new HttpError(400, 'month YYYY-MM biçiminde olmalıdır');
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  if (year < 1000 || month < 1 || month > 12) {
    throw new HttpError(400, 'month geçerli bir ay olmalıdır');
  }

  const start = parseCashHandoverDateKey(`${value}-01`);
  const endExclusive = startOfNextMonth(start);

  return {
    month: value,
    start,
    endExclusive,
    startKey: dateKey(start),
    endKey: dateKey(addDays(endExclusive, -1))
  };
}

function buildCashHandoverPayload(body) {
  rejectUnknownFields(body, ['kalemler', 'durum']);
  const payload = {};

  if (hasOwn(body, 'kalemler')) {
    if (!Array.isArray(body.kalemler)) {
      throw new HttpError(400, 'kalemler bir dizi olmalıdır');
    }

    payload.kalemler = body.kalemler.map((item, index) => {
      try {
        ensureBodyObject(item);
        rejectUnknownFields(item, ['abone', 'tutar', 'aciklama']);

        return {
          abone: requiredString(item.abone, 'abone', { maxLength: 200 }),
          tutar: finiteNumber(item.tutar, 'tutar', { min: 0 }),
          aciklama: hasOwn(item, 'aciklama')
            ? (requiredString(item.aciklama, 'aciklama', {
                maxLength: 1000,
                required: false
              }) || '')
            : ''
        };
      } catch (error) {
        if (error instanceof HttpError) {
          throw new HttpError(error.statusCode, `kalemler[${index}]: ${error.message}`);
        }
        throw error;
      }
    });
    payload.toplam = calculateCashHandoverTotal(payload.kalemler);
  }

  if (hasOwn(body, 'durum')) {
    payload.durum = enumValue(body.durum, 'durum', CASH_HANDOVER_STATUSES);
  }

  if (Object.keys(payload).length === 0) {
    throw new HttpError(400, 'kalemler veya durum alanlarından en az biri gönderilmelidir');
  }

  return payload;
}

module.exports = {
  buildCashHandoverPayload,
  buildAutomaticCashItems,
  calculateCashHandoverComponents,
  calculateCashHandoverTotal,
  parseCashHandoverDateKey,
  parseCashHandoverMonth,
  roundCurrency
};
