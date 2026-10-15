const $ = s => document.querySelector(s);
let session = JSON.parse(localStorage.getItem('session') || 'null');
const fmtDT = d => new Date(d).toLocaleString('tr-TR');
const money = v => Number(v).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' });
const dur = m => `${Math.floor(m / 60)} sa ${m % 60} dk`;
// Kullanıcı girdisi içeren her değer HTML'e basılmadan önce kaçışlanır (XSS koruması)
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function toast(msg, type = 'success') {
  const el = document.createElement('div');
  el.className = `alert alert-${type} shadow position-fixed top-0 end-0 m-3`;
  el.style.zIndex = 2000; el.textContent = msg;
  document.body.append(el); setTimeout(() => el.remove(), 3500);
}
const fail = e => toast(e.message, 'danger');

async function api(path, opts = {}) {
  const headers = { ...(opts.headers || {}) };
  if (session) headers.Authorization = 'Bearer ' + session.token;
  if (opts.body && !(opts.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
    opts = { ...opts, body: JSON.stringify(opts.body) };
  }
  const res = await fetch('/api' + path, { ...opts, headers });
  if (res.status === 401 && session && path !== '/auth/login') { logout(); throw new Error('Oturum süresi doldu, tekrar giriş yapın.'); }
  if (res.status === 403) throw new Error('Bu işlem için yetkiniz yok.');
  const data = res.status === 204 ? null : await res.json().catch(() => null);
  if (!res.ok) throw new Error(data?.message || `İşlem başarısız (${res.status})`);
  return data;
}

/* ---------- Oturum ---------- */
async function login(e) {
  e.preventDefault();
  try {
    session = null; // eski/bozuk token gönderilmesin
    session = await api('/auth/login', { method: 'POST', body: { username: $('#lu').value, password: $('#lp').value } });
    localStorage.setItem('session', JSON.stringify(session));
    $('#loginErr').textContent = ''; $('#lp').value = '';
    initApp();
  } catch (err) { session = null; $('#loginErr').textContent = err.message; }
}
function logout() {
  session = null; localStorage.removeItem('session');
  $('#appView').classList.add('d-none'); $('#loginView').classList.remove('d-none');
}
async function changePw() {
  const o = prompt('Mevcut şifre:'); if (!o) return;
  const n = prompt('Yeni şifre (en az 6 karakter):'); if (!n) return;
  try { await api('/auth/change-password', { method: 'POST', body: { oldPassword: o, newPassword: n } }); toast('Şifre değiştirildi'); } catch (e) { fail(e); }
}
function initApp() {
  $('#loginView').classList.add('d-none'); $('#appView').classList.remove('d-none');
  $('#who').textContent = `${session.fullName} (${session.role})`;
  const isAdmin = session.role === 'Admin';
  document.querySelectorAll('[data-admin]').forEach(el => el.classList.toggle('d-none', !isAdmin));
  showTab('giris'); checkExpiring();
}
async function checkExpiring() {
  try {
    const list = await api('/subscriptions');
    const n = list.filter(s => s.status === 'Yakında Bitiyor').length;
    $('#alertBar').innerHTML = n ? `<div class="alert alert-warning py-2">⚠️ ${n} aboneliğin süresi 7 gün içinde doluyor.
      <a href="#" onclick="showTab('abonelik');return false">Abonelikleri gör</a></div>` : '';
  } catch { /* sessizce geç */ }
}

/* ---------- Sekmeler ---------- */
const TABS = { giris: async () => {}, doluluk: loadSlots, aktif: loadActive, gecmis: loadHistory, abonelik: loadSubs, tarife: loadTariffs, rapor: loadReports, personel: loadUsers };
function showTab(name) {
  document.querySelectorAll('.tab').forEach(t => t.classList.add('d-none'));
  $('#tab-' + name).classList.remove('d-none');
  document.querySelectorAll('#tabs .nav-link').forEach(b => b.classList.toggle('active', b.dataset.tab === name));
  TABS[name]().catch(fail);
}
// Doluluk ve içerideki araçlar sekmeleri açıkken otomatik yenile
setInterval(() => {
  if (!session) return;
  if (!$('#tab-doluluk').classList.contains('d-none')) loadSlots().catch(() => {});
  if (!$('#tab-aktif').classList.contains('d-none')) loadActive().catch(() => {});
}, 15000);

/* ---------- Giriş / Çıkış ---------- */
async function doEntry() {
  try {
    const r = await api('/parking/entry', { method: 'POST', body: { plate: $('#ePlate').value, vehicleType: $('#eType').value } });
    const url = `${location.origin}/ticket.html?code=${r.ticketCode}`;
    $('#entryResult').innerHTML = `
      <div id="ticketBox" class="border rounded p-3 text-center bg-white">
        <h5>GİRİŞ FİŞİ</h5>
        <div class="fs-3 fw-bold">${esc(r.plate)}</div>
        <div>${esc(r.vehicleType)} • Yer: <b>${esc(r.slot)}</b></div>
        <div class="small text-muted">${fmtDT(r.entryTime)}</div>
        <div id="qr" class="d-flex justify-content-center my-2"></div>
        <div class="font-monospace">${esc(r.ticketCode)}</div>
      </div>
      ${r.isSubscriber ? '<div class="alert alert-info mt-2 mb-0">Abone araç – çıkışta ücret alınmaz.</div>' : ''}
      ${r.warning ? `<div class="alert alert-warning mt-2 mb-0">${esc(r.warning)}</div>` : ''}
      <button class="btn btn-outline-secondary mt-2" onclick="window.print()">Fişi yazdır</button>`;
    new QRCode($('#qr'), { text: url, width: 128, height: 128 });
    $('#ePlate').value = '';
    toast('Giriş kaydedildi');
  } catch (e) { fail(e); }
}

async function readPlate(input) {
  const f = input.files[0]; if (!f) return;
  const fd = new FormData(); fd.append('image', f);
  try {
    const r = await api('/lpr/read', { method: 'POST', body: fd });
    $('#ePlate').value = r.plate;
    toast(`Plaka okundu: ${r.plate} (%${Math.round(r.confidence * 100)})${r.simulated ? ' – simülasyon' : ''}`, 'info');
  } catch (e) { fail(e); }
  input.value = '';
}

function qv() { const v = $('#xQuery').value.trim(); const m = v.match(/code=([A-Za-z0-9]+)/); return m ? m[1] : v; }
async function doQuote() {
  try { showQuote(await api('/parking/quote?query=' + encodeURIComponent(qv())), false); } catch (e) { fail(e); }
}
async function doExit() {
  try {
    const q = await api('/parking/exit', { method: 'POST', body: { query: qv(), paymentType: $('#xPay').value } });
    showQuote(q, true); $('#xQuery').value = ''; toast('Çıkış tamamlandı'); checkExpiring();
  } catch (e) { fail(e); }
}
function showQuote(q, done) {
  $('#exitResult').innerHTML = `
    <div class="card"><div class="card-body" ${done ? 'id="receiptBox"' : ''}>
      <div class="fs-4 fw-bold">${esc(q.plate)}</div>
      <div>${esc(q.vehicleType)} • Yer: ${esc(q.slot ?? '-')}</div>
      <div>Giriş: ${fmtDT(q.entryTime)}</div>
      <div>Kalış: ${dur(q.minutes)}</div>
      <div class="display-6 my-2">${q.isSubscriber ? 'ABONE – ÜCRETSİZ' : money(q.fee)}</div>
      ${q.warning ? `<div class="alert alert-warning">${esc(q.warning)}</div>` : ''}
      ${done ? `<span class="badge bg-success">Çıkış yapıldı ${fmtDT(q.exitTime)}</span>
        <button class="btn btn-sm btn-outline-secondary ms-2" onclick="window.print()">Makbuz yazdır</button>` : ''}
    </div></div>`;
}
$('#ePlate').addEventListener('keydown', e => { if (e.key === 'Enter') doEntry(); });
$('#xQuery').addEventListener('keydown', e => { if (e.key === 'Enter') doQuote(); });
$('#hPlate').addEventListener('keydown', e => { if (e.key === 'Enter') loadHistory().catch(fail); });

/* ---------- Doluluk ---------- */
async function loadSlots() {
  const d = await api('/slots');
  const isAdmin = session.role === 'Admin';
  $('#slotSummary').innerHTML = `
    <div class="d-flex align-items-center gap-3 flex-wrap">
      <span class="fs-5">Toplam <b>${d.total}</b></span>
      <span class="fs-5 text-danger">Dolu <b>${d.occupied}</b></span>
      <span class="fs-5 text-success">Boş <b>${d.free}</b></span>
      <div class="progress flex-grow-1" style="height:22px;min-width:200px"><div class="progress-bar bg-danger" style="width:${d.rate}%">%${d.rate}</div></div>
    </div>`;
  $('#slotGrid').innerHTML = d.slots.map(s => {
    const cls = !s.isActive ? 'off' : s.occupied ? 'busy' : 'free';
    const click = isAdmin ? ` clickable" onclick="toggleSlot(${s.id})` : '';
    return `<div class="slot ${cls}${click}" title="${s.since ? 'Giriş: ' + fmtDT(s.since) : ''}"><b>${esc(s.code)}</b><span>${esc(s.plate ?? '')}</span></div>`;
  }).join('');
}
async function toggleSlot(id) {
  try { await api(`/slots/${id}/toggle`, { method: 'PUT' }); loadSlots(); } catch (e) { fail(e); }
}
async function addSlot() {
  const code = prompt('Yeni yer kodu (örn. B01):'); if (!code) return;
  try { await api('/slots', { method: 'POST', body: { code } }); toast('Yer eklendi'); loadSlots(); } catch (e) { fail(e); }
}

/* ---------- İçerideki araçlar ---------- */
async function loadActive() {
  const list = await api('/parking/active');
  $('#activeBody').innerHTML = list.length ? list.map(r => `
    <tr><td><b>${esc(r.plate)}</b></td><td>${esc(r.vehicleType)}</td><td>${esc(r.slot ?? '-')}</td>
    <td>${fmtDT(r.entryTime)}</td><td>${dur(r.minutes)}</td><td class="font-monospace">${esc(r.ticketCode)}</td></tr>`).join('')
    : '<tr><td colspan="6" class="text-center text-muted">İçeride araç yok</td></tr>';
}

/* ---------- Geçmiş ---------- */
let historyCache = [];
async function loadHistory() {
  historyCache = await api('/parking/history?take=200&plate=' + encodeURIComponent($('#hPlate').value));
  $('#histBody').innerHTML = historyCache.length ? historyCache.map(r => `
    <tr><td><b>${esc(r.plate)}</b></td><td>${esc(r.vehicleType)}</td><td>${fmtDT(r.entryTime)}</td><td>${fmtDT(r.exitTime)}</td>
    <td>${dur(r.minutes)}</td><td>${r.isSubscriber ? 'Abone' : money(r.fee)}</td></tr>`).join('')
    : '<tr><td colspan="6" class="text-center text-muted">Kayıt yok</td></tr>';
}
function exportCsv() {
  const rows = [['Plaka', 'Tür', 'Giriş', 'Çıkış', 'Süre (dk)', 'Ücret', 'Abone'],
    ...historyCache.map(r => [r.plate, r.vehicleType, fmtDT(r.entryTime), fmtDT(r.exitTime), r.minutes, r.fee, r.isSubscriber ? 'Evet' : 'Hayır'])];
  const csv = '\ufeff' + rows.map(r => r.map(v => `"${String(v).replace(/"/g, '""')}"`).join(';')).join('\r\n');
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  a.download = 'gecmis.csv'; a.click(); URL.revokeObjectURL(a.href);
}

/* ---------- Abonelikler ---------- */
async function loadSubs() {
  const list = await api('/subscriptions');
  const isAdmin = session.role === 'Admin';
  const badge = { 'Aktif': 'success', 'Yakında Bitiyor': 'warning', 'Süresi Doldu': 'danger', 'Pasif': 'secondary' };
  $('#subBody').innerHTML = list.map(s => `
    <tr><td><b>${esc(s.plate)}</b></td><td>${esc(s.ownerName)}</td><td>${esc(s.phone ?? '')}</td>
    <td>${new Date(s.endDate).toLocaleDateString('tr-TR')}</td><td>${s.daysLeft} gün</td>
    <td><span class="badge bg-${badge[s.status] ?? 'secondary'}">${esc(s.status)}</span></td>
    ${isAdmin ? `<td class="text-nowrap">
      <button class="btn btn-sm btn-outline-primary" onclick="renewSub(${s.id})">Yenile</button>
      <button class="btn btn-sm btn-outline-danger" onclick="delSub(${s.id})">Pasifleştir</button></td>` : ''}</tr>`).join('');
}
async function addSub() {
  try {
    await api('/subscriptions', { method: 'POST', body: {
      plate: $('#sPlate').value, ownerName: $('#sOwner').value, phone: $('#sPhone').value || null,
      months: +$('#sMonths').value, fee: +$('#sFee').value, paymentType: $('#sPay').value } });
    toast('Abonelik eklendi'); ['#sPlate', '#sOwner', '#sPhone'].forEach(i => $(i).value = ''); loadSubs(); checkExpiring();
  } catch (e) { fail(e); }
}
async function renewSub(id) {
  const months = +prompt('Kaç ay uzatılsın?', '1'); if (!months) return;
  const fee = +prompt('Tahsil edilen ücret (₺)?', '0') || 0;
  const pay = confirm('Ödeme kredi kartı ile mi? (Tamam = Kart, İptal = Nakit)') ? 'KrediKarti' : 'Nakit';
  try { await api(`/subscriptions/${id}/renew`, { method: 'PUT', body: { months, fee, paymentType: pay } }); toast('Abonelik yenilendi'); loadSubs(); checkExpiring(); } catch (e) { fail(e); }
}
async function delSub(id) {
  if (!confirm('Abonelik pasifleştirilsin mi?')) return;
  try { await api('/subscriptions/' + id, { method: 'DELETE' }); loadSubs(); checkExpiring(); } catch (e) { fail(e); }
}

/* ---------- Tarifeler ---------- */
const TF = ['freeMinutes', 'firstHourFee', 'nextHourFee', 'dailyMaxFee', 'nightFee', 'nightStartHour', 'nightEndHour'];
async function loadTariffs() {
  const list = await api('/tariffs');
  $('#tariffBody').innerHTML = list.map(t => `<tr><td><b>${esc(t.vehicleType)}</b></td>
    ${TF.map(k => `<td><input type="number" step="any" min="0" class="form-control form-control-sm" data-k="${k}" value="${Number(t[k])}"></td>`).join('')}
    <td><button class="btn btn-sm btn-primary" onclick="saveTariff(${t.id},this)">Kaydet</button></td></tr>`).join('');
}
async function saveTariff(id, btn) {
  const body = {}; btn.closest('tr').querySelectorAll('input').forEach(i => body[i.dataset.k] = Number(i.value));
  try { await api('/tariffs/' + id, { method: 'PUT', body }); toast('Tarife güncellendi'); } catch (e) { fail(e); }
}

/* ---------- Raporlar ---------- */
const charts = {};
function chart(id, cfg) { charts[id]?.destroy(); charts[id] = new Chart($('#' + id), { ...cfg, options: { maintainAspectRatio: false, ...(cfg.options || {}) } }); }
async function loadReports() {
  const [s, d, a] = await Promise.all([api('/reports/summary'), api('/reports/daily?days=30'), api('/reports/analytics')]);
  const card = (t, x) => `<div class="col-md-4"><div class="card"><div class="card-body">
    <h6 class="text-muted">${t}</h6><div class="fs-3">${money(x.total)}</div>
    <div class="small text-muted">Nakit ${money(x.nakit)} • Kart ${money(x.krediKarti)}<br>Park ${money(x.park)} • Abonelik ${money(x.abonelik)}</div>
    </div></div></div>`;
  $('#sumCards').innerHTML = card('Bugün', s.today) + card('Bu hafta', s.week) + card('Bu ay', s.month);
  chart('cDaily', { type: 'bar', data: { labels: d.map(x => x.date), datasets: [{ label: 'Günlük gelir (₺)', data: d.map(x => x.amount) }] } });
  chart('cHour', { type: 'line', data: { labels: [...Array(24).keys()].map(h => h + ':00'), datasets: [{ label: 'Saatlik giriş (son 90 gün)', data: a.hourly, fill: true, tension: .3 }] } });
  chart('cDay', { type: 'bar', data: { labels: a.gun, datasets: [{ label: 'Gün bazında giriş', data: a.weekday }] } });
  chart('cType', { type: 'doughnut', data: { labels: a.byType.map(x => x.type), datasets: [{ data: a.byType.map(x => x.count) }] } });
  $('#avgInfo').textContent = `Ortalama kalış: ${dur(Math.round(a.avgMinutes))} • En yoğun saat: ${a.peakHour}:00 • Şu an içeride: ${s.inside} araç`;
}

/* ---------- Personel ---------- */
async function loadUsers() {
  const list = await api('/users');
  $('#userBody').innerHTML = list.map(u => `<tr><td>${esc(u.username)}</td><td>${esc(u.fullName)}</td><td>${esc(u.role)}</td>
    <td>${u.isActive ? '<span class="badge bg-success">Aktif</span>' : '<span class="badge bg-secondary">Pasif</span>'}</td>
    <td>${u.isActive ? `<button class="btn btn-sm btn-outline-danger" onclick="delUser(${u.id})">Pasifleştir</button>` : ''}</td></tr>`).join('');
}
async function addUser() {
  try {
    await api('/users', { method: 'POST', body: { username: $('#uName').value, fullName: $('#uFull').value, password: $('#uPass').value, role: $('#uRole').value } });
    toast('Kullanıcı eklendi'); ['#uName', '#uFull', '#uPass'].forEach(i => $(i).value = ''); loadUsers();
  } catch (e) { fail(e); }
}
async function delUser(id) {
  if (!confirm('Kullanıcı pasifleştirilsin mi?')) return;
  try { await api('/users/' + id, { method: 'DELETE' }); loadUsers(); } catch (e) { fail(e); }
}

/* ---------- Başlangıç ---------- */
if (session) initApp();
