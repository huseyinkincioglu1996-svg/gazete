import React, { useCallback, useEffect, useState } from 'react';
import api from '../api';
import './Dashboard.css';

const getToday = () => {
  const today = new Date();
  const offset = today.getTimezoneOffset();
  return new Date(today.getTime() - offset * 60 * 1000).toISOString().slice(0, 10);
};

const formatCurrency = (value) =>
  new Intl.NumberFormat('tr-TR', {
    style: 'currency',
    currency: 'TRY',
    minimumFractionDigits: 2,
  }).format(Number(value) || 0);

const formatDate = (value) => {
  if (!value) return '—';

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('tr-TR');
};

const getErrorMessage = (error) =>
  error.response?.data?.hata || 'Rapor verileri yüklenirken bir hata oluştu.';

function Dashboard() {
  const [selectedDate, setSelectedDate] = useState(getToday);
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchReport = useCallback(async (date) => {
    setLoading(true);
    setError('');

    try {
      const response = await api.get(`/api/reports/daily/${encodeURIComponent(date)}`);
      setReport(response.data);
    } catch (requestError) {
      setReport(null);
      setError(getErrorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchReport(selectedDate);
  }, [fetchReport, selectedDate]);

  const deliveries = report?.deliveries || [];
  const payments = report?.payments || [];
  const summary = report?.ozet || {};
  const completedDeliveries = deliveries.filter((delivery) => delivery.durum === 'Tamamlandı').length;
  const pendingDeliveries = deliveries.filter((delivery) => delivery.durum === 'Beklemede').length;

  return (
    <div className="dashboard">
      <div className="page-heading">
        <div>
          <h1>Raporlar ve Özet</h1>
          <p>Seçtiğiniz güne ait dağıtım ve ödeme durumunu takip edin.</p>
        </div>

        <div className="report-controls">
          <label htmlFor="report-date">Rapor tarihi</label>
          <div className="report-controls-row">
            <input
              id="report-date"
              type="date"
              value={selectedDate}
              onChange={(event) => setSelectedDate(event.target.value)}
            />
            <button
              type="button"
              className="btn-secondary"
              onClick={() => fetchReport(selectedDate)}
              disabled={loading}
            >
              {loading ? 'Yükleniyor…' : 'Yenile'}
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="feedback feedback-error" role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => fetchReport(selectedDate)}>
            Tekrar dene
          </button>
        </div>
      )}

      <section className="stats-grid" aria-label="Günlük özet">
        <article className="stat-card">
          <h2>Dağıtılan gazete</h2>
          <p className="stat-number">{Number(summary.totalGazete) || 0}</p>
          <span>adet</span>
        </article>
        <article className="stat-card success">
          <h2>Tamamlanan dağıtım</h2>
          <p className="stat-number">{completedDeliveries}</p>
          <span>{pendingDeliveries} beklemede</span>
        </article>
        <article className="stat-card">
          <h2>Toplam ödeme</h2>
          <p className="stat-number stat-currency">{formatCurrency(summary.totalTutar)}</p>
          <span>{payments.length} ödeme kaydı</span>
        </article>
        <article className="stat-card warning">
          <h2>Bekleyen ödeme</h2>
          <p className="stat-number stat-currency">{formatCurrency(summary.totalBeklemede)}</p>
          <span>Tahsil oranı: %{Number(summary.tahsilOrani) || 0}</span>
        </article>
      </section>

      {loading ? (
        <p className="loading-state" role="status">Rapor verileri yükleniyor…</p>
      ) : !error && (
        <div className="dashboard-panels">
          <section className="report-panel" aria-labelledby="daily-deliveries-title">
            <div className="panel-heading">
              <div>
                <h2 id="daily-deliveries-title">Günlük dağıtımlar</h2>
                <p>{formatDate(selectedDate)} için kayıtlar</p>
              </div>
              <span className="panel-count">{deliveries.length}</span>
            </div>

            {deliveries.length === 0 ? (
              <p className="empty-state">Bu tarih için dağıtım kaydı bulunmuyor.</p>
            ) : (
              <div className="table-wrapper">
                <table>
                  <thead>
                    <tr>
                      <th scope="col">Dağıtıcı</th>
                      <th scope="col">Gazete</th>
                      <th scope="col">Tutar</th>
                      <th scope="col">Durum</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deliveries.map((delivery) => (
                      <tr key={delivery._id}>
                        <td>{delivery.distributor_id?.isim || 'Silinmiş dağıtıcı'}</td>
                        <td>{Number(delivery.gazeteSayisi) || 0} adet</td>
                        <td>{formatCurrency(delivery.tutar)}</td>
                        <td>
                          <span className={`status status-${delivery.durum === 'Tamamlandı' ? 'success' : 'pending'}`}>
                            {delivery.durum}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="report-panel" aria-labelledby="daily-payments-title">
            <div className="panel-heading">
              <div>
                <h2 id="daily-payments-title">Günlük ödemeler</h2>
                <p>{formatDate(selectedDate)} tarihinde vadesi olanlar</p>
              </div>
              <span className="panel-count">{payments.length}</span>
            </div>

            {payments.length === 0 ? (
              <p className="empty-state">Bu tarih için ödeme kaydı bulunmuyor.</p>
            ) : (
              <div className="table-wrapper">
                <table>
                  <thead>
                    <tr>
                      <th scope="col">Dağıtıcı</th>
                      <th scope="col">Tür</th>
                      <th scope="col">Tutar</th>
                      <th scope="col">Durum</th>
                    </tr>
                  </thead>
                  <tbody>
                    {payments.map((payment) => (
                      <tr key={payment._id}>
                        <td>{payment.distributor_id?.isim || 'Silinmiş dağıtıcı'}</td>
                        <td>{payment.odeme_turu}</td>
                        <td>{formatCurrency(payment.tutar)}</td>
                        <td>
                          <span className={`status status-${payment.durum === 'Ödendi' ? 'success' : 'pending'}`}>
                            {payment.durum}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  );
}

export default Dashboard;
