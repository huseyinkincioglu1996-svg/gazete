const {
  DISTRIBUTOR_ZONES,
  PAYMENT_TYPES,
  DELIVERY_STATUSES,
  PAYMENT_STATUSES,
  SUBSCRIBER_PAYMENT_METHODS,
  SUBSCRIBER_NEWSPAPER_DAYS
} = require('./constants');
const { toValidDate } = require('./date');
const { HttpError } = require('./http');
const {
  ensureBodyObject,
  hasOwn,
  rejectUnknownFields,
  requiredString,
  finiteNumber,
  enumValue,
  booleanValue,
  imageDataUrl,
  dayArray,
  normalizedDate,
  objectId
} = require('./validation');

function ensureNonEmptyUpdate(payload, partial) {
  if (partial && Object.keys(payload).length === 0) {
    throw new HttpError(400, 'Güncellenecek en az bir alan gönderilmelidir');
  }
}

function buildDistributorPayload(body, { partial = false } = {}) {
  const allowed = [
    'isim',
    'adres',
    'telefon',
    'profil_gorseli',
    'bolge',
    'dagetim_gunleri',
    'odeme_tipi',
    'odeme_gunleri_hafta',
    'odeme_gunleri_ay',
    'gazete_fiyat',
    'aktif'
  ];
  rejectUnknownFields(body, allowed);
  const payload = {};

  for (const [field, maxLength] of [['isim', 120], ['adres', 500], ['telefon', 40]]) {
    if (!partial || hasOwn(body, field)) {
      payload[field] = requiredString(body[field], field, { maxLength, required: !partial });
    }
  }

  if (hasOwn(body, 'profil_gorseli')) {
    payload.profil_gorseli = imageDataUrl(body.profil_gorseli, 'profil_gorseli');
  }

  if (!partial || hasOwn(body, 'bolge')) {
    payload.bolge = enumValue(body.bolge, 'bolge', DISTRIBUTOR_ZONES);
  }
  if (hasOwn(body, 'dagetim_gunleri')) {
    payload.dagetim_gunleri = dayArray(body.dagetim_gunleri, 'dagetim_gunleri');
  }
  if (hasOwn(body, 'odeme_tipi')) {
    payload.odeme_tipi = enumValue(body.odeme_tipi, 'odeme_tipi', PAYMENT_TYPES);
  }
  if (hasOwn(body, 'odeme_gunleri_hafta')) {
    payload.odeme_gunleri_hafta = dayArray(body.odeme_gunleri_hafta, 'odeme_gunleri_hafta');
  }
  if (hasOwn(body, 'odeme_gunleri_ay')) {
    payload.odeme_gunleri_ay = dayArray(body.odeme_gunleri_ay, 'odeme_gunleri_ay', { min: 1, max: 31 });
  }
  if (hasOwn(body, 'gazete_fiyat')) {
    payload.gazete_fiyat = finiteNumber(body.gazete_fiyat, 'gazete_fiyat', { min: 0 });
  }
  if (hasOwn(body, 'aktif')) {
    payload.aktif = booleanValue(body.aktif, 'aktif');
  }

  ensureNonEmptyUpdate(payload, partial);
  return payload;
}

function buildCompanySettingsPayload(body) {
  rejectUnknownFields(body, ['firma_logosu', 'vitrin_dagitici_id']);
  const payload = {};

  if (hasOwn(body, 'firma_logosu')) {
    payload.firma_logosu = imageDataUrl(body.firma_logosu, 'firma_logosu');
  }
  if (hasOwn(body, 'vitrin_dagitici_id')) {
    payload.vitrin_dagitici_id = body.vitrin_dagitici_id === null
      ? null
      : objectId(body.vitrin_dagitici_id, 'vitrin_dagitici_id');
  }

  ensureNonEmptyUpdate(payload, true);
  return payload;
}

function buildDeliveryPayload(body, { partial = false } = {}) {
  const allowed = ['distributor_id', 'tarih', 'gun', 'gazeteSayisi', 'tutar', 'durum', 'notlar'];
  rejectUnknownFields(body, allowed);
  const payload = {};

  if (!partial || hasOwn(body, 'distributor_id')) {
    payload.distributor_id = objectId(body.distributor_id, 'distributor_id');
  }
  if (!partial || hasOwn(body, 'tarih')) {
    payload.tarih = normalizedDate(body.tarih, 'tarih');
  }
  if (hasOwn(body, 'gun')) {
    payload.gun = finiteNumber(body.gun, 'gun', { min: 0, integer: true });
    if (payload.gun > 6) {
      throw new HttpError(400, 'gun 0-6 aralığında olmalıdır');
    }
  }
  if (hasOwn(body, 'gazeteSayisi')) {
    payload.gazeteSayisi = finiteNumber(body.gazeteSayisi, 'gazeteSayisi', { min: 0 });
  }
  if (hasOwn(body, 'tutar')) {
    payload.tutar = finiteNumber(body.tutar, 'tutar', { min: 0 });
  }
  if (hasOwn(body, 'durum')) {
    payload.durum = enumValue(body.durum, 'durum', DELIVERY_STATUSES);
  }
  if (hasOwn(body, 'notlar')) {
    payload.notlar = requiredString(body.notlar, 'notlar', { maxLength: 1000, required: false });
  }

  ensureNonEmptyUpdate(payload, partial);
  return payload;
}

function buildPaymentPayload(body, { partial = false, allowStatus = false } = {}) {
  const allowed = [
    'distributor_id',
    'tutar',
    'tarih',
    'donem_baslangic',
    'donem_bitis',
    'aciklama',
    'odeme_turu'
  ];
  if (allowStatus) {
    allowed.push('durum', 'odeme_tarihi');
  }

  rejectUnknownFields(body, allowed);
  const payload = {};

  if (!partial || hasOwn(body, 'distributor_id')) {
    payload.distributor_id = objectId(body.distributor_id, 'distributor_id');
  }
  if (!partial || hasOwn(body, 'tutar')) {
    payload.tutar = finiteNumber(body.tutar, 'tutar', { min: 0 });
  }
  for (const field of ['tarih', 'donem_baslangic', 'donem_bitis']) {
    if (!partial || hasOwn(body, field)) {
      payload[field] = normalizedDate(body[field], field);
    }
  }
  if (hasOwn(body, 'aciklama')) {
    payload.aciklama = requiredString(body.aciklama, 'aciklama', { maxLength: 1000, required: false });
  }
  if (!partial || hasOwn(body, 'odeme_turu')) {
    payload.odeme_turu = enumValue(body.odeme_turu, 'odeme_turu', PAYMENT_TYPES);
  }
  if (allowStatus && hasOwn(body, 'durum')) {
    payload.durum = enumValue(body.durum, 'durum', PAYMENT_STATUSES);
  }
  if (allowStatus && hasOwn(body, 'odeme_tarihi')) {
    if (body.odeme_tarihi === null) {
      payload.odeme_tarihi = null;
    } else {
      try {
        payload.odeme_tarihi = toValidDate(body.odeme_tarihi, 'odeme_tarihi');
      } catch (error) {
        throw new HttpError(400, error.message);
      }
    }
  }

  ensureNonEmptyUpdate(payload, partial);
  return payload;
}

function validateSubscriberNewspaperDays(value) {
  if (!Array.isArray(value)) {
    throw new HttpError(400, 'gazete_gunleri bir dizi olmalıdır');
  }

  const days = value.map((day) =>
    enumValue(day, 'gazete_gunleri', SUBSCRIBER_NEWSPAPER_DAYS)
  );

  if (new Set(days).size !== days.length) {
    throw new HttpError(400, 'gazete_gunleri tekrar eden değer içeremez');
  }

  if (
    days.includes('pazar_pazartesi') &&
    (days.includes('pazar') || days.includes('pazartesi'))
  ) {
    throw new HttpError(
      400,
      'pazar_pazartesi, pazar veya pazartesi ile birlikte seçilemez'
    );
  }

  return days;
}

function buildSubscriberPayload(body, { partial = false } = {}) {
  rejectUnknownFields(body, [
    'isim',
    'telefon',
    'adres',
    'aylik_ucret',
    'notlar',
    'aktif',
    'gazete_gunleri',
    'odeme_periyodu_id',
    'distributor_id',
    'konum'
  ]);
  const payload = {};

  if (!partial || hasOwn(body, 'isim')) {
    payload.isim = requiredString(body.isim, 'isim', {
      maxLength: 160,
      required: true
    });
  }

  for (const [field, maxLength] of [
    ['telefon', 40],
    ['adres', 500],
    ['notlar', 1000]
  ]) {
    if (hasOwn(body, field)) {
      payload[field] = requiredString(body[field], field, {
        maxLength,
        required: false
      }) || '';
    }
  }

  if (hasOwn(body, 'aylik_ucret')) {
    payload.aylik_ucret = finiteNumber(body.aylik_ucret, 'aylik_ucret', { min: 0 });
  }
  if (hasOwn(body, 'aktif')) {
    payload.aktif = booleanValue(body.aktif, 'aktif');
  }
  if (hasOwn(body, 'gazete_gunleri')) {
    payload.gazete_gunleri = validateSubscriberNewspaperDays(body.gazete_gunleri);
  }
  if (hasOwn(body, 'odeme_periyodu_id')) {
    payload.odeme_periyodu_id = body.odeme_periyodu_id === null
      ? null
      : objectId(body.odeme_periyodu_id, 'odeme_periyodu_id');
  }
  if (hasOwn(body, 'distributor_id')) {
    payload.distributor_id = body.distributor_id === null
      ? null
      : objectId(body.distributor_id, 'distributor_id');
  }
  if (hasOwn(body, 'konum')) {
    if (body.konum === null) {
      payload.konum = null;
    } else {
      ensureBodyObject(body.konum);
      rejectUnknownFields(body.konum, ['enlem', 'boylam']);
      if (!hasOwn(body.konum, 'enlem') || !hasOwn(body.konum, 'boylam')) {
        throw new HttpError(400, 'konum.enlem ve konum.boylam birlikte gönderilmelidir');
      }

      const enlem = finiteNumber(body.konum.enlem, 'konum.enlem', { min: -90 });
      const boylam = finiteNumber(body.konum.boylam, 'konum.boylam', { min: -180 });
      if (enlem > 90) {
        throw new HttpError(400, 'konum.enlem -90 ile 90 arasında olmalıdır');
      }
      if (boylam > 180) {
        throw new HttpError(400, 'konum.boylam -180 ile 180 arasında olmalıdır');
      }
      payload.konum = { enlem, boylam };
    }
  }

  ensureNonEmptyUpdate(payload, partial);
  return payload;
}

function buildSubscriberStatusPayload(body) {
  rejectUnknownFields(body, ['aktif']);
  if (!hasOwn(body, 'aktif')) {
    throw new HttpError(400, 'aktif zorunludur');
  }

  return {
    aktif: booleanValue(body.aktif, 'aktif')
  };
}

function buildSubscriberDailyDeliveryPayload(body) {
  rejectUnknownFields(body, ['kayitlar']);
  if (!hasOwn(body, 'kayitlar') || !Array.isArray(body.kayitlar)) {
    throw new HttpError(400, 'kayitlar bir dizi olmalıdır');
  }

  const subscriberIds = new Set();
  const records = body.kayitlar.map((record, index) => {
    try {
      ensureBodyObject(record);
      rejectUnknownFields(record, [
        'subscriber_id',
        'teslim_edildi',
        'tahsil_edildi',
        'tutar',
        'odeme_yontemi'
      ]);

      const subscriberId = objectId(record.subscriber_id, 'subscriber_id');
      if (subscriberIds.has(subscriberId)) {
        throw new HttpError(400, 'aynı subscriber_id birden fazla gönderilemez');
      }
      subscriberIds.add(subscriberId);

      const delivery = {
        subscriber_id: subscriberId,
        teslim_edildi: booleanValue(record.teslim_edildi, 'teslim_edildi'),
        tahsil_edildi: booleanValue(record.tahsil_edildi, 'tahsil_edildi'),
        tutar: finiteNumber(record.tutar, 'tutar', { min: 0 }),
        odeme_yontemi: enumValue(
          record.odeme_yontemi,
          'odeme_yontemi',
          SUBSCRIBER_PAYMENT_METHODS
        )
      };

      if (delivery.tahsil_edildi && delivery.tutar <= 0) {
        throw new HttpError(400, 'tahsil_edildi true iken tutar 0 değerinden büyük olmalıdır');
      }

      return delivery;
    } catch (error) {
      if (error instanceof HttpError) {
        throw new HttpError(error.statusCode, `kayitlar[${index}]: ${error.message}`);
      }
      throw error;
    }
  });

  return { kayitlar: records };
}

function buildPaymentPeriodPayload(body, { partial = false } = {}) {
  rejectUnknownFields(body, ['ad', 'gun_sayisi', 'aciklama', 'aktif']);
  const payload = {};

  if (!partial || hasOwn(body, 'ad')) {
    payload.ad = requiredString(body.ad, 'ad', {
      maxLength: 120,
      required: true
    });
  }
  if (!partial || hasOwn(body, 'gun_sayisi')) {
    payload.gun_sayisi = finiteNumber(body.gun_sayisi, 'gun_sayisi', {
      min: 1,
      integer: true
    });
    if (payload.gun_sayisi > 365) {
      throw new HttpError(400, 'gun_sayisi 365 değerinden büyük olamaz');
    }
  }
  if (hasOwn(body, 'aciklama')) {
    payload.aciklama = requiredString(body.aciklama, 'aciklama', {
      maxLength: 500,
      required: false
    }) || '';
  }
  if (hasOwn(body, 'aktif')) {
    payload.aktif = booleanValue(body.aktif, 'aktif');
  }

  ensureNonEmptyUpdate(payload, partial);
  return payload;
}

function buildPaymentPeriodStatusPayload(body) {
  rejectUnknownFields(body, ['aktif']);
  if (!hasOwn(body, 'aktif')) {
    throw new HttpError(400, 'aktif zorunludur');
  }
  return { aktif: booleanValue(body.aktif, 'aktif') };
}

module.exports = {
  buildDistributorPayload,
  buildCompanySettingsPayload,
  buildDeliveryPayload,
  buildPaymentPayload,
  buildSubscriberPayload,
  buildSubscriberStatusPayload,
  buildSubscriberDailyDeliveryPayload,
  buildPaymentPeriodPayload,
  buildPaymentPeriodStatusPayload,
  validateSubscriberNewspaperDays
};
