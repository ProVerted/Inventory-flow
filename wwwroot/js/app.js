// Global State
let currentUser = null;
let currentProducts = [];
let cart = []; // { product, quantity }
let isLowStockOnly = false;
let searchTimeout = null;

// Initial Bootstrap
document.addEventListener('DOMContentLoaded', () => {
  checkAuth();
});

// ================= AUTHENTICATION =================
async function checkAuth() {
  try {
    const res = await fetch('/auth/me');
    if (res.ok) {
      currentUser = await res.json();
      onAuthSuccess();
    } else {
      showLoginOverlay();
    }
  } catch (err) {
    showLoginOverlay();
  }
}

function showLoginOverlay() {
  document.getElementById('authOverlay').classList.remove('hidden');
}

function hideLoginOverlay() {
  document.getElementById('authOverlay').classList.add('hidden');
}

function fillLogin(u, p) {
  document.getElementById('loginUsername').value = u;
  document.getElementById('loginPassword').value = p;
}

async function handleLogin(e) {
  e.preventDefault();
  const errorDiv = document.getElementById('loginError');
  const submitBtn = document.getElementById('loginSubmitBtn');
  errorDiv.classList.add('hidden');

  const username = document.getElementById('loginUsername').value.trim();
  const password = document.getElementById('loginPassword').value;

  submitBtn.disabled = true;
  submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Signing in...';

  try {
    const res = await fetch('/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });

    if (res.ok) {
      currentUser = await res.json();
      onAuthSuccess();
    } else {
      const err = await res.json().catch(() => ({ message: 'Invalid username or password' }));
      errorDiv.textContent = err.message || 'Login failed. Please check credentials.';
      errorDiv.classList.remove('hidden');
    }
  } catch (err) {
    errorDiv.textContent = 'Network error. Ensure the server is running.';
    errorDiv.classList.remove('hidden');
  } finally {
    submitBtn.disabled = false;
    submitBtn.innerHTML = '<span>Sign In to Dashboard</span><i class="fa-solid fa-arrow-right text-xs"></i>';
  }
}

async function handleLogout() {
  await fetch('/auth/logout', { method: 'POST' });
  currentUser = null;
  showLoginOverlay();
}

function onAuthSuccess() {
  hideLoginOverlay();

  // Populate user pill
  document.getElementById('userName').textContent = currentUser.username;
  document.getElementById('userRole').textContent = currentUser.role;
  document.getElementById('userAvatar').textContent = currentUser.username.charAt(0).toUpperCase();

  // Color-code role pill
  const roleEl = document.getElementById('userRole');
  if (currentUser.role.toLowerCase() === 'admin') {
    roleEl.className = 'inline-block px-1.5 py-0.2 rounded text-[10px] font-semibold bg-indigo-100 text-indigo-700 uppercase';
  } else {
    roleEl.className = 'inline-block px-1.5 py-0.2 rounded text-[10px] font-semibold bg-emerald-100 text-emerald-700 uppercase';
  }

  // Toggle admin-only features
  const adminElements = document.querySelectorAll('.admin-only');
  adminElements.forEach(el => {
    if (currentUser.role.toLowerCase() === 'admin') {
      el.classList.remove('hidden');
    } else {
      el.classList.add('hidden');
    }
  });

  // Switch to Dashboard and load data
  switchTab('dashboard');
}

// ================= TAB NAVIGATION =================
function switchTab(tabName) {
  const tabs = ['dashboard', 'products', 'pos', 'invoices'];

  tabs.forEach(t => {
    const view = document.getElementById(`view-${t}`);
    const btn = document.getElementById(`tab-${t}`);
    if (t === tabName) {
      view.classList.remove('hidden');
      if (btn) {
        btn.classList.add('bg-white', 'text-indigo-600', 'shadow-xs');
        btn.classList.remove('text-slate-600');
      }
    } else {
      view.classList.add('hidden');
      if (btn) {
        btn.classList.remove('bg-white', 'text-indigo-600', 'shadow-xs');
        btn.classList.add('text-slate-600');
      }
    }
  });

  if (tabName === 'dashboard') loadDashboard();
  if (tabName === 'products') loadProducts();
  if (tabName === 'pos') loadPosProducts();
  if (tabName === 'invoices') loadInvoices();
}

// ================= DASHBOARD =================
async function loadDashboard() {
  try {
    const res = await fetch('/reports/dashboard');
    if (!res.ok) return;
    const data = await res.json();

    document.getElementById('kpi-revenue').textContent = formatCurrency(data.totalRevenue);
    document.getElementById('kpi-valuation').textContent = formatCurrency(data.totalInventoryValuation);
    document.getElementById('kpi-lowstock').textContent = data.lowStockProductsCount;
    document.getElementById('kpi-invoices-count').textContent = `${data.totalInvoicesCount} sales completed`;
    document.getElementById('kpi-stock-count').textContent = `${data.totalStockQuantity} items in stock`;
    document.getElementById('kpi-products-count').textContent = `${data.totalProducts} SKUs`;
  } catch (err) {
    console.error('Failed to load dashboard metrics', err);
  }
}

// ================= PRODUCTS CATALOG =================
async function loadProducts() {
  const tbody = document.getElementById('productsTableBody');
  const search = document.getElementById('productSearchInput').value.trim();

  let url = `/products?`;
  if (search) url += `search=${encodeURIComponent(search)}&`;
  if (isLowStockOnly) url += `lowStockOnly=true&`;

  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error('Failed to load');
    currentProducts = await res.json();
    renderProductsTable(currentProducts);
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="6" class="text-center py-6 text-red-500 text-xs">Error loading products.</td></tr>`;
  }
}

function renderProductsTable(products) {
  const tbody = document.getElementById('productsTableBody');
  if (products.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6" class="text-center py-10 text-slate-400 text-xs">No products match your criteria.</td></tr>`;
    return;
  }

  tbody.innerHTML = products.map(p => {
    let badgeClass = 'bg-emerald-50 text-emerald-600 border border-emerald-100';
    let badgeText = 'In Stock';

    if (p.stockQuantity === 0) {
      badgeClass = 'bg-red-50 text-red-600 border border-red-100';
      badgeText = 'Out of Stock';
    } else if (p.isLowStock) {
      badgeClass = 'bg-amber-50 text-amber-600 border border-amber-100';
      badgeText = 'Low Stock';
    }

    const isAdmin = currentUser && currentUser.role.toLowerCase() === 'admin';

    return `
      <tr class="hover:bg-slate-50/70 transition">
        <td class="py-3 px-4 font-bold text-slate-900">${escapeHtml(p.name)}</td>
        <td class="py-3 px-4 font-mono text-xs text-slate-500">${escapeHtml(p.sku)}</td>
        <td class="py-3 px-4 font-semibold text-slate-900">${formatCurrency(p.price)}</td>
        <td class="py-3 px-4 font-bold ${p.isLowStock ? 'text-amber-600' : 'text-slate-800'}">
          ${p.stockQuantity} <span class="text-xs font-normal text-slate-400">(alert: ${p.lowStockAlert})</span>
        </td>
        <td class="py-3 px-4">
          <span class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${badgeClass}">
            ${badgeText}
          </span>
        </td>
        <td class="py-3 px-4 text-right">
          ${isAdmin ? `
            <div class="flex items-center justify-end gap-1">
              <button onclick="openRestockModal(${p.id}, '${escapeHtml(p.name)}', ${p.stockQuantity})" title="Quick Restock"
                class="p-1.5 rounded-lg text-indigo-600 hover:bg-indigo-50 transition text-xs font-semibold">
                <i class="fa-solid fa-boxes-packing mr-1"></i> Restock
              </button>
              <button onclick="openEditProductModal(${JSON.stringify(p).replace(/"/g, '&quot;')})" title="Edit"
                class="p-1.5 rounded-lg text-slate-500 hover:text-slate-800 hover:bg-slate-100 transition">
                <i class="fa-solid fa-pen-to-square"></i>
              </button>
              <button onclick="handleDeleteProduct(${p.id}, '${escapeHtml(p.name)}')" title="Delete"
                class="p-1.5 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 transition">
                <i class="fa-solid fa-trash-can"></i>
              </button>
            </div>
          ` : `<span class="text-xs text-slate-400 font-medium">View Only</span>`}
        </td>
      </tr>
    `;
  }).join('');
}

function debounceProductSearch() {
  clearTimeout(searchTimeout);
  searchTimeout = setTimeout(loadProducts, 250);
}

function toggleLowStockFilter() {
  isLowStockOnly = !isLowStockOnly;
  const btn = document.getElementById('filterLowStockBtn');
  if (isLowStockOnly) {
    btn.classList.add('bg-amber-500', 'text-white', 'border-amber-500');
    btn.classList.remove('border-slate-200', 'text-slate-600');
  } else {
    btn.classList.remove('bg-amber-500', 'text-white', 'border-amber-500');
    btn.classList.add('border-slate-200', 'text-slate-600');
  }
  loadProducts();
}

// Add/Edit Product Modal
function openAddProductModal() {
  document.getElementById('productModalTitle').textContent = 'Add New Product';
  document.getElementById('modalProductId').value = '';
  document.getElementById('modalProductName').value = '';
  document.getElementById('modalProductSku').value = '';
  document.getElementById('modalProductPrice').value = '';
  document.getElementById('modalProductStock').value = '25';
  document.getElementById('modalProductAlert').value = '5';
  document.getElementById('productModal').classList.remove('hidden');
  document.getElementById('productModal').classList.add('flex');
}

function openEditProductModal(p) {
  document.getElementById('productModalTitle').textContent = 'Edit Product';
  document.getElementById('modalProductId').value = p.id;
  document.getElementById('modalProductName').value = p.name;
  document.getElementById('modalProductSku').value = p.sku;
  document.getElementById('modalProductPrice').value = p.price;
  document.getElementById('modalProductStock').value = p.stockQuantity;
  document.getElementById('modalProductAlert').value = p.lowStockAlert;
  document.getElementById('productModal').classList.remove('hidden');
  document.getElementById('productModal').classList.add('flex');
}

function closeProductModal() {
  document.getElementById('productModal').classList.add('hidden');
  document.getElementById('productModal').classList.remove('flex');
}

async function handleSaveProduct(e) {
  e.preventDefault();
  const id = document.getElementById('modalProductId').value;
  const body = {
    name: document.getElementById('modalProductName').value.trim(),
    sku: document.getElementById('modalProductSku').value.trim().toUpperCase(),
    price: parseFloat(document.getElementById('modalProductPrice').value),
    stockQuantity: parseInt(document.getElementById('modalProductStock').value, 10),
    lowStockAlert: parseInt(document.getElementById('modalProductAlert').value, 10)
  };

  const isEdit = !!id;
  const url = isEdit ? `/products/${id}` : '/products';
  const method = isEdit ? 'PUT' : 'POST';

  try {
    const res = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });

    if (res.ok) {
      closeProductModal();
      loadProducts();
      loadDashboard();
    } else {
      const err = await res.json().catch(() => ({ message: 'Save failed' }));
      alert(err.message || 'Error saving product');
    }
  } catch (err) {
    alert('Network error while saving product');
  }
}

async function handleDeleteProduct(id, name) {
  if (!confirm(`Are you sure you want to delete "${name}"?`)) return;

  try {
    const res = await fetch(`/products/${id}`, { method: 'DELETE' });
    if (res.ok) {
      loadProducts();
      loadDashboard();
    } else {
      const err = await res.json().catch(() => ({ message: 'Cannot delete product' }));
      alert(err.message || 'Delete failed.');
    }
  } catch (err) {
    alert('Network error during deletion');
  }
}

// Quick Restock Modal
function openRestockModal(id, name, currentStock) {
  document.getElementById('restockProductId').value = id;
  document.getElementById('restockProductName').textContent = name;
  document.getElementById('restockCurrentStock').textContent = `Current Stock: ${currentStock}`;
  document.getElementById('restockQuantity').value = '';
  document.getElementById('restockReason').value = 'Supplier Restock Shipment';
  document.getElementById('restockModal').classList.remove('hidden');
  document.getElementById('restockModal').classList.add('flex');
}

function closeRestockModal() {
  document.getElementById('restockModal').classList.add('hidden');
  document.getElementById('restockModal').classList.remove('flex');
}

async function handleQuickRestock(e) {
  e.preventDefault();
  const id = document.getElementById('restockProductId').value;
  const adjustmentQuantity = parseInt(document.getElementById('restockQuantity').value, 10);
  const reason = document.getElementById('restockReason').value.trim();

  try {
    const res = await fetch(`/products/${id}/stock`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ adjustmentQuantity, reason })
    });

    if (res.ok) {
      closeRestockModal();
      loadProducts();
      loadDashboard();
    } else {
      const err = await res.json().catch(() => ({ message: 'Stock adjustment failed' }));
      alert(err.message || 'Failed to adjust stock');
    }
  } catch (err) {
    alert('Network error during stock adjustment');
  }
}

// ================= POINT OF SALE (POS) =================
let allPosProducts = [];

async function loadPosProducts() {
  const grid = document.getElementById('posProductsGrid');
  grid.innerHTML = '<div class="col-span-2 text-center py-10 text-slate-400 text-xs"><i class="fa-solid fa-spinner fa-spin text-lg mb-2 block"></i> Loading catalog...</div>';

  try {
    const res = await fetch('/products');
    if (!res.ok) throw new Error();
    allPosProducts = await res.json();
    renderPosProducts(allPosProducts);
  } catch (err) {
    grid.innerHTML = '<div class="col-span-2 text-center py-6 text-red-500 text-xs">Failed to load catalog products.</div>';
  }
}

function renderPosProducts(products) {
  const grid = document.getElementById('posProductsGrid');
  if (products.length === 0) {
    grid.innerHTML = '<div class="col-span-2 text-center py-10 text-slate-400 text-xs">No products available.</div>';
    return;
  }

  grid.innerHTML = products.map(p => {
    const isOut = p.stockQuantity <= 0;
    return `
      <div class="bg-white p-4 rounded-xl border border-slate-200/80 shadow-xs flex flex-col justify-between transition hover:border-indigo-300">
        <div>
          <div class="flex items-start justify-between gap-2">
            <h4 class="font-bold text-slate-900 text-sm leading-snug">${escapeHtml(p.name)}</h4>
            <span class="text-xs font-bold text-indigo-600">${formatCurrency(p.price)}</span>
          </div>
          <span class="font-mono text-[11px] text-slate-400 block mt-0.5">${escapeHtml(p.sku)}</span>
        </div>

        <div class="mt-4 flex items-center justify-between pt-3 border-t border-slate-100">
          <span class="text-xs font-semibold ${isOut ? 'text-red-500' : 'text-slate-500'}">
            ${isOut ? 'Out of stock' : `${p.stockQuantity} in stock`}
          </span>
          <button onclick="addToCart(${p.id})" ${isOut ? 'disabled' : ''}
            class="px-3 py-1.5 bg-indigo-50 hover:bg-indigo-600 hover:text-white disabled:opacity-40 disabled:hover:bg-indigo-50 disabled:hover:text-indigo-600 text-indigo-600 font-bold rounded-lg text-xs transition flex items-center gap-1">
            <i class="fa-solid fa-plus text-[10px]"></i> Add
          </button>
        </div>
      </div>
    `;
  }).join('');
}

function filterPosProducts() {
  const term = document.getElementById('posSearchInput').value.trim().toLowerCase();
  const filtered = allPosProducts.filter(p => p.name.toLowerCase().includes(term) || p.sku.toLowerCase().includes(term));
  renderPosProducts(filtered);
}

function addToCart(productId) {
  const product = allPosProducts.find(p => p.id === productId);
  if (!product) return;

  const existing = cart.find(c => c.product.id === productId);
  if (existing) {
    if (existing.quantity + 1 > product.stockQuantity) {
      alert(`Cannot add more than available stock (${product.stockQuantity} available)`);
      return;
    }
    existing.quantity++;
  } else {
    cart.push({ product, quantity: 1 });
  }

  renderCart();
}

function updateCartQty(productId, delta) {
  const item = cart.find(c => c.product.id === productId);
  if (!item) return;

  const newQty = item.quantity + delta;
  if (newQty <= 0) {
    cart = cart.filter(c => c.product.id !== productId);
  } else {
    if (newQty > item.product.stockQuantity) {
      alert(`Cannot exceed available stock of ${item.product.stockQuantity}`);
      return;
    }
    item.quantity = newQty;
  }
  renderCart();
}

function clearCart() {
  cart = [];
  renderCart();
}

function renderCart() {
  const list = document.getElementById('cartItemsList');
  const countBadge = document.getElementById('cartItemCountBadge');
  const subtotalEl = document.getElementById('cartSubtotal');
  const grandTotalEl = document.getElementById('cartGrandTotal');
  const checkoutBtn = document.getElementById('checkoutBtn');

  const totalItems = cart.reduce((sum, c) => sum + c.quantity, 0);
  const totalAmount = cart.reduce((sum, c) => sum + (c.quantity * c.product.price), 0);

  countBadge.textContent = `${totalItems} item${totalItems === 1 ? '' : 's'}`;
  subtotalEl.textContent = formatCurrency(totalAmount);
  grandTotalEl.textContent = formatCurrency(totalAmount);
  checkoutBtn.disabled = cart.length === 0;

  if (cart.length === 0) {
    list.innerHTML = '<p class="text-center py-10 text-slate-400 text-xs">Your cart is empty. Click items on the left to add.</p>';
    return;
  }

  list.innerHTML = cart.map(item => `
    <div class="py-2.5 flex items-center justify-between text-xs">
      <div class="flex-1 pr-2">
        <span class="font-bold text-slate-900 block leading-tight">${escapeHtml(item.product.name)}</span>
        <span class="text-[11px] text-slate-400">${formatCurrency(item.product.price)} each</span>
      </div>

      <div class="flex items-center gap-1.5">
        <button onclick="updateCartQty(${item.product.id}, -1)" class="w-6 h-6 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 flex items-center justify-center font-bold text-xs">-</button>
        <span class="w-6 text-center font-bold text-slate-800 text-xs">${item.quantity}</span>
        <button onclick="updateCartQty(${item.product.id}, 1)" class="w-6 h-6 rounded bg-slate-100 hover:bg-slate-200 text-slate-700 flex items-center justify-center font-bold text-xs">+</button>
      </div>

      <div class="w-16 text-right font-extrabold text-slate-900">
        ${formatCurrency(item.quantity * item.product.price)}
      </div>
    </div>
  `).join('');
}

async function handleCheckout() {
  if (cart.length === 0) return;

  const checkoutBtn = document.getElementById('checkoutBtn');
  checkoutBtn.disabled = true;
  checkoutBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Processing Checkout...';

  const payload = {
    items: cart.map(c => ({
      productId: c.product.id,
      quantity: c.quantity
    }))
  };

  try {
    const res = await fetch('/invoices', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (res.ok) {
      const invoice = await res.json();
      clearCart();
      loadPosProducts(); // refresh available stocks
      showInvoiceReceipt(invoice);
    } else {
      const err = await res.json().catch(() => ({ message: 'Invoice checkout failed.' }));
      alert(err.message || 'Checkout failed');
    }
  } catch (err) {
    alert('Network error while processing invoice checkout');
  } finally {
    checkoutBtn.disabled = false;
    checkoutBtn.innerHTML = '<i class="fa-solid fa-circle-check"></i> Complete Sale & Issue Invoice';
  }
}

// ================= INVOICES ARCHIVE & RECEIPT =================
async function loadInvoices() {
  const tbody = document.getElementById('invoicesTableBody');
  tbody.innerHTML = '<tr><td colspan="6" class="text-center py-10 text-slate-400 text-xs"><i class="fa-solid fa-spinner fa-spin text-lg mb-2 block"></i> Loading invoices...</td></tr>';

  try {
    const res = await fetch('/invoices');
    if (!res.ok) throw new Error();
    const invoices = await res.json();
    renderInvoicesTable(invoices);
  } catch (err) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-6 text-red-500 text-xs">Error loading invoices.</td></tr>';
  }
}

function renderInvoicesTable(invoices) {
  const tbody = document.getElementById('invoicesTableBody');
  if (invoices.length === 0) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-center py-10 text-slate-400 text-xs">No sales invoices recorded yet.</td></tr>';
    return;
  }

  tbody.innerHTML = invoices.map(inv => {
    const dateFormatted = new Date(inv.createdAt).toLocaleString();
    return `
      <tr class="hover:bg-slate-50/70 transition">
        <td class="py-3 px-4 font-mono font-bold text-indigo-600 text-xs">${escapeHtml(inv.invoiceNumber)}</td>
        <td class="py-3 px-4 text-xs text-slate-500">${dateFormatted}</td>
        <td class="py-3 px-4 text-xs font-semibold text-slate-700">${escapeHtml(inv.createdByUsername)}</td>
        <td class="py-3 px-4 text-xs text-slate-600">${inv.totalItemsCount} units</td>
        <td class="py-3 px-4 font-extrabold text-slate-900">${formatCurrency(inv.totalAmount)}</td>
        <td class="py-3 px-4 text-right">
          <button onclick="fetchAndShowInvoiceReceipt(${inv.id})" class="px-2.5 py-1 rounded-lg border border-slate-200 hover:border-indigo-300 text-xs font-semibold text-slate-600 hover:text-indigo-600 transition">
            <i class="fa-regular fa-eye mr-1"></i> View Receipt
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

async function fetchAndShowInvoiceReceipt(id) {
  try {
    const res = await fetch(`/invoices/${id}`);
    if (res.ok) {
      const inv = await res.json();
      showInvoiceReceipt(inv);
    }
  } catch (err) {
    alert('Could not load invoice receipt');
  }
}

function showInvoiceReceipt(invoice) {
  const container = document.getElementById('printableInvoiceContent');
  const dateFormatted = new Date(invoice.createdAt).toLocaleString();

  container.innerHTML = `
    <div class="text-center border-b border-slate-200 pb-5">
      <div class="w-12 h-12 bg-indigo-600 text-white rounded-xl flex items-center justify-center font-bold text-xl mx-auto mb-2">
        <i class="fa-solid fa-boxes-stacked"></i>
      </div>
      <h2 class="text-xl font-bold text-slate-900 tracking-tight">InventoryFlow Store</h2>
      <p class="text-xs text-slate-400">Official Sales Receipt & Tax Invoice</p>
    </div>

    <div class="grid grid-cols-2 text-xs gap-3 py-1">
      <div>
        <span class="text-slate-400 block font-medium">Invoice Number</span>
        <span class="font-bold text-slate-900 font-mono">${escapeHtml(invoice.invoiceNumber)}</span>
      </div>
      <div class="text-right">
        <span class="text-slate-400 block font-medium">Date & Time</span>
        <span class="font-medium text-slate-800">${dateFormatted}</span>
      </div>
      <div>
        <span class="text-slate-400 block font-medium">Served By Cashier</span>
        <span class="font-medium text-slate-800">${escapeHtml(invoice.createdByUsername)}</span>
      </div>
      <div class="text-right">
        <span class="text-slate-400 block font-medium">Status</span>
        <span class="inline-block px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-50 text-emerald-600">PAID</span>
      </div>
    </div>

    <!-- Items Table -->
    <div class="border-t border-b border-slate-200 py-3">
      <table class="w-full text-xs text-left">
        <thead class="text-[10px] uppercase font-bold text-slate-400 border-b border-slate-100">
          <tr>
            <th class="pb-2">Description</th>
            <th class="pb-2 text-center">Qty</th>
            <th class="pb-2 text-right">Price</th>
            <th class="pb-2 text-right">Amount</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          ${invoice.items.map(i => `
            <tr>
              <td class="py-2 pr-2">
                <span class="font-bold text-slate-900 block leading-tight">${escapeHtml(i.productName)}</span>
                <span class="text-[10px] text-slate-400 font-mono">${escapeHtml(i.productSKU)}</span>
              </td>
              <td class="py-2 text-center text-slate-700 font-semibold">${i.quantity}</td>
              <td class="py-2 text-right text-slate-700">${formatCurrency(i.unitPrice)}</td>
              <td class="py-2 text-right font-bold text-slate-900">${formatCurrency(i.subTotal)}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>

    <!-- Summary -->
    <div class="space-y-1.5 text-xs">
      <div class="flex justify-between text-slate-500">
        <span>Subtotal</span>
        <span class="font-medium text-slate-800">${formatCurrency(invoice.totalAmount)}</span>
      </div>
      <div class="flex justify-between text-slate-500">
        <span>Sales Tax (0%)</span>
        <span class="font-medium text-slate-800">$0.00</span>
      </div>
      <div class="flex justify-between text-sm font-extrabold text-slate-900 pt-2 border-t border-slate-200">
        <span>Grand Total Paid</span>
        <span class="text-indigo-600 text-base">${formatCurrency(invoice.totalAmount)}</span>
      </div>
    </div>

    <div class="text-center text-[11px] text-slate-400 pt-3">
      Thank you for your business!
    </div>
  `;

  document.getElementById('invoiceModal').classList.remove('hidden');
  document.getElementById('invoiceModal').classList.add('flex');
}

function closeInvoiceModal() {
  document.getElementById('invoiceModal').classList.add('hidden');
  document.getElementById('invoiceModal').classList.remove('flex');
}

// ================= HELPERS =================
function formatCurrency(amount) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount || 0);
}

function escapeHtml(str) {
  if (!str) return '';
  return str.toString()
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}
