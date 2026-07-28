import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import api from '../api';
import './Deliveries.css';

const PAYMENT_METHODS = ['Nakit', 'Kart', 'Havale/EFT'];

const getToday = () => {
  const today = new Date();
  const offset = today.getTimezoneOffset();
  return new Date(today.getTime() - offset * 60 * 1000).toISOString().slice(0, 10);
};

const toLocalDate = (value) => {
  if (!value) return null;
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? new Date(`${value}T12:00:00`)
    : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
};

const formatDate = (value) => {
  const date = toLocalDate(value);
  return date ? date.toLocaleDateString('tr-TR', { dateStyle: 'long' }) : '—';
};

const formatShortDate = (value) => {
  const date = toLocalDate(value);
  return date
    ? date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'short' })
    : '';
};

const formatCurrency = (value) =>
  new Intl.NumberFormat('tr-TR', {
    style: 'currency',
    currency: 'TRY',
    minimumFractionDigits: 2,
  }).format(Number(value) || 0);

const getErrorMessage = (error, fallback) =>
  error.response?.data?.hata
  || error.response?.data?.mesaj
  || error.response?.data?.message
  || fallback;

const normalizeRecord = (record, index) => {
  const subscriber = typeof record.subscriber_id === 'object' && record.subscriber_id
    ? record.subscriber_id
    : null;
  const distributorFromLabel = typeof record.dagitici === 'object' && record.dagitici
    ? record.dagitici
    : null;
  const subscriberDistributor = typeof subscriber?.distributor_id === 'object'
    && subscriber.distributor_id
    ? subscriber.distributor_id
    : null;
  const distributor = typeof record.distributor_id === 'object' && record.distributor_id
    ? record.distributor_id
    : distributorFromLabel || subscriberDistributor;
  const subscriberId = subscriber?._id
    || record.subscriber_id
    || record.abone_id
    || record.subscriber?.id
    || '';
  const distributorId = distributor?._id
    || record.distributor_id
    || subscriberDistributor?._id
    || subscriber?.distributor_id
    || '';
  const distributorName = typeof record.dagitici === 'string'
    ? record.dagitici
    : distributor?.isim || record.dagitici_adi || 'Atanmamış';
  const amount = Number(record.tutar ?? record.varsayilan_tutar ?? subscriber?.aylik_ucret ?? 0);
  const normalizedAmount = Number.isFinite(amount) && amount >= 0 ? amount : 0;
  const paymentMethod = PAYMENT_METHODS.includes(record.odeme_yontemi)
    ? record.odeme_yontemi
    : 'Nakit';

  return {
    clientId: record._id || `${subscriberId || 'subscriber'}-${index}`,
    subscriber_id: String(subscriberId),
    abone: record.abone || subscriber?.isim || record.isim || 'İsimsiz abone',
    distributor_id: distributorId ? String(distributorId) : '',
    dagitici: distributorName,
    gazete_gunleri: Array.isArray(record.gazete_gunleri) ? record.gazete_gunleri : [],
    planli: record.planli !== false,
    kapsanan_tarihler: Array.isArray(record.kapsanan_tarihler)
      ? record.kapsanan_tarihler
      : [],
    gazete_adedi: Math.max(1, Number(record.gazete_adedi) || 1),
    teslim_edildi: Boolean(record.teslim_edildi),
    tahsil_edildi: Boolean(record.tahsil_edildi),
    tutar: String(normalizedAmount),
    varsayilan_tutar: normalizedAmount,
    odeme_yontemi: paymentMethod,
  };
};

const toPayloadRecords = (records) => records.map((record) => ({
  subscriber_id: record.subscriber_id,
  teslim_edildi: Boolean(record.teslim_edildi),
  tahsil_edildi: Boolean(record.tahsil_edildi),
  tutar: Number(String(record.tutar).replace(',', '.')) || 0,
  odeme_yontemi: PAYMENT_METHODS.includes(record.odeme_yontemi)
    ? record.odeme_yontemi
    : 'Nakit',
}));

const getPayloadSignature = (records) => JSON.stringify(toPayloadRecords(records));

function Deliveries() {
  const [selectedDate, setSelectedDate] = useState(getToday);
  const [records, setRecords] = useState([]);
  const [baselineSignature, setBaselineSignature] = useState('[]');
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const requestId = useRef(0);

  const currentSignature = useMemo(() => getPayloadSignature(records), [records]);
  const hasChanges = currentSignature !== baselineSignature;

  const summary = useMemo(() => {
    const delivered = records.filter((record) => record.teslim_edildi).length;
    const collected = records
      .filter((record) => record.tahsil_edildi)
      .reduce((total, record) => {
        const amount = Number(String(record.tutar).replace(',', '.'));
        return total + (Number.isFinite(amount) ? amount : 0);
      }, 0);

    return {
      listed: records.length,
      delivered,
      collected,
      collectedCount: records.filter((record) => record.tahsil_edildi).length,
    };
  }, [records]);

  const applyDailyData = useCallback((data) => {
    const rawRecords = Array.isArray(data)
      ? data
      : Array.isArray(data?.kayitlar)
        ? data.kayitlar
        : [];
    const normalizedRecords = rawRecords.map(normalizeRecord);
    setRecords(normalizedRecords);
    setBaselineSignature(getPayloadSignature(normalizedRecords));
  }, []);

  const loadDaily = useCallback(async (date, { showLoading = true } = {}) => {
    const nextRequestId = requestId.current + 1;
    requestId.current = nextRequestId;
    if (showLoading) setLoading(true);
    setLoadFailed(false);
    setError('');

    try {
      const response = await api.get(`/api/subscriber-deliveries/daily/${date}`);
      if (requestId.current === nextRequestId) applyDailyData(response.data);
    } catch (requestError) {
      if (requestId.current !== nextRequestId) return;
      setRecords([]);
      setBaselineSignature('[]');
      setLoadFailed(true);
      setError(
        getErrorMessage(requestError, 'Günlük abone listesi yüklenirken bir hata oluştu.'),
      );
    } finally {
      if (requestId.current === nextRequestId && showLoading) setLoading(false);
    }
  }, [applyDailyData]);

  useEffect(() => {
    setNotice('');
    loadDaily(selectedDate);
  }, [loadDaily, selectedDate]);

  const handleDateChange = (event) => {
    const nextDate = event.target.value;
    if (!nextDate || nextDate === selectedDate) return;
    if (
      hasChanges
      && !window.confirm('Kaydedilmemiş değişiklikler silinecek. Tarihi değiştirmek istiyor musunuz?')
    ) {
      return;
    }

    setRecords([]);
    setBaselineSignature('[]');
    setLoading(true);
    requestId.current += 1;
    setSelectedDate(nextDate);
  };

  const updateRecord = (clientId, changes) => {
    setRecords((currentRecords) => currentRecords.map(
      (record) => (record.clientId === clientId ? { ...record, ...changes } : record),
    ));
    setError('');
    setNotice('');
  };

  const handlePaymentToggle = (record, checked) => {
    const currentAmount = Number(String(record.tutar).replace(',', '.'));
    const nextAmount = checked && (!Number.isFinite(currentAmount) || currentAmount <= 0)
      ? record.varsayilan_tutar
      : record.tutar;

    updateRecord(record.clientId, {
      tahsil_edildi: checked,
      tutar: String(nextAmount),
      odeme_yontemi: PAYMENT_METHODS.includes(record.odeme_yontemi)
        ? record.odeme_yontemi
        : 'Nakit',
    });
  };

  const validateRecords = () => {
    for (const record of records) {
      if (!record.subscriber_id) {
        return `${record.abone} için abone kimliği bulunamadı. Listeyi yenileyin.`;
      }

      if (record.tahsil_edildi) {
        const amount = Number(String(record.tutar).replace(',', '.'));
        if (!Number.isFinite(amount) || amount <= 0) {
          return `${record.abone} için tahsil edilen tutar sıfırdan büyük olmalıdır.`;
        }
        if (!PAYMENT_METHODS.includes(record.odeme_yontemi)) {
          return `${record.abone} için geçerli bir ödeme yöntemi seçin.`;
        }
      }
    }
    return '';
  };

  const handleSave = async () => {
    const validationError = validateRecords();
    setError('');
    setNotice('');

    if (validationError) {
      setError(validationError);
      return;
    }

    setSaving(true);
    try {
      const response = await api.put(`/api/subscriber-deliveries/daily/${selectedDate}`, {
        kayitlar: toPayloadRecords(records),
      });

      if (Array.isArray(response.data?.kayitlar) || Array.isArray(response.data)) {
        applyDailyData(response.data);
      } else {
        await loadDaily(selectedDate, { showLoading: false });
      }
      setNotice('Günlük dağıtım değişiklikleri kaydedildi.');
    } catch (requestError) {
      setError(
        getErrorMessage(requestError, 'Günlük dağıtım değişiklikleri kaydedilemedi.'),
      );
    } finally {
      setSaving(false);
    }
  };

  const getCoverageText = (record) => {
    const coveredDates = record.kapsanan_tarihler.map(formatShortDate).filter(Boolean);
    if (coveredDates.length) return coveredDates.join(' + ');
    if (record.gazete_gunleri.includes('pazar_pazartesi')) return 'Pazar + Pazartesi';
    return record.planli ? 'Seçili gün' : 'Plan dışı';
  };

  return (
    <div className="daily-deliveries">
      <header className="daily-delivery-heading">
        <div>
          <h1>Günlük Dağıtımlar</h1>
          <p>Abonelerin gazete teslimini ve o gün alınan ödemeleri birlikte kaydedin.</p>
        </div>
        <div className="daily-date-control">
          <label htmlFor="daily-delivery-date">Dağıtım tarihi</label>
          <input
            id="daily-delivery-date"
            type="date"
            required
            value={selectedDate}
            onChange={handleDateChange}
            disabled={saving}
          />
        </div>
      </header>

      {error && (
        <div className="daily-delivery-feedback daily-delivery-feedback-error" role="alert">
          <span>{error}</span>
          {loadFailed && (
            <button type="button" onClick={() => loadDaily(selectedDate)} disabled={loading}>
              Tekrar dene
            </button>
          )}
        </div>
      )}
      {notice && (
        <div className="daily-delivery-feedback daily-delivery-feedback-success" role="status">
          {notice}
        </div>
      )}

      <section className="daily-delivery-summary" aria-label="Günlük dağıtım özeti">
        <article className="daily-summary-card delivered">
          <span>Teslim edilen</span>
          <strong>{loading ? '—' : summary.delivered}</strong>
          <small>{loading ? 'Liste yükleniyor' : `${summary.listed} aboneden`}</small>
        </article>
        <article className="daily-summary-card collected">
          <span>Tahsil edilen toplam</span>
          <strong>{loading ? '—' : formatCurrency(summary.collected)}</strong>
          <small>{loading ? 'Liste yükleniyor' : `${summary.collectedCount} ödeme alındı`}</small>
        </article>
        <article className="daily-summary-card listed">
          <span>Listelenen abone</span>
          <strong>{loading ? '—' : summary.listed}</strong>
          <small>{formatDate(selectedDate)}</small>
        </article>
      </section>

      <section
        className="daily-delivery-panel"
        aria-labelledby="daily-delivery-list-title"
        aria-busy={loading}
      >
        <div className="daily-delivery-panel-heading">
          <div>
            <h2 id="daily-delivery-list-title">{formatDate(selectedDate)} abone listesi</h2>
            <p>Teslim ve ödeme durumlarını işaretleyip değişiklikleri tek seferde kaydedin.</p>
          </div>
          {!loading && records.length > 0 && (
            <span className={`daily-change-state ${hasChanges ? 'dirty' : 'saved'}`} role="status">
              {hasChanges ? 'Kaydedilmemiş değişiklikler' : 'Tüm değişiklikler kayıtlı'}
            </span>
          )}
        </div>

        <div className="daily-delivery-table-wrapper">
          <table className="daily-delivery-table">
            <caption className="sr-only">
              {formatDate(selectedDate)} tarihli abone teslimat ve tahsilat listesi
            </caption>
            <thead>
              <tr>
                <th scope="col">Teslim</th>
                <th scope="col">Abone</th>
                <th scope="col">Dağıtıcı</th>
                <th scope="col">Ödeme alındı</th>
                <th scope="col">Tutar</th>
                <th scope="col">Yöntem</th>
                <th scope="col">Kapsam</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="7" className="daily-delivery-message">
                    Günlük abone listesi yükleniyor…
                  </td>
                </tr>
              ) : loadFailed ? (
                <tr>
                  <td colSpan="7" className="daily-delivery-message daily-delivery-message-error">
                    Liste yüklenemedi. Yukarıdaki uyarıdan tekrar deneyebilirsiniz.
                  </td>
                </tr>
              ) : records.length === 0 ? (
                <tr>
                  <td colSpan="7" className="daily-delivery-message">
                    Bu tarih için listelenecek abone bulunmuyor.
                  </td>
                </tr>
              ) : (
                records.map((record, index) => (
                  <tr
                    key={record.clientId}
                    className={record.teslim_edildi ? 'daily-delivery-complete-row' : ''}
                  >
                    <td data-label="Teslim" className="daily-check-cell">
                      <div className="daily-delivery-status">
                        <div className="daily-check-control">
                          <input
                            type="checkbox"
                            checked={record.teslim_edildi}
                            onChange={(event) => updateRecord(record.clientId, {
                              teslim_edildi: event.target.checked,
                            })}
                            disabled={saving}
                            aria-label={`${record.abone} için gazete teslim edildi`}
                          />
                          <span>{record.teslim_edildi ? 'Teslim edildi' : 'Bekliyor'}</span>
                        </div>
                        <div className="daily-delivery-quantity">
                          <strong>{record.gazete_adedi} gazete</strong>
                        </div>
                      </div>
                    </td>
                    <td data-label="Abone" className="daily-subscriber-cell">
                      <strong>{record.abone}</strong>
                      <span className={`daily-plan-badge ${record.planli ? 'planned' : 'unplanned'}`}>
                        {record.planli ? 'Planlı dağıtım' : 'Plan dışı'}
                      </span>
                    </td>
                    <td data-label="Dağıtıcı" className="daily-distributor-cell">
                      <span
                        className={`daily-distributor-badge${record.distributor_id ? '' : ' unassigned'}`}
                      >
                        {record.dagitici}
                      </span>
                    </td>
                    <td data-label="Ödeme alındı" className="daily-check-cell">
                      <div className="daily-check-control">
                        <input
                          type="checkbox"
                          checked={record.tahsil_edildi}
                          onChange={(event) => handlePaymentToggle(record, event.target.checked)}
                          disabled={saving}
                          aria-label={`${record.abone} için ödeme alındı`}
                        />
                        <span>{record.tahsil_edildi ? 'Ödeme alındı' : 'Alınmadı'}</span>
                      </div>
                    </td>
                    <td data-label="Tutar">
                      <label className="sr-only" htmlFor={`delivery-amount-${index}`}>
                        {record.abone} tahsilat tutarı
                      </label>
                      <div className="daily-amount-input">
                        <span aria-hidden="true">₺</span>
                        <input
                          id={`delivery-amount-${index}`}
                          type="number"
                          inputMode="decimal"
                          min="0.01"
                          step="0.01"
                          value={record.tutar}
                          onChange={(event) => updateRecord(record.clientId, {
                            tutar: event.target.value,
                          })}
                          disabled={!record.tahsil_edildi || saving}
                          aria-describedby={`delivery-payment-help-${index}`}
                        />
                      </div>
                      <span id={`delivery-payment-help-${index}`} className="sr-only">
                        Ödeme alındı seçildiğinde düzenlenebilir.
                      </span>
                    </td>
                    <td data-label="Yöntem">
                      <label className="sr-only" htmlFor={`delivery-method-${index}`}>
                        {record.abone} ödeme yöntemi
                      </label>
                      <select
                        id={`delivery-method-${index}`}
                        value={record.odeme_yontemi}
                        onChange={(event) => updateRecord(record.clientId, {
                          odeme_yontemi: event.target.value,
                        })}
                        disabled={!record.tahsil_edildi || saving}
                      >
                        {PAYMENT_METHODS.map((method) => (
                          <option key={method} value={method}>{method}</option>
                        ))}
                      </select>
                    </td>
                    <td data-label="Kapsam" className="daily-coverage-cell">
                      <span>{getCoverageText(record)}</span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {!loading && !loadFailed && records.length > 0 && (
          <div className="daily-delivery-actions">
            <p>
              {hasChanges
                ? 'Değişikliklerinizi kaydetmeyi unutmayın.'
                : 'Günlük liste güncel.'}
            </p>
            <button
              type="button"
              onClick={handleSave}
              disabled={saving || !hasChanges}
            >
              {saving ? 'Kaydediliyor…' : 'Değişiklikleri kaydet'}
            </button>
          </div>
        )}
      </section>
    </div>
  );
}

export default Deliveries;
