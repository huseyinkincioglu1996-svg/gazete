import React, { useCallback, useEffect, useState } from 'react';
import api from '../api';
import './Distributors.css';

const WEEKDAYS = [
  { value: 0, label: 'Pazartesi', shortLabel: 'Pzt' },
  { value: 1, label: 'Salı', shortLabel: 'Sal' },
  { value: 2, label: 'Çarşamba', shortLabel: 'Çar' },
  { value: 3, label: 'Perşembe', shortLabel: 'Per' },
  { value: 4, label: 'Cuma', shortLabel: 'Cum' },
  { value: 5, label: 'Cumartesi', shortLabel: 'Cmt' },
  { value: 6, label: 'Pazar', shortLabel: 'Paz' },
];

const MONTH_DAYS = Array.from({ length: 31 }, (_, index) => index + 1);

const createEmptyForm = () => ({
  isim: '',
  adres: '',
  telefon: '',
  bolge: 'Bölge 1',
  gazete_fiyat: 5,
  dagetim_gunleri: [],
  odeme_tipi: 'Günlük',
  odeme_gunleri_hafta: [],
  odeme_gunleri_ay: [],
});

const toNumberArray = (value) =>
  Array.isArray(value)
    ? [...new Set(value.map(Number).filter((item) => Number.isFinite(item)))].sort((first, second) => first - second)
    : [];

const normalizeDistributor = (distributor) => ({
  isim: distributor.isim || '',
  adres: distributor.adres || '',
  telefon: distributor.telefon || '',
  bolge: distributor.bolge || 'Bölge 1',
  gazete_fiyat: distributor.gazete_fiyat ?? 5,
  dagetim_gunleri: toNumberArray(distributor.dagetim_gunleri),
  odeme_tipi: distributor.odeme_tipi || 'Günlük',
  odeme_gunleri_hafta: toNumberArray(distributor.odeme_gunleri_hafta),
  odeme_gunleri_ay: toNumberArray(distributor.odeme_gunleri_ay),
});

const formatCurrency = (value) =>
  new Intl.NumberFormat('tr-TR', {
    style: 'currency',
    currency: 'TRY',
    minimumFractionDigits: 2,
  }).format(Number(value) || 0);

const formatWeekdays = (days) => {
  const labels = toNumberArray(days)
    .map((day) => WEEKDAYS.find((option) => option.value === day)?.shortLabel)
    .filter(Boolean);
  return labels.length ? labels.join(', ') : 'Belirlenmedi';
};

const formatPaymentSchedule = (distributor) => {
  if (distributor.odeme_tipi === 'Günlük') return 'Her gün';
  if (distributor.odeme_tipi === 'Haftalık') return formatWeekdays(distributor.odeme_gunleri_hafta);

  const days = toNumberArray(distributor.odeme_gunleri_ay).filter((day) => day >= 1 && day <= 31);
  return days.length ? `${days.join(', ')}. gün` : 'Belirlenmedi';
};

const getErrorMessage = (error, fallback) => error.response?.data?.hata || fallback;

function Distributors() {
  const [distributors, setDistributors] = useState([]);
  const [formData, setFormData] = useState(createEmptyForm);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const fetchDistributors = useCallback(async ({ showLoading = true } = {}) => {
    if (showLoading) setLoading(true);
    setError('');

    try {
      const response = await api.get('/api/distributors');
      setDistributors(response.data);
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Dağıtıcılar yüklenirken bir hata oluştu.'));
    } finally {
      if (showLoading) setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDistributors();
  }, [fetchDistributors]);

  const resetForm = () => {
    setEditingId(null);
    setFormData(createEmptyForm());
  };

  const toggleDay = (field, value) => {
    setFormData((current) => {
      const currentValues = toNumberArray(current[field]);
      const nextValues = currentValues.includes(value)
        ? currentValues.filter((item) => item !== value)
        : [...currentValues, value].sort((first, second) => first - second);
      return { ...current, [field]: nextValues };
    });
  };

  const handlePaymentTypeChange = (event) => {
    const odeme_tipi = event.target.value;
    setFormData((current) => ({
      ...current,
      odeme_tipi,
      odeme_gunleri_hafta: odeme_tipi === 'Haftalık' ? current.odeme_gunleri_hafta : [],
      odeme_gunleri_ay: odeme_tipi === 'Aylık' ? current.odeme_gunleri_ay : [],
    }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setNotice('');
    setError('');

    const gazeteFiyat = Number(formData.gazete_fiyat);
    if (!formData.dagetim_gunleri.length) {
      setError('Otomatik planlama için en az bir dağıtım günü seçmelisiniz.');
      return;
    }
    if (formData.odeme_tipi === 'Haftalık' && !formData.odeme_gunleri_hafta.length) {
      setError('Haftalık ödeme için en az bir ödeme günü seçmelisiniz.');
      return;
    }
    if (formData.odeme_tipi === 'Aylık' && !formData.odeme_gunleri_ay.length) {
      setError('Aylık ödeme için en az bir ödeme günü seçmelisiniz.');
      return;
    }
    if (!Number.isFinite(gazeteFiyat) || gazeteFiyat < 0) {
      setError('Gazete fiyatı sıfır veya daha büyük geçerli bir sayı olmalıdır.');
      return;
    }

    const payload = {
      ...formData,
      isim: formData.isim.trim(),
      adres: formData.adres.trim(),
      telefon: formData.telefon.trim(),
      gazete_fiyat: gazeteFiyat,
      dagetim_gunleri: toNumberArray(formData.dagetim_gunleri),
      odeme_gunleri_hafta: toNumberArray(formData.odeme_gunleri_hafta),
      odeme_gunleri_ay: toNumberArray(formData.odeme_gunleri_ay),
    };

    setSaving(true);
    try {
      if (editingId) {
        await api.put(`/api/distributors/${editingId}`, payload);
        setNotice('Dağıtıcı bilgileri güncellendi.');
      } else {
        await api.post('/api/distributors', payload);
        setNotice('Dağıtıcı oluşturuldu.');
      }
      resetForm();
      await fetchDistributors({ showLoading: false });
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Dağıtıcı kaydedilemedi.'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id) => {
    if (!window.confirm('Bu dağıtıcıyı silmek istediğinizden emin misiniz?')) return;

    setDeletingId(id);
    setNotice('');
    setError('');
    try {
      await api.delete(`/api/distributors/${id}`);
      if (editingId === id) resetForm();
      setNotice('Dağıtıcı silindi.');
      await fetchDistributors({ showLoading: false });
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Dağıtıcı silinemedi.'));
    } finally {
      setDeletingId('');
    }
  };

  const handleEdit = (distributor) => {
    setNotice('');
    setError('');
    setFormData(normalizeDistributor(distributor));
    setEditingId(distributor._id);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <div className="distributors">
      <div className="page-heading">
        <div>
          <h1>Dağıtıcı Yönetimi</h1>
          <p>Dağıtıcı bilgilerini, dağıtım planlarını ve ödeme takvimlerini yönetin.</p>
        </div>
      </div>

      {error && <div className="distributor-feedback distributor-feedback-error" role="alert">{error}</div>}
      {notice && <div className="distributor-feedback distributor-feedback-success" role="status">{notice}</div>}

      <form className="distributor-form" onSubmit={handleSubmit}>
        <div className="form-header">
          <div>
            <h2>{editingId ? 'Dağıtıcıyı düzenle' : 'Yeni dağıtıcı'}</h2>
            <p>Yıldızlı alanlar zorunludur.</p>
          </div>
          {editingId && <span className="edit-badge">Düzenleme modunda</span>}
        </div>

        <div className="distributor-form-grid">
          <div className="form-group">
            <label htmlFor="distributor-name">İsim *</label>
            <input
              id="distributor-name"
              type="text"
              required
              value={formData.isim}
              onChange={(event) => setFormData((current) => ({ ...current, isim: event.target.value }))}
            />
          </div>

          <div className="form-group">
            <label htmlFor="distributor-phone">Telefon *</label>
            <input
              id="distributor-phone"
              type="tel"
              required
              value={formData.telefon}
              onChange={(event) => setFormData((current) => ({ ...current, telefon: event.target.value }))}
            />
          </div>

          <div className="form-group form-group-wide">
            <label htmlFor="distributor-address">Adres *</label>
            <input
              id="distributor-address"
              type="text"
              required
              value={formData.adres}
              onChange={(event) => setFormData((current) => ({ ...current, adres: event.target.value }))}
            />
          </div>

          <div className="form-group">
            <label htmlFor="distributor-zone">Bölge *</label>
            <select
              id="distributor-zone"
              required
              value={formData.bolge}
              onChange={(event) => setFormData((current) => ({ ...current, bolge: event.target.value }))}
            >
              <option value="Bölge 1">Bölge 1</option>
              <option value="Bölge 2">Bölge 2</option>
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="distributor-price">Gazete fiyatı (₺) *</label>
            <input
              id="distributor-price"
              type="number"
              min="0"
              step="0.01"
              required
              value={formData.gazete_fiyat}
              onChange={(event) => setFormData((current) => ({ ...current, gazete_fiyat: event.target.value }))}
            />
          </div>

          <div className="form-group">
            <label htmlFor="payment-type">Ödeme tipi *</label>
            <select id="payment-type" value={formData.odeme_tipi} onChange={handlePaymentTypeChange}>
              <option value="Günlük">Günlük</option>
              <option value="Haftalık">Haftalık</option>
              <option value="Aylık">Aylık</option>
            </select>
          </div>
        </div>

        <fieldset className="day-selector">
          <legend>Dağıtım günleri *</legend>
          <p className="field-description">0=Pazartesi, 6=Pazar. Otomatik dağıtım bu günlerde oluşturulur.</p>
          <div className="day-options">
            {WEEKDAYS.map((day) => (
              <label key={day.value} className="day-option" htmlFor={`distribution-day-${day.value}`}>
                <input
                  id={`distribution-day-${day.value}`}
                  type="checkbox"
                  checked={formData.dagetim_gunleri.includes(day.value)}
                  onChange={() => toggleDay('dagetim_gunleri', day.value)}
                />
                <span>{day.label}</span>
              </label>
            ))}
          </div>
        </fieldset>

        {formData.odeme_tipi === 'Haftalık' && (
          <fieldset className="day-selector">
            <legend>Haftalık ödeme günleri *</legend>
            <p className="field-description">Ödeme, seçilen hafta günlerinde hesaplanır.</p>
            <div className="day-options">
              {WEEKDAYS.map((day) => (
                <label key={day.value} className="day-option" htmlFor={`weekly-payment-day-${day.value}`}>
                  <input
                    id={`weekly-payment-day-${day.value}`}
                    type="checkbox"
                    checked={formData.odeme_gunleri_hafta.includes(day.value)}
                    onChange={() => toggleDay('odeme_gunleri_hafta', day.value)}
                  />
                  <span>{day.label}</span>
                </label>
              ))}
            </div>
          </fieldset>
        )}

        {formData.odeme_tipi === 'Aylık' && (
          <fieldset className="day-selector">
            <legend>Aylık ödeme günleri *</legend>
            <p className="field-description">Ödeme, seçilen ay günlerinde hesaplanır.</p>
            <div className="month-day-options">
              {MONTH_DAYS.map((day) => (
                <label key={day} className="month-day-option" htmlFor={`monthly-payment-day-${day}`}>
                  <input
                    id={`monthly-payment-day-${day}`}
                    type="checkbox"
                    checked={formData.odeme_gunleri_ay.includes(day)}
                    onChange={() => toggleDay('odeme_gunleri_ay', day)}
                  />
                  <span>{day}</span>
                </label>
              ))}
            </div>
          </fieldset>
        )}

        <div className="form-actions">
          <button type="submit" className="btn-primary" disabled={saving}>
            {saving ? 'Kaydediliyor…' : editingId ? 'Değişiklikleri kaydet' : 'Dağıtıcı ekle'}
          </button>
          {editingId && (
            <button type="button" className="btn-secondary" onClick={resetForm} disabled={saving}>
              İptal
            </button>
          )}
        </div>
      </form>

      <section className="distributors-list" aria-labelledby="distributor-list-title">
        <div className="list-heading">
          <div>
            <h2 id="distributor-list-title">Kayıtlı dağıtıcılar</h2>
            <p>{distributors.length} dağıtıcı listeleniyor.</p>
          </div>
          <button type="button" className="btn-secondary" onClick={() => fetchDistributors()} disabled={loading}>
            {loading ? 'Yükleniyor…' : 'Yenile'}
          </button>
        </div>

        <div className="distributor-table-wrapper">
          <table>
            <thead>
              <tr>
                <th scope="col">Dağıtıcı</th>
                <th scope="col">İletişim</th>
                <th scope="col">Bölge</th>
                <th scope="col">Birim fiyat</th>
                <th scope="col">Dağıtım günleri</th>
                <th scope="col">Ödeme planı</th>
                <th scope="col"><span className="sr-only">İşlemler</span></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="7" className="table-message">Dağıtıcılar yükleniyor…</td>
                </tr>
              ) : distributors.length === 0 ? (
                <tr>
                  <td colSpan="7" className="table-message">Henüz dağıtıcı kaydı bulunmuyor.</td>
                </tr>
              ) : (
                distributors.map((distributor) => (
                  <tr key={distributor._id}>
                    <td>
                      <strong>{distributor.isim}</strong>
                      <span className="address-text">{distributor.adres}</span>
                    </td>
                    <td>{distributor.telefon}</td>
                    <td>{distributor.bolge}</td>
                    <td>{formatCurrency(distributor.gazete_fiyat)}</td>
                    <td>{formatWeekdays(distributor.dagetim_gunleri)}</td>
                    <td>
                      <strong>{distributor.odeme_tipi}</strong>
                      <span className="schedule-text">{formatPaymentSchedule(distributor)}</span>
                    </td>
                    <td className="action-cell">
                      <button type="button" className="btn-edit" onClick={() => handleEdit(distributor)}>
                        Düzenle
                      </button>
                      <button
                        type="button"
                        className="btn-delete"
                        onClick={() => handleDelete(distributor._id)}
                        disabled={deletingId === distributor._id}
                      >
                        {deletingId === distributor._id ? 'Siliniyor…' : 'Sil'}
                      </button>
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

export default Distributors;
