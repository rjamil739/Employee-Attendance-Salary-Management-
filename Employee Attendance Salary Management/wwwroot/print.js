window.printAttendancePanel = function (selector) {
    const panel = document.querySelector(selector || '.att-print-panel');
    if (!panel) return;

    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.position = 'fixed';
    frame.style.right = '0';
    frame.style.bottom = '0';
    frame.style.width = '0';
    frame.style.height = '0';
    frame.style.border = '0';
    document.body.appendChild(frame);

    const printDocument = frame.contentDocument || frame.contentWindow.document;
    printDocument.open();
    printDocument.write(`<!doctype html><html><head><title>Shift profile</title><style>
        @page{size:A4 portrait;margin:16mm}
        *{box-sizing:border-box}body{margin:0;color:#172944;background:#fff;font-family:Arial,sans-serif}
        .att-dialog{width:100%;overflow:hidden;border:1px solid #dbe4ef;border-radius:14px}
        .att-dialog>header{padding:18px 22px;color:#fff;background:#2857b8}
        .att-dialog>header span{color:#dce7ff;font-size:10px;font-weight:800;letter-spacing:1px;text-transform:uppercase}
        .att-dialog>header h2{margin:5px 0 0;color:#fff;font-size:24px}
        .att-no-print{display:none!important}.att-shift-detail{padding:22px}
        .att-shift-print-head{padding:16px;display:flex;align-items:center;gap:12px;border:1px solid #dfe7f0;border-radius:12px;background:#f8faff}
        .att-shift-print-head>i{width:44px;height:44px;display:grid;place-items:center;border-radius:11px;color:#2857b8;background:#e7efff;font-size:21px;font-style:normal}
        .att-shift-print-head>div{flex:1;display:grid;gap:3px}.att-shift-print-head span{color:#8492a5;font-size:9px;text-transform:uppercase}
        .att-shift-print-head em{padding:6px 9px;border-radius:15px;color:#176f57;background:#e5f7f0;font-size:9px;font-style:normal;font-weight:800}
        dl{margin:16px 0;display:grid;grid-template-columns:1fr 1fr;gap:1px;overflow:hidden;border:1px solid #e1e8f0;border-radius:11px;background:#e1e8f0}
        dl div{padding:14px;background:#fff}dt{color:#8794a5;font-size:9px;text-transform:uppercase}dd{margin:5px 0 0;color:#344a63;font-size:12px;font-weight:800}
        .att-shift-signature{width:290px;margin:58px 0 12px auto;display:flex;align-items:flex-end;gap:10px;color:#40536b;font-size:10px;font-weight:800}
        .att-shift-signature i{flex:1;border-bottom:1px solid #344a63}
    </style></head><body>${panel.outerHTML}</body></html>`);
    printDocument.close();

    frame.onload = function () {
        setTimeout(function () {
            frame.contentWindow.focus();
            frame.contentWindow.print();
            setTimeout(function () { frame.remove(); }, 1000);
        }, 150);
    };
};

window.printShiftPanel = function () {
    window.printAttendancePanel('.att-shift-print');
};
