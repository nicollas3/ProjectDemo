// Botão "Imprimir / Salvar como PDF" (sem script inline, por causa da CSP).
document.querySelectorAll('[data-print]').forEach(function (button) {
    button.addEventListener('click', function () { window.print(); });
});
