document.addEventListener("click", event => {
    const button = event.target.closest("[data-password-toggle]");
    if (!button) return;

    const input = document.getElementById(button.dataset.passwordToggle);
    if (!input) return;

    const show = input.type === "password";
    input.type = show ? "text" : "password";
    button.classList.toggle("showing", show);
    button.setAttribute("aria-pressed", show ? "true" : "false");
    button.setAttribute("aria-label", show ? "Hide password" : "Show password");
    input.focus({ preventScroll: true });
});
