const DISTRIBUTOR_ZONES = Object.freeze(['Bölge 1', 'Bölge 2']);
const PAYMENT_TYPES = Object.freeze(['Günlük', 'Haftalık', 'Aylık']);
const DELIVERY_STATUSES = Object.freeze(['Beklemede', 'Tamamlandı', 'İptal']);
const PAYMENT_STATUSES = Object.freeze(['Beklemede', 'Ödendi']);
const CASH_HANDOVER_STATUSES = Object.freeze(['Taslak', 'Teslim Edildi']);
const SUBSCRIBER_PAYMENT_METHODS = Object.freeze(['Nakit', 'Kart', 'Havale/EFT']);
const SUBSCRIBER_NEWSPAPER_DAYS = Object.freeze([
  'pazartesi',
  'sali',
  'carsamba',
  'persembe',
  'cuma',
  'cumartesi',
  'pazar',
  'pazar_pazartesi'
]);

// İş kuralı: 0 = Pazartesi, 1 = Salı, ... 6 = Pazar.
const BUSINESS_DAY_NAMES = Object.freeze([
  'Pazartesi',
  'Salı',
  'Çarşamba',
  'Perşembe',
  'Cuma',
  'Cumartesi',
  'Pazar'
]);

module.exports = {
  DISTRIBUTOR_ZONES,
  PAYMENT_TYPES,
  DELIVERY_STATUSES,
  PAYMENT_STATUSES,
  CASH_HANDOVER_STATUSES,
  SUBSCRIBER_PAYMENT_METHODS,
  SUBSCRIBER_NEWSPAPER_DAYS,
  BUSINESS_DAY_NAMES
};
