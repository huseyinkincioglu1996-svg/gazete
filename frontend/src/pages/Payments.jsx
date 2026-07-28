import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import api from '../api';
import './Payments.css';

const getCurrentMonth = () => {
  const today = new Date();
  const offset = today.getTimezoneOffset();
  return new Date(today.getTime() - offset * 60 * 1000).toISOString().slice(0, 7);
};

const formatCurrency = (value) =>
  new Intl.NumberFormat('tr-TR', {
    style: 'currency',
    currency: 'TRY',
    minimumFractionDigits: 2,
  }).format(Number(value) || 0);

const formatDate = (value) => {
  if (!value) return '—';
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? new Date(`${value}T12:00:00`)
    : new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('tr-TR');
};

const formatMonth = (value) => {
  const [year, month] = value.split('-').map(Number);
  const date = new Date(year, month - 1, 1, 12);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString('tr-TR', { month: 'long', year: 'numeric' });
};

const getErrorMessage = (error, fallback) =>
  error.response?.data?.hata
  || error.response?.data?.mesaj
  || error.response?.data?.message
  || fallback;

const getList = (data, primaryKey, fallbackKeys = []) => {
  if (Array.isArray(data?.[primaryKey])) return data[primaryKey];
  for (const key of fallbackKeys) {
    if (Array.isArray(data?.[key])) return data[key];
  }
  return [];
};

const finiteNumberOr = (value, fallback) => {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
};

const getDistributorName = (record, distributors, fallback) => {
  if (typeof record.dagitici === 'string' && record.dagitici) return record.dagitici;
  if (record.dagitici?.isim) return record.dagitici.isim;
  if (record.distributor_id?.isim) return record.distributor_id.isim;

  const distributorId = typeof record.distributor_id === 'string'
    ? record.distributor_id
    : record.distributor_id?._id;
  return distributors.find((distributor) => distributor._id === distributorId)?.isim
    || fallback;
};

const emptyTracking = (month) => ({
  ay: month,
  distributor_id: null,
  ozet: {
    dagitici_odeme_toplami: 0,
    odenen_toplami: 0,
    bekleyen_toplami: 0,
    nakit_tahsilat_toplami: 0,
    nakit_tahsilat_adedi: 0,
  },
  odemeler: [],
  nakit_tahsilatlar: [],
});

const normalizeTracking = (rawData, month) => {
  const data = rawData?.tracking || rawData || {};
  const payments = getList(data, 'odemeler', ['payments']);
  const cashCollections = getList(data, 'nakit_tahsilatlar', [
    'tahsilatlar',
    'collections',
  ]).filter((collection) => !collection.odeme_yontemi || collection.odeme_yontemi === 'Nakit');
  const summary = data.ozet || data.summary || {};
  const paymentTotalFallback = payments.reduce(
    (sum, payment) => sum + (Number(payment.tutar) || 0),
    0,
  );
  const paidTotalFallback = payments
    .filter((payment) => payment.durum === 'Ödendi')
    .reduce((sum, payment) => sum + (Number(payment.tutar) || 0), 0);
  const pendingTotalFallback = payments
    .filter((payment) => payment.durum === 'Beklemede')
    .reduce((sum, payment) => sum + (Number(payment.tutar) || 0), 0);
  const cashTotalFallback = cashCollections.reduce(
    (sum, collection) => sum + (Number(collection.tutar) || 0),
    0,
  );

  return {
    ay: data.ay || month,
    distributor_id: data.distributor_id || null,
    ozet: {
      dagitici_odeme_toplami: finiteNumberOr(
        summary.dagitici_odeme_toplami,
        paymentTotalFallback,
      ),
      odenen_toplami: finiteNumberOr(summary.odenen_toplami, paidTotalFallback),
      bekleyen_toplami: finiteNumberOr(summary.bekleyen_toplami, pendingTotalFallback),
      nakit_tahsilat_toplami: finiteNumberOr(
        summary.nakit_tahsilat_toplami,
        cashTotalFallback,
      ),
      nakit_tahsilat_adedi: finiteNumberOr(
        summary.nakit_tahsilat_adedi,
        cashCollections.length,
      ),
    },
    odemeler: payments,
    nakit_tahsilatlar: cashCollections,
  };
};

function Payments() {
  const [month, setMonth] = useState(getCurrentMonth);
  const [selectedDistributor, setSelectedDistributor] = useState('');
  const [distributors, setDistributors] = useState([]);
  const [tracking, setTracking] = useState(() => emptyTracking(getCurrentMonth()));
  const [filterStatus, setFilterStatus] = useState('');
  const [distributorsLoading, setDistributorsLoading] = useState(true);
  const [trackingLoading, setTrackingLoading] = useState(true);
  const [payingId, setPayingId] = useState('');
  const [distributorsError, setDistributorsError] = useState('');
  const [trackingError, setTrackingError] = useState('');
  const [notice, setNotice] = useState('');
  const trackingRequestId = useRef(0);
  const hasResolvedDefaultDistributor = useRef(false);

  const fetchDistributors = useCallback(async () => {
    setDistributorsLoading(true);
    setDistributorsError('');

    try {
      const response = await api.get('/api/distributors', {
        params: { includeInactive: true },
      });
      const list = Array.isArray(response.data)
        ? response.data
        : Array.isArray(response.data?.distributors)
          ? response.data.distributors
          : [];
      const orderedList = [...list].sort(
        (first, second) => (first.isim || '').localeCompare(second.isim || '', 'tr'),
      );
      setDistributors(orderedList);

      if (!hasResolvedDefaultDistributor.current) {
        const activeDistributors = orderedList.filter(
          (distributor) => distributor.aktif !== false,
        );
        if (activeDistributors.length === 1) {
          setSelectedDistributor(activeDistributors[0]._id);
        }
        hasResolvedDefaultDistributor.current = true;
      }
    } catch (requestError) {
      setDistributorsError(
        getErrorMessage(requestError, 'Dağıtıcı listesi yüklenirken bir hata oluştu.'),
      );
    } finally {
      setDistributorsLoading(false);
    }
  }, []);

  const fetchTracking = useCallback(async (
    selectedMonth,
    distributorId,
    { showLoading = true } = {},
  ) => {
    const requestId = trackingRequestId.current + 1;
    trackingRequestId.current = requestId;
    if (showLoading) setTrackingLoading(true);
    setTrackingError('');

    try {
      const params = { month: selectedMonth };
      if (distributorId) params.distributor_id = distributorId;
      const response = await api.get('/api/payments/tracking', { params });
      if (trackingRequestId.current === requestId) {
        setTracking(normalizeTracking(response.data, selectedMonth));
      }
    } catch (requestError) {
      if (trackingRequestId.current !== requestId) return;
      if (showLoading) setTracking(emptyTracking(selectedMonth));
      setTrackingError(
        getErrorMessage(requestError, 'Ödeme takip bilgileri yüklenirken bir hata oluştu.'),
      );
    } finally {
      if (trackingRequestId.current === requestId && showLoading) {
        setTrackingLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    fetchDistributors();
  }, [fetchDistributors]);

  useEffect(() => {
    if (!distributorsLoading) {
      setNotice('');
      fetchTracking(month, selectedDistributor);
    }
  }, [distributorsLoading, fetchTracking, month, selectedDistributor]);

  const filteredPayments = useMemo(
    () => (
      filterStatus
        ? tracking.odemeler.filter((payment) => payment.durum === filterStatus)
        : tracking.odemeler
    ),
    [filterStatus, tracking.odemeler],
  );

  const pendingCount = tracking.odemeler.filter(
    (payment) => payment.durum === 'Beklemede',
  ).length;
  const paidCount = tracking.odemeler.filter(
    (payment) => payment.durum === 'Ödendi',
  ).length;

  const selectedDistributorName = selectedDistributor
    ? distributors.find((distributor) => distributor._id === selectedDistributor)?.isim
      || 'Seçili dağıtıcı'
    : 'Tüm dağıtıcılar';

  const handlePayment = async (id) => {
    if (!window.confirm('Bu ödemeyi tamamlandı olarak işaretlemek istiyor musunuz?')) return;

    setPayingId(id);
    setNotice('');
    setTrackingError('');
    try {
      await api.put(`/api/payments/${id}/pay`);
      setNotice('Ödeme tamamlandı olarak işaretlendi.');
      await fetchTracking(month, selectedDistributor, { showLoading: false });
    } catch (requestError) {
      setTrackingError(getErrorMessage(requestError, 'Ödeme durumu güncellenemedi.'));
    } finally {
      setPayingId('');
    }
  };

  return (
    <div className="payments">
      <header className="payment-page-heading">
        <div>
          <h1>Dağıtıcı Ödeme Takibi</h1>
          <p>Dağıtıcı ödemelerini ve otomatik aktarılan nakit tahsilatları dönem bazında izleyin.</p>
        </div>
        <div className="payment-controls" aria-label="Ödeme takip filtreleri">
          <div className="payment-control">
            <label htmlFor="payment-distributor">Dağıtıcı</label>
            <select
              id="payment-distributor"
              value={selectedDistributor}
              onChange={(event) => {
                setSelectedDistributor(event.target.value);
                setFilterStatus('');
              }}
              disabled={distributorsLoading}
            >
              <option value="">Tüm dağıtıcılar</option>
              {distributors.map((distributor) => (
                <option key={distributor._id} value={distributor._id}>
                  {distributor.isim}{distributor.aktif === false ? ' (Pasif)' : ''}
                </option>
              ))}
            </select>
          </div>
          <div className="payment-control">
            <label htmlFor="payment-month">Ay</label>
            <input
              id="payment-month"
              type="month"
              required
              value={month}
              onChange={(event) => {
                if (event.target.value) {
                  setMonth(event.target.value);
                  setFilterStatus('');
                }
              }}
            />
          </div>
        </div>
      </header>

      {distributorsError && (
        <div className="payment-feedback payment-feedback-warning" role="alert">
          <span>{distributorsError} Tüm dağıtıcılar görünümü kullanılabilir.</span>
          <button type="button" onClick={fetchDistributors} disabled={distributorsLoading}>
            Tekrar dene
          </button>
        </div>
      )}
      {trackingError && (
        <div className="payment-feedback payment-feedback-error" role="alert">
          <span>{trackingError}</span>
          <button
            type="button"
            onClick={() => fetchTracking(month, selectedDistributor)}
            disabled={trackingLoading}
          >
            Tekrar dene
          </button>
        </div>
      )}
      {notice && <div className="payment-feedback payment-feedback-success" role="status">{notice}</div>}

      <section
        className="payment-summary"
        aria-label={`${formatMonth(month)} ${selectedDistributorName} ödeme özeti`}
        aria-busy={trackingLoading}
      >
        <article className="payment-summary-card cash">
          <h2>Tahsil ettiği nakit</h2>
          <p>{trackingLoading ? '—' : formatCurrency(tracking.ozet.nakit_tahsilat_toplami)}</p>
          <span>
            {trackingLoading ? 'Yükleniyor…' : `${tracking.ozet.nakit_tahsilat_adedi} nakit tahsilat`}
          </span>
        </article>
        <article className="payment-summary-card total">
          <h2>Dağıtıcı ödeme toplamı</h2>
          <p>{trackingLoading ? '—' : formatCurrency(tracking.ozet.dagitici_odeme_toplami)}</p>
          <span>Seçili ay ve dağıtıcıya göre</span>
        </article>
        <article className="payment-summary-card paid">
          <h2>Ödenen</h2>
          <p>{trackingLoading ? '—' : formatCurrency(tracking.ozet.odenen_toplami)}</p>
          <span>{trackingLoading ? 'Yükleniyor…' : `${paidCount} ödeme tamamlandı`}</span>
        </article>
        <article className="payment-summary-card pending">
          <h2>Bekleyen</h2>
          <p>{trackingLoading ? '—' : formatCurrency(tracking.ozet.bekleyen_toplami)}</p>
          <span>{trackingLoading ? 'Yükleniyor…' : `${pendingCount} ödeme bekliyor`}</span>
        </article>
      </section>

      <p className="payment-accounting-note">
        Nakit tahsilatlar ile dağıtıcı ödemeleri ayrı kalemlerdir; bu ekranda birbirine eklenmez veya mahsup edilmez.
      </p>

      <section
        className="cash-collections"
        aria-labelledby="cash-collections-title"
        aria-busy={trackingLoading}
      >
        <div className="payment-section-heading">
          <div>
            <h2 id="cash-collections-title">Otomatik nakit tahsilatlar</h2>
            <p>Dağıtımlar ekranında “Nakit” olarak kaydedilen tahsilatlar otomatik listelenir.</p>
          </div>
          <span>{tracking.nakit_tahsilatlar.length} kayıt</span>
        </div>

        <div className="payment-cash-table-wrapper">
          <table className="cash-collection-table">
            <caption className="sr-only">
              {formatMonth(month)} {selectedDistributorName} nakit tahsilatları
            </caption>
            <thead>
              <tr>
                <th scope="col">Abone</th>
                <th scope="col">Dağıtıcı</th>
                <th scope="col">Tarih</th>
                <th scope="col">Tutar</th>
                <th scope="col">Yöntem</th>
                <th scope="col">Durum</th>
              </tr>
            </thead>
            <tbody>
              {trackingLoading ? (
                <tr>
                  <td colSpan="6" className="payment-table-message">
                    Nakit tahsilatlar yükleniyor…
                  </td>
                </tr>
              ) : trackingError ? (
                <tr>
                  <td colSpan="6" className="payment-table-message payment-table-error">
                    Nakit tahsilatlar yüklenemedi.
                  </td>
                </tr>
              ) : tracking.nakit_tahsilatlar.length === 0 ? (
                <tr>
                  <td colSpan="6" className="payment-table-message">
                    Bu seçim için otomatik nakit tahsilat bulunmuyor.
                  </td>
                </tr>
              ) : (
                tracking.nakit_tahsilatlar.map((collection) => (
                  <tr key={collection._id}>
                    <td data-label="Abone">{collection.abone || 'Bilinmeyen abone'}</td>
                    <td data-label="Dağıtıcı">
                      {getDistributorName(collection, distributors, 'Atanmamış')}
                    </td>
                    <td data-label="Tarih">{formatDate(collection.tarih)}</td>
                    <td data-label="Tutar" className="payment-amount">
                      {formatCurrency(collection.tutar)}
                    </td>
                    <td data-label="Yöntem">{collection.odeme_yontemi || 'Nakit'}</td>
                    <td data-label="Durum">
                      <span className="cash-collection-status">
                        {collection.durum || 'Tahsil Edildi'}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="distributor-payments" aria-labelledby="distributor-payments-title">
        <div className="payment-section-heading distributor-payment-heading">
          <div>
            <h2 id="distributor-payments-title">Dağıtıcı ödeme kayıtları</h2>
            <p>{filteredPayments.length} kayıt gösteriliyor.</p>
          </div>
          <button
            type="button"
            className="payment-refresh-button"
            onClick={() => fetchTracking(month, selectedDistributor)}
            disabled={trackingLoading}
          >
            {trackingLoading ? 'Yükleniyor…' : 'Yenile'}
          </button>
        </div>

        <div className="payment-filters" aria-label="Ödeme durumu filtresi">
          <button
            type="button"
            className={filterStatus === '' ? 'active' : ''}
            aria-pressed={filterStatus === ''}
            onClick={() => setFilterStatus('')}
          >
            Tümü <span>{tracking.odemeler.length}</span>
          </button>
          <button
            type="button"
            className={filterStatus === 'Beklemede' ? 'active' : ''}
            aria-pressed={filterStatus === 'Beklemede'}
            onClick={() => setFilterStatus('Beklemede')}
          >
            Beklemede <span>{pendingCount}</span>
          </button>
          <button
            type="button"
            className={filterStatus === 'Ödendi' ? 'active' : ''}
            aria-pressed={filterStatus === 'Ödendi'}
            onClick={() => setFilterStatus('Ödendi')}
          >
            Ödendi <span>{paidCount}</span>
          </button>
        </div>

        <div className="payment-table-wrapper" aria-busy={trackingLoading}>
          <table className="distributor-payment-table">
            <caption className="sr-only">
              {formatMonth(month)} {selectedDistributorName} ödeme kayıtları
            </caption>
            <thead>
              <tr>
                <th scope="col">Dağıtıcı</th>
                <th scope="col">Tutar</th>
                <th scope="col">Dönem</th>
                <th scope="col">Tür</th>
                <th scope="col">Durum</th>
                <th scope="col"><span className="sr-only">İşlemler</span></th>
              </tr>
            </thead>
            <tbody>
              {trackingLoading ? (
                <tr>
                  <td colSpan="6" className="payment-table-message">
                    Dağıtıcı ödemeleri yükleniyor…
                  </td>
                </tr>
              ) : trackingError ? (
                <tr>
                  <td colSpan="6" className="payment-table-message payment-table-error">
                    Dağıtıcı ödemeleri yüklenemedi.
                  </td>
                </tr>
              ) : filteredPayments.length === 0 ? (
                <tr>
                  <td colSpan="6" className="payment-table-message">
                    {filterStatus
                      ? 'Bu filtreye uygun ödeme bulunmuyor.'
                      : 'Bu seçim için dağıtıcı ödeme kaydı bulunmuyor.'}
                  </td>
                </tr>
              ) : (
                filteredPayments.map((payment) => (
                  <tr key={payment._id} className={payment.durum === 'Ödendi' ? 'payment-paid-row' : ''}>
                    <td data-label="Dağıtıcı">
                      {getDistributorName(payment, distributors, 'Silinmiş dağıtıcı')}
                    </td>
                    <td data-label="Tutar" className="payment-amount">
                      {formatCurrency(payment.tutar)}
                    </td>
                    <td data-label="Dönem">
                      {formatDate(payment.donem_baslangic)} – {formatDate(payment.donem_bitis)}
                    </td>
                    <td data-label="Tür">{payment.odeme_turu || '—'}</td>
                    <td data-label="Durum">
                      <span className={`payment-status ${payment.durum === 'Ödendi' ? 'paid' : 'pending'}`}>
                        {payment.durum}
                      </span>
                    </td>
                    <td className="payment-row-action">
                      {payment.durum === 'Beklemede' && (
                        <button
                          type="button"
                          className="payment-complete-button"
                          onClick={() => handlePayment(payment._id)}
                          disabled={payingId === payment._id}
                          aria-label={`${getDistributorName(payment, distributors, 'Dağıtıcı')} ödemesini tamamla`}
                        >
                          {payingId === payment._id ? 'İşleniyor…' : 'Ödemeyi tamamla'}
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

export default Payments;
