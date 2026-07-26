import React, { useState, useEffect } from 'react';
import api from '../api';
import './Payments.css';

function Payments() {
  const [payments, setPayments] = useState([]);
  const [filterStatus, setFilterStatus] = useState('');

  useEffect(() => {
    fetchPayments();
  }, []);

  const fetchPayments = async () => {
    try {
      const response = await api.get('/api/payments');
      setPayments(response.data);
    } catch (err) {
      console.error('❌ Ödemeler yüklenirken hata:', err);
    }
  };

  const handlePayment = async (id) => {
    if (window.confirm('Ödemeyi tamamlamak istediğinize emin misiniz?')) {
      try {
        await api.put(`/api/payments/${id}/pay`);
        fetchPayments();
      } catch (err) {
        console.error('❌ Ödeme hatası:', err);
      }
    }
  };

  const filteredPayments = filterStatus
    ? payments.filter(p => p.durum === filterStatus)
    : payments;

  const totalAmount = filteredPayments.reduce((sum, p) => sum + p.tutar, 0);
  const paidAmount = filteredPayments
    .filter(p => p.durum === 'Ödendi')
    .reduce((sum, p) => sum + p.tutar, 0);

  return (
    <div className="payments">
      <h1>💰 Ödeme Yönetimi</h1>

      <div className="payment-summary">
        <div className="summary-card">
          <h3>Toplam Tutar</h3>
          <p>{totalAmount.toFixed(2)} ₺</p>
        </div>
        <div className="summary-card success">
          <h3>Ödenen</h3>
          <p>{paidAmount.toFixed(2)} ₺</p>
        </div>
        <div className="summary-card warning">
          <h3>Beklemede</h3>
          <p>{(totalAmount - paidAmount).toFixed(2)} ₺</p>
        </div>
      </div>

      <div className="filters">
        <button
          className={`filter-btn ${filterStatus === '' ? 'active' : ''}`}
          onClick={() => setFilterStatus('')}
        >
          Tümü ({payments.length})
        </button>
        <button
          className={`filter-btn ${filterStatus === 'Beklemede' ? 'active' : ''}`}
          onClick={() => setFilterStatus('Beklemede')}
        >
          Beklemede ({payments.filter(p => p.durum === 'Beklemede').length})
        </button>
        <button
          className={`filter-btn ${filterStatus === 'Ödendi' ? 'active' : ''}`}
          onClick={() => setFilterStatus('Ödendi')}
        >
          Ödendi ({payments.filter(p => p.durum === 'Ödendi').length})
        </button>
      </div>

      <div className="payments-list">
        <table>
          <thead>
            <tr>
              <th>Dağıtıcı</th>
              <th>Tutar</th>
              <th>Dönem</th>
              <th>Tür</th>
              <th>Durum</th>
              <th>İşlemler</th>
            </tr>
          </thead>
          <tbody>
            {filteredPayments.map((payment) => (
              <tr key={payment._id} className={payment.durum === 'Ödendi' ? 'paid' : ''}>
                <td>{payment.distributor_id?.isim}</td>
                <td className="amount">{payment.tutar.toFixed(2)} ₺</td>
                <td>
                  {new Date(payment.donem_baslangic).toLocaleDateString('tr-TR')} -
                  {new Date(payment.donem_bitis).toLocaleDateString('tr-TR')}
                </td>
                <td>{payment.odeme_turu}</td>
                <td>
                  <span className={`status ${payment.durum === 'Ödendi' ? 'paid' : 'pending'}`}>
                    {payment.durum}
                  </span>
                </td>
                <td>
                  {payment.durum === 'Beklemede' && (
                    <button
                      className="btn-pay"
                      onClick={() => handlePayment(payment._id)}
                    >
                      Ödemeyi Tamamla
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default Payments;
