const DAY_IN_MILLISECONDS = 24 * 60 * 60 * 1000;
const BUSINESS_TIME_ZONE = process.env.BUSINESS_TIME_ZONE || process.env.CRON_TIMEZONE || 'Europe/Istanbul';

const dateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: BUSINESS_TIME_ZONE,
  calendar: 'iso8601',
  numberingSystem: 'latn',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23'
});

function invalidDate(fieldName) {
  return new RangeError(`${fieldName} geçerli bir tarih olmalıdır`);
}

function isValidCalendarDate(year, month, day) {
  const candidate = new Date(Date.UTC(year, month - 1, day));
  return (
    candidate.getUTCFullYear() === year &&
    candidate.getUTCMonth() === month - 1 &&
    candidate.getUTCDate() === day
  );
}

function getBusinessDateParts(value) {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    throw invalidDate('tarih');
  }

  const parts = Object.fromEntries(
    dateTimeFormatter
      .formatToParts(date)
      .filter((part) => part.type !== 'literal')
      .map((part) => [part.type, Number(part.value)])
  );

  return {
    year: parts.year,
    month: parts.month,
    day: parts.day,
    hour: parts.hour,
    minute: parts.minute,
    second: parts.second
  };
}

/**
 * Creates an instant for a wall-clock time in the configured business time
 * zone. The small correction loop also handles zones that have DST changes.
 */
function createBusinessDate(year, month, day, hour = 0, minute = 0, second = 0) {
  let timestamp = Date.UTC(year, month - 1, day, hour, minute, second);
  const target = Date.UTC(year, month - 1, day, hour, minute, second);

  for (let attempt = 0; attempt < 3; attempt += 1) {
    const actual = getBusinessDateParts(new Date(timestamp));
    const renderedTimestamp = Date.UTC(
      actual.year,
      actual.month - 1,
      actual.day,
      actual.hour,
      actual.minute,
      actual.second
    );
    const correction = target - renderedTimestamp;
    if (correction === 0) {
      break;
    }
    timestamp += correction;
  }

  return new Date(timestamp);
}

/**
 * Converts a date-like value into an instant. Date-only ISO values represent a
 * calendar day in the Turkish business time zone rather than UTC.
 */
function toValidDate(value, fieldName = 'tarih') {
  if (value instanceof Date) {
    if (Number.isNaN(value.getTime())) {
      throw invalidDate(fieldName);
    }
    return new Date(value.getTime());
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) {
      throw invalidDate(fieldName);
    }

    const datePrefix = /^(\d{4})-(\d{2})-(\d{2})(?:$|T)/.exec(trimmed);
    if (datePrefix) {
      const [, yearText, monthText, dayText] = datePrefix;
      const year = Number(yearText);
      const month = Number(monthText);
      const day = Number(dayText);
      if (!isValidCalendarDate(year, month, day)) {
        throw invalidDate(fieldName);
      }
      if (trimmed.length === 10) {
        return createBusinessDate(year, month, day);
      }
    }

    const parsed = new Date(trimmed);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed;
    }
    throw invalidDate(fieldName);
  }

  if (typeof value === 'number' && Number.isFinite(value)) {
    const parsed = new Date(value);
    if (!Number.isNaN(parsed.getTime())) {
      return parsed;
    }
  }

  throw invalidDate(fieldName);
}

function startOfDay(value = new Date(), fieldName = 'tarih') {
  const date = toValidDate(value, fieldName);
  const { year, month, day } = getBusinessDateParts(date);
  return createBusinessDate(year, month, day);
}

/**
 * Maps the application's business calendar to 0=Pazartesi ... 6=Pazar.
 * Unlike Date#getDay(), this does not depend on the machine's local timezone.
 */
function getTurkishBusinessDay(value = new Date()) {
  const date = toValidDate(value);
  const { year, month, day } = getBusinessDateParts(date);
  return (new Date(Date.UTC(year, month - 1, day)).getUTCDay() + 6) % 7;
}

function addDays(value, days) {
  const date = startOfDay(value);
  const { year, month, day } = getBusinessDateParts(date);
  const target = new Date(Date.UTC(year, month - 1, day + days));
  return createBusinessDate(
    target.getUTCFullYear(),
    target.getUTCMonth() + 1,
    target.getUTCDate()
  );
}

function startOfWeek(value = new Date()) {
  return addDays(value, -getTurkishBusinessDay(value));
}

function startOfMonth(value = new Date()) {
  const date = toValidDate(value);
  const { year, month } = getBusinessDateParts(date);
  return createBusinessDate(year, month, 1);
}

function startOfNextMonth(value = new Date()) {
  const date = toValidDate(value);
  const { year, month } = getBusinessDateParts(date);
  const target = new Date(Date.UTC(year, month, 1));
  return createBusinessDate(target.getUTCFullYear(), target.getUTCMonth() + 1, 1);
}

function addMonthsClamped(value, monthOffset) {
  const date = startOfDay(value);
  const { year, month, day } = getBusinessDateParts(date);
  const targetMonthIndex = month - 1 + monthOffset;
  const targetYear = year + Math.floor(targetMonthIndex / 12);
  const targetMonth = ((targetMonthIndex % 12) + 12) % 12 + 1;
  const lastDayOfTargetMonth = new Date(Date.UTC(targetYear, targetMonth, 0)).getUTCDate();

  return createBusinessDate(targetYear, targetMonth, Math.min(day, lastDayOfTargetMonth));
}

function createInclusiveDateRange(startValue, endValue, startField = 'başlangıç tarihi', endField = 'bitiş tarihi') {
  const start = startOfDay(startValue, startField);
  const endInclusive = startOfDay(endValue, endField);

  if (endInclusive < start) {
    throw new RangeError('Bitiş tarihi başlangıç tarihinden önce olamaz');
  }

  return {
    start,
    endExclusive: addDays(endInclusive, 1)
  };
}

function dateKey(value) {
  const date = startOfDay(value);
  const { year, month, day } = getBusinessDateParts(date);
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

module.exports = {
  DAY_IN_MILLISECONDS,
  BUSINESS_TIME_ZONE,
  toValidDate,
  startOfDay,
  getTurkishBusinessDay,
  addDays,
  startOfWeek,
  startOfMonth,
  startOfNextMonth,
  addMonthsClamped,
  createInclusiveDateRange,
  dateKey,
  getBusinessDateParts
};
