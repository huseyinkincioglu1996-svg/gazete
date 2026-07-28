import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import api from '../api';
import './Settings.css';

const createEmptyForm = () => ({
  ad: '',
  gun_sayisi: '',
  aciklama: '',
  aktif: true,
});

const normalizeDefinition = (definition) => ({
  ad: definition.ad || '',
  gun_sayisi: definition.gun_sayisi ?? '',
  aciklama: definition.aciklama || '',
  aktif: definition.aktif !== false,
});

const getDefinitions = (data) => {
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.tanimlar)) return data.tanimlar;
  if (Array.isArray(data?.paymentPeriods)) return data.paymentPeriods;
  return [];
};

const getErrorMessage = (error, fallback) =>
  error.response?.data?.hata
  || error.response?.data?.mesaj
  || error.response?.data?.message
  || fallback;

function Settings() {
  const [definitions, setDefinitions] = useState([]);
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

  const fetchDefinitions = useCallback(async ({ showLoading = true } = {}) => {
    if (showLoading) setLoading(true);
    setLoadFailed(false);
    setError('');

    try {
      const response = await api.get('/api/payment-periods');
      const orderedDefinitions = getDefinitions(response.data).sort((first, second) => {
        const dayDifference = Number(first.gun_sayisi) - Number(second.gun_sayisi);
        return dayDifference || (first.ad || '').localeCompare(second.ad || '', 'tr');
      });
      setDefinitions(orderedDefinitions);
    } catch (requestError) {
      setLoadFailed(true);
      setError(
        getErrorMessage(requestError, 'Ödeme periyodu tanımları yüklenirken bir hata oluştu.'),
      );
    } finally {
      if (showLoading) setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDefinitions();
  }, [fetchDefinitions]);

  const counts = useMemo(() => {
    const active = definitions.filter((definition) => definition.aktif !== false).length;
    return {
      all: definitions.length,
      active,
      passive: definitions.length - active,
    };
  }, [definitions]);

  const filteredDefinitions = useMemo(() => {
    if (statusFilter === 'active') {
      return definitions.filter((definition) => definition.aktif !== false);
    }
    if (statusFilter === 'passive') {
      return definitions.filter((definition) => definition.aktif === false);
    }
    return definitions;
  }, [definitions, statusFilter]);

  const resetForm = () => {
    setEditingId('');
    setFormData(createEmptyForm());
    setError('');
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setNotice('');

    const name = formData.ad.trim();
    const dayCount = Number(formData.gun_sayisi);

    if (!name) {
      setError('Periyot adını girin.');
      return;
    }
    if (!Number.isInteger(dayCount) || dayCount < 1 || dayCount > 365) {
      setError('Gün sayısı 1 ile 365 arasında bir tam sayı olmalıdır.');
      return;
    }

    const payload = {
      ad: name,
      gun_sayisi: dayCount,
      aciklama: formData.aciklama.trim(),
      aktif: Boolean(formData.aktif),
    };

    setSaving(true);
    try {
      if (editingId) {
        await api.put(`/api/payment-periods/${editingId}`, payload);
        setNotice('Ödeme periyodu güncellendi.');
      } else {
        await api.post('/api/payment-periods', payload);
        setNotice('Ödeme periyodu eklendi.');
      }

      setEditingId('');
      setFormData(createEmptyForm());
      await fetchDefinitions({ showLoading: false });
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Ödeme periyodu kaydedilemedi.'));
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (definition) => {
    setEditingId(definition._id);
    setFormData(normalizeDefinition(definition));
    setError('');
    setNotice('');
    formRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const handleStatusToggle = async (definition) => {
    const nextStatus = definition.aktif === false;
    setTogglingId(definition._id);
    setError('');
    setNotice('');

    try {
      await api.patch(`/api/payment-periods/${definition._id}/status`, {
        aktif: nextStatus,
      });
      setNotice(
        nextStatus
          ? `${definition.ad} aktif duruma getirildi.`
          : `${definition.ad} pasif duruma alındı.`,
      );
      if (editingId === definition._id) {
        setFormData((current) => ({ ...current, aktif: nextStatus }));
      }
      await fetchDefinitions({ showLoading: false });
    } catch (requestError) {
      setError(getErrorMessage(requestError, 'Ödeme periyodu durumu değiştirilemedi.'));
    } finally {
      setTogglingId('');
    }
  };

  const emptyMessage = statusFilter === 'active'
    ? 'Aktif ödeme periyodu bulunmuyor.'
    : statusFilter === 'passive'
      ? 'Pasif ödeme periyodu bulunmuyor.'
      : 'Henüz ödeme periyodu tanımlanmamış.';

  return (
    <div className="settings-page">
      <header className="settings-page-heading">
        <div>
          <h1>Ayarlar</h1>
          <p>Abone ödeme planlarında kullanılacak periyot tanımlarını yönetin.</p>
        </div>
      </header>

      {error && (
        <div className="settings-feedback settings-feedback-error" role="alert">
          <span>{error}</span>
          {loadFailed && (
            <button type="button" onClick={() => fetchDefinitions()} disabled={loading}>
              Tekrar dene
            </button>
          )}
        </div>
      )}
      {notice && (
        <div className="settings-feedback settings-feedback-success" role="status">
          {notice}
        </div>
      )}

      <form
        ref={formRef}
        className="period-form"
        onSubmit={handleSubmit}
        aria-labelledby="period-form-title"
      >
        <div className="period-form-heading">
          <div>
            <h2 id="period-form-title">
              {editingId ? 'Ödeme periyodunu düzenle' : 'Yeni ödeme periyodu'}
            </h2>
            <p>Periyot süresi 1 ile 365 gün arasında olmalıdır.</p>
          </div>
          {editingId && <span className="period-edit-badge">Düzenleme modunda</span>}
        </div>

        <div className="period-form-grid">
          <div className="period-field">
            <label htmlFor="period-name">Periyot adı *</label>
            <input
              id="period-name"
              type="text"
              required
              maxLength="120"
              value={formData.ad}
              onChange={(event) => setFormData((current) => ({
                ...current,
                ad: event.target.value,
              }))}
              placeholder="Örn. 30 Günlük"
            />
          </div>

          <div className="period-field">
            <label htmlFor="period-day-count">Gün sayısı *</label>
            <input
              id="period-day-count"
              type="number"
              inputMode="numeric"
              required
              min="1"
              max="365"
              step="1"
              value={formData.gun_sayisi}
              onChange={(event) => setFormData((current) => ({
                ...current,
                gun_sayisi: event.target.value,
              }))}
              placeholder="30"
            />
          </div>

          <div className="period-field period-field-description">
            <label htmlFor="period-description">Açıklama</label>
            <textarea
              id="period-description"
              rows="3"
              maxLength="500"
              value={formData.aciklama}
              onChange={(event) => setFormData((current) => ({
                ...current,
                aciklama: event.target.value,
              }))}
              placeholder="Bu periyodun ne zaman kullanılacağını açıklayın"
            />
          </div>
        </div>

        <label className="period-active-control" htmlFor="period-active">
          <input
            id="period-active"
            type="checkbox"
            checked={formData.aktif}
            onChange={(event) => setFormData((current) => ({
              ...current,
              aktif: event.target.checked,
            }))}
          />
          <span>
            <strong>Periyot aktif</strong>
            <small>Aktif tanımlar yeni abone ödeme planlarında seçilebilir.</small>
          </span>
        </label>

        <div className="period-form-actions">
          <button type="submit" className="period-save-button" disabled={saving}>
            {saving
              ? 'Kaydediliyor…'
              : editingId
                ? 'Değişiklikleri kaydet'
                : 'Periyot ekle'}
          </button>
          {editingId && (
            <button
              type="button"
              className="period-cancel-button"
              onClick={resetForm}
              disabled={saving}
            >
              İptal
            </button>
          )}
        </div>
      </form>

      <section className="period-list" aria-labelledby="period-list-title">
        <div className="period-list-heading">
          <div>
            <h2 id="period-list-title">Ödeme periyotları</h2>
            <p>{filteredDefinitions.length} tanım gösteriliyor.</p>
          </div>
          <button
            type="button"
            className="period-refresh-button"
            onClick={() => fetchDefinitions()}
            disabled={loading}
          >
            {loading ? 'Yükleniyor…' : 'Yenile'}
          </button>
        </div>

        <div className="period-filters" aria-label="Ödeme periyodu durumuna göre filtrele">
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

        <div className="period-table-wrapper" aria-busy={loading}>
          <table className="period-table">
            <caption className="sr-only">Ödeme periyodu tanımları</caption>
            <thead>
              <tr>
                <th scope="col">Periyot</th>
                <th scope="col">Gün sayısı</th>
                <th scope="col">Açıklama</th>
                <th scope="col">Durum</th>
                <th scope="col"><span className="sr-only">İşlemler</span></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="5" className="period-table-message">
                    Ödeme periyotları yükleniyor…
                  </td>
                </tr>
              ) : loadFailed ? (
                <tr>
                  <td colSpan="5" className="period-table-message period-table-error">
                    Ödeme periyotları yüklenemedi. Tekrar deneyin.
                  </td>
                </tr>
              ) : filteredDefinitions.length === 0 ? (
                <tr>
                  <td colSpan="5" className="period-table-message">{emptyMessage}</td>
                </tr>
              ) : (
                filteredDefinitions.map((definition) => {
                  const isActive = definition.aktif !== false;
                  return (
                    <tr key={definition._id} className={isActive ? '' : 'period-passive-row'}>
                      <td data-label="Periyot">
                        <strong>{definition.ad}</strong>
                      </td>
                      <td data-label="Gün sayısı" className="period-day-count">
                        {Number(definition.gun_sayisi) || 0} gün
                      </td>
                      <td data-label="Açıklama">
                        <span className={definition.aciklama ? '' : 'period-muted'}>
                          {definition.aciklama || 'Açıklama yok'}
                        </span>
                      </td>
                      <td data-label="Durum">
                        <span className={`period-status ${isActive ? 'active' : 'passive'}`}>
                          {isActive ? 'Aktif' : 'Pasif'}
                        </span>
                      </td>
                      <td className="period-row-actions">
                        <button
                          type="button"
                          className="period-edit-button"
                          onClick={() => handleEdit(definition)}
                          disabled={Boolean(togglingId)}
                        >
                          Düzenle
                        </button>
                        <button
                          type="button"
                          className={`period-toggle-button ${isActive ? 'deactivate' : 'activate'}`}
                          onClick={() => handleStatusToggle(definition)}
                          disabled={togglingId === definition._id}
                          aria-label={`${definition.ad} periyodunu ${isActive ? 'pasife al' : 'aktif et'}`}
                        >
                          {togglingId === definition._id
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

export default Settings;
