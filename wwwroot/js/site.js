// MANDI-STYLED CONFIRMATIONS
// Any form with data-confirm="Your question?" asks first in the Mandi pop-up
// (#confirmModal in _Layout.cshtml) instead of the browser's plain confirm box.
// Cancel = nothing happens. "Yes, continue" = the form is sent as normal.
(function () {
    const modalElement = document.getElementById('confirmModal');
    if (!modalElement || !window.bootstrap) return;

    const modal = new bootstrap.Modal(modalElement);
    const questionText = document.getElementById('confirmModalText');
    const okButton = document.getElementById('confirmModalOk');
    let waitingForm = null;

    // Catch the form BEFORE it is sent, and ask first
    document.addEventListener('submit', function (event) {
        const form = event.target;
        if (!form.matches('form[data-confirm]')) return;
        if (form.dataset.confirmed === 'true') return;   // already said yes: let it through

        event.preventDefault();
        waitingForm = form;
        questionText.textContent = form.dataset.confirm;
        modal.show();
    });

    // "Yes, continue": mark the form as confirmed and send it
    okButton.addEventListener('click', function () {
        if (!waitingForm) return;
        waitingForm.dataset.confirmed = 'true';
        modal.hide();
        if (waitingForm.requestSubmit) waitingForm.requestSubmit();
        else waitingForm.submit();
    });

    // Closed with Cancel, the X or Escape: forget the form
    modalElement.addEventListener('hidden.bs.modal', function () {
        if (waitingForm && waitingForm.dataset.confirmed !== 'true') waitingForm = null;
    });
})();