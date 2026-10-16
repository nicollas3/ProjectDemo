(() => {
  const q = document.getElementById('questions-container');
  const b = document.getElementById('bands-container');
  if (!q || !b) return;

  const renumber = (container, rowSelector, prefix) => {
    [...container.querySelectorAll(rowSelector)].forEach((row, i) => {
      row.querySelectorAll('[name]').forEach(el => {
        el.name = el.name.replace(new RegExp(`^${prefix}\\[\\d+\\]`), `${prefix}[${i}]`);
      });
    });
  };

  const wireRemove = root => root.querySelectorAll('[data-remove-row]').forEach(btn => {
    btn.onclick = () => {
      const row = btn.closest('[data-question-row], [data-band-row]');
      const parent = row?.parentElement;
      row?.remove();
      if (parent === q) renumber(q, '[data-question-row]', 'Items');
      if (parent === b) renumber(b, '[data-band-row]', 'Bands');
    };
  });

  document.querySelector('[data-add-question]')?.addEventListener('click', () => {
    const i = q.querySelectorAll('[data-question-row]').length;
    q.insertAdjacentHTML('beforeend', `<div class="card mb-2 question-row" data-question-row><div class="card-body"><div class="row g-2"><div class="col-md-7"><label class="form-label">Pergunta</label><input class="form-control" name="Items[${i}].Text" /></div><div class="col-md-4"><label class="form-label">Opções</label><input class="form-control" name="Items[${i}].Options" value="0=Não;1=Sim" /><div class="form-text">Formato: 0=Não;1=Sim</div></div><div class="col-md-1 d-flex align-items-end"><button type="button" class="btn btn-outline-danger w-100" data-remove-row>×</button></div></div></div></div>`);
    wireRemove(q);
  });

  document.querySelector('[data-add-band]')?.addEventListener('click', () => {
    const i = b.querySelectorAll('[data-band-row]').length;
    b.insertAdjacentHTML('beforeend', `<div class="row g-2 mb-2 band-row" data-band-row><div class="col-md-2"><input class="form-control" type="number" name="Bands[${i}].Min" placeholder="Mín." /></div><div class="col-md-2"><input class="form-control" type="number" name="Bands[${i}].Max" placeholder="Máx." /></div><div class="col-md-7"><input class="form-control" name="Bands[${i}].Label" placeholder="Rótulo" /></div><div class="col-md-1"><button type="button" class="btn btn-outline-danger w-100" data-remove-row>×</button></div></div>`);
    wireRemove(b);
  });

  wireRemove(document);
})();
