import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import api from '../api';
import LocationPicker, { buildGoogleMapsUrl } from './LocationPicker';
import './Subscribers.css';

const NEWSPAPER_DAY_OPTIONS = [
  { value: 'pazartesi', label: 'Pazartesi' },
  { value: 'sali', label: 'Salı' },
  { value: 'carsamba', label: 'Çarşamba' },
  { value: 'persembe', label: 'Perşembe' },
  { value: 'cuma', label: 'Cuma' },
  { value: 'cumartesi', label: 'Cumartesi' },
  { value: 'pazar', label: 'Pazar' },
  { value: 'pazar_pazartesi', label: 'Pazar Pazartesi' },
];

const NEWSPAPER_DAY_VALUES = new Set(NEWSPAPER_DAY_OPTIONS.map((day) => day.value));

const normalizeNewspaperDays = (days) => {
  if (!Array.isArray(days)) return [];

  const orderedDays = NEWSPAPER_DAY_OPTIONS
    .map((option) => option.value)
    .filter((value) => days.includes(value) && NEWSPAPER_DAY_VALUES.has(value));

  return orderedDays.includes('pazar_pazartesi')
    ? orderedDays.filter((value) => value !== 'pazar' && value !== 'pazartesi')
    : orderedDays;
};

const createEmptyForm = () => ({
  isim: '',
  telefon: '',
  adres: '',
  aylik_ucret: '0',
  odeme_periyodu_id: '',
  distributor_id: '',
  konum: null,
  notlar: '',
  aktif: true,
  gazete_gunleri: [],
});

const getPaymentPeriodId = (paymentPeriod) => (
  typeof paymentPeriod === 'object' && paymentPeriod !== null
    ? paymentPeriod._id || ''
    : paymentPeriod || ''
);

const getDistributorId = (distributor) => (
  typeof distributor === 'object' && distributor !== null
    ? distributor._id || ''
    : distributor || ''
);

const normalizeSubscriber = (subscriber) => ({
  isim: subscriber.isim || '',
  telefon: subscriber.telefon || '',
  adres: subscriber.adres || '',
  aylik_ucret: subscriber.aylik_ucret ?? 0,
  odeme_periyodu_id: getPaymentPeriodId(subscriber.odeme_periyodu_id),
  distributor_id: getDistributorId(subscriber.distributor_id),
  konum: subscriber.konum?.enlem !== undefined && subscriber.konum?.boylam !== undefined
    ? {
      enlem: subscriber.konum.enlem,
      boylam: subscriber.konum.boylam,
    }
    : null,
  notlar: subscriber.notlar || '',
  aktif: subscriber.aktif !== false,
  gazete_gunleri: normalizeNewspaperDays(subscriber.gazete_gunleri),
});

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

const getSubscriberList = (data) => {
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.aboneler)) return data.aboneler;
  if (Array.isArray(data?.subscribers)) return data.subscribers;
  return [];
};

const getPaymentPeriodList = (data) => {
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.odeme_periyotlari)) return data.odeme_periyotlari;
  if (Array.isArray(data?.paymentPeriods)) return data.paymentPeriods;
  return [];
};

const getDistributorList = (data) => {
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.dagiticilar)) return data.dagiticilar;
  if (Array.isArray(data?.distributors)) return data.distributors;
  return [];
};

const normalizeLocationForPayload = (location) => {
  const latitudeValue = String(location?.enlem ?? '').trim();
  const longitudeValue = String(location?.boylam ?? '').trim();
  if (!latitudeValue && !longitudeValue) return null;

  const latitude = Number(latitudeValue.replace(',', '.'));
  const longitude = Number(longitudeValue.replace(',', '.'));
  if (
    !latitudeValue
    || !longitudeValue
    || !Number.isFinite(latitude)
    || !Number.isFinite(longitude)
    || latitude < -90
    || latitude > 90
    || longitude < -180
    || longitude > 180
  ) {
    return undefined;
  }

  return { enlem: latitude, boylam: longitude };
};

function Subscribers() {
  const [subscribers, setSubscribers] = useState([]);
  const [paymentPeriods, setPaymentPeriods] = useState([]);
  const [paymentPeriodsLoading, setPaymentPeriodsLoading] = useState(true);
  const [paymentPeriodsError, setPaymentPeriodsError] = useState('');
  const [distributors, setDistributors] = useState([]);
  const [distributorsLoading, setDistributorsLoading] = useState(true);
  const [distributorsError, setDistributorsError] = useState('');
  const [formData, setFormData] = useState(createEmptyForm);
  const [editingId, setEditingId] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [togglingId, setTogglingId] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const formRef = useRef(null);

  const fetchSubscribers = useCallback(async ({ showLoading = true } = {}) => {
    if (showLoading) setLoading(true);
    setLoadFailed(false);
    setError('');

    try {
      const response = await api.get('/api/subscribers');
      const orderedSubscribers = getSubscriberList(response.data).sort(
        (first, second) => (first.isim || '').localeCompare(second.isim || '', 'tr'),
      );
      setSubscribers(orderedSubscribers);
    } catch (requestError) {
      setLoadFailed(true);
      setError(getErrorMessage(requestError, 'Aboneler yüklenirken bir hata oluştu.'));
    } finally {
      if (showLoading) setLoading(false);
    }
  }, []);

  const fetchPaymentPeriods = useCallback(async ({ includeInactive = false } = {}) => {
    setPaymentPeriodsLoading(true);
    setPaymentPeriodsError('');

    try {
      const response = await api.get(
        '/api/payment-periods',
        includeInactive ? undefined : { params: { aktif: true } },
      );
      const orderedPeriods = getPaymentPeriodList(response.data).sort(
        (first, second) => (first.ad || '').localeCompare(second.ad || '', 'tr'),
      );
      setPaymentPeriods(orderedPeriods);
    } catch (requestError) {
      setPaymentPeriodsError(
        getErrorMessage(requestError, 'Ödeme periyotları yüklenemedi.'),
      );
    } finally {
      setPaymentPeriodsLoading(false);
    }
  }, []);

  const fetchDistributors = useCallback(async () => {
    setDistributorsLoading(true);
    setDistributorsError('');

    try {
      const response = await api.get('/api/distributors', {
        params: { includeInactive: true },
      });
      const orderedDistributors = getDistributorList(response.data).sort(
        (first, second) => (first.isim || '').localeCompare(second.isim || '', 'tr'),
      );
      setDistributors(orderedDistributors);
    } catch (requestError) {
      setDistributorsError(
        getErrorMessage(requestError, 'Dağıtıcılar yüklenemedi.'),
      );
    } finally {
      setDistributorsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSubscribers();
    fetchPaymentPeriods();
    fetchDistributors();
  }, [fetchDistributors, fetchPaymentPeriods, fetchSubscribers]);

  const counts = useMemo(() => {
    const active = subscribers.filter((subscriber) => subscriber.aktif !== false).length;
    return {
      all: subscribers.length,
      active,
      passive: subscribers.length - active,
    };
  }, [subscribers]);

  const selectablePaymentPeriods = useMemo(
    () => paymentPeriods.filter(
      (period) => (
        period.aktif !== false
        || period._id === formData.odeme_periyodu_id
      ),
    ),
    [formData.odeme_periyodu_id, paymentPeriods],
  );

  const activeDistributors = useMemo(
    () => distributors.filter((distributor) => distributor.aktif !== false),
    [distributors],
  );

  const selectableDistributors = useMemo(
    () => distributors.filter(
      (distributor) => (
        distributor.aktif !== false
        || distributor._id === formData.distributor_id
      ),
    ),
    [distributors, formData.distributor_id],
  );

  const defaultDistributorId = activeDistributors.length === 1
    ? activeDistributors[0]._id
    : '';

  useEffect(() => {
    if (!editingId && defaultDistributorId) {
      setFormData((current) => (
        current.distributor_id
          ? current
          : { ...current, distributor_id: defaultDistributorId }
      ));
    }
  }, [defaultDistributorId, editingId]);

  const filteredSubscribers = useMemo(() => {
    if (statusFilter === 'active') {
      return subscribers.filter((subscriber) => subscriber.aktif !== false);
    }
    if (statusFilter === 'passive') {
      return subscribers.filter((subscriber) => subscriber.aktif === false);
    }
    return subscribers;
  }, [statusFilter, subscribers]);

  const resetForm = () => {
    setEditingId('');
    setFormData({ ...createEmptyForm(), distributor_id: defaultDistributorId });
    setError('');
  };

  const toggleNewspaperDay = (dayValue) => {
    setFormData((current) => {
      const currentDays = normalizeNewspaperDays(current.gazete_gunleri);
      if (currentDays.includes(dayValue)) {
        return {
          ...current,
          gazete_gunleri: currentDays.filter((value) => value !== dayValue),
        };
      }

      let nextDays = currentDays;
      if (dayValue === 'pazar_pazartesi') {
        nextDays = nextDays.filter((value) => value !== 'pazar' && value !== 'pazartesi');
      } else if (dayValue === 'pazar' || dayValue === 'pazartesi') {
        nextDays = nextDays.filter((value) => value !== 'pazar_pazartesi');
      }

      return {
        ...current,
        gazete_gunleri: normalizeNewspaperDays([...nextDays, dayValue]),
      };
    });
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setNotice('');

    const name = formData.isim.trim();
    const monthlyFee = Number(String(formData.aylik_ucret).replace(',', '.'));

    if (!name) {
      setError('Abone adını girin.');
      return;
    }
    if (!Number.isFinite(monthlyFee) || monthlyFee < 0) {
      setError('Aylık ücret sıfır veya daha büyük geçerli bir sayı olmalıdır.');
      return;
    }

    const normalizedLocation = normalizeLocationForPayload(formData.konum);
    if (normalizedLocation === undefined) {
      setError('Teslimat konumu için geçerli enlem ve boylam değerlerini birlikte girin.');
      return;
    }

    const payload = {
      isim: name,
      telefon: formData.telefon.trim(),
      adres: formData.adres.trim(),
      aylik_ucret: monthlyFee,
      odeme_periyodu_id: formData.odeme_periyodu_id || null,
      distributor_id: formData.distributor_id || null,
      konum: normalizedLocation,
      notlar: formData.notlar.trim(),
      aktif: Boolean(formData.aktif),
      gazete_gunleri: normalizeNewspaperDays(formData.gazete_gunleri),
    };

    setSaving(true);
    try {
      if (editingId) {
        await api.put(`/api/subscribers/${editingId}`, payload);
        setNotice('Abone bilgileri güncellendi.');
      } else {
        await api.post('/api/subscribers', payload);
        setNotice('Yeni abone eklendi.');
      }

      setEditingId('');
      setFormData({ ...createEmptyForm(), distributor_id: defaultDistributorId });
      await fetchSubscribers({ showLoading: false });
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Abone kaydedilemedi.'));
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (subscriber) => {
    const selectedPeriod = subscriber.odeme_periyodu_id;
    const selectedPeriodId = getPaymentPeriodId(selectedPeriod);

    if (
      selectedPeriodId
      && !paymentPeriods.some((period) => period._id === selectedPeriodId)
    ) {
      if (typeof selectedPeriod === 'object' && selectedPeriod?.ad) {
        setPaymentPeriods((current) => [...current, selectedPeriod].sort(
          (first, second) => (first.ad || '').localeCompare(second.ad || '', 'tr'),
        ));
      } else {
        fetchPaymentPeriods({ includeInactive: true });
      }
    }

    const selectedDistributor = subscriber.distributor_id;
    const selectedDistributorId = getDistributorId(selectedDistributor);
    if (
      selectedDistributorId
      && !distributors.some((distributor) => distributor._id === selectedDistributorId)
      && typeof selectedDistributor === 'object'
      && selectedDistributor?.isim
    ) {
      setDistributors((current) => [...current, selectedDistributor].sort(
        (first, second) => (first.isim || '').localeCompare(second.isim || '', 'tr'),
      ));
    }

    setEditingId(subscriber._id);
    setFormData(normalizeSubscriber(subscriber));
    setError('');
    setNotice('');
    formRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const handleStatusToggle = async (subscriber) => {
    const nextStatus = subscriber.aktif === false;
    setTogglingId(subscriber._id);
    setError('');
    setNotice('');

    try {
      await api.patch(`/api/subscribers/${subscriber._id}/status`, { aktif: nextStatus });
      setNotice(
        nextStatus
          ? `${subscriber.isim} aktif duruma getirildi.`
          : `${subscriber.isim} pasif duruma alındı.`,
      );
      if (editingId === subscriber._id) {
        setFormData((current) => ({ ...current, aktif: nextStatus }));
      }
      await fetchSubscribers({ showLoading: false });
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Abone durumu değiştirilemedi.'));
    } finally {
      setTogglingId('');
    }
  };

  const emptyMessage = statusFilter === 'active'
    ? 'Aktif abone bulunmuyor.'
    : statusFilter === 'passive'
      ? 'Pasif abone bulunmuyor.'
      : 'Henüz abone kaydı bulunmuyor. Yukarıdaki formdan ilk aboneyi ekleyebilirsiniz.';

  return (
    <div className="subscribers">
      <header className="subscriber-page-heading">
        <div>
          <h1>Abone Yönetimi</h1>
          <p>Abonelerin dağıtıcı, ödeme periyodu ve hassas teslimat konumunu yönetin.</p>
        </div>
      </header>

      {error && (
        <div className="subscriber-feedback subscriber-feedback-error" role="alert">
          <span>{error}</span>
          {loadFailed && (
            <button type="button" onClick={() => fetchSubscribers()} disabled={loading}>
              Tekrar dene
            </button>
          )}
        </div>
      )}
      {notice && (
        <div className="subscriber-feedback subscriber-feedback-success" role="status">
          {notice}
        </div>
      )}

      <form
        ref={formRef}
        className="subscriber-form"
        onSubmit={handleSubmit}
        aria-labelledby="subscriber-form-title"
      >
        <div className="subscriber-form-header">
          <div>
            <h2 id="subscriber-form-title">
              {editingId ? 'Abone bilgilerini düzenle' : 'Yeni abone ekle'}
            </h2>
            <p>Yalnızca abone adı zorunludur.</p>
          </div>
          {editingId && <span className="subscriber-edit-badge">Düzenleme modunda</span>}
        </div>

        <div className="subscriber-form-grid">
          <div className="subscriber-field">
            <label htmlFor="subscriber-name">Abone adı *</label>
            <input
              id="subscriber-name"
              type="text"
              required
              maxLength="160"
              autoComplete="name"
              value={formData.isim}
              onChange={(event) => setFormData((current) => ({
                ...current,
                isim: event.target.value,
              }))}
              placeholder="Ad soyad"
            />
          </div>

          <div className="subscriber-field">
            <label htmlFor="subscriber-phone">Telefon</label>
            <input
              id="subscriber-phone"
              type="tel"
              maxLength="40"
              autoComplete="tel"
              value={formData.telefon}
              onChange={(event) => setFormData((current) => ({
                ...current,
                telefon: event.target.value,
              }))}
              placeholder="05xx xxx xx xx"
            />
          </div>

          <div className="subscriber-field">
            <label htmlFor="subscriber-fee">Aylık ücret (₺)</label>
            <input
              id="subscriber-fee"
              type="number"
              inputMode="decimal"
              min="0"
              step="0.01"
              value={formData.aylik_ucret}
              onChange={(event) => setFormData((current) => ({
                ...current,
                aylik_ucret: event.target.value,
              }))}
              placeholder="0,00"
            />
          </div>

          <div className="subscriber-field subscriber-field-payment-period">
            <label htmlFor="subscriber-payment-period">Ödeme periyodu</label>
            <select
              id="subscriber-payment-period"
              value={formData.odeme_periyodu_id}
              onChange={(event) => setFormData((current) => ({
                ...current,
                odeme_periyodu_id: event.target.value,
              }))}
              disabled={paymentPeriodsLoading}
            >
              <option value="">
                {paymentPeriodsLoading ? 'Periyotlar yükleniyor…' : 'Ödeme periyodu seçilmedi'}
              </option>
              {selectablePaymentPeriods.map((period) => (
                <option key={period._id} value={period._id}>
                  {period.ad}
                  {period.gun_sayisi ? ` — ${period.gun_sayisi} gün` : ''}
                  {period.aktif === false ? ' (Pasif)' : ''}
                </option>
              ))}
            </select>
            {paymentPeriodsError ? (
              <span className="subscriber-field-message error">
                {paymentPeriodsError}
                {' '}
                <button type="button" onClick={() => fetchPaymentPeriods()}>
                  Tekrar dene
                </button>
              </span>
            ) : !paymentPeriodsLoading && selectablePaymentPeriods.length === 0 ? (
              <span className="subscriber-field-message">
                Ayarlar menüsünden ödeme periyodu tanımlayabilirsiniz.
              </span>
            ) : null}
          </div>

          <div className="subscriber-field subscriber-field-distributor">
            <label htmlFor="subscriber-distributor">Dağıtıcı</label>
            <select
              id="subscriber-distributor"
              value={formData.distributor_id}
              onChange={(event) => setFormData((current) => ({
                ...current,
                distributor_id: event.target.value,
              }))}
              disabled={distributorsLoading}
            >
              <option value="">
                {distributorsLoading ? 'Dağıtıcılar yükleniyor…' : 'Dağıtıcı seçilmedi'}
              </option>
              {selectableDistributors.map((distributor) => (
                <option key={distributor._id} value={distributor._id}>
                  {distributor.isim}
                  {distributor.bolge ? ` — ${distributor.bolge}` : ''}
                  {distributor.aktif === false ? ' (Pasif)' : ''}
                </option>
              ))}
            </select>
            {distributorsError ? (
              <span className="subscriber-field-message error">
                {distributorsError}
                {' '}
                <button type="button" onClick={fetchDistributors}>
                  Tekrar dene
                </button>
              </span>
            ) : !distributorsLoading && selectableDistributors.length === 0 ? (
              <span className="subscriber-field-message">
                Önce Dağıtıcılar menüsünden aktif bir dağıtıcı tanımlayın.
              </span>
            ) : null}
            <span className="subscriber-field-message">
              Günlük dağıtımda alınan nakit tahsilatlar, Ödeme Takibi’nde bu dağıtıcıya bağlanır.
            </span>
          </div>

          <div className="subscriber-field subscriber-field-address">
            <label htmlFor="subscriber-address">Adres</label>
            <textarea
              id="subscriber-address"
              rows="3"
              maxLength="500"
              autoComplete="street-address"
              value={formData.adres}
              onChange={(event) => setFormData((current) => ({
                ...current,
                adres: event.target.value,
              }))}
              placeholder="Teslimat adresi"
            />
          </div>

          <div className="subscriber-field subscriber-field-notes">
            <label htmlFor="subscriber-notes">Notlar</label>
            <textarea
              id="subscriber-notes"
              rows="3"
              maxLength="1000"
              value={formData.notlar}
              onChange={(event) => setFormData((current) => ({
                ...current,
                notlar: event.target.value,
              }))}
              placeholder="İsteğe bağlı abone notu"
            />
          </div>
        </div>

        <LocationPicker
          value={formData.konum}
          address={formData.adres}
          onChange={(konum) => setFormData((current) => ({ ...current, konum }))}
        />

        <fieldset className="subscriber-day-selector">
          <legend>Gazete alınacak günler</legend>
          <p>Abonenin gazete alacağı günleri seçin. Birden fazla gün seçebilirsiniz.</p>
          <div className="subscriber-day-options">
            {NEWSPAPER_DAY_OPTIONS.map((day) => (
              <label
                key={day.value}
                className={`subscriber-day-option${day.value === 'pazar_pazartesi' ? ' subscriber-day-option-special' : ''}`}
                htmlFor={`subscriber-newspaper-day-${day.value}`}
              >
                <input
                  id={`subscriber-newspaper-day-${day.value}`}
                  type="checkbox"
                  checked={formData.gazete_gunleri.includes(day.value)}
                  onChange={() => toggleNewspaperDay(day.value)}
                />
                <span>{day.label}</span>
              </label>
            ))}
          </div>
          <small>
            “Pazar Pazartesi”: Pazar günü gazete verilmez; Pazar ve Pazartesi gazeteleri
            Pazartesi günü birlikte teslim edilir.
          </small>
        </fieldset>

        <label className="subscriber-active-control" htmlFor="subscriber-active">
          <input
            id="subscriber-active"
            type="checkbox"
            checked={formData.aktif}
            onChange={(event) => setFormData((current) => ({
              ...current,
              aktif: event.target.checked,
            }))}
          />
          <span>
            <strong>Abone aktif</strong>
            <small>Aktif aboneler günlük kasa girişlerinde önerilir.</small>
          </span>
        </label>

        <div className="subscriber-form-actions">
          <button type="submit" className="subscriber-save-button" disabled={saving}>
            {saving
              ? 'Kaydediliyor…'
              : editingId
                ? 'Değişiklikleri kaydet'
                : 'Abone ekle'}
          </button>
          {editingId && (
            <button
              type="button"
              className="subscriber-cancel-button"
              onClick={resetForm}
              disabled={saving}
            >
              İptal
            </button>
          )}
        </div>
      </form>

      <section className="subscriber-list" aria-labelledby="subscriber-list-title">
        <div className="subscriber-list-header">
          <div>
            <h2 id="subscriber-list-title">Kayıtlı aboneler</h2>
            <p>{filteredSubscribers.length} abone gösteriliyor.</p>
          </div>
          <button
            type="button"
            className="subscriber-refresh-button"
            onClick={() => fetchSubscribers()}
            disabled={loading}
          >
            {loading ? 'Yükleniyor…' : 'Yenile'}
          </button>
        </div>

        <div className="subscriber-filters" aria-label="Abone durumuna göre filtrele">
          <button
            type="button"
            className={statusFilter === 'all' ? 'active' : ''}
            aria-pressed={statusFilter === 'all'}
            onClick={() => setStatusFilter('all')}
          >
            Tümü <span>{counts.all}</span>
          </button>
          <button
            type="button"
            className={statusFilter === 'active' ? 'active' : ''}
            aria-pressed={statusFilter === 'active'}
            onClick={() => setStatusFilter('active')}
          >
            Aktif <span>{counts.active}</span>
          </button>
          <button
            type="button"
            className={statusFilter === 'passive' ? 'active' : ''}
            aria-pressed={statusFilter === 'passive'}
            onClick={() => setStatusFilter('passive')}
          >
            Pasif <span>{counts.passive}</span>
          </button>
        </div>

        <div className="subscriber-table-wrapper" aria-busy={loading}>
          <table className="subscriber-table">
            <caption className="sr-only">Filtrelenmiş abone kayıtları</caption>
            <thead>
              <tr>
                <th scope="col">Abone</th>
                <th scope="col">İletişim</th>
                <th scope="col">Adres</th>
                <th scope="col">Gazete günleri</th>
                <th scope="col">Aylık ücret</th>
                <th scope="col">Ödeme periyodu</th>
                <th scope="col">Dağıtıcı</th>
                <th scope="col">Durum</th>
                <th scope="col"><span className="sr-only">İşlemler</span></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="9" className="subscriber-table-message">
                    Aboneler yükleniyor…
                  </td>
                </tr>
              ) : loadFailed ? (
                <tr>
                  <td colSpan="9" className="subscriber-table-message subscriber-table-error">
                    Abone listesi yüklenemedi. Tekrar deneyin.
                  </td>
                </tr>
              ) : filteredSubscribers.length === 0 ? (
                <tr>
                  <td colSpan="9" className="subscriber-table-message">
                    {emptyMessage}
                  </td>
                </tr>
              ) : (
                filteredSubscribers.map((subscriber) => {
                  const isActive = subscriber.aktif !== false;
                  const selectedPeriod = typeof subscriber.odeme_periyodu_id === 'object'
                    ? subscriber.odeme_periyodu_id
                    : paymentPeriods.find(
                      (period) => period._id === subscriber.odeme_periyodu_id,
                    );
                  const selectedDistributor = typeof subscriber.distributor_id === 'object'
                    ? subscriber.distributor_id
                    : distributors.find(
                      (distributor) => distributor._id === subscriber.distributor_id,
                    );
                  const subscriberLocation = normalizeLocationForPayload(subscriber.konum);
                  return (
                    <tr key={subscriber._id} className={isActive ? '' : 'subscriber-passive-row'}>
                      <td data-label="Abone">
                        <strong>{subscriber.isim}</strong>
                        {subscriber.notlar && (
                          <span className="subscriber-secondary-text">{subscriber.notlar}</span>
                        )}
                      </td>
                      <td data-label="İletişim">
                        {subscriber.telefon ? (
                          <a href={`tel:${subscriber.telefon.replace(/[^\d+]/g, '')}`}>
                            {subscriber.telefon}
                          </a>
                        ) : (
                          <span className="subscriber-muted">Belirtilmedi</span>
                        )}
                      </td>
                      <td data-label="Adres">
                        <span className="subscriber-address-text">
                          {subscriber.adres || 'Belirtilmedi'}
                        </span>
                        {subscriberLocation && (
                          <a
                            className="subscriber-map-link"
                            href={buildGoogleMapsUrl(subscriberLocation)}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Haritada aç
                          </a>
                        )}
                      </td>
                      <td data-label="Gazete günleri">
                        {normalizeNewspaperDays(subscriber.gazete_gunleri).length ? (
                          <div className="subscriber-day-badges">
                            {normalizeNewspaperDays(subscriber.gazete_gunleri).map((dayValue) => (
                              <span key={dayValue}>
                                {NEWSPAPER_DAY_OPTIONS.find(
                                  (option) => option.value === dayValue,
                                )?.label}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span className="subscriber-muted">Belirtilmedi</span>
                        )}
                      </td>
                      <td data-label="Aylık ücret" className="subscriber-fee">
                        {formatCurrency(subscriber.aylik_ucret)}
                      </td>
                      <td data-label="Ödeme periyodu">
                        {selectedPeriod ? (
                          <span className="subscriber-payment-period">
                            {selectedPeriod.ad}
                            {selectedPeriod.aktif === false ? ' (Pasif)' : ''}
                          </span>
                        ) : (
                          <span className="subscriber-muted">Belirtilmedi</span>
                        )}
                      </td>
                      <td data-label="Dağıtıcı">
                        {selectedDistributor ? (
                          <span className="subscriber-distributor">
                            {selectedDistributor.isim}
                            {selectedDistributor.aktif === false ? ' (Pasif)' : ''}
                          </span>
                        ) : (
                          <span className="subscriber-muted">Atanmamış</span>
                        )}
                      </td>
                      <td data-label="Durum">
                        <span className={`subscriber-status ${isActive ? 'active' : 'passive'}`}>
                          {isActive ? 'Aktif' : 'Pasif'}
                        </span>
                      </td>
                      <td className="subscriber-actions">
                        <button
                          type="button"
                          className="subscriber-edit-button"
                          onClick={() => handleEdit(subscriber)}
                          disabled={Boolean(togglingId)}
                        >
                          Düzenle
                        </button>
                        <button
                          type="button"
                          className={`subscriber-toggle-button ${isActive ? 'deactivate' : 'activate'}`}
                          onClick={() => handleStatusToggle(subscriber)}
                          disabled={togglingId === subscriber._id}
                          aria-label={`${subscriber.isim} adlı aboneyi ${isActive ? 'pasife al' : 'aktif et'}`}
                        >
                          {togglingId === subscriber._id
                            ? 'İşleniyor…'
                            : isActive
                              ? 'Pasife al'
                              : 'Aktifleştir'}
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

export default Subscribers;
