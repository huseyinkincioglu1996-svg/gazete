import React, { useState, useEffect } from 'react';
import api from '../api';
import './Dashboard.css';

function Dashboard() {
  const [stats, setStats] = useState({
    totalDistributors: 0,
    totalDeliveries: 0,
    totalPayments: 0,
    totalRevenue: 0,
    paidAmount: 0,
    pendingAmount: 0
  });

  useEffect(() => {
    fetchStats();
  }, []);

  const fetchStats = async () => {
    try {
      const distributors = await api.get('/api/distributors');
      const deliveries = await api.get('/api/deliveries');
      const payments = await api.get('/api/payments');

      const totalRevenue = payments.data.reduce((sum, p) => sum + p.tutar, 0);
      const paidAmount = payments.data
        .filter(p => p.durum === 'Ödendi')
        .reduce((sum, p) => sum + p.tutar, 0);
      const pendingAmount = totalRevenue - paidAmount;

      setStats({
        totalDistributors: distributors.data.length,
        totalDeliveries: deliveries.data.length,
        totalPayments: payments.data.length,
        totalRevenue,
        paidAmount,
        pendingAmount
      });
    } catch (err) {
      console.error('❌ Veriler yüklenirken hata:', err);
    }
  };

  return (
    <div className="dashboard">
      <h1>📊 Raporlar</h1>
      
      <div className="stats-grid">
        <div className="stat-card">
          <h3>Dağıtıcılar</h3>
          <p className="stat-number">{stats.totalDistributors}</p>
        </div>
        
        <div className="stat-card">
          <h3>Dağıtımlar</h3>
          <p className="stat-number">{stats.totalDeliveries}</p>
        </div>
        
        <div className="stat-card">
          <h3>Ödemeler</h3>
          <p className="stat-number">{stats.totalPayments}</p>
        </div>
        
        <div className="stat-card">
          <h3>Toplam Gelir</h3>
          <p className="stat-number">{stats.totalRevenue.toFixed(2)} ₺</p>
        </div>
        
        <div className="stat-card success">
          <h3>Ödenen</h3>
          <p className="stat-number">{stats.paidAmount.toFixed(2)} ₺</p>
        </div>
        
        <div className="stat-card warning">
          <h3>Beklemede</h3>
          <p className="stat-number">{stats.pendingAmount.toFixed(2)} ₺</p>
        </div>
      </div>
    </div>
  );
}

export default Dashboard;
