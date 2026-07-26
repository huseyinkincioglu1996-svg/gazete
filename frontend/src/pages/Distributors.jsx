import React, { useState, useEffect } from 'react';
import api from '../api';
import './Distributors.css';

function Distributors() {
  const [distributors, setDistributors] = useState([]);
  const [formData, setFormData] = useState({
    isim: '',
    adres: '',
    telefon: '',
    bolge: 'Bölge 1',
    gazete_fiyat: 5,
    odeme_tipi: 'Günlük'
  });
  const [editingId, setEditingId] = useState(null);

  useEffect(() => {
    fetchDistributors();
  }, []);

  const fetchDistributors = async () => {
    try {
      const response = await api.get('/api/distributors');
      setDistributors(response.data);
    } catch (err) {
      console.error('❌ Dağıtıcılar yüklenirken hata:', err);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/api/distributors/${editingId}`, formData);
        setEditingId(null);
      } else {
        await api.post('/api/distributors', formData);
      }
      setFormData({
        isim: '',
        adres: '',
        telefon: '',
        bolge: 'Bölge 1',
        gazete_fiyat: 5,
        odeme_tipi: 'Günlük'
      });
      fetchDistributors();
    } catch (err) {
      console.error('❌ Hata:', err);
    }
  };

  const handleDelete = async (id) => {
    if (window.confirm('Silmek istediğinize emin misiniz?')) {
      try {
        await api.delete(`/api/distributors/${id}`);
        fetchDistributors();
      } catch (err) {
        console.error('❌ Silme hatası:', err);
      }
    }
  };

  const handleEdit = (distributor) => {
    setFormData(distributor);
    setEditingId(distributor._id);
  };

  return (
    <div className="distributors">
      <h1>👥 Dağıtıcı Yönetimi</h1>

      <form className="form" onSubmit={handleSubmit}>
        <div className="form-group">
          <label>İsim *</label>
          <input
            type="text"
            required
            value={formData.isim}
            onChange={(e) => setFormData({ ...formData, isim: e.target.value })}
          />
        </div>

        <div className="form-group">
          <label>Adres *</label>
          <input
            type="text"
            required
            value={formData.adres}
            onChange={(e) => setFormData({ ...formData, adres: e.target.value })}
          />
        </div>

        <div className="form-group">
          <label>Telefon *</label>
          <input
            type="tel"
            required
            value={formData.telefon}
            onChange={(e) => setFormData({ ...formData, telefon: e.target.value })}
          />
        </div>

        <div className="form-group">
          <label>Bölge</label>
          <select
            value={formData.bolge}
            onChange={(e) => setFormData({ ...formData, bolge: e.target.value })}
          >
            <option>Bölge 1</option>
            <option>Bölge 2</option>
          </select>
        </div>

        <div className="form-group">
          <label>Gazete Fiyatı (₺)</label>
          <input
            type="number"
            value={formData.gazete_fiyat}
            onChange={(e) => setFormData({ ...formData, gazete_fiyat: parseFloat(e.target.value) })}
          />
        </div>

        <div className="form-group">
          <label>Ödeme Tipi</label>
          <select
            value={formData.odeme_tipi}
            onChange={(e) => setFormData({ ...formData, odeme_tipi: e.target.value })}
          >
            <option>Günlük</option>
            <option>Haftalık</option>
            <option>Aylık</option>
          </select>
        </div>

        <button type="submit" className="btn-primary">
          {editingId ? 'Güncelle' : 'Ekle'}
        </button>
        {editingId && (
          <button
            type="button"
            className="btn-secondary"
            onClick={() => {
              setEditingId(null);
              setFormData({
                isim: '',
                adres: '',
                telefon: '',
                bolge: 'Bölge 1',
                gazete_fiyat: 5,
                odeme_tipi: 'Günlük'
              });
            }}
          >
            İptal
          </button>
        )}
      </form>

      <div className="distributors-list">
        <table>
          <thead>
            <tr>
              <th>İsim</th>
              <th>Adres</th>
              <th>Telefon</th>
              <th>Bölge</th>
              <th>Fiyat</th>
              <th>Ödeme Tipi</th>
              <th>İşlemler</th>
            </tr>
          </thead>
          <tbody>
            {distributors.map((distributor) => (
              <tr key={distributor._id}>
                <td>{distributor.isim}</td>
                <td>{distributor.adres}</td>
                <td>{distributor.telefon}</td>
                <td>{distributor.bolge}</td>
                <td>{distributor.gazete_fiyat} ₺</td>
                <td>{distributor.odeme_tipi}</td>
                <td>
                  <button
                    className="btn-edit"
                    onClick={() => handleEdit(distributor)}
                  >
                    Düzenle
                  </button>
                  <button
                    className="btn-delete"
                    onClick={() => handleDelete(distributor._id)}
                  >
                    Sil
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default Distributors;
