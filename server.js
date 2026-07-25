const express = require('express');
const cors = require('cors');
require('dotenv').config();

const app = express();

// Middleware
app.use(cors());
app.use(express.json());

// Test Endpoint
app.get('/api/test', (req, res) => {
  res.json({ mesaj: 'Server çalışıyor! 🚀' });
});

// Server Başlat
const PORT = process.env.PORT || 5000;
app.listen(PORT, () => {
  console.log(`Server ${PORT} portunda çalışıyor...`);
});