import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import api from '../api';
import './CashHandover.css';

let rowSequence = 0;

const getToday = () => {
  const today = new Date();
  const offset = today.getTimezoneOffset();
  return new Date(today.getTime() - offset * 60 * 1000).toISOString().slice(0, 10);
};

const createRow = (item = {}) => {
  rowSequence += 1;
  return {
    clientId: `cash-row-${Date.now()}-${rowSequence}`,
    abone: item.abone || '',
    tutar: item.tutar ?? '',
    aciklama: item.aciklama || '',
  };
};

const createAutomaticRow = (item = {}, index = 0) => ({
  clientId: `automatic-cash-row-${item.kaynak_id || item._id || index}`,
  abone: item.abone || '',
  tutar: item.tutar ?? 0,
  aciklama: item.aciklama || 'Dağıtımlar menüsünden aktarıldı',
  odemeYontemi: item.odeme_yontemi || 'Nakit',
});

const formatCurrency = (value) =>
  new Intl.NumberFormat('tr-TR', {
    style: 'currency',
    currency: 'TRY',
    minimumFractionDigits: 2,
  }).format(Number(value) || 0);

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

const formatDateTime = (value) => {
  const date = toLocalDate(value);
  return date
    ? date.toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })
    : '—';
};

const getMonthRangeLabel = (monthKey) => {
  const [year, month] = monthKey.split('-').map(Number);
  const firstDay = new Date(year, month - 1, 1, 12);
  const lastDay = new Date(year, month, 0, 12);
  const start = firstDay.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long' });
  const end = lastDay.toLocaleDateString('tr-TR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
  return `${start} – ${end}`;
};

const getMonthTitle = (monthKey) => {
  const [year, month] = monthKey.split('-').map(Number);
  return new Date(year, month - 1, 1, 12).toLocaleDateString('tr-TR', {
    month: 'long',
    year: 'numeric',
  });
};

const getErrorMessage = (error, fallback) =>
  error.response?.data?.hata
  || error.response?.data?.mesaj
  || error.response?.data?.message
  || fallback;

const unwrapDailyData = (data) => data?.kasaTeslimi || data;

function CashHandover() {
  const [selectedDate, setSelectedDate] = useState(getToday);
  const [rows, setRows] = useState(() => [createRow()]);
  const [automaticRows, setAutomaticRows] = useState([]);
  const [status, setStatus] = useState('Taslak');
  const [deliveredAt, setDeliveredAt] = useState('');
  const [isEmpty, setIsEmpty] = useState(true);
  const [dailyLoading, setDailyLoading] = useState(true);
  const [monthlyLoading, setMonthlyLoading] = useState(true);
  const [savingMode, setSavingMode] = useState('');
  const [dailyError, setDailyError] = useState('');
  const [dailyLoadFailed, setDailyLoadFailed] = useState(false);
  const [monthlyError, setMonthlyError] = useState('');
  const [notice, setNotice] = useState('');
  const [monthlySummary, setMonthlySummary] = useState({
    toplam: 0,
    teslimEdilenGunSayisi: 0,
  });
  const [subscriberSuggestions, setSubscriberSuggestions] = useState([]);
  const dailyRequestId = useRef(0);
  const monthlyRequestId = useRef(0);

  const monthKey = selectedDate.slice(0, 7);
  const isDelivered = status === 'Teslim Edildi';

  const dailyTotal = useMemo(
    () => [...automaticRows, ...rows].reduce((total, row) => {
      const amount = Number(String(row.tutar).replace(',', '.'));
      return total + (Number.isFinite(amount) ? amount : 0);
    }, 0),
    [automaticRows, rows],
  );

  const applyDailyData = useCallback((rawData) => {
    const data = unwrapDailyData(rawData);
    const items = Array.isArray(data?.kalemler) ? data.kalemler : [];
    const automaticItems = Array.isArray(data?.otomatik_kalemler)
      ? data.otomatik_kalemler
      : Array.isArray(data?.otomatikKalemler)
        ? data.otomatikKalemler
        : [];
    const hasRecord = Boolean(
      data
      && (
        data._id
        || items.length
        || automaticItems.length
        || data.teslim_tarihi
        || Number(data.toplam) > 0
      ),
    );

    setRows(items.length ? items.map(createRow) : [createRow()]);
    setAutomaticRows(automaticItems.map(createAutomaticRow));
    setStatus(data?.durum === 'Teslim Edildi' ? 'Teslim Edildi' : 'Taslak');
    setDeliveredAt(data?.teslim_tarihi || '');
    setIsEmpty(!hasRecord);
  }, []);

  const resetDailyView = useCallback(() => {
    setRows([createRow()]);
    setAutomaticRows([]);
    setStatus('Taslak');
    setDeliveredAt('');
    setIsEmpty(true);
    setDailyError('');
    setDailyLoadFailed(false);
    setNotice('');
  }, []);

  const loadDaily = useCallback(async (date, { showLoading = true } = {}) => {
    const requestId = dailyRequestId.current + 1;
    dailyRequestId.current = requestId;
    if (showLoading) setDailyLoading(true);
    setDailyError('');
    setDailyLoadFailed(false);

    try {
      const response = await api.get(`/api/cash-handovers/daily/${date}`);
      if (dailyRequestId.current === requestId) applyDailyData(response.data);
    } catch (requestError) {
      if (dailyRequestId.current !== requestId) return;

      if (requestError.response?.status === 404) {
        applyDailyData(null);
      } else {
        setDailyLoadFailed(true);
        setDailyError(
          getErrorMessage(requestError, 'Günlük kasa kaydı yüklenirken bir hata oluştu.'),
        );
      }
    } finally {
      if (dailyRequestId.current === requestId && showLoading) setDailyLoading(false);
    }
  }, [applyDailyData]);

  const loadMonthly = useCallback(async (month, { showLoading = true } = {}) => {
    const requestId = monthlyRequestId.current + 1;
    monthlyRequestId.current = requestId;
    if (showLoading) setMonthlyLoading(true);
    setMonthlyError('');

    try {
      const response = await api.get(`/api/cash-handovers/monthly/${month}`);
      if (monthlyRequestId.current !== requestId) return;

      setMonthlySummary({
        toplam: Number(response.data?.toplam) || 0,
        teslimEdilenGunSayisi: Number(
          response.data?.teslimEdilenGunSayisi
          ?? response.data?.teslim_edilen_gun_sayisi,
        ) || 0,
      });
    } catch (requestError) {
      if (monthlyRequestId.current !== requestId) return;
      setMonthlySummary({ toplam: 0, teslimEdilenGunSayisi: 0 });
      setMonthlyError(
        getErrorMessage(requestError, 'Aylık kasa toplamı yüklenirken bir hata oluştu.'),
      );
    } finally {
      if (monthlyRequestId.current === requestId && showLoading) setMonthlyLoading(false);
    }
  }, []);

  useEffect(() => {
    loadDaily(selectedDate);
  }, [loadDaily, selectedDate]);

  useEffect(() => {
    loadMonthly(monthKey);
  }, [loadMonthly, monthKey]);

  useEffect(() => {
    let isMounted = true;

    api.get('/api/subscribers', { params: { aktif: true } })
      .then((response) => {
        if (!isMounted) return;

        const subscribers = Array.isArray(response.data)
          ? response.data
          : Array.isArray(response.data?.aboneler)
            ? response.data.aboneler
            : [];
        const uniqueNames = [...new Set(
          subscribers
            .filter((subscriber) => subscriber?.aktif !== false && subscriber?.isim)
            .map((subscriber) => subscriber.isim.trim())
            .filter(Boolean),
        )].sort((first, second) => first.localeCompare(second, 'tr'));
        setSubscriberSuggestions(uniqueNames);
      })
      .catch(() => {
        // Öneriler yardımcıdır; API erişilemezse serbest metin girişi çalışmaya devam eder.
        if (isMounted) setSubscriberSuggestions([]);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const handleDateChange = (event) => {
    if (!event.target.value) return;
    resetDailyView();
    setSelectedDate(event.target.value);
  };

  const updateRow = (clientId, field, value) => {
    setRows((currentRows) => currentRows.map(
      (row) => (row.clientId === clientId ? { ...row, [field]: value } : row),
    ));
    setIsEmpty(false);
    setNotice('');
  };

  const addRow = () => {
    setRows((currentRows) => [...currentRows, createRow()]);
    setIsEmpty(false);
    setNotice('');
  };

  const removeRow = (clientId) => {
    setRows((currentRows) => {
      const remainingRows = currentRows.filter((row) => row.clientId !== clientId);
      return remainingRows.length ? remainingRows : [createRow()];
    });
    setNotice('');
  };

  const prepareItems = () => {
    const nonEmptyRows = rows.filter(
      (row) => row.abone.trim() || String(row.tutar).trim() || row.aciklama.trim(),
    );

    if (!nonEmptyRows.length && !automaticRows.length) {
      return { error: 'Kaydetmek için en az bir abone ve tahsilat tutarı ekleyin.' };
    }

    for (let index = 0; index < nonEmptyRows.length; index += 1) {
      const row = nonEmptyRows[index];
      const amountText = String(row.tutar).trim();
      const amount = Number(amountText.replace(',', '.'));

      if (!row.abone.trim()) {
        return { error: `${index + 1}. satırda abone adını girin.` };
      }
      if (!amountText || !Number.isFinite(amount) || amount < 0) {
        return { error: `${index + 1}. satırda geçerli bir tahsilat tutarı girin.` };
      }
    }

    return {
      items: nonEmptyRows.map((row) => ({
        abone: row.abone.trim(),
        tutar: Number(String(row.tutar).replace(',', '.')),
        aciklama: row.aciklama.trim(),
      })),
    };
  };

  const saveHandover = async (nextStatus) => {
    if (isDelivered) return;

    setNotice('');
    setDailyError('');
    setDailyLoadFailed(false);
    const prepared = prepareItems();

    if (prepared.error) {
      setDailyError(prepared.error);
      return;
    }

    if (
      nextStatus === 'Teslim Edildi'
      && !window.confirm(
        `${formatDate(selectedDate)} tarihli ${formatCurrency(dailyTotal)} tutarındaki kasayı teslim etmek istiyor musunuz?`,
      )
    ) {
      return;
    }

    setSavingMode(nextStatus);
    try {
      const response = await api.put(`/api/cash-handovers/daily/${selectedDate}`, {
        kalemler: prepared.items,
        durum: nextStatus,
      });

      const responseData = unwrapDailyData(response.data);
      if (Array.isArray(responseData?.kalemler)) {
        applyDailyData(responseData);
      } else {
        await loadDaily(selectedDate, { showLoading: false });
      }

      setNotice(
        nextStatus === 'Teslim Edildi'
          ? 'Günlük kasa başarıyla teslim edildi.'
          : 'Günlük kasa taslak olarak kaydedildi.',
      );
      await loadMonthly(monthKey, { showLoading: false });
    } catch (requestError) {
      setDailyError(
        getErrorMessage(
          requestError,
          nextStatus === 'Teslim Edildi'
            ? 'Kasa teslim edilemedi.'
            : 'Kasa taslağı kaydedilemedi.',
        ),
      );
    } finally {
      setSavingMode('');
    }
  };

  return (
    <div className="cash-handover">
      <header className="cash-page-heading">
        <div>
          <h1>Günlük Kasa Teslimi</h1>
          <p>Abonelerden topladığınız ücretleri kaydedin ve gün sonunda kasayı teslim edin.</p>
        </div>
        <div className="cash-date-control">
          <label htmlFor="cash-handover-date">Kasa tarihi</label>
          <input
            id="cash-handover-date"
            type="date"
            required
            value={selectedDate}
            onChange={handleDateChange}
            disabled={Boolean(savingMode)}
          />
        </div>
      </header>

      <section
        className="monthly-cash-card"
        aria-labelledby="monthly-cash-title"
        aria-busy={monthlyLoading}
      >
        <div className="monthly-cash-icon" aria-hidden="true">₺</div>
        <div className="monthly-cash-copy">
          <p className="monthly-cash-eyebrow">{getMonthTitle(monthKey)} kasa özeti</p>
          <h2 id="monthly-cash-title">Ay içinde teslim edilen toplam</h2>
          <p className="monthly-cash-range">{getMonthRangeLabel(monthKey)}</p>
        </div>
        <div className="monthly-cash-value" aria-live="polite">
          {monthlyLoading ? (
            <span className="monthly-cash-loading">Aylık toplam yükleniyor…</span>
          ) : monthlyError ? (
            <div className="monthly-cash-error" role="alert">
              <span>{monthlyError}</span>
              <button type="button" onClick={() => loadMonthly(monthKey)}>
                Tekrar dene
              </button>
            </div>
          ) : (
            <>
              <strong>{formatCurrency(monthlySummary.toplam)}</strong>
              <span>
                {monthlySummary.teslimEdilenGunSayisi} teslim edilmiş gün
              </span>
            </>
          )}
        </div>
      </section>

      {dailyError && (
        <div className="cash-feedback cash-feedback-error" role="alert">
          <span>{dailyError}</span>
          {dailyLoadFailed && !savingMode && (
            <button type="button" onClick={() => loadDaily(selectedDate)}>
              Tekrar dene
            </button>
          )}
        </div>
      )}
      {notice && (
        <div className="cash-feedback cash-feedback-success" role="status">
          {notice}
        </div>
      )}

      <section
        className="cash-editor"
        aria-labelledby="daily-cash-title"
        aria-busy={dailyLoading}
      >
        <div className="cash-editor-heading">
          <div>
            <div className="cash-title-line">
              <h2 id="daily-cash-title">{formatDate(selectedDate)} tahsilatları</h2>
              <span
                className={`cash-status ${isDelivered ? 'cash-status-delivered' : 'cash-status-draft'}`}
              >
                {isDelivered ? 'Teslim Edildi' : isEmpty ? 'Yeni kayıt' : 'Taslak'}
              </span>
            </div>
            <p>
              Dağıtımlar menüsündeki nakit tahsilatlar otomatik gelir; diğer tutarları
              manuel ekleyebilirsiniz.
            </p>
          </div>
          {isDelivered && (
            <p className="cash-delivery-time">
              Teslim zamanı: <strong>{formatDateTime(deliveredAt)}</strong>
            </p>
          )}
        </div>

        {isEmpty && !dailyLoading && !dailyError && (
          <div className="cash-empty-notice" role="status">
            Bu tarih için henüz kasa kaydı yok. İlk tahsilatı girerek başlayabilirsiniz.
          </div>
        )}

        {isDelivered && (
          <div className="cash-locked-notice" role="status">
            Bu güne ait kasa teslim edildiği için kayıt değişikliğe kapalıdır.
          </div>
        )}

        {automaticRows.length > 0 && (
          <div className="cash-automatic-summary" role="status">
            <div>
              <strong>Dağıtımlar’dan otomatik aktarılan nakit</strong>
              <span>{automaticRows.length} tahsilat</span>
            </div>
            <strong>
              {formatCurrency(
                automaticRows.reduce((sum, row) => sum + (Number(row.tutar) || 0), 0),
              )}
            </strong>
          </div>
        )}

        <form
          onSubmit={(event) => {
            event.preventDefault();
            saveHandover('Taslak');
          }}
          noValidate
        >
          <datalist id="active-subscriber-options">
            {subscriberSuggestions.map((subscriberName) => (
              <option key={subscriberName} value={subscriberName} />
            ))}
          </datalist>
          <div className="cash-table-wrapper">
            <table className="cash-table">
              <caption className="sr-only">
                {formatDate(selectedDate)} tarihinde abonelerden tahsil edilen ücretler
              </caption>
              <thead>
                <tr>
                  <th scope="col">Abone adı</th>
                  <th scope="col">Tahsil edilen tutar</th>
                  <th scope="col">Açıklama</th>
                  <th scope="col"><span className="sr-only">Satır işlemleri</span></th>
                </tr>
              </thead>
              <tbody>
                {dailyLoading ? (
                  <tr>
                    <td colSpan="4" className="cash-table-message">
                      Günlük kasa kaydı yükleniyor…
                    </td>
                  </tr>
                ) : dailyLoadFailed ? (
                  <tr>
                    <td colSpan="4" className="cash-table-message cash-table-error">
                      Kayıt yüklenemedi. Yukarıdaki uyarıdan tekrar deneyebilirsiniz.
                    </td>
                  </tr>
                ) : (
                  <>
                    {automaticRows.map((row) => (
                      <tr key={row.clientId} className="cash-automatic-row">
                        <td data-label="Abone adı">
                          <div className="cash-automatic-subscriber">
                            <strong>{row.abone}</strong>
                            <span>Dağıtımlar’dan otomatik</span>
                          </div>
                        </td>
                        <td data-label="Tahsil edilen tutar">
                          <strong className="cash-automatic-amount">
                            {formatCurrency(row.tutar)}
                          </strong>
                        </td>
                        <td data-label="Açıklama">
                          <span>{row.aciklama}</span>
                        </td>
                        <td className="cash-row-action">
                          <span className="cash-automatic-badge">
                            {row.odemeYontemi} · Otomatik
                          </span>
                        </td>
                      </tr>
                    ))}
                    {rows.map((row, index) => (
                      <tr key={row.clientId}>
                      <td data-label="Abone adı">
                        <label className="sr-only" htmlFor={`${row.clientId}-subscriber`}>
                          {index + 1}. satır abone adı
                        </label>
                        <input
                          id={`${row.clientId}-subscriber`}
                          type="text"
                          list="active-subscriber-options"
                          value={row.abone}
                          onChange={(event) => updateRow(row.clientId, 'abone', event.target.value)}
                          placeholder="Örn. Ayşe Yılmaz"
                          maxLength="120"
                          disabled={isDelivered || Boolean(savingMode)}
                        />
                      </td>
                      <td data-label="Tahsil edilen tutar">
                        <label className="sr-only" htmlFor={`${row.clientId}-amount`}>
                          {index + 1}. satır tahsil edilen tutar
                        </label>
                        <div className="cash-amount-input">
                          <span aria-hidden="true">₺</span>
                          <input
                            id={`${row.clientId}-amount`}
                            type="number"
                            inputMode="decimal"
                            min="0"
                            step="0.01"
                            value={row.tutar}
                            onChange={(event) => updateRow(row.clientId, 'tutar', event.target.value)}
                            placeholder="0,00"
                            disabled={isDelivered || Boolean(savingMode)}
                          />
                        </div>
                      </td>
                      <td data-label="Açıklama">
                        <label className="sr-only" htmlFor={`${row.clientId}-description`}>
                          {index + 1}. satır açıklaması
                        </label>
                        <input
                          id={`${row.clientId}-description`}
                          type="text"
                          value={row.aciklama}
                          onChange={(event) => updateRow(row.clientId, 'aciklama', event.target.value)}
                          placeholder="İsteğe bağlı not"
                          maxLength="250"
                          disabled={isDelivered || Boolean(savingMode)}
                        />
                      </td>
                      <td className="cash-row-action">
                        <button
                          type="button"
                          className="cash-remove-button"
                          onClick={() => removeRow(row.clientId)}
                          disabled={isDelivered || Boolean(savingMode)}
                          aria-label={`${index + 1}. tahsilat satırını sil`}
                        >
                          Sil
                        </button>
                      </td>
                      </tr>
                    ))}
                  </>
                )}
              </tbody>
              {!dailyLoading && !dailyLoadFailed && (
                <tfoot>
                  <tr>
                    <td colSpan="4">
                      <span>Günlük toplam</span>
                      <output aria-live="polite">{formatCurrency(dailyTotal)}</output>
                    </td>
                  </tr>
                </tfoot>
              )}
            </table>
          </div>

          {!dailyLoading && !dailyLoadFailed && (
            <div className="cash-editor-footer">
              <button
                type="button"
                className="cash-add-button"
                onClick={addRow}
                disabled={isDelivered || Boolean(savingMode)}
              >
                <span aria-hidden="true">+</span> Tahsilat satırı ekle
              </button>
              <div className="cash-form-actions">
                <button
                  type="submit"
                  className="cash-draft-button"
                  disabled={isDelivered || Boolean(savingMode)}
                >
                  {savingMode === 'Taslak' ? 'Kaydediliyor…' : 'Taslak Kaydet'}
                </button>
                <button
                  type="button"
                  className="cash-deliver-button"
                  onClick={() => saveHandover('Teslim Edildi')}
                  disabled={isDelivered || Boolean(savingMode)}
                >
                  {savingMode === 'Teslim Edildi' ? 'Teslim ediliyor…' : 'Kasayı Teslim Et'}
                </button>
              </div>
            </div>
          )}
        </form>
      </section>
    </div>
  );
}

export default CashHandover;
