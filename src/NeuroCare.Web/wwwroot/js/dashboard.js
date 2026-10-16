// Gráficos (bar/line). Dados vêm de data-attributes (sem script inline, por causa da CSP).
document.querySelectorAll('canvas[data-chart]').forEach((canvas) => {
    const type = canvas.dataset.chart === 'line' ? 'line' : 'bar';
    const labels = JSON.parse(canvas.dataset.labels || '[]');
    const values = JSON.parse(canvas.dataset.values || '[]');
    const max = canvas.dataset.max ? Number(canvas.dataset.max) : undefined;
    new Chart(canvas, {
        type,
        data: {
            labels,
            datasets: [{
                label: canvas.dataset.label || '',
                data: values,
                backgroundColor: '#2b5f9e',
                borderColor: '#2b5f9e',
                tension: 0.2
            }]
        },
        options: {
            plugins: { legend: { display: false } },
            scales: { y: { beginAtZero: true, max, ticks: { precision: 0 } } }
        }
    });
});
