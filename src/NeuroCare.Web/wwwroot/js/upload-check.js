// Valida o tamanho ANTES de enviar (evita que o servidor derrube a conexão em arquivos grandes)
// e evita duplo clique no botão. Sem script inline (CSP).
document.querySelectorAll('input[type="file"][data-max-bytes]').forEach(function (input) {
    var max = Number(input.getAttribute('data-max-bytes'));
    var target = document.getElementById(input.getAttribute('data-error-target'));
    var form = input.closest('form');
    var button = form ? form.querySelector('button[type="submit"]') : null;

    input.addEventListener('change', function () {
        var file = input.files && input.files[0];
        if (file && max > 0 && file.size > max) {
            var mb = function (n) { return (n / 1048576).toFixed(1).replace('.', ','); };
            input.value = '';
            if (target) {
                target.textContent = 'O arquivo tem ' + mb(file.size) + ' MB e o limite é ' + Math.round(max / 1048576) + ' MB. Reduza o arquivo e tente novamente.';
                target.classList.remove('d-none');
            }
        } else if (target) {
            target.classList.add('d-none');
        }
    });

    if (form && button) {
        form.addEventListener('submit', function () {
            if (input.files && input.files.length > 0) {
                button.disabled = true;
                button.textContent = 'Enviando…';
            }
        });
    }
});
