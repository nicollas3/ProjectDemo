// Botão mostrar/ocultar senha. Sem script inline (CSP). Uso:
// <button type="button" data-toggle-password="#IdDoCampo" ...>
//(function () {
    // Emojis de cada estado (troque aqui se preferir outros).
    //var HIDDEN = '\u{1F648}';                            // 🙈 senha oculta (olhos fechados)
    //var VISIBLE = '\u{1F435}';                            // 🐵 senha visível (macaquinho de olhos abertos)
    //var STETHOSCOPE = '\u{1FA7A}';                        // 🩺

    //document.querySelectorAll('[data-toggle-password]').forEach(function (button) {
        //var input = document.querySelector(button.getAttribute('data-toggle-password'));
        //if (!input) return;

       // function render(visible) {
        //    input.type = visible ? 'text' : 'password';
         //   button.setAttribute('aria-pressed', visible ? 'true' : 'false');
         //   button.setAttribute('aria-label', visible ? 'Ocultar senha' : 'Mostrar senha');
           // button.title = visible ? 'Ocultar senha' : 'Mostrar senha';
       // }

      //  button.addEventListener('click', function () {
         //   render(input.type === 'password');
          //  input.focus();
       // });
       // render(false);
//})();
document.addEventListener("DOMContentLoaded", function () {

    const buttons = document.querySelectorAll(
        "[data-toggle-password]"
    );

    buttons.forEach(function (button) {

        const wrapper = button.closest(
            ".neurocare-input-wrapper"
        );

        if (!wrapper) {
            return;
        }

        const input = wrapper.querySelector(
            ".neurocare-password-input"
        );

        if (!input) {
            return;
        }

        button.addEventListener("click", function () {

            const isPassword =
                input.type === "password";

            input.type =
                isPassword
                    ? "text"
                    : "password";

            button.classList.toggle(
                "is-visible",
                isPassword
            );

            button.setAttribute(
                "aria-pressed",
                String(isPassword)
            );

            button.setAttribute(
                "aria-label",
                isPassword
                    ? "Ocultar senha"
                    : "Mostrar senha"
            );

            button.setAttribute(
                "title",
                isPassword
                    ? "Ocultar senha"
                    : "Mostrar senha"
            );

        });

    });

});
